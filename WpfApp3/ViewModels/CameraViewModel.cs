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
        private readonly SemaphoreSlim _synchronizedCaptureGate = new(1, 1);
        private Process? _ownedAiServiceProcess;
        private const int LivePreviewMaxWidth = 480;
        private const int LivePreviewRefreshMs = 250;
        private const int CameraStopSettleDelayMs = 0;
        private const int FreshFrameTimeoutMs = 2000;
        private const int FreshFramePollIntervalMs = 20;
        private bool _disposed;

        [ObservableProperty]
        private string cameraStatus = "Disconnected";

        [ObservableProperty]
        private string cameraResolution = "N/A";

        [ObservableProperty]
        private string previewResolution = "N/A";

        [ObservableProperty]
        private double cameraFrameRate = 0.0;

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
        private string aiServiceStatus = "Checking local AI service";

        [ObservableProperty]
        private InspectionResult? lastInspectionResult;

        public ObservableCollection<string> SessionSystemLog =>
            SystemLogService.SessionEntries;

        public int SystemLogEntryCount => SessionSystemLog.Count;

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

        /// <summary>
        /// Loads an explicitly requested report-only preview. This method is
        /// never called during normal operation; ReportPreviewExporter invokes
        /// it only when WPFAPP3_REPORT_PREVIEW_DIR is set.
        /// </summary>
        public InspectionResult LoadReportPlaceholder(string imagePath)
        {
            BitmapSource image = LoadCapturedImage(imagePath);

            CameraFrame = image;
            CameraStatus = "Connected - report preview";
            CameraResolution = $"{image.PixelWidth} x {image.PixelHeight}";
            PreviewResolution = CameraResolution;
            CameraFrameRate = 6.0;
            AiServiceStatus = "Connected - YOLOv8 model ready (report preview)";

            AvailableCameras.Clear();
            AvailableCameras.Add("IMITECH IMB-770GC - report preview");
            SelectedCameraName = AvailableCameras[0];

            // Replace only this process' in-memory history. The persistent
            // system.log file is intentionally left untouched.
            SessionSystemLog.Clear();
            SessionSystemLog.Add(
                "2026-08-09 15:07:41  [REPORT] Documentation placeholder mode - no hardware command is sent.");
            SessionSystemLog.Add(
                "2026-08-09 15:07:41  [IMITECH] Connected to IMB-770GC; synchronized frame acquired.");
            SessionSystemLog.Add(
                "2026-08-09 15:07:41  [CONVEYOR] Arduino confirmed stop at the camera checkpoint.");
            SessionSystemLog.Add(
                "2026-08-20 15:50:36  [AI] Result: dented, confidence 93.2%, 34.5 ms.");
            SessionSystemLog.Add(
                "2026-08-20 15:50:36  [AI ROUTING] Dented battery assigned to the DENTED robot cycle.");

            double width = image.PixelWidth;
            double height = image.PixelHeight;
            var result = new InspectionResult
            {
                InspectionId = "REPORT-PREVIEW-001",
                Status = InspectionResultStatus.Detected,
                DetectedClass = "dented",
                Confidence = 0.932294,
                RecommendedRobotCycle = "ROBOT PICK - DENTED bin",
                InferenceTimeMs = 34.5,
                InspectionTime = DateTime.Now,
                AnnotatedImage = image,
                BoundingBoxes = new ObservableCollection<BoundingBox>
                {
                    new()
                    {
                        ClassName = "battery",
                        Confidence = 0.924805,
                        X = width * 0.3188394502,
                        Y = height * 0.1630798022,
                        Width = width * 0.6008525671,
                        Height = height * 0.7216259321
                    },
                    new()
                    {
                        ClassName = "dented",
                        Confidence = 0.932294,
                        X = width * 0.6801046771,
                        Y = height * 0.2849521955,
                        Width = width * 0.1394041638,
                        Height = height * 0.1314658801
                    },
                    new()
                    {
                        ClassName = "dented",
                        Confidence = 0.893623,
                        X = width * 0.5027226293,
                        Y = height * 0.4931748390,
                        Width = width * 0.1519381501,
                        Height = height * 0.0955348651
                    }
                }
            };

            LastInspectionResult = result;
            return result;
        }

        private void SystemLog_CollectionChanged(
            object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SystemLogEntryCount));
        }

        /// <summary>
        /// Start a background task to pull frames from the service and convert them on UI thread
        /// </summary>
        private void StartFrameRefresh()
        {
            _frameTimer = new System.Windows.Threading.DispatcherTimer
            {
                // Preserve the full frame for Capture/AI and update only a
                // reduced WPF preview at about four frames per second.
                Interval = TimeSpan.FromMilliseconds(LivePreviewRefreshMs)
            };
            _frameTimer.Tick += (s, e) =>
            {
                try
                {
                    // Ask the service for an already reduced copy so the UI
                    // does not clone the 3856 x 2764 source frame on each tick.
                    using var previewFrame =
                        _cameraService.GetPreviewMatFrame(LivePreviewMaxWidth);
                    if (previewFrame == null || previewFrame.Empty())
                        return;

                    // The Arduino sensor now determines the capture instant.
                    // The live preview therefore shows the untouched frame;
                    // no image-based Capture Zone is evaluated or rendered.
                    var bitmap =
                        WpfBitmapHelper.MatToWriteableBitmap(previewFrame);
                    if (bitmap != null)
                    {
                        CameraFrame = bitmap;
                        PreviewResolution =
                            $"{bitmap.PixelWidth} x {bitmap.PixelHeight}";
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

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in frame refresh: {ex.Message}");
                }
            };
            _frameTimer.Start();
        }

        /// <summary>
        /// Suspends only WPF bitmap rendering while the AI Camera tab is
        /// hidden. Camera acquisition and the source frame used by
        /// Arduino-synchronized Capture/AI remain active.
        /// </summary>
        public void SetLivePreviewActive(bool active)
        {
            if (_disposed || _frameTimer == null)
                return;

            if (active)
            {
                if (!_frameTimer.IsEnabled)
                    _frameTimer.Start();
            }
            else
            {
                _frameTimer.Stop();
            }
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
                LogMessage($"Connected to {SelectedCamera.Name}");
                if (_cameraService is MvsCameraService mvsService)
                {
                    LogMessage(
                        "IMB-770GC/MVS image nodes (read-only check): " +
                        mvsService.ImagingCapabilitiesSummary);
                    LogMessage(
                        "IMB-770GC/MVS frame-rate profile: " +
                        mvsService.FrameRateConfigurationSummary);
                    LogMessage(
                        $"Live preview limited to {LivePreviewMaxWidth}px " +
                        "wide at approximately 4 FPS; Capture/AI keeps the " +
                        "original camera frame.");
                }
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
                PreviewResolution = "N/A";
                CameraFrameRate = 0;
                CameraFrame = null;
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
        private async Task CheckAiService() =>
            await CheckAiServiceAvailabilityAsync(logSuccess: true);

        public async Task<InspectionResult?> CaptureAndInspectFromArduinoAsync(
            CancellationToken cancellationToken = default)
        {
            if (!_cameraService.IsRunning)
            {
                LogMessage(
                    "Arduino requested a synchronized capture, but the camera is not running.");
                return null;
            }

            if (!await _synchronizedCaptureGate.WaitAsync(0, cancellationToken))
            {
                LogMessage(
                    "Duplicate Arduino camera-stop event ignored while capture is in progress.");
                return null;
            }

            try
            {
                LogMessage(
                    "Arduino camera-stop event received; waiting for the " +
                    $"conveyor to settle for {CameraStopSettleDelayMs} ms.");

                await Task.Delay(
                    CameraStopSettleDelayMs,
                    cancellationToken);

                // Capture a fresh frame from the configured ROI stream.
                // Handshake firmware holds the conveyor until this inspection
                // has finished and the caller acknowledges CAMERA_READY.
                long frameSequenceAfterSettle =
                    _cameraService.FrameSequence;
                bool freshFrameAvailable =
                    await WaitForFreshFrameAfterAsync(
                        frameSequenceAfterSettle,
                        cancellationToken);
                if (!freshFrameAvailable)
                {
                    LogMessage(
                        "Synchronized capture was skipped because IMITECH " +
                        $"did not deliver a new frame within {FreshFrameTimeoutMs} ms.");
                    return null;
                }

                string? path = await Task.Run(
                    _cameraService.CaptureFrame,
                    cancellationToken);
                if (_disposed)
                    return null;

                if (path == null)
                {
                    LogMessage("Arduino-synchronized frame could not be saved.");
                    return null;
                }

                LogMessage(
                    $"Arduino-synchronized raw frame captured for AI: {path}");
                InspectionResult? result = await RunAiInferenceAsync(
                    path,
                    cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                LogMessage($"Arduino-synchronized capture failed: {ex.Message}");
                return null;
            }
            finally
            {
                _synchronizedCaptureGate.Release();
            }
        }

        private async Task<bool> WaitForFreshFrameAfterAsync(
            long previousFrameSequence,
            CancellationToken cancellationToken)
        {
            var timeout = Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < FreshFrameTimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_cameraService.FrameSequence > previousFrameSequence)
                    return true;

                await Task.Delay(
                    FreshFramePollIntervalMs,
                    cancellationToken);
            }

            return false;
        }

        public async Task<bool> PrepareCameraAndAiForSystemStartAsync()
        {
            bool aiReady = await CheckAiServiceAvailabilityAsync();
            if (!aiReady && TryStartAiService())
            {
                AiServiceStatus = "Starting AI service...";
                for (int attempt = 0; attempt < 45 && !aiReady; attempt++)
                {
                    await Task.Delay(1000);
                    aiReady = await CheckAiServiceAvailabilityAsync();
                }
            }

            if (!aiReady)
                return false;

            await CheckAiServiceAvailabilityAsync(logSuccess: true);

            if (!_cameraService.IsRunning)
            {
                if (SelectedCamera == null)
                    Refresh();

                Connect();
            }

            return _cameraService.IsRunning && aiReady;
        }

        /// <summary>
        /// Stops only the IMITECH acquisition owned by WPF. The AI service is
        /// deliberately left running so a later START SYSTEM does not pay the
        /// model warm-up cost again.
        /// </summary>
        public void StopCameraForSystem()
        {
            SetLivePreviewActive(false);
            Disconnect();
            SystemLogService.Add(
                "AI",
                "STOP SYSTEM left the AI service running for a fast restart.");
        }

        private bool TryStartAiService()
        {
            if (_ownedAiServiceProcess is { HasExited: false })
                return true;

            if (TryStartBundledAiService())
                return true;

            if (TryStartDevelopmentAiService())
                return true;

            SystemLogService.Add(
                "AI",
                "AI service is offline and no runnable bundled or " +
                "development service was found. Place the offline " +
                "AIService folder beside WpfApp3.exe or configure " +
                "WPFAPP3_AI_SERVICE_EXE/WPFAPP3_AI_SERVICE_STARTER.");
            return false;
        }

        private bool TryStartBundledAiService()
        {
            string? configuredExecutable =
                Environment.GetEnvironmentVariable("WPFAPP3_AI_SERVICE_EXE");
            string[] executableCandidates =
            [
                configuredExecutable ?? "",
                Path.Combine(
                    AppContext.BaseDirectory,
                    "AIService",
                    "BatteryAIService.exe"),
                Path.Combine(
                    AppContext.BaseDirectory,
                    "BatteryAIService.exe")
            ];

            string? executablePath = executableCandidates
                .FirstOrDefault(File.Exists);
            if (executablePath == null)
                return false;

            string executableDirectory =
                Path.GetDirectoryName(executablePath) ??
                AppContext.BaseDirectory;
            string? configuredModel =
                Environment.GetEnvironmentVariable("AI_MODEL_PATH");
            string[] modelCandidates =
            [
                configuredModel ?? "",
                Path.Combine(executableDirectory, "models", "best.pt"),
                Path.Combine(
                    AppContext.BaseDirectory,
                    "AIService",
                    "models",
                    "best.pt")
            ];
            string? modelPath = modelCandidates.FirstOrDefault(File.Exists);
            if (modelPath == null)
            {
                SystemLogService.Add(
                    "AI",
                    "Bundled AI executable was found, but models/best.pt " +
                    "is missing. The conveyor will remain stopped.");
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = executableDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("--model");
                startInfo.ArgumentList.Add(modelPath);
                startInfo.ArgumentList.Add("--host");
                startInfo.ArgumentList.Add("127.0.0.1");
                startInfo.ArgumentList.Add("--port");
                startInfo.ArgumentList.Add("7100");
                startInfo.ArgumentList.Add("--device");
                startInfo.ArgumentList.Add("cpu");
                startInfo.ArgumentList.Add("--imgsz");
                startInfo.ArgumentList.Add(
                    Environment.GetEnvironmentVariable("AI_IMAGE_SIZE") ??
                    "512");

                _ownedAiServiceProcess = Process.Start(startInfo);
                SystemLogService.Add(
                    "AI",
                    $"Started bundled AI service with model: {modelPath}");
                return _ownedAiServiceProcess != null;
            }
            catch (Exception ex)
            {
                SystemLogService.Add(
                    "AI",
                    $"Cannot start bundled AI service: {ex.Message}");
                return false;
            }
        }

        private bool TryStartDevelopmentAiService()
        {
            string? configuredStarter =
                Environment.GetEnvironmentVariable(
                    "WPFAPP3_AI_SERVICE_STARTER");
            string? starterPath = !string.IsNullOrWhiteSpace(configuredStarter) &&
                                  File.Exists(configuredStarter)
                ? configuredStarter
                : FindFileAboveAppDirectory(
                    "Tools",
                    "AI",
                    "Service",
                    "start_ai_service.ps1");

            if (starterPath == null)
                return false;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    WorkingDirectory =
                        Path.GetDirectoryName(starterPath) ??
                        AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(starterPath);

                string? configuredModel =
                    Environment.GetEnvironmentVariable("AI_MODEL_PATH");
                if (!string.IsNullOrWhiteSpace(configuredModel))
                {
                    startInfo.ArgumentList.Add("-ModelPath");
                    startInfo.ArgumentList.Add(configuredModel);
                }

                _ownedAiServiceProcess = Process.Start(startInfo);
                SystemLogService.Add(
                    "AI",
                    $"Started development AI service: {starterPath}");
                return _ownedAiServiceProcess != null;
            }
            catch (Exception ex)
            {
                SystemLogService.Add(
                    "AI",
                    $"Cannot start development AI service: {ex.Message}");
                return false;
            }
        }

        private static string? FindFileAboveAppDirectory(
            params string[] relativeSegments)
        {
            DirectoryInfo? directory =
                new DirectoryInfo(AppContext.BaseDirectory);

            for (int depth = 0; depth < 8 && directory != null; depth++)
            {
                string candidate = Path.Combine(
                    [directory.FullName, .. relativeSegments]);
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            return null;
        }

        private async Task<bool> CheckAiServiceAvailabilityAsync(
            bool logSuccess = false)
        {
            try
            {
                AiHealthResponse health =
                    await _aiInferenceClient.GetHealthAsync();
                if (_disposed)
                    return false;

                bool isReady = health.Status.Equals(
                    "ok",
                    StringComparison.OrdinalIgnoreCase);
                AiServiceStatus = isReady
                    ? "Connected — model loaded"
                    : $"Service status: {health.Status}";
                if (logSuccess)
                {
                    SystemLogService.Add(
                        "AI",
                        $"AI service connected. Model: {health.ModelPath}");
                }

                return isReady;
            }
            catch (Exception ex)
            {
                if (_disposed)
                    return false;

                AiServiceStatus = "Offline — start AI service on port 7100";
                if (logSuccess)
                    SystemLogService.Add("AI", $"AI service unavailable: {ex.Message}");
                return false;
            }
        }

        private async Task<InspectionResult?> RunAiInferenceAsync(
            string imagePath,
            CancellationToken cancellationToken = default)
        {
            AiServiceStatus = "Running inference";
            try
            {
                AiPredictionResponse response =
                    await _aiInferenceClient.PredictAsync(
                        imagePath,
                        cancellationToken);
                if (_disposed)
                    return null;

                var result = new InspectionResult
                {
                    InspectionId = response.InspectionId,
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

                LastInspectionResult = result;
                string displayedClass = string.IsNullOrWhiteSpace(
                        response.DetectedClass)
                    ? "no battery"
                    : response.DetectedClass;
                AiServiceStatus = "Connected — last inference completed";
                SystemLogService.Add(
                    "AI",
                    $"Arduino-synchronized result: {displayedClass}, " +
                    $"confidence {response.Confidence:P1}, " +
                    $"{response.Detections.Count} box(es), " +
                    $"{response.InferenceTimeMs:0.0} ms.");
                return result;
            }
            catch (Exception ex)
            {
                AiServiceStatus = "Inference failed";
                SystemLogService.Add("AI", $"Inference failed: {ex.Message}");
                return null;
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
            try
            {
                if (_ownedAiServiceProcess is { HasExited: false })
                    _ownedAiServiceProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Cannot stop the AI service started by WPF: {ex.Message}");
            }
            finally
            {
                _ownedAiServiceProcess?.Dispose();
                _ownedAiServiceProcess = null;
            }
            _synchronizedCaptureGate.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
