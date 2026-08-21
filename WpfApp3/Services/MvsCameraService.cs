using MvCameraControl;
using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media.Imaging;
using WpfApp3.Models;

namespace WpfApp3.Services;

public sealed record ResolutionSwitchTestResult(
    int OriginalWidth,
    int OriginalHeight,
    int FullWidth,
    int FullHeight,
    double StopAcquisitionMs,
    double ConfigureFullResolutionMs,
    double FirstFullFrameMs,
    double SaveFullFrameMs,
    double RestorePreviewMs,
    double TotalMs,
    string CapturedImagePath);

/// <summary>
/// Industrial camera service backed by the MVS SDK. It enumerates GigE,
/// USB3 Vision and GenTL GigE devices directly instead of relying on
/// DirectShow/OpenCV camera indexes.
/// </summary>
public sealed class MvsCameraService : ICameraService
{
    private const float TargetAcquisitionFrameRate = 4.0f;
    private static readonly long MinimumProcessedFrameIntervalTicks =
        Math.Max(1, Stopwatch.Frequency / 4);
    private const DeviceTLayerType SupportedLayerTypes =
        DeviceTLayerType.MvGigEDevice |
        DeviceTLayerType.MvUsbDevice |
        DeviceTLayerType.MvGenTLGigEDevice |
        DeviceTLayerType.MvGenTLCXPDevice |
        DeviceTLayerType.MvGenTLCameraLinkDevice |
        DeviceTLayerType.MvGenTLXoFDevice;

    private static readonly object SdkLock = new();
    private static bool _sdkInitialized;

    private readonly object _frameLock = new();
    private readonly object _resolutionSwitchTestLock = new();
    private readonly string _captureFolder;
    private IDevice? _device;
    private Thread? _captureThread;
    private volatile bool _shouldStop;
    private volatile bool _isRunning;
    private long _frameSequence;
    private Mat? _currentMat;
    private bool _disposed;

    public bool IsRunning => _isRunning;
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public double FramesPerSecond { get; private set; }
    public long FrameSequence => Interlocked.Read(ref _frameSequence);
    public string? LastError { get; private set; }
    public string ImagingCapabilitiesSummary { get; private set; } =
        "Resolution nodes have not been inspected.";
    public string FrameRateConfigurationSummary { get; private set; } =
        "Frame-rate control has not been inspected.";

    public MvsCameraService()
    {
        EnsureSdkInitialized();
        _captureFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyPictures),
            "WpfApp3_CapturedFrames");
        Directory.CreateDirectory(_captureFolder);
    }

    public static List<CameraDevice> EnumerateCameras()
    {
        EnsureSdkInitialized();
        int result = DeviceEnumerator.EnumDevices(
            SupportedLayerTypes,
            out List<IDeviceInfo> deviceInfos);
        if (result != MvError.MV_OK)
        {
            throw new InvalidOperationException(
                FormatMvsError(
                    "MVS cannot enumerate camera devices",
                    result));
        }

        var cameras = new List<CameraDevice>(
            deviceInfos.Count);
        for (int index = 0;
             index < deviceInfos.Count;
             index++)
        {
            IDeviceInfo info = deviceInfos[index];
            string deviceName =
                BuildDeviceDisplayName(info);
            cameras.Add(
                new CameraDevice(
                    index,
                    deviceName,
                    info.SerialNumber));
        }

        return cameras;
    }

    public bool StartCamera(CameraDevice camera)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(camera);
        if (_isRunning)
            return true;

        StopCamera();
        LastError = null;

        try
        {
            int result = DeviceEnumerator.EnumDevices(
                SupportedLayerTypes,
                out List<IDeviceInfo> deviceInfos);
            if (result != MvError.MV_OK)
            {
                throw new InvalidOperationException(
                    FormatMvsError(
                        "MVS cannot enumerate camera devices",
                        result));
            }

            if (camera.Index < 0 ||
                camera.Index >= deviceInfos.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(camera),
                    "The selected MVS camera no longer exists.");
            }

            _device = DeviceFactory.CreateDevice(
                deviceInfos[camera.Index]);
            if (_device == null)
            {
                throw new InvalidOperationException(
                    "MVS could not create a camera connection.");
            }

            result = _device.Open();
            if (result != MvError.MV_OK)
            {
                throw new InvalidOperationException(
                    FormatMvsError(
                        "MVS cannot open the selected camera",
                        result));
            }

            ConfigureGigEPacketSize(_device);
            ImagingCapabilitiesSummary =
                InspectImagingCapabilities(_device);
            _device.Parameters.SetEnumValueByString(
                "AcquisitionMode",
                "Continuous");
            result = _device.Parameters.SetEnumValueByString(
                "TriggerMode",
                "Off");
            if (result != MvError.MV_OK)
            {
                throw new InvalidOperationException(
                    FormatMvsError(
                        "MVS cannot disable trigger mode",
                        result));
            }

            FrameRateConfigurationSummary =
                ConfigureAcquisitionFrameRate(_device);

            _device.StreamGrabber.SetImageNodeNum(5);
            result = _device.StreamGrabber.StartGrabbing();
            if (result != MvError.MV_OK)
            {
                throw new InvalidOperationException(
                    FormatMvsError(
                        "MVS cannot start image acquisition",
                        result));
            }

            TryReadFrameRate(_device);
            _shouldStop = false;
            _isRunning = true;
            _captureThread = new Thread(CaptureFrames)
            {
                IsBackground = true,
                Name = "MvsCaptureThread"
            };
            _captureThread.Start();
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Debug.WriteLine(
                $"MVS start camera error: {ex}");
            CleanupDevice();
            return false;
        }
    }

    public void StopCamera()
    {
        _shouldStop = true;
        _isRunning = false;

        if (_captureThread is { IsAlive: true })
            _captureThread.Join(TimeSpan.FromSeconds(3));
        _captureThread = null;

        CleanupDevice();

        lock (_frameLock)
        {
            _currentMat?.Dispose();
            _currentMat = null;
        }

        FrameWidth = 0;
        FrameHeight = 0;
        FramesPerSecond = 0;
    }

    public Mat? GetCurrentMatFrame()
    {
        lock (_frameLock)
            return _currentMat?.Clone();
    }

    public Mat? GetPreviewMatFrame(int maximumWidth)
    {
        if (maximumWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));

        lock (_frameLock)
        {
            if (_currentMat == null || _currentMat.Empty())
                return null;
            if (_currentMat.Width <= maximumWidth)
                return _currentMat.Clone();

            double scale = maximumWidth / (double)_currentMat.Width;
            int height = Math.Max(
                1,
                (int)Math.Round(_currentMat.Height * scale));
            var preview = new Mat();
            Cv2.Resize(
                _currentMat,
                preview,
                new OpenCvSharp.Size(maximumWidth, height),
                0,
                0,
                InterpolationFlags.Linear);
            return preview;
        }
    }

    public BitmapImage? GetCurrentFrame()
    {
        using Mat? frame = GetCurrentMatFrame();
        if (frame == null || frame.Empty())
            return null;

        Cv2.ImEncode(".png", frame, out byte[] encoded);
        using var stream = new MemoryStream(encoded);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public string? CaptureFrame()
    {
        using Mat? frame = GetCurrentMatFrame();
        if (frame == null || frame.Empty())
            return null;

        string path = Path.Combine(
            _captureFolder,
            $"capture_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png");
        return Cv2.ImWrite(path, frame) ? path : null;
    }

    /// <summary>
    /// Switches from the camera's current ROI to its maximum Width/Height,
    /// acquires and saves one full-resolution frame, and restores the exact
    /// original ROI. The caller decides whether the saved frame is used only
    /// for diagnostics or forwarded to AI.
    /// </summary>
    public ResolutionSwitchTestResult RunResolutionSwitchTimingTest()
    {
        ThrowIfDisposed();

        lock (_resolutionSwitchTestLock)
        {
            IDevice device = _device ?? throw new InvalidOperationException(
                "The MVS camera is not connected.");
            if (!_isRunning)
            {
                throw new InvalidOperationException(
                    "Start the camera before running the resolution-switch test.");
            }

            IIntValue widthNode = GetRequiredIntegerNode(device, "Width");
            IIntValue heightNode = GetRequiredIntegerNode(device, "Height");
            long originalWidth = widthNode.CurValue;
            long originalHeight = heightNode.CurValue;
            long fullWidth = widthNode.Max;
            long fullHeight = heightNode.Max;
            long originalOffsetX = GetOptionalIntegerNodeValue(
                device,
                "OffsetX");
            long originalOffsetY = GetOptionalIntegerNodeValue(
                device,
                "OffsetY");

            var total = Stopwatch.StartNew();
            double stopAcquisitionMs = 0;
            double configureFullResolutionMs = 0;
            double firstFullFrameMs = 0;
            double saveFullFrameMs = 0;
            double restorePreviewMs = 0;
            string capturedImagePath = "";
            bool streamStarted = true;
            bool backgroundCaptureRestarted = false;

            try
            {
                var stage = Stopwatch.StartNew();
                StopCaptureThreadForReconfiguration();
                StopGrabbingOrThrow(device);
                streamStarted = false;
                stopAcquisitionMs = stage.Elapsed.TotalMilliseconds;

                stage.Restart();
                SetResolutionAndOffsets(
                    device,
                    fullWidth,
                    fullHeight,
                    0,
                    0);
                configureFullResolutionMs = stage.Elapsed.TotalMilliseconds;

                stage.Restart();
                StartGrabbingOrThrow(device);
                streamStarted = true;
                using Mat fullFrame = AcquireOneFrame(device, 5000);
                firstFullFrameMs = stage.Elapsed.TotalMilliseconds;
                ReplaceCurrentFrame(fullFrame);

                stage.Restart();
                capturedImagePath = Path.Combine(
                    _captureFolder,
                    $"resolution_switch_test_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png");
                if (!Cv2.ImWrite(capturedImagePath, fullFrame))
                {
                    throw new IOException(
                        "OpenCV could not save the full-resolution test frame.");
                }
                saveFullFrameMs = stage.Elapsed.TotalMilliseconds;

                stage.Restart();
                StopGrabbingOrThrow(device);
                streamStarted = false;
                SetResolutionAndOffsets(
                    device,
                    originalWidth,
                    originalHeight,
                    originalOffsetX,
                    originalOffsetY);
                StartGrabbingOrThrow(device);
                streamStarted = true;
                using Mat restoredFrame = AcquireOneFrame(device, 5000);
                ReplaceCurrentFrame(restoredFrame);
                StartCaptureThreadForExistingStream();
                backgroundCaptureRestarted = true;
                restorePreviewMs = stage.Elapsed.TotalMilliseconds;

                total.Stop();
                return new ResolutionSwitchTestResult(
                    checked((int)originalWidth),
                    checked((int)originalHeight),
                    checked((int)fullWidth),
                    checked((int)fullHeight),
                    stopAcquisitionMs,
                    configureFullResolutionMs,
                    firstFullFrameMs,
                    saveFullFrameMs,
                    restorePreviewMs,
                    total.Elapsed.TotalMilliseconds,
                    capturedImagePath);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                throw;
            }
            finally
            {
                if (!backgroundCaptureRestarted)
                {
                    TryRestoreStreamAfterResolutionTest(
                        device,
                        streamStarted,
                        originalWidth,
                        originalHeight,
                        originalOffsetX,
                        originalOffsetY);
                }
            }
        }
    }

    /// <summary>
    /// Arduino-synchronized capture entry point. It shares the measured and
    /// exception-safe resolution switching implementation used by the manual
    /// timing test.
    /// </summary>
    public ResolutionSwitchTestResult CaptureFullResolutionFrameWithRestore()
    {
        return RunResolutionSwitchTimingTest();
    }

    private void StopCaptureThreadForReconfiguration()
    {
        _shouldStop = true;
        if (_captureThread is { IsAlive: true } &&
            !_captureThread.Join(TimeSpan.FromSeconds(3)))
        {
            throw new TimeoutException(
                "The MVS capture thread did not stop within three seconds.");
        }

        _captureThread = null;
        _isRunning = false;
    }

    private void StartCaptureThreadForExistingStream()
    {
        _shouldStop = false;
        _isRunning = true;
        _captureThread = new Thread(CaptureFrames)
        {
            IsBackground = true,
            Name = "MvsCaptureThread"
        };
        _captureThread.Start();
    }

    private static IIntValue GetRequiredIntegerNode(
        IDevice device,
        string nodeName)
    {
        int result = device.Parameters.GetIntValue(
            nodeName,
            out IIntValue value);
        if (result != MvError.MV_OK)
        {
            throw new NotSupportedException(
                FormatMvsError(
                    $"Camera does not expose the required {nodeName} node",
                    result));
        }

        return value;
    }

    private static long GetOptionalIntegerNodeValue(
        IDevice device,
        string nodeName)
    {
        int result = device.Parameters.GetIntValue(
            nodeName,
            out IIntValue value);
        return result == MvError.MV_OK
            ? value.CurValue
            : 0;
    }

    private static void SetResolutionAndOffsets(
        IDevice device,
        long width,
        long height,
        long offsetX,
        long offsetY)
    {
        // GenICam requires offsets to be reset before increasing Width/Height.
        SetIntegerNodeOrThrow(device, "OffsetX", 0);
        SetIntegerNodeOrThrow(device, "OffsetY", 0);
        SetIntegerNodeOrThrow(device, "Width", width);
        SetIntegerNodeOrThrow(device, "Height", height);
        SetIntegerNodeOrThrow(device, "OffsetX", offsetX);
        SetIntegerNodeOrThrow(device, "OffsetY", offsetY);
    }

    private static void SetIntegerNodeOrThrow(
        IDevice device,
        string nodeName,
        long value)
    {
        int result = device.Parameters.SetIntValue(nodeName, value);
        if (result != MvError.MV_OK)
        {
            throw new InvalidOperationException(
                FormatMvsError(
                    $"MVS cannot set {nodeName}={value}",
                    result));
        }
    }

    private static void StopGrabbingOrThrow(IDevice device)
    {
        int result = device.StreamGrabber.StopGrabbing();
        if (result != MvError.MV_OK)
        {
            throw new InvalidOperationException(
                FormatMvsError("MVS cannot stop acquisition", result));
        }
    }

    private static void StartGrabbingOrThrow(IDevice device)
    {
        int result = device.StreamGrabber.StartGrabbing();
        if (result != MvError.MV_OK)
        {
            throw new InvalidOperationException(
                FormatMvsError("MVS cannot restart acquisition", result));
        }
    }

    private static Mat AcquireOneFrame(
        IDevice device,
        int timeoutMs)
    {
        int result = device.StreamGrabber.GetImageBuffer(
            checked((uint)timeoutMs),
            out IFrameOut frameOut);
        if (result != MvError.MV_OK)
        {
            throw new TimeoutException(
                FormatMvsError(
                    "MVS did not deliver a frame after changing resolution",
                    result));
        }

        try
        {
            return ConvertToBgrMat(device, frameOut.Image);
        }
        finally
        {
            device.StreamGrabber.FreeImageBuffer(frameOut);
        }
    }

    private void ReplaceCurrentFrame(Mat frame)
    {
        FrameWidth = frame.Cols;
        FrameHeight = frame.Rows;
        lock (_frameLock)
        {
            _currentMat?.Dispose();
            _currentMat = frame.Clone();
        }

        Interlocked.Increment(ref _frameSequence);
    }

    private void TryRestoreStreamAfterResolutionTest(
        IDevice device,
        bool streamStarted,
        long originalWidth,
        long originalHeight,
        long originalOffsetX,
        long originalOffsetY)
    {
        try
        {
            if (streamStarted)
                device.StreamGrabber.StopGrabbing();

            SetResolutionAndOffsets(
                device,
                originalWidth,
                originalHeight,
                originalOffsetX,
                originalOffsetY);
            StartGrabbingOrThrow(device);
            using Mat restoredFrame = AcquireOneFrame(device, 5000);
            ReplaceCurrentFrame(restoredFrame);
            StartCaptureThreadForExistingStream();
        }
        catch (Exception restoreError)
        {
            LastError =
                $"Resolution test failed and the original stream could not " +
                $"be restored: {restoreError.Message}";
            Debug.WriteLine(LastError);
            _isRunning = false;
        }
    }

    private void CaptureFrames()
    {
        IDevice? device = _device;
        if (device == null)
            return;

        try
        {
            long nextFrameProcessingAt = 0;
            while (!_shouldStop)
            {
                int result =
                    device.StreamGrabber.GetImageBuffer(
                        1000,
                        out IFrameOut frameOut);
                if (result == MvError.MV_E_NODATA)
                    continue;
                if (result != MvError.MV_OK)
                {
                    Debug.WriteLine(
                        FormatMvsError(
                            "MVS cannot read a camera frame",
                            result));
                    continue;
                }

                try
                {
                    long now = Stopwatch.GetTimestamp();
                    if (now < nextFrameProcessingAt)
                        continue;
                    nextFrameProcessingAt =
                        now + MinimumProcessedFrameIntervalTicks;

                    using Mat frame =
                        ConvertToBgrMat(device, frameOut.Image);
                    FrameWidth = frame.Cols;
                    FrameHeight = frame.Rows;

                    lock (_frameLock)
                    {
                        _currentMat?.Dispose();
                        _currentMat = frame.Clone();
                    }
                    Interlocked.Increment(ref _frameSequence);
                }
                finally
                {
                    device.StreamGrabber.FreeImageBuffer(
                        frameOut);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"MVS capture thread stopped: {ex}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private static Mat ConvertToBgrMat(
        IDevice device,
        IImage inputImage)
    {
        int width = checked((int)inputImage.Width);
        int height = checked((int)inputImage.Height);

        if (inputImage.PixelType ==
            MvGvspPixelType.PixelType_Gvsp_BGR8_Packed)
        {
            return CopyImageBytesToMat(
                inputImage.PixelData,
                width,
                height,
                3);
        }

        if (inputImage.PixelType ==
            MvGvspPixelType.PixelType_Gvsp_Mono8)
        {
            using Mat mono = CopyImageBytesToMat(
                inputImage.PixelData,
                width,
                height,
                1);
            var bgr = new Mat();
            Cv2.CvtColor(
                mono,
                bgr,
                ColorConversionCodes.GRAY2BGR);
            return bgr;
        }

        int result =
            device.PixelTypeConverter.ConvertPixelType(
                inputImage,
                out IImage convertedImage,
                MvGvspPixelType.PixelType_Gvsp_BGR8_Packed);
        if (result != MvError.MV_OK)
        {
            throw new InvalidOperationException(
                FormatMvsError(
                    $"Cannot convert {inputImage.PixelType} to BGR8",
                    result));
        }

        try
        {
            return CopyImageBytesToMat(
                convertedImage.PixelData,
                checked((int)convertedImage.Width),
                checked((int)convertedImage.Height),
                3);
        }
        finally
        {
            convertedImage.Dispose();
        }
    }

    private static Mat CopyImageBytesToMat(
        byte[] pixels,
        int width,
        int height,
        int channels)
    {
        int requiredLength = checked(
            width * height * channels);
        if (pixels.Length < requiredLength)
        {
            throw new InvalidOperationException(
                $"MVS frame buffer is too small: " +
                $"{pixels.Length} < {requiredLength}.");
        }

        var mat = new Mat(
            height,
            width,
            channels == 1
                ? MatType.CV_8UC1
                : MatType.CV_8UC3);
        Marshal.Copy(
            pixels,
            0,
            mat.Data,
            requiredLength);
        return mat;
    }

    private static void ConfigureGigEPacketSize(
        IDevice device)
    {
        if (device is not IGigEDevice gigEDevice)
            return;

        int result = gigEDevice.GetOptimalPacketSize(
            out int packetSize);
        if (result != MvError.MV_OK ||
            packetSize <= 0)
        {
            Debug.WriteLine(
                FormatMvsError(
                    "MVS cannot calculate the optimal GigE packet size",
                    result));
            return;
        }

        result = device.Parameters.SetIntValue(
            "GevSCPSPacketSize",
            packetSize);
        if (result != MvError.MV_OK)
        {
            Debug.WriteLine(
                FormatMvsError(
                    "MVS cannot set the GigE packet size",
                    result));
        }
    }

    private void TryReadFrameRate(IDevice device)
    {
        int result = device.Parameters.GetFloatValue(
            "ResultingFrameRate",
            out IFloatValue frameRate);
        if (result != MvError.MV_OK)
        {
            result = device.Parameters.GetFloatValue(
                "AcquisitionFrameRate",
                out frameRate);
        }
        FramesPerSecond =
            result == MvError.MV_OK
                ? frameRate.CurValue
                : 0;
    }

    private static string ConfigureAcquisitionFrameRate(IDevice device)
    {
        int result = device.Parameters.SetBoolValue(
            "AcquisitionFrameRateEnable",
            true);
        if (result != MvError.MV_OK)
        {
            return "Camera frame-rate control is unavailable " +
                   $"({FormatMvsError("enable failed", result)}); " +
                   "software conversion remains capped at 4 FPS.";
        }

        result = device.Parameters.GetFloatValue(
            "AcquisitionFrameRate",
            out IFloatValue frameRate);
        float requested = TargetAcquisitionFrameRate;
        if (result == MvError.MV_OK)
        {
            requested = (float)Math.Clamp(
                TargetAcquisitionFrameRate,
                frameRate.Min,
                frameRate.Max);
        }

        result = device.Parameters.SetFloatValue(
            "AcquisitionFrameRate",
            requested);
        if (result != MvError.MV_OK)
        {
            return "Camera rejected the 4 FPS request " +
                   $"({FormatMvsError("set failed", result)}); " +
                   "software conversion remains capped at 4 FPS.";
        }

        return $"Camera acquisition requested at {requested:0.##} FPS; " +
               "software conversion is capped at 4 FPS.";
    }

    private static string InspectImagingCapabilities(IDevice device)
    {
        var values = new List<string>();
        AddIntegerNodeDescription(device, "Width", values);
        AddIntegerNodeDescription(device, "Height", values);
        AddIntegerNodeDescription(device, "BinningHorizontal", values);
        AddIntegerNodeDescription(device, "BinningVertical", values);
        AddIntegerNodeDescription(device, "DecimationHorizontal", values);
        AddIntegerNodeDescription(device, "DecimationVertical", values);

        return values.Count == 0
            ? "The camera did not expose readable Width/Height, binning or " +
              "decimation nodes through MVS."
            : string.Join("; ", values);
    }

    private static void AddIntegerNodeDescription(
        IDevice device,
        string nodeName,
        ICollection<string> values)
    {
        int result = device.Parameters.GetIntValue(
            nodeName,
            out IIntValue value);
        if (result != MvError.MV_OK)
            return;

        values.Add(
            $"{nodeName}={value.CurValue} " +
            $"[{value.Min}..{value.Max}, step {value.Inc}]");
    }

    private void CleanupDevice()
    {
        IDevice? device = _device;
        _device = null;
        if (device == null)
            return;

        try
        {
            device.StreamGrabber.StopGrabbing();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"MVS StopGrabbing cleanup error: {ex.Message}");
        }

        try
        {
            device.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"MVS close cleanup error: {ex.Message}");
        }

        device.Dispose();
    }

    private static string BuildDeviceDisplayName(
        IDeviceInfo info)
    {
        string name = string.IsNullOrWhiteSpace(
            info.UserDefinedName)
            ? $"{info.ManufacturerName} {info.ModelName}".Trim()
            : info.UserDefinedName;

        string transport = info.TLayerType.ToString();
        string address = "";
        if (info is IGigEDeviceInfo gigEInfo)
        {
            uint ip = gigEInfo.CurrentIp;
            address =
                $" • {(ip >> 24) & 0xff}." +
                $"{(ip >> 16) & 0xff}." +
                $"{(ip >> 8) & 0xff}." +
                $"{ip & 0xff}";
        }

        return $"{transport}: {name} " +
               $"({info.SerialNumber}){address}";
    }

    private static void EnsureSdkInitialized()
    {
        lock (SdkLock)
        {
            if (_sdkInitialized)
                return;

            SDKSystem.Initialize();
            _sdkInitialized = true;
            AppDomain.CurrentDomain.ProcessExit +=
                (_, _) => FinalizeSdk();
        }
    }

    private static void FinalizeSdk()
    {
        lock (SdkLock)
        {
            if (!_sdkInitialized)
                return;

            SDKSystem.Finalize();
            _sdkInitialized = false;
        }
    }

    private static string FormatMvsError(
        string message,
        int errorCode) =>
        $"{message}: 0x{unchecked((uint)errorCode):X8}";

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopCamera();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
