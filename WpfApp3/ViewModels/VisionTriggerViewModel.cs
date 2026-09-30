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
    private readonly VisionApiClient _visionApi;
    private const int StatusPollIntervalMs = 1000;
    private const int PreviewReconnectDelayMs = 500;

    private readonly DispatcherTimer _statusPollTimer;
    private bool _refreshInProgress;
    private CancellationTokenSource? _previewStreamCancellation;
    private Task? _previewStreamTask;
    private bool _disposed;
    private int _consecutiveRefreshFailures;
    private InspectionResult? _pendingDirectInspection;
    private string? _inspectionCycleId;
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
    private long frameCount;

    [ObservableProperty]
    private BitmapSource? remoteFrame;

    [ObservableProperty]
    private string detectionSummary = "No object in the entry zone";

    [ObservableProperty]
    private string lastError = "-";

    public VisionTriggerViewModel(VisionApiClient? visionApi = null)
    {
        _visionApi = visionApi ?? new VisionApiClient();
        _statusPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(StatusPollIntervalMs)
        };
        _statusPollTimer.Tick += async (_, _) =>
            await RefreshStatusAsync();

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
        FrameCount = 12840;
        DetectionSummary = "Battery detected inside the checkpoint zone";
        LastError = "-";
    }

    /// <summary>
    /// Stores the latest IMITECH result locally. No classification is sent to
    /// Vision API: Arduino decides when the battery reaches the robot and WPF
    /// then sends one direct /robot/pick command to Jetson.
    /// </summary>
    public void ClearDirectInspection()
    {
        _pendingDirectInspection = null;
        _inspectionCycleId = null;
    }

    public bool StoreDirectInspectionResult(InspectionResult result, string cycleId)
    {
        string className = result.DetectedClass.Trim().ToLowerInvariant();
        if (className is not ("normal" or "dented" or "scratched" or "swollen"))
        {
            _pendingDirectInspection = null;
            SystemLogService.Add(
                "AI ROUTING",
                $"Unsupported AI class '{className}'; no robot signal will be sent.");
            return false;
        }

        _pendingDirectInspection = result;
        _inspectionCycleId = cycleId;

        SystemLogService.Add(
            "AI ROUTING",
            $"Stored {className} ({result.Confidence:P1}) locally; " +
            "waiting for EVENT:ROBOT_STOPPED.");

        if (!double.IsFinite(result.Confidence) || result.Confidence < 0.40 || result.Confidence > 1)
        {
            ClearDirectInspection();
            SystemLogService.Add("SAFETY", "AI confidence below 0.40; conveyor will remain stopped.");
            return false;
        }
        return true;
    }

    public async Task<bool> PrepareRobotForSystemStartAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _visionApi.Configure(RobotBaseUrl);
            RobotHealthResponse health = await _visionApi.GetHealthAsync(cancellationToken);
            if (health.ConveyorHandshakeVersion != 1)
                throw new InvalidOperationException("Deploy the Jetson handshake backend before starting the conveyor.");
            if (health.ServoFeedbackAvailable is not true)
            {
                SystemLogService.Add(
                    "ROBOT",
                    "Servo feedback is unavailable; pose completion is currently based on commanded state.");
            }
            if (health.PickGuardEnabled)
            {
                ApplyStatus(await _visionApi.StartAsync(cancellationToken));
                IsConnected = true;
            }
            RobotStatusResponse response =
                await _visionApi.GetRobotStatusAsync(cancellationToken);
            RobotStatusResponse robot = response.EffectiveState;

            if (!robot.MotionEnabled || robot.Busy || robot.QueueSize > 0)
            {
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
        StopPolling();

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
    /// produce no motion. The caller may release the matching Arduino cycle
    /// only after this method confirms job completion and HOME readiness.
    /// </summary>
    public async Task<bool> TriggerAtRobotCheckpointAsync(
        string cycleId,
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
            if (inspection is null || _inspectionCycleId != cycleId)
            {
                throw new InvalidOperationException(
                    "No local AI result is waiting for the Arduino robot checkpoint.");
            }

            string className = inspection.DetectedClass.Trim().ToLowerInvariant();
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
                    "Arduino remains stopped.");
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

            if (className == "normal")
            {
                SystemLogService.Add("AI ROUTING", "NORMAL -> PASS with robot ready.");
                CompleteCheckpoint(inspection);
                return true;
            }

            string inspectionId = "conveyor-" + cycleId.Replace(':', '-');
            RobotJobResponse response = await _visionApi.SendDirectPickAsync(
                className,
                inspectionId,
                cancellationToken);

            VisionJobSummary? job = response.Job?.NestedJob ?? response.Job;
            if (!response.Success || job?.JobId != inspectionId)
            {
                throw new InvalidOperationException(
                    response.Error ?? "Robot API did not accept the direct pick signal.");
            }

            string jobId = job.JobId;
            SystemLogService.Add(
                "ROBOT",
                $"Arduino checkpoint -> direct {className} pick accepted " +
                $"(job {jobId[..Math.Min(8, jobId.Length)]}). " +
                "Conveyor remains held until this exact job completes.");

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(100));
            while (true)
            {
                await Task.Delay(300, deadline.Token);
                var progress = await _visionApi.GetJobAsync(jobId, deadline.Token);
                var current = progress.Job?.NestedJob ?? progress.Job;
                if (!progress.Success || current?.JobId != jobId)
                    throw new InvalidOperationException("Robot job response does not match the active cycle.");
                if (current.Status == "error")
                    throw new InvalidOperationException(current.Error ?? "Robot job failed.");
                if (current.Status != "completed") continue;
                if (!current.ConveyorReleaseAllowed)
                    throw new InvalidOperationException("Job completed without conveyor release permission.");
                var finalState = (await _visionApi.GetRobotStatusAsync(deadline.Token)).EffectiveState;
                if (finalState.Busy || finalState.QueueSize != 0 || !finalState.MotionEnabled ||
                    finalState.State != "vision_ready")
                    throw new InvalidOperationException("Robot is not ready at the end of the job.");
                SystemLogService.Add("ROBOT", $"Completed {jobId}; HOME confirmation: {current.PoseConfirmation}.");
                break;
            }

            CompleteCheckpoint(inspection);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SystemLogService.Add(
                "SAFETY",
                "Checkpoint failed or completion is uncertain. A submitted robot job may still be moving; " +
                "Arduino must remain stopped. Operator " +
                $"inspection is required: {ex.Message}");
            return false;
        }
        finally
        {
            _checkpointInProgress = false;
        }
    }

    private void CompleteCheckpoint(
        InspectionResult processedInspection)
    {
        if (ReferenceEquals(
                _pendingDirectInspection,
                processedInspection))
        {
            ClearDirectInspection();
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
            "Checkpoint validated. Caller may now acknowledge this cycle to Arduino.");
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            StopPolling();
            _consecutiveRefreshFailures = 0;
            ConnectionStatus = "Connecting...";
            _visionApi.Configure(RobotBaseUrl);
            VisionStatusResponse status =
                await _visionApi.GetStatusAsync();
            IsConnected = true;
            ConnectionStatus = "Connected";
            ApplyStatus(status);
            StartPolling();
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
        StopPolling();
        IsConnected = false;
        ConnectionStatus = "Disconnected";
        RemoteFrame = null;
        SystemLogService.Add(
            "DOFBOT CAM",
            "Stopped monitoring Vision API.");
    }

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
            StartPolling();
            SystemLogService.Add(
                "DOFBOT CAM",
                "DOFBOT camera and TensorRT monitoring started. " +
                "Automatic robot routing remains controlled by Arduino.");
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

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    private void StartPolling()
    {
        _statusPollTimer.Start();
        StartPreviewStream();
    }

    private void StopPolling()
    {
        _statusPollTimer.Stop();
        _previewStreamCancellation?.Cancel();
        _previewStreamCancellation = null;
        _previewStreamTask = null;
    }

    private async Task RefreshStatusAsync()
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

            StartPreviewStream();

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

    private void ApplyStatus(VisionStatusResponse status)
    {
        VisionRunning = status.Running;
        CameraOpen = status.CameraOpen;
        FrameCount = status.FrameCount;
        LastError = status.LastError ?? "-";

        if (!status.VisionProcessingEnabled && status.CameraOpen)
        {
            DetectionSummary =
                "Vision processing disabled • raw DOFBOT camera preview only";
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
    }

    private void StartPreviewStream()
    {
        if (!IsConnected || !VisionRunning || !CameraOpen ||
            _previewStreamTask is { IsCompleted: false })
        {
            return;
        }

        _previewStreamCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _previewStreamCancellation = cancellation;
        _previewStreamTask = ReceivePreviewStreamAsync(cancellation.Token);
    }

    private async Task ReceivePreviewStreamAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    await foreach (byte[] jpeg in _visionApi
                        .StreamPreviewFramesAsync(cancellationToken))
                    {
                        RemoteFrame = CreateBitmap(jpeg);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SystemLogService.Add(
                        "DOFBOT CAM",
                        $"MJPEG preview reconnecting: {ex.Message}");
                }

                await Task.Delay(
                    PreviewReconnectDelayMs,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal disconnect, stop, or camera restart.
        }
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
        StopPolling();
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

        StopPolling();
        _visionApi.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
