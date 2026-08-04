using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using OpenCvSharp;
using System.Windows.Media.Imaging;
using WpfApp3.Helpers;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for Camera control and display
    /// Responsible for managing camera state and interactions
    /// </summary>
    public partial class CameraViewModel : ObservableObject, IDisposable
    {
        private readonly ICameraService _cameraService;
        private System.Windows.Threading.DispatcherTimer? _frameTimer;
        private BackgroundSubtractorMOG2? _captureZoneBackground;
        private long _lastCaptureZoneFrameSequence = -1;
        private int _captureZoneWarmupFrames;
        private int _captureZoneStableFrames;
        private int _captureZoneClearFrames;
        private bool _autoCaptureInProgress;
        private bool _disposed;

        private const int CaptureZoneWarmupTarget = 150;
        private const int CaptureZoneStableTarget = 4;
        private const int CaptureZoneClearTarget = 10;
        private const double CaptureZoneMinimumContourPercent = 1.5;

        [ObservableProperty]
        private string cameraStatus = "Disconnected";

        [ObservableProperty]
        private string cameraResolution = "N/A";

        [ObservableProperty]
        private double cameraFrameRate = 0.0;

        [ObservableProperty]
        private string lensType = "Standard";

        [ObservableProperty]
        private double exposureTime = 0.0;

        [ObservableProperty]
        private double cameraTemperature = 0.0;

        [ObservableProperty]
        private int capturedFrames = 0;

        [ObservableProperty]
        private string lastCaptureTime = "N/A";

        [ObservableProperty]
        private bool isRecording = false;

        [ObservableProperty]
        private int recordingDuration = 0;

        [ObservableProperty]
        private ObservableCollection<string> availableCameras = new ObservableCollection<string>();

        private string? _selectedCameraName;
        public string? SelectedCameraName
        {
            get => _selectedCameraName;
            set
            {
                if (SetProperty(ref _selectedCameraName, value))
                {
                    // Update SelectedCamera when the name changes
                    if (!string.IsNullOrEmpty(value) && _cameraLookup.TryGetValue(value, out var camera))
                    {
                        SelectedCamera = camera;
                    }
                }
            }
        }

        private CameraDevice? _selectedCamera;
        public CameraDevice? SelectedCamera
        {
            get => _selectedCamera;
            set => SetProperty(ref _selectedCamera, value);
        }

        [ObservableProperty]
        private BitmapSource? cameraFrame;

        [ObservableProperty]
        private bool isCaptureZoneEnabled = true;

        [ObservableProperty]
        private bool isAutoCaptureEnabled;

        [ObservableProperty]
        private bool captureZoneLatched;

        [ObservableProperty]
        private string captureZoneStatus = "Waiting for camera";

        [ObservableProperty]
        private double captureZoneForegroundPercent;

        public ObservableCollection<string> SystemLog =>
            SystemLogService.Entries;

        public ObservableCollection<string> SessionSystemLog =>
            SystemLogService.SessionEntries;

        public string SystemLogPath =>
            SystemLogService.LogFilePath;

        public string LatestSystemLogEntry =>
            SessionSystemLog.Count == 0
                ? "No process event recorded"
                : SessionSystemLog[^1];

        public int SystemLogEntryCount => SessionSystemLog.Count;

        public string CurrentWorkflowStage =>
            GetWorkflowStage(LatestSystemLogEntry);

        private Dictionary<string, CameraDevice> _cameraLookup = new();

        public CameraViewModel()
        {
            // Initialize camera service
            _cameraService = new MvsCameraService();

            SessionSystemLog.CollectionChanged +=
                SystemLog_CollectionChanged;

            // Subscribe to frame updates
            StartFrameRefresh();
        }

        private void SystemLog_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(LatestSystemLogEntry));
            OnPropertyChanged(nameof(SystemLogEntryCount));
            OnPropertyChanged(nameof(CurrentWorkflowStage));
        }

        private static string GetWorkflowStage(string entry)
        {
            if (entry.Contains(
                    "[IMITECH]",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "1. Image acquisition";
            }

            if (entry.Contains(
                    "[AI]",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "2. Windows AI inspection";
            }

            if (entry.Contains(
                    "[CONVEYOR]",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "3. Conveyor transport";
            }

            if (entry.Contains(
                    "[DOFBOT CAM]",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "4. Entry-zone monitoring";
            }

            if (entry.Contains(
                    "[ROBOT]",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "5. Robot handling cycle";
            }

            return "System idle / initialization";
        }

        /// <summary>
        /// Start a background task to pull frames from the service and convert them on UI thread
        /// </summary>
        private void StartFrameRefresh()
        {
            _frameTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _frameTimer.Tick += (s, e) =>
            {
                try
                {
                    // Get the raw Mat frame from camera service
                    var matFrame = _cameraService.GetCurrentMatFrame();
                    if (matFrame == null || matFrame.Empty())
                        return;

                    using Mat previewFrame = matFrame.Clone();
                    UpdateCaptureZone(matFrame, previewFrame);

                    // The overlay is drawn only on the preview clone. Manual
                    // and automatic captures still save the untouched raw
                    // frame held by the camera service.
                    var bitmap = WpfBitmapHelper.MatToWriteableBitmap(previewFrame);
                    if (bitmap != null)
                    {
                        CameraFrame = bitmap;
                        if (_cameraService.FrameWidth > 0 &&
                            _cameraService.FrameHeight > 0)
                        {
                            CameraResolution =
                                $"{_cameraService.FrameWidth}×" +
                                $"{_cameraService.FrameHeight}";
                        }

                        if (_cameraService.FramesPerSecond > 0)
                            CameraFrameRate =
                                _cameraService.FramesPerSecond;
                    }

                    // Dispose the Mat after conversion
                    matFrame?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in frame refresh: {ex.Message}");
                }
            };
            _frameTimer.Start();
        }

        [RelayCommand]
        private void Connect()
        {
            if (SelectedCamera == null)
            {
                LogMessage("No camera selected");
                return;
            }

            try
            {
                bool started = _cameraService.StartCamera(
                    SelectedCamera);
                if (!started || !_cameraService.IsRunning)
                {
                    CameraStatus = "Error";
                    string detail =
                        (_cameraService as MvsCameraService)
                            ?.LastError ?? "";
                    LogMessage(
                        "MVS could not open the selected camera stream. " +
                        "Stop grabbing in the MVS application and try again." +
                        (string.IsNullOrWhiteSpace(detail)
                            ? ""
                            : $" Detail: {detail}"));
                    return;
                }

                CameraStatus = "Connected";
                CameraResolution =
                    _cameraService.FrameWidth > 0
                        ? $"{_cameraService.FrameWidth}×" +
                          $"{_cameraService.FrameHeight}"
                        : "Waiting for first frame";
                CameraFrameRate = _cameraService.FramesPerSecond;
                ResetCaptureZoneState(logReset: false);
                LogMessage($"Connected to {SelectedCamera.Name}");
            }
            catch (Exception ex)
            {
                CameraStatus = "Error";
                LogMessage($"Error connecting to camera: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void Disconnect()
        {
            try
            {
                _cameraService.StopCamera();
                CameraStatus = "Disconnected";
                CameraResolution = "N/A";
                CameraFrameRate = 0;
                CameraFrame = null;
                CaptureZoneStatus = IsCaptureZoneEnabled
                    ? "Waiting for camera"
                    : "Disabled";
                LogMessage("Disconnected from camera");
            }
            catch (Exception ex)
            {
                LogMessage($"Error disconnecting: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void Refresh()
        {
            try
            {
                AvailableCameras.Clear();
                _cameraLookup.Clear();
                var cameras = MvsCameraService.EnumerateCameras();

                if (cameras.Count == 0)
                {
                    LogMessage("No cameras detected");
                    SelectedCamera = null;
                    SelectedCameraName = null;
                    return;
                }

                foreach (var camera in cameras)
                {
                    AvailableCameras.Add(camera.Name);
                    _cameraLookup[camera.Name] = camera;
                }

                // Select first camera by default
                if (AvailableCameras.Count > 0)
                {
                    SelectedCameraName = AvailableCameras[0];
                }
                LogMessage($"Found {cameras.Count} camera(s)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing cameras: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void Capture()
        {
            if (!_cameraService.IsRunning)
            {
                LogMessage("Camera is not running");
                return;
            }

            try
            {
                string? filepath = _cameraService.CaptureFrame();
                if (filepath != null)
                {
                    CapturedFrames++;
                    LastCaptureTime = DateTime.Now.ToString("s");
                    LogMessage($"Frame captured: {filepath}");
                }
                else
                {
                    LogMessage("Failed to capture frame");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error capturing frame: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void StartRecording()
        {
            if (!IsRecording)
            {
                IsRecording = true;
                RecordingDuration = 0;
                LogMessage("Started recording");
            }
        }

        [RelayCommand]
        private void StopRecording()
        {
            if (IsRecording)
            {
                IsRecording = false;
                LogMessage($"Stopped recording after {RecordingDuration} seconds");
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            SystemLogService.Clear();
        }

        [RelayCommand]
        private void ResetCaptureZone() =>
            ResetCaptureZoneState(logReset: true);

        partial void OnIsCaptureZoneEnabledChanged(bool value)
        {
            if (value)
            {
                ResetCaptureZoneState(logReset: true);
            }
            else
            {
                CaptureZoneLatched = false;
                CaptureZoneForegroundPercent = 0;
                CaptureZoneStatus = "Disabled";
                LogMessage("Capture Zone disabled");
            }
        }

        private void ResetCaptureZoneState(bool logReset)
        {
            _captureZoneBackground?.Dispose();
            _captureZoneBackground =
                BackgroundSubtractorMOG2.Create(
                    history: 500,
                    varThreshold: 20,
                    detectShadows: false);
            _lastCaptureZoneFrameSequence = -1;
            _captureZoneWarmupFrames = 0;
            _captureZoneStableFrames = 0;
            _captureZoneClearFrames = 0;
            CaptureZoneLatched = false;
            CaptureZoneForegroundPercent = 0;
            CaptureZoneStatus = IsCaptureZoneEnabled
                ? "Learning empty conveyor background"
                : "Disabled";

            if (logReset && IsCaptureZoneEnabled)
            {
                LogMessage(
                    "Capture Zone reset. Keep the zone empty while the " +
                    $"first {CaptureZoneWarmupTarget} camera frames establish " +
                    "the moving conveyor background.");
            }
        }

        private void UpdateCaptureZone(Mat rawFrame, Mat previewFrame)
        {
            if (!IsCaptureZoneEnabled || rawFrame.Empty())
                return;

            Rect zone = CreateCaptureZone(rawFrame.Width, rawFrame.Height);
            long sequence = _cameraService.FrameSequence;
            if (sequence != _lastCaptureZoneFrameSequence)
            {
                _lastCaptureZoneFrameSequence = sequence;
                AnalyseCaptureZone(rawFrame, zone);
            }

            Scalar colour = CaptureZoneLatched
                ? new Scalar(40, 40, 245)
                : _captureZoneStableFrames > 0
                    ? new Scalar(0, 165, 255)
                    : new Scalar(0, 215, 255);
            int thickness = Math.Max(2, rawFrame.Width / 1200);
            double fontScale = Math.Max(0.65, rawFrame.Width / 2600.0);
            Cv2.Rectangle(previewFrame, zone, colour, thickness);
            Cv2.PutText(
                previewFrame,
                $"CAPTURE ZONE - {CaptureZoneStatus}",
                new Point(zone.X, Math.Max(30, zone.Y - 12)),
                HersheyFonts.HersheySimplex,
                fontScale,
                colour,
                thickness,
                LineTypes.AntiAlias);
        }

        private void AnalyseCaptureZone(Mat rawFrame, Rect zone)
        {
            _captureZoneBackground ??=
                BackgroundSubtractorMOG2.Create(500, 20, false);

            using var roi = new Mat(rawFrame, zone);
            int analysisWidth = Math.Min(480, roi.Width);
            int analysisHeight = Math.Max(
                1,
                (int)Math.Round(
                    roi.Height * (analysisWidth / (double)roi.Width)));
            using var resized = new Mat();
            Cv2.Resize(roi, resized, new OpenCvSharp.Size(
                analysisWidth,
                analysisHeight));
            using var gray = new Mat();
            Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(7, 7), 0);
            using var foreground = new Mat();

            double learningRate =
                _captureZoneWarmupFrames < CaptureZoneWarmupTarget
                    ? 0.03
                    : 0.005;
            _captureZoneBackground.Apply(gray, foreground, learningRate);

            using Mat kernel = Cv2.GetStructuringElement(
                MorphShapes.Rect,
                new OpenCvSharp.Size(5, 5));
            Cv2.MorphologyEx(
                foreground,
                foreground,
                MorphTypes.Open,
                kernel);
            Cv2.MorphologyEx(
                foreground,
                foreground,
                MorphTypes.Close,
                kernel,
                iterations: 2);

            if (_captureZoneWarmupFrames < CaptureZoneWarmupTarget)
            {
                _captureZoneWarmupFrames++;
                CaptureZoneForegroundPercent = 0;
                CaptureZoneStatus =
                    $"Learning background {_captureZoneWarmupFrames}/" +
                    $"{CaptureZoneWarmupTarget}";
                return;
            }

            Cv2.FindContours(
                foreground,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);
            double largestArea = contours.Length == 0
                ? 0
                : contours.Max(contour => Cv2.ContourArea(contour));
            double analysisArea = analysisWidth * analysisHeight;
            CaptureZoneForegroundPercent =
                analysisArea > 0
                    ? largestArea / analysisArea * 100.0
                    : 0;

            bool objectPresent =
                CaptureZoneForegroundPercent >=
                CaptureZoneMinimumContourPercent;
            if (objectPresent)
            {
                _captureZoneStableFrames++;
                _captureZoneClearFrames = 0;
                if (!CaptureZoneLatched &&
                    _captureZoneStableFrames >=
                    CaptureZoneStableTarget)
                {
                    CaptureZoneLatched = true;
                    CaptureZoneStatus = "Object latched";
                    LogMessage(
                        $"Capture Zone trigger latched at " +
                        $"{CaptureZoneForegroundPercent:0.0}% foreground.");

                    if (IsAutoCaptureEnabled)
                        BeginAutomaticCapture();
                    else
                        LogMessage("Auto capture is OFF; preview trigger only.");
                }
                else if (!CaptureZoneLatched)
                {
                    CaptureZoneStatus =
                        $"Candidate {_captureZoneStableFrames}/" +
                        $"{CaptureZoneStableTarget}";
                }
            }
            else
            {
                _captureZoneStableFrames = 0;
                _captureZoneClearFrames++;
                if (CaptureZoneLatched &&
                    _captureZoneClearFrames >= CaptureZoneClearTarget)
                {
                    CaptureZoneLatched = false;
                    CaptureZoneStatus = "Armed";
                    LogMessage("Capture Zone rearmed after the object cleared.");
                }
                else if (!CaptureZoneLatched)
                {
                    CaptureZoneStatus = "Armed";
                }
            }
        }

        private async void BeginAutomaticCapture()
        {
            if (_autoCaptureInProgress || !_cameraService.IsRunning)
                return;

            _autoCaptureInProgress = true;
            try
            {
                string? path = await Task.Run(
                    _cameraService.CaptureFrame);
                if (_disposed)
                    return;

                if (path == null)
                {
                    LogMessage("Automatic Capture Zone frame could not be saved.");
                    return;
                }

                CapturedFrames++;
                LastCaptureTime = DateTime.Now.ToString("s");
                LogMessage(
                    $"Automatic raw frame captured for AI: {path}");
            }
            catch (Exception ex)
            {
                LogMessage($"Automatic capture failed: {ex.Message}");
            }
            finally
            {
                _autoCaptureInProgress = false;
            }
        }

        private static Rect CreateCaptureZone(int width, int height)
        {
            // A single central box covers 65% of both image dimensions. The
            // normalized size keeps the overlay consistent at every camera
            // resolution while leaving a visible margin around the conveyor.
            int zoneWidth = Math.Max(1, (int)(width * 0.65));
            int zoneHeight = Math.Max(1, (int)(height * 0.65));
            int x = Math.Max(0, (width - zoneWidth) / 2);
            int y = Math.Max(0, (height - zoneHeight) / 2);
            return new Rect(x, y, zoneWidth, zoneHeight);
        }

        private void LogMessage(string message)
        {
            SystemLogService.Add("IMITECH", message);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _frameTimer?.Stop();
            _frameTimer = null;
            SessionSystemLog.CollectionChanged -=
                SystemLog_CollectionChanged;
            _cameraService.StopCamera();
            _cameraService.Dispose();
            _captureZoneBackground?.Dispose();
            _captureZoneBackground = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
