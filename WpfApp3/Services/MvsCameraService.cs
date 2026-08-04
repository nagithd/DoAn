using MvCameraControl;
using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media.Imaging;
using WpfApp3.Models;

namespace WpfApp3.Services;

/// <summary>
/// Industrial camera service backed by the MVS SDK. It enumerates GigE,
/// USB3 Vision and GenTL GigE devices directly instead of relying on
/// DirectShow/OpenCV camera indexes.
/// </summary>
public sealed class MvsCameraService : ICameraService
{
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

    private void CaptureFrames()
    {
        IDevice? device = _device;
        if (device == null)
            return;

        try
        {
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
        FramesPerSecond =
            result == MvError.MV_OK
                ? frameRate.CurValue
                : 0;
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
