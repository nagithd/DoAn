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
                    : "Vision started in MONITOR ONLY mode.");
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
    private async Task VisionHomeAsync()
    {
        if (!EnsureConnected())
            return;

        try
        {
            SystemLogService.Add(
                "ROBOT",
                "Moving to VISION_HOME.");
            RobotCommandResponse response =
                await _visionApi.VisionHomeAsync();
            if (!response.Success)
            {
                throw new InvalidOperationException(
                    response.Error ?? "VISION_HOME failed.");
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
                    : "Timing saved; monitor-only mode remains enabled.");
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

        if (status.LastDetection is { } detection)
        {
            DetectionSummary =
                $"Position ({detection.CenterX:0}, {detection.CenterY:0})  •  " +
                $"object {detection.RelativeAngleDeg:0.0}°  •  " +
                $"servo 5 {detection.WristAngle:0.0}°";
        }
        else
        {
            DetectionSummary = "No object in the entry zone";
        }

        if (status.LastTrigger is { } trigger)
        {
            TriggerSummary =
                $"{trigger.ClassName ?? "unclassified"}  •  " +
                (trigger.Confidence is null
                    ? ""
                    : $"{trigger.Confidence:P1}  •  ") +
                (string.IsNullOrWhiteSpace(trigger.Action)
                    ? ""
                    : $"{trigger.Action}  •  ") +
                $"servo 5 {trigger.WristAngle:0.0}°  •  " +
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
