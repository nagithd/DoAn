using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels;

public partial class VisionTriggerViewModel : ObservableObject, IDisposable
{
    private readonly VisionApiClient _visionApi = new();
    private readonly DispatcherTimer _pollTimer;
    private bool _refreshInProgress;
    private bool _configInitialized;
    private bool _disposed;
    private int _consecutiveRefreshFailures;
    private string? _lastLoggedRoutingKey;
    private InspectionResult? _pendingDirectInspection;
    private bool _checkpointInProgress;
    private const int MaximumRefreshFailures = 3;

    [ObservableProperty]
    private string robotBaseUrl = "http://192.168.137.179:7000";

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool visionRunning;

    [ObservableProperty]
    private bool cameraOpen;

    [ObservableProperty]
    private bool robotReady;

    [ObservableProperty]
    private bool automaticTriggerActive;

    [ObservableProperty]
    private bool automaticTriggerRequested;

    [ObservableProperty]
    private bool triggerLatched;

    [ObservableProperty]
    private long frameCount;

    [ObservableProperty]
    private int stableCount;

    [ObservableProperty]
    private BitmapSource? remoteFrame;

    [ObservableProperty]
    private string detectionSummary = "No object in the entry zone";

    [ObservableProperty]
    private string triggerSummary = "No trigger recorded";

    [ObservableProperty]
    private string jobSummary = "No vision job";

    [ObservableProperty]
    private string timingSummary = "Timing is not available";

    [ObservableProperty]
    private string lastError = "-";

    [ObservableProperty]
    private double beltSpeedMmS = 40;

    [ObservableProperty]
    private string selectedTestClass = "normal";

    [ObservableProperty]
    private double testConfidence = 0.90;

    [ObservableProperty]
    private int aiQueueSize;

    [ObservableProperty]
    private string nextAiResult = "Queue empty";

    [ObservableProperty]
    private string routingSummary = "No routing action";

    [ObservableProperty]
    private double distanceToPickMm = 200;

    [ObservableProperty]
    private int robotTimeToGripMs = 4150;

    [ObservableProperty]
    private int processingMarginMs = 350;

    public VisionTriggerViewModel()
    {
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pollTimer.Tick += async (_, _) =>
            await RefreshStatusAsync(includeFrame: true);
    }

    /// <summary>
    /// Loads a static DOFBOT-camera state for report screenshots only.
    /// The poll timer remains stopped and no HTTP request is sent.
    /// </summary>
    public void LoadReportPlaceholder(string imagePath)
    {
        RemoteFrame = CreateBitmap(File.ReadAllBytes(imagePath));
        ConnectionStatus = "Connected";
        IsConnected = true;
        VisionRunning = true;
        CameraOpen = true;
        RobotReady = true;
        AutomaticTriggerActive = false;
        AutomaticTriggerRequested = false;
        TriggerLatched = false;
        FrameCount = 12840;
        StableCount = 0;
        DetectionSummary = "Battery detected inside the checkpoint zone";
        TriggerSummary = "Arduino checkpoint signal ready";
        JobSummary = "No active robot job";
        RoutingSummary = "scratched -> direct fixed-pose pick";
        NextAiResult = "scratched (84.0%) waiting for Arduino checkpoint";
        AiQueueSize = 0;
        LastError = "-";
    }

    /// <summary>
    /// Stores the latest IMITECH result locally. No classification is sent to
    /// Vision API: Arduino decides when the battery reaches the robot and WPF
    /// then sends one direct /robot/pick command to Jetson.
    /// </summary>
    public void StoreDirectInspectionResult(InspectionResult result)
    {
        string className = result.DetectedClass.Trim().ToLowerInvariant();
        if (className is not ("normal" or "dented" or "scratched" or "swollen"))
        {
            _pendingDirectInspection = null;
            NextAiResult = "No routable AI result";
            SystemLogService.Add(
                "AI ROUTING",
                $"Unsupported AI class '{className}'; no robot signal will be sent.");
            return;
        }

        _pendingDirectInspection = result;
        NextAiResult = $"{className} ({result.Confidence:P1}) waiting for Arduino checkpoint";
        RoutingSummary = className == "normal"
            ? "NORMAL -> conveyor pass"
            : $"{className} -> direct fixed-pose pick";

        SystemLogService.Add(
            "AI ROUTING",
            $"Stored {className} ({result.Confidence:P1}) locally; " +
            "waiting for EVENT:ROBOT_STOPPED.");

        if (result.Confidence < 0.40)
        {
            SystemLogService.Add(
                "AI ROUTING",
                "Low-confidence result will still use the selected class in " +
                "the simplified open-loop test mode.");
        }
    }

    public async Task<bool> PrepareRobotForSystemStartAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _visionApi.Configure(RobotBaseUrl);
            RobotStatusResponse response =
                await _visionApi.GetRobotStatusAsync(cancellationToken);
            RobotStatusResponse robot = response.EffectiveState;

            if (!robot.MotionEnabled || robot.Busy || robot.QueueSize > 0)
            {
                RobotReady = false;
                LastError = $"Robot state: {robot.State}; busy={robot.Busy}; " +
                    $"queue={robot.QueueSize}; motion={robot.MotionEnabled}";
                SystemLogService.Add(
                    "SAFETY",
                    "System start cannot move DOFBOT to HOME because it is " +
                    $"not idle. {LastError}");
                return false;
            }

            if (string.Equals(
                    robot.State,
                    "vision_ready",
                    StringComparison.OrdinalIgnoreCase))
            {
                RobotReady = true;
                LastError = "-";
                SystemLogService.Add(
                    "ROBOT",
                    "DOFBOT is already at HOME and ready for startup.");
                return true;
            }

            if (!string.Equals(
                    robot.State,
                    "idle",
                    StringComparison.OrdinalIgnoreCase))
            {
                RobotReady = false;
                LastError = $"Robot state: {robot.State}";
                SystemLogService.Add(
                    "SAFETY",
                    "Automatic HOME is allowed only from the idle state. " +
                    $"Current state is '{robot.State}'. Operator inspection " +
                    "is required.");
                return false;
            }

            SystemLogService.Add(
                "ROBOT",
                "DOFBOT is idle; START SYSTEM is moving it to HOME.");
            RobotCommandResponse homeResponse =
                await _visionApi.HomeAsync(cancellationToken);
            if (!homeResponse.Success)
            {
                throw new InvalidOperationException(
                    homeResponse.Error ?? "Robot HOME command failed.");
            }

            response = await _visionApi.GetRobotStatusAsync(cancellationToken);
            robot = response.EffectiveState;

            bool ready = robot.MotionEnabled &&
                !robot.Busy &&
                robot.QueueSize == 0 &&
                string.Equals(
                    robot.State,
                    "vision_ready",
                    StringComparison.OrdinalIgnoreCase);

            RobotReady = ready;
            LastError = ready
                ? "-"
                : $"Robot state: {robot.State}; busy={robot.Busy}; " +
                  $"queue={robot.QueueSize}; motion={robot.MotionEnabled}";

            SystemLogService.Add(
                ready ? "ROBOT" : "SAFETY",
                ready
                    ? "DOFBOT reached HOME and is ready for automatic startup."
                    : "DOFBOT did not enter vision_ready after HOME. " +
                      LastError);
            return ready;
        }
        catch (Exception ex)
        {
            RobotReady = false;
            LastError = ex.Message;
            SystemLogService.Add(
                "SAFETY",
                $"System start could not verify Robot API: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stops DOFBOT camera processing and WPF polling without stopping the
    /// Robot API, powering off Jetson, resetting the arm or interrupting a
    /// robot motion command.
    /// </summary>
    public async Task StopForSystemAsync(
        CancellationToken cancellationToken = default)
    {
        _pollTimer.Stop();

        if (IsConnected && VisionRunning)
        {
            try
            {
                VisionStatusResponse status =
                    await _visionApi.StopAsync(cancellationToken);
                ApplyStatus(status);
                SystemLogService.Add(
                    "DOFBOT CAM",
                    "STOP SYSTEM stopped DOFBOT camera processing.");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                SystemLogService.Add(
                    "SAFETY",
                    "DOFBOT camera stop could not be confirmed; WPF polling " +
                    $"will still stop. Detail: {ex.Message}");
            }
        }

        IsConnected = false;
        ConnectionStatus = "Disconnected";
        VisionRunning = false;
        CameraOpen = false;
        RemoteFrame = null;
        SystemLogService.Add(
            "DOFBOT CAM",
            "WPF monitoring stopped. Jetson and Robot API remain powered.");
    }

    /// <summary>
    /// Called only after Arduino confirms that the conveyor is stopped at the
    /// robot. Defects produce one direct Robot API command; normal batteries
    /// produce no motion. Arduino owns the checkpoint dwell and conveyor
    /// restart, so WPF neither waits for job completion nor sends robot_done@.
    /// </summary>
    public async Task<bool> TriggerAtRobotCheckpointAsync(
        CancellationToken cancellationToken = default)
    {
        if (_checkpointInProgress)
        {
            SystemLogService.Add(
                "SAFETY",
                "A robot checkpoint request is already in progress.");
            return false;
        }

        _checkpointInProgress = true;
        try
        {
            InspectionResult? inspection = _pendingDirectInspection;
            if (inspection is null)
            {
                throw new InvalidOperationException(
                    "No local AI result is waiting for the Arduino robot checkpoint.");
            }

            string className = inspection.DetectedClass.Trim().ToLowerInvariant();
            if (className == "normal")
            {
                SystemLogService.Add(
                    "AI ROUTING",
                    "NORMAL -> PASS. No robot command was sent.");
                CompleteArduinoControlledCheckpoint(inspection);
                return true;
            }

            _visionApi.Configure(RobotBaseUrl);
            RobotStatusResponse statusResponse =
                await _visionApi.GetRobotStatusAsync(cancellationToken);
            RobotStatusResponse robot = statusResponse.EffectiveState;

            if (!robot.MotionEnabled)
            {
                throw new InvalidOperationException(
                    "DOFBOT motion is disabled; checkpoint routing was not started.");
            }

            if (robot.Busy || robot.QueueSize > 0)
            {
                throw new InvalidOperationException(
                    "DOFBOT is busy or has a queued job. No new pick was sent; " +
                    "Arduino will continue its programmed cycle.");
            }

            if (!string.Equals(
                    robot.State,
                    "vision_ready",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"DOFBOT is '{robot.State}', not vision_ready. " +
                    "Move it to HOME and verify the gripper is open before retrying.");
            }

            SystemLogService.Add(
                "ROBOT",
                "DOFBOT state confirmed as vision_ready; skipping duplicate " +
                "HOME, gripper-open and PICK_ABOVE commands.");

            string inspectionId = string.IsNullOrWhiteSpace(inspection.InspectionId)
                ? Guid.NewGuid().ToString("N")
                : inspection.InspectionId;
            RobotJobResponse response = await _visionApi.SendDirectPickAsync(
                className,
                inspectionId,
                cancellationToken);

            VisionJobSummary? job = response.Job?.NestedJob ?? response.Job;
            if (string.IsNullOrWhiteSpace(job?.JobId))
            {
                throw new InvalidOperationException(
                    response.Error ?? "Robot API did not accept the direct pick signal.");
            }

            string jobId = job.JobId;
            SystemLogService.Add(
                "ROBOT",
                $"Arduino checkpoint -> direct {className} pick accepted " +
                $"(job {jobId[..Math.Min(8, jobId.Length)]}). " +
                "WPF will not wait or send robot_done@; Arduino controls " +
                "checkpoint timing and conveyor restart.");

            CompleteArduinoControlledCheckpoint(inspection);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SystemLogService.Add(
                "SAFETY",
                "Checkpoint routing failed. No robot job was submitted; " +
                "Arduino may still continue its programmed cycle. Operator " +
                $"inspection is required: {ex.Message}");
            return false;
        }
        finally
        {
            _checkpointInProgress = false;
        }
    }

    [RelayCommand]
    private async Task RetryCheckpointAsync() =>
        await TriggerAtRobotCheckpointAsync();

    private void CompleteArduinoControlledCheckpoint(
        InspectionResult processedInspection)
    {
        if (ReferenceEquals(
                _pendingDirectInspection,
                processedInspection))
        {
            _pendingDirectInspection = null;
            NextAiResult = "No AI result waiting";
        }
        else
        {
            SystemLogService.Add(
                "AI ROUTING",
                "A newer AI result arrived while the checkpoint request was " +
                "being submitted; it was preserved for the next cycle.");
        }

        SystemLogService.Add(
            "AI ROUTING",
            "WPF checkpoint handling finished. Arduino independently " +
            "controls conveyor restart; no robot_done@ was transmitted.");
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            _pollTimer.Stop();
            _consecutiveRefreshFailures = 0;
            ConnectionStatus = "Connecting...";
            _visionApi.Configure(RobotBaseUrl);
            VisionStatusResponse status =
                await _visionApi.GetStatusAsync();
            IsConnected = true;
            ConnectionStatus = "Connected";
            ApplyStatus(status, initializeConfig: true);
            _pollTimer.Start();
            SystemLogService.Add(
                "DOFBOT CAM",
                "Connected to Vision API.");
        }
        catch (Exception ex)
        {
            SetDisconnected(ex.Message);
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        _pollTimer.Stop();
        IsConnected = false;
        ConnectionStatus = "Disconnected";
        RemoteFrame = null;
        SystemLogService.Add(
            "DOFBOT CAM",
            "Stopped monitoring Vision API.");
    }

    [RelayCommand]
    private async Task RefreshAsync() =>
        await RefreshStatusAsync(includeFrame: true);

    [RelayCommand]
    private async Task StartVisionAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            VisionStatusResponse status =
                await _visionApi.StartAsync();
            ApplyStatus(status);
            SystemLogService.Add(
                "DOFBOT CAM",
                AutomaticTriggerActive
                    ? "Vision started in AUTO mode."
                    : "DOFBOT camera preview started in CHECKPOINT mode. " +
                      "Automatic robot routing is controlled by Arduino.");
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    [RelayCommand]
    private async Task StopVisionAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            VisionStatusResponse status =
                await _visionApi.StopAsync();
            ApplyStatus(status);
            RemoteFrame = null;
            SystemLogService.Add(
                "DOFBOT CAM",
                "Vision monitoring stopped.");
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    [RelayCommand]
    private async Task ResetTriggerAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            VisionStatusResponse status =
                await _visionApi.ResetTriggerAsync();
            ApplyStatus(status);
            SystemLogService.Add(
                "DOFBOT CAM",
                "Trigger latch reset.");
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    [RelayCommand]
    private async Task HomeAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            SystemLogService.Add(
                "ROBOT",
                "Moving to HOME.");
            RobotCommandResponse response =
                await _visionApi.HomeAsync();
            if (!response.Success)
            {
                throw new InvalidOperationException(
                    response.Error ?? "HOME failed.");
            }

            await RefreshStatusAsync(includeFrame: false);
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    [RelayCommand]
    private async Task ApplyTimingConfigAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            var config = new VisionConfig
            {
                BeltSpeedMmS = Math.Max(0, BeltSpeedMmS),
                DistanceToPickMm = Math.Max(0, DistanceToPickMm),
                RobotTimeToGripMs = Math.Max(0, RobotTimeToGripMs),
                ProcessingMarginMs = Math.Max(0, ProcessingMarginMs),
                AutoTrigger = AutomaticTriggerRequested
            };

            VisionConfigResponse response =
                await _visionApi.UpdateConfigAsync(config);
            ApplyConfig(response.Config);
            SystemLogService.Add(
                "DOFBOT CAM",
                AutomaticTriggerActive
                    ? "Timing saved; AUTO trigger is enabled."
                    : "Timing saved; Arduino CHECKPOINT mode remains active.");
            await RefreshStatusAsync(includeFrame: false);
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    [RelayCommand]
    private async Task EnqueueTestClassificationAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            string className = SelectedTestClass.Trim().ToLowerInvariant();
            double confidence = Math.Clamp(TestConfidence, 0, 1);
            VisionClassificationResponse response =
                await _visionApi.SubmitClassificationAsync(
                    className,
                    confidence);
            AiQueueSize = response.QueueSize;
            ApplyNextClassification(response.NextResult);
            SystemLogService.Add(
                "AI ROUTING",
                $"Queued {className} ({confidence:P1}) for DOFBOT trigger.");
            await RefreshStatusAsync(includeFrame: false);
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    [RelayCommand]
    private async Task ClearClassificationQueueAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            VisionClassificationResponse response =
                await _visionApi.ClearClassificationsAsync();
            AiQueueSize = response.QueueSize;
            ApplyNextClassification(response.NextResult);
            SystemLogService.Add(
                "AI ROUTING",
                $"Cleared classification queue ({response.Removed} removed).");
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    private async Task RefreshStatusAsync(bool includeFrame)
    {
        if (!IsConnected || _refreshInProgress)
            return;

        _refreshInProgress = true;
        try
        {
            VisionStatusResponse status =
                await _visionApi.GetStatusAsync();
            ApplyStatus(status);
            bool recovered =
                _consecutiveRefreshFailures > 0;
            _consecutiveRefreshFailures = 0;
            if (recovered)
            {
                ConnectionStatus = "Connected";
                SystemLogService.Add(
                    "DOFBOT CAM",
                    "Vision API connection recovered.");
            }

            if (includeFrame &&
                status.Running &&
                status.CameraOpen)
            {
                try
                {
                    byte[] jpeg =
                        await _visionApi.GetLatestFrameAsync();
                    RemoteFrame = CreateBitmap(jpeg);
                }
                catch (HttpRequestException)
                {
                    // A 503 is normal during camera warm-up.
                }
            }
        }
        catch (Exception ex)
        {
            _consecutiveRefreshFailures++;
            LastError = ex.Message;
            ConnectionStatus =
                $"Connection unstable " +
                $"({_consecutiveRefreshFailures}/" +
                $"{MaximumRefreshFailures})";
            SystemLogService.Add(
                "DOFBOT CAM",
                "Vision API did not respond; " +
                $"retry {_consecutiveRefreshFailures}/" +
                $"{MaximumRefreshFailures}: {ex.Message}");

            if (_consecutiveRefreshFailures >=
                MaximumRefreshFailures)
            {
                SetDisconnected(ex.Message);
            }
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void ApplyStatus(
        VisionStatusResponse status,
        bool initializeConfig = false)
    {
        VisionRunning = status.Running;
        CameraOpen = status.CameraOpen;
        RobotReady = status.RobotReady;
        AutomaticTriggerActive = status.AutoTrigger;
        TriggerLatched = status.TriggerLatched;
        FrameCount = status.FrameCount;
        StableCount = status.StableCount;
        LastError = status.LastError ?? "-";
        AiQueueSize = status.ClassificationQueue?.QueueSize ?? 0;
        ApplyNextClassification(
            status.ClassificationQueue?.NextResult);

        if (!status.VisionProcessingEnabled && status.CameraOpen)
        {
            DetectionSummary =
                "Vision processing disabled • raw DOFBOT camera preview only";
            TriggerSummary = "Vision trigger disabled";
            TriggerLatched = false;
            StableCount = 0;
        }
        else if (status.LastDetection is { } detection)
        {
            if (string.Equals(
                    detection.Detector,
                    "yolov8n_tensorrt_fp16",
                    StringComparison.OrdinalIgnoreCase))
            {
                DetectionSummary =
                    $"Battery at ({detection.CenterX:0}, {detection.CenterY:0})  •  " +
                    $"confidence {(detection.Confidence ?? 0):P1}  •  " +
                    $"TensorRT {(detection.InferenceMs ?? status.LastInferenceMs ?? 0):0.0} ms  •  " +
                    "MONITOR ONLY";
            }
            else
            {
                DetectionSummary =
                    $"Position ({detection.CenterX:0}, {detection.CenterY:0})  •  " +
                    $"object {detection.RelativeAngleDeg:0.0}°  •  " +
                    $"camera angle {(detection.WristAngle ?? 0):0.0}° (diagnostic)";
            }
        }
        else
        {
            DetectionSummary = status.MonitorOnly && status.DetectorAvailable
                ? "No battery detected in the entry zone • TensorRT MONITOR ONLY"
                : "No object in the entry zone";
        }

        if (!status.VisionProcessingEnabled)
        {
            TriggerSummary = "Vision trigger disabled";
        }
        else if (status.LastTrigger is { } trigger)
        {
            TriggerSummary =
                $"{trigger.ClassName ?? "unclassified"}  •  " +
                (trigger.Confidence is null
                    ? ""
                    : $"{trigger.Confidence:P1}  •  ") +
                (string.IsNullOrWhiteSpace(trigger.Action)
                    ? ""
                    : $"{trigger.Action}  •  ") +
                (trigger.WristAngle is null
                    ? "servo 5 from fixed pose  •  "
                    : $"servo 5 {trigger.WristAngle:0.0}°  •  ") +
                $"delay {trigger.StartDelayMs} ms" +
                (trigger.Late ? "  •  LATE" : "");

            string routingKey =
                $"{trigger.InspectionId}:{status.AutoTrigger}";
            if (!string.IsNullOrWhiteSpace(trigger.InspectionId) &&
                routingKey != _lastLoggedRoutingKey)
            {
                _lastLoggedRoutingKey = routingKey;
                string action = trigger.Action == "pass"
                    ? "PASS (no robot pick)"
                    : "ROBOT PICK";
                SystemLogService.Add(
                    "AI ROUTING",
                    $"{trigger.ClassName} ({trigger.Confidence:P1}) -> " +
                    $"{action}; inspection {trigger.InspectionId}.");
            }
        }
        else
        {
            TriggerSummary = "No trigger recorded";
        }

        if (status.LastJob is { } job)
        {
            string shortId = string.IsNullOrWhiteSpace(job.JobId)
                ? "-"
                : job.JobId[..Math.Min(8, job.JobId.Length)];
            JobSummary =
                $"{shortId}  •  {job.Status ?? "unknown"}" +
                (string.IsNullOrWhiteSpace(job.CurrentStep)
                    ? ""
                    : $"  •  {job.CurrentStep}");
            RoutingSummary = job.Action == "pass"
                ? "NORMAL -> PASS (no robot pick)"
                : $"{job.ClassName ?? "defect"} -> robot pick";
        }
        else
        {
            JobSummary = "No vision job";
            RoutingSummary = "No routing action";
        }

        if (status.Timing is { } timing)
        {
            TimingSummary = timing.ArrivalMs is null
                ? timing.Reason ?? "Belt timing is disabled"
                : $"Arrival {timing.ArrivalMs} ms  •  " +
                  $"start delay {timing.StartDelayMs} ms" +
                  (timing.Late ? "  •  LATE" : "");
        }

        if (status.Config != null &&
            (initializeConfig || !_configInitialized))
        {
            ApplyConfig(status.Config);
            _configInitialized = true;
        }
    }

    private void ApplyConfig(VisionConfig config)
    {
        BeltSpeedMmS = config.BeltSpeedMmS;
        DistanceToPickMm = config.DistanceToPickMm;
        RobotTimeToGripMs = config.RobotTimeToGripMs;
        ProcessingMarginMs = config.ProcessingMarginMs;
        AutomaticTriggerRequested = config.AutoTrigger;
        AutomaticTriggerActive = config.AutoTrigger;
    }

    private void ApplyNextClassification(VisionClassification? result)
    {
        NextAiResult = result == null
            ? "Queue empty"
            : $"{result.ClassName} ({result.Confidence:P1}) -> " +
              (result.Action == "pass" ? "PASS" : "ROBOT PICK");
    }

    private static BitmapImage CreateBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private bool EnsureConnected()
    {
        if (IsConnected)
            return true;

        SystemLogService.Add(
            "DOFBOT CAM",
            "Vision API is not connected.");
        return false;
    }

    private void HandleCommandError(Exception ex)
    {
        LastError = ex.Message;
        SystemLogService.Add(
            "DOFBOT CAM",
            $"Error: {ex.Message}");
    }

    private void SetDisconnected(string error)
    {
        _pollTimer.Stop();
        IsConnected = false;
        ConnectionStatus = "Disconnected";
        LastError = error;
        SystemLogService.Add(
            "DOFBOT CAM",
            $"Connection lost: {error}");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _pollTimer.Stop();
        _visionApi.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
