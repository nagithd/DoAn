using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
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
        private readonly AiInferenceClient _aiInferenceClient;
        private System.Windows.Threading.DispatcherTimer? _frameTimer;
        private long _lastCaptureZoneFrameSequence = -1;
        private int _captureZoneStableFrames;
        private int _captureZoneClearFrames;
        private bool _captureZoneAiRequestInProgress;
        private DateTime _lastCaptureZoneAiRequestUtc = DateTime.MinValue;
        private bool _autoCaptureInProgress;
        private bool _disposed;

        private const int CaptureZoneStableTarget = 2;
        private const int CaptureZoneClearTarget = 3;
        private static readonly TimeSpan CaptureZoneAiInterval =
            TimeSpan.FromMilliseconds(200);

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
        private double captureZoneAiConfidence;

        [ObservableProperty]
        private string aiServiceStatus = "Checking local AI service";

        [ObservableProperty]
        private string lastAiResult = "No result yet";

        private InspectionResult? _lastInspectionResult;

        public event EventHandler<InspectionResult>? InspectionResultRequested;

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
            _aiInferenceClient = new AiInferenceClient();

            SessionSystemLog.CollectionChanged +=
                SystemLog_CollectionChanged;

            // Subscribe to frame updates
            StartFrameRefresh();
            _ = CheckAiServiceAvailabilityAsync();
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

        [RelayCommand]
        private async Task CheckAiService() =>
            await CheckAiServiceAvailabilityAsync(logSuccess: true);

        [RelayCommand]
        private void ShowLastAiResult()
        {
            if (_lastInspectionResult == null)
            {
                SystemLogService.Add("AI", "No AI inference result is available yet.");
                return;
            }

            InspectionResultRequested?.Invoke(this, _lastInspectionResult);
        }

        partial void OnIsCaptureZoneEnabledChanged(bool value)
        {
            if (value)
            {
                ResetCaptureZoneState(logReset: true);
            }
            else
            {
                CaptureZoneLatched = false;
                CaptureZoneAiConfidence = 0;
                CaptureZoneStatus = "Disabled";
                LogMessage("Capture Zone disabled");
            }
        }

        private void ResetCaptureZoneState(bool logReset)
        {
            _lastCaptureZoneFrameSequence = -1;
            _captureZoneStableFrames = 0;
            _captureZoneClearFrames = 0;
            _lastCaptureZoneAiRequestUtc = DateTime.MinValue;
            CaptureZoneLatched = false;
            CaptureZoneAiConfidence = 0;
            CaptureZoneStatus = IsCaptureZoneEnabled
                ? "YOLO armed"
                : "Disabled";

            if (logReset && IsCaptureZoneEnabled)
            {
                LogMessage(
                    "Capture Zone reset. YOLO will trigger only after " +
                    $"detecting a battery in {CaptureZoneStableTarget} " +
                    "consecutive checks.");
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
                QueueCaptureZoneAiCheck(rawFrame, zone);
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

        private void QueueCaptureZoneAiCheck(Mat rawFrame, Rect zone)
        {
            if (_captureZoneAiRequestInProgress ||
                _autoCaptureInProgress ||
                DateTime.UtcNow - _lastCaptureZoneAiRequestUtc <
                CaptureZoneAiInterval)
            {
                return;
            }

            using var roi = new Mat(rawFrame, zone);
            int analysisWidth = Math.Min(640, roi.Width);
            int analysisHeight = Math.Max(
                1,
                (int)Math.Round(
                    roi.Height * (analysisWidth / (double)roi.Width)));
            using var resized = new Mat();
            Cv2.Resize(roi, resized, new OpenCvSharp.Size(
                analysisWidth,
                analysisHeight));
            Cv2.ImEncode(
                ".jpg",
                resized,
                out byte[] jpeg,
                new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));
            _captureZoneAiRequestInProgress = true;
            _lastCaptureZoneAiRequestUtc = DateTime.UtcNow;
            _ = AnalyseCaptureZoneWithAiAsync(jpeg);
        }

        private async Task AnalyseCaptureZoneWithAiAsync(byte[] jpeg)
        {
            try
            {
                AiZoneDetectionResponse response =
                    await _aiInferenceClient.DetectZoneAsync(jpeg);
                if (_disposed || !IsCaptureZoneEnabled)
                    return;

                CaptureZoneAiConfidence = response.Confidence;
                if (response.BatteryDetected)
                {
                    _captureZoneStableFrames++;
                    _captureZoneClearFrames = 0;
                    if (!CaptureZoneLatched &&
                        _captureZoneStableFrames >=
                        CaptureZoneStableTarget)
                    {
                        CaptureZoneLatched = true;
                        CaptureZoneStatus = "Battery latched by YOLO";
                        LogMessage(
                            $"Capture Zone YOLO trigger latched at " +
                            $"{response.Confidence:P1} confidence.");

                        if (IsAutoCaptureEnabled)
                            BeginAutomaticCapture();
                        else
                            LogMessage("Auto capture is OFF; preview trigger only.");
                    }
                    else if (!CaptureZoneLatched)
                    {
                        CaptureZoneStatus =
                            $"YOLO candidate {_captureZoneStableFrames}/" +
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
                        CaptureZoneStatus = "YOLO armed";
                        LogMessage(
                            "Capture Zone rearmed after YOLO confirmed " +
                            "that the battery cleared.");
                    }
                    else if (!CaptureZoneLatched)
                    {
                        CaptureZoneStatus = "YOLO armed";
                    }
                }
            }
            catch (Exception ex)
            {
                CaptureZoneStatus = "YOLO service unavailable";
                AiServiceStatus = "Capture Zone AI check failed";
                Debug.WriteLine($"Capture Zone AI check failed: {ex.Message}");
            }
            finally
            {
                _captureZoneAiRequestInProgress = false;
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
                await RunAiInferenceAsync(path);
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

        private async Task CheckAiServiceAvailabilityAsync(
            bool logSuccess = false)
        {
            try
            {
                AiHealthResponse health =
                    await _aiInferenceClient.GetHealthAsync();
                if (_disposed)
                    return;

                AiServiceStatus = health.Status.Equals(
                        "ok",
                        StringComparison.OrdinalIgnoreCase)
                    ? "Connected — model loaded"
                    : $"Service status: {health.Status}";
                if (logSuccess)
                {
                    SystemLogService.Add(
                        "AI",
                        $"AI service connected. Model: {health.ModelPath}");
                }
            }
            catch (Exception ex)
            {
                if (_disposed)
                    return;

                AiServiceStatus = "Offline — start AI service on port 7100";
                if (logSuccess)
                    SystemLogService.Add("AI", $"AI service unavailable: {ex.Message}");
            }
        }

        private async Task RunAiInferenceAsync(string imagePath)
        {
            AiServiceStatus = "Running inference";
            try
            {
                AiPredictionResponse response =
                    await _aiInferenceClient.PredictAsync(imagePath);
                if (_disposed)
                    return;

                var result = new InspectionResult
                {
                    Status = response.Status.ToLowerInvariant() switch
                    {
                        "detected" => InspectionResultStatus.Detected,
                        "rejected" => InspectionResultStatus.Rejected,
                        _ => InspectionResultStatus.Error
                    },
                    DetectedClass = response.DetectedClass,
                    Confidence = response.Confidence,
                    RecommendedRobotCycle = response.RecommendedRobotCycle,
                    InferenceTimeMs = response.InferenceTimeMs,
                    InspectionTime = response.InspectionTime.ToLocalTime(),
                    AnnotatedImage = LoadCapturedImage(response.ImagePath),
                    BoundingBoxes = new ObservableCollection<BoundingBox>(
                        response.Detections.Select(detection => new BoundingBox
                        {
                            ClassName = detection.ClassName,
                            Confidence = detection.Confidence,
                            X = detection.X,
                            Y = detection.Y,
                            Width = detection.Width,
                            Height = detection.Height
                        })),
                    Error = string.IsNullOrWhiteSpace(response.DetectedClass)
                        ? "The AI service did not detect a battery."
                        : null
                };

                _lastInspectionResult = result;
                string displayedClass = string.IsNullOrWhiteSpace(
                        response.DetectedClass)
                    ? "no battery"
                    : response.DetectedClass;
                LastAiResult =
                    $"{displayedClass} — {response.Confidence:P1} — " +
                    $"{response.InferenceTimeMs:0.0} ms";
                AiServiceStatus = "Connected — last inference completed";
                SystemLogService.Add(
                    "AI",
                    $"Capture Zone result: {displayedClass}, " +
                    $"confidence {response.Confidence:P1}, " +
                    $"{response.Detections.Count} box(es), " +
                    $"{response.InferenceTimeMs:0.0} ms.");
            }
            catch (Exception ex)
            {
                AiServiceStatus = "Inference failed";
                LastAiResult = "AI error";
                SystemLogService.Add("AI", $"Inference failed: {ex.Message}");
            }
        }

        private static BitmapSource LoadCapturedImage(string imagePath)
        {
            using var stream = new FileStream(
                imagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
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
            _aiInferenceClient.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
