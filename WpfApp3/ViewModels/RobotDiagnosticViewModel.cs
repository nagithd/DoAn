using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading.Channels;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels;

public partial class RobotDiagnosticViewModel : ObservableObject, IDisposable
{
    private readonly VisionApiClient _api = new();
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Channel<LiveServoMove> _liveServoMoves =
        Channel.CreateBounded<LiveServoMove>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    private bool _disposed;
    private bool _liveInteractionActive;
    private int _liveInteractionVersion;
    private const int LiveServoMoveTimeMs = 350;
    private const int LiveServoCommandIntervalMs = 80;

    private readonly record struct LiveServoMove(
        int ServoId,
        double Angle);

    public ObservableCollection<ServoChannelViewModel> Servos { get; } =
    [
        new(1, "Base"),
        new(2, "Shoulder"),
        new(3, "Elbow"),
        new(4, "Wrist pitch"),
        new(5, "Wrist rotate"),
        new(6, "Gripper")
    ];

    public ObservableCollection<string> DiagnosticLog { get; } = [];

    [ObservableProperty]
    private string robotBaseUrl;

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private string robotState = "unknown";

    [ObservableProperty]
    private string currentStep = "-";

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool motionEnabled;

    [ObservableProperty]
    private bool feedbackAvailable;

    [ObservableProperty]
    private int queueSize;

    [ObservableProperty]
    private int servoMoveTimeMs = 1600;

    [ObservableProperty]
    private bool commandInProgress;

    [ObservableProperty]
    private string lastRefresh = "Not refreshed";

    public bool CanIssueManualMotion =>
        IsConnected &&
        MotionEnabled &&
        FeedbackAvailable &&
        !IsBusy &&
        QueueSize == 0 &&
        !CommandInProgress;

    public bool CanUseServoSliders =>
        IsConnected &&
        MotionEnabled &&
        FeedbackAvailable &&
        !IsBusy &&
        QueueSize == 0 &&
        (!CommandInProgress || _liveInteractionActive);

    public RobotDiagnosticViewModel(string robotBaseUrl)
    {
        RobotBaseUrl = robotBaseUrl;
        _ = ProcessLiveServoMovesAsync(_lifetimeCancellation.Token);
    }

    public Task InitializeAsync() => ConnectAsync();

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (_disposed)
            return;

        try
        {
            ConnectionStatus = "Connecting...";
            _api.Configure(RobotBaseUrl);

            RobotHealthResponse health = await _api.GetHealthAsync(
                _lifetimeCancellation.Token);
            if (!health.Status.Equals("ok", StringComparison.OrdinalIgnoreCase) ||
                !health.RobotInitialized)
            {
                throw new InvalidOperationException(
                    $"Robot API is not ready: {health.Status}");
            }

            IsConnected = true;
            MotionEnabled = health.MotionEnabled;
            ConnectionStatus = "Connected";
            AddLog("Connected to Robot API. No periodic polling is active.");
            await RefreshCoreAsync(
                includeServoFeedback: true,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Expected when the window closes during initialization.
        }
        catch (Exception ex)
        {
            SetDisconnected(ex.Message);
        }
    }

    [RelayCommand]
    private void Disconnect()
    {
        IsConnected = false;
        IsBusy = false;
        MotionEnabled = false;
        FeedbackAvailable = false;
        QueueSize = 0;
        RobotState = "unknown";
        CurrentStep = "-";
        ConnectionStatus = "Disconnected";
        AddLog("Diagnostic connection closed locally.");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsConnected || CommandInProgress)
            return;

        try
        {
            await RefreshCoreAsync(
                includeServoFeedback: true,
                _lifetimeCancellation.Token);
            AddLog("Robot status and six servo angles refreshed.");
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    [RelayCommand]
    private async Task SetServoAsync(ServoChannelViewModel? servo)
    {
        if (servo == null)
            return;

        // Capture the requested value before the safety refresh. The refresh
        // intentionally synchronizes the displayed target with real feedback,
        // but it must not replace the value the operator just submitted.
        double requestedTarget = Math.Clamp(
            Math.Round(servo.TargetAngle, 1),
            servo.MinimumAngle,
            servo.MaximumAngle);

        await ExecuteManualCommandAsync(
            $"Servo {servo.ServoId} ({servo.Name})",
            async cancellationToken =>
            {
                int moveTime = Math.Clamp(ServoMoveTimeMs, 100, 5000);
                AddLog(
                    $"Sending servo {servo.ServoId} to " +
                    $"{requestedTarget:0.0} degrees in {moveTime} ms.");
                RobotCommandResponse response = await _api.SetServoAsync(
                    servo.ServoId,
                    requestedTarget,
                    moveTime,
                    cancellationToken);
                EnsureSuccessful(response, "Servo command was rejected.");
                await Task.Delay(moveTime + 250, cancellationToken);
            });
    }

    public void QueueLiveServoMove(
        ServoChannelViewModel servo,
        double targetAngle)
    {
        if (_disposed ||
            !IsConnected ||
            !MotionEnabled ||
            !FeedbackAvailable ||
            IsBusy ||
            QueueSize > 0 ||
            (CommandInProgress && !_liveInteractionActive))
        {
            return;
        }

        double target = Math.Clamp(
            Math.Round(targetAngle, 1),
            servo.MinimumAngle,
            servo.MaximumAngle);

        if (!_liveInteractionActive)
        {
            _liveInteractionActive = true;
            CommandInProgress = true;
            NotifyManualMotionStateChanged();
            AddLog(
                $"Live slider control started for servo {servo.ServoId}. " +
                "Only the newest queued angle is retained.");
        }

        Interlocked.Increment(ref _liveInteractionVersion);
        _liveServoMoves.Writer.TryWrite(
            new LiveServoMove(servo.ServoId, target));
    }

    public async Task EndLiveServoInteractionAsync()
    {
        if (!_liveInteractionActive || _disposed)
            return;

        int completedVersion = Volatile.Read(
            ref _liveInteractionVersion);
        try
        {
            // Allow the retained final angle to reach Jetson and the servo to
            // settle before reading all six positions once.
            await Task.Delay(
                LiveServoMoveTimeMs + 200,
                _lifetimeCancellation.Token);

            if (completedVersion != Volatile.Read(
                    ref _liveInteractionVersion))
            {
                return;
            }

            await RefreshCoreAsync(
                includeServoFeedback: true,
                _lifetimeCancellation.Token);
            AddLog("Live slider control completed; servo feedback refreshed.");
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Expected when the diagnostic window closes.
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
        finally
        {
            if (completedVersion == Volatile.Read(
                    ref _liveInteractionVersion))
            {
                _liveInteractionActive = false;
                CommandInProgress = false;
                NotifyManualMotionStateChanged();
            }
        }
    }

    private async Task ProcessLiveServoMovesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (
                LiveServoMove move in
                _liveServoMoves.Reader.ReadAllAsync(cancellationToken))
            {
                if (!IsConnected ||
                    !MotionEnabled ||
                    !FeedbackAvailable ||
                    IsBusy ||
                    QueueSize > 0)
                {
                    continue;
                }

                try
                {
                    using var requestCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            cancellationToken);
                    requestCancellation.CancelAfter(TimeSpan.FromSeconds(2));
                    RobotCommandResponse response = await _api.SetServoAsync(
                        move.ServoId,
                        move.Angle,
                        LiveServoMoveTimeMs,
                        requestCancellation.Token);
                    EnsureSuccessful(
                        response,
                        "Live servo command was rejected.");

                    // Stream the newest target at roughly 10 Hz. The bounded
                    // channel drops intermediate values if the API is slower.
                    await Task.Delay(
                        LiveServoCommandIntervalMs,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    HandleCommandError(ex);
                    _liveInteractionActive = false;
                    CommandInProgress = false;
                    NotifyManualMotionStateChanged();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the diagnostic window closes.
        }
    }

    [RelayCommand]
    private Task HomeAsync() => ExecuteManualCommandAsync(
        "HOME",
        async cancellationToken =>
        {
            AddLog("Sending HOME command.");
            RobotCommandResponse response =
                await _api.HomeAsync(cancellationToken);
            EnsureSuccessful(response, "HOME command failed.");
            await Task.Delay(1900, cancellationToken);
        });

    [RelayCommand]
    private Task ResetAsync() => ExecuteManualCommandAsync(
        "RESET",
        async cancellationToken =>
        {
            AddLog("Sending RESET command.");
            RobotCommandResponse response =
                await _api.ResetRobotAsync(cancellationToken);
            EnsureSuccessful(response, "RESET command failed.");
            await Task.Delay(1900, cancellationToken);
        });

    [RelayCommand]
    private void ClearLog() => DiagnosticLog.Clear();

    private async Task ExecuteManualCommandAsync(
        string description,
        Func<CancellationToken, Task> action)
    {
        if (_disposed)
            return;

        if (!await _commandGate.WaitAsync(0))
        {
            AddLog("Another diagnostic command is already running.");
            return;
        }

        try
        {
            CommandInProgress = true;
            await VerifySafeManualStateAsync(_lifetimeCancellation.Token);
            await action(_lifetimeCancellation.Token);
            await RefreshCoreAsync(
                includeServoFeedback: true,
                _lifetimeCancellation.Token);
            AddLog($"{description} completed; feedback refreshed.");
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellation.IsCancellationRequested)
        {
            AddLog(
                "Window closed while waiting. A command already accepted " +
                "by Jetson may still finish on the robot.");
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
        finally
        {
            CommandInProgress = false;
            _commandGate.Release();
        }
    }

    private async Task VerifySafeManualStateAsync(
        CancellationToken cancellationToken)
    {
        await RefreshCoreAsync(
            includeServoFeedback: true,
            cancellationToken);

        if (!IsConnected)
            throw new InvalidOperationException("Robot API is not connected.");
        if (!MotionEnabled)
            throw new InvalidOperationException("Robot motion is disabled.");
        if (IsBusy || QueueSize > 0)
            throw new InvalidOperationException(
                "Automatic robot motion is active or queued.");
        if (!FeedbackAvailable)
            throw new InvalidOperationException(
                "Six-servo feedback is unavailable; manual motion blocked.");
    }

    private async Task RefreshCoreAsync(
        bool includeServoFeedback,
        CancellationToken cancellationToken)
    {
        RobotStatusResponse response =
            await _api.GetRobotStatusAsync(cancellationToken);
        RobotStatusResponse state = response.EffectiveState;

        IsConnected = true;
        ConnectionStatus = "Connected";
        RobotState = state.State;
        IsBusy = state.Busy;
        CurrentStep = state.CurrentStep ?? "-";
        MotionEnabled = state.MotionEnabled;
        QueueSize = state.QueueSize;

        if (includeServoFeedback && !state.Busy)
        {
            RobotServosResponse servoResponse =
                await _api.GetServosAsync(cancellationToken);
            FeedbackAvailable =
                servoResponse.Success &&
                servoResponse.FeedbackAvailable &&
                servoResponse.Servos.Count >= Servos.Count &&
                servoResponse.Servos.Take(Servos.Count).All(
                    value => value.HasValue);

            for (int index = 0;
                 index < Servos.Count && index < servoResponse.Servos.Count;
                 index++)
            {
                double? angle = servoResponse.Servos[index];
                Servos[index].CurrentAngle = angle;
                if (angle.HasValue)
                    Servos[index].TargetAngle = angle.Value;
            }
        }

        LastRefresh = DateTime.Now.ToString("HH:mm:ss");
    }

    private static void EnsureSuccessful(
        RobotCommandResponse response,
        string fallbackMessage)
    {
        if (!response.Success)
            throw new InvalidOperationException(
                response.Error ?? fallbackMessage);
    }

    private void HandleCommandError(Exception ex)
    {
        AddLog($"ERROR: {ex.Message}");
    }

    private void SetDisconnected(string error)
    {
        IsConnected = false;
        IsBusy = false;
        MotionEnabled = false;
        FeedbackAvailable = false;
        QueueSize = 0;
        ConnectionStatus = "Disconnected";
        RobotState = "unknown";
        CurrentStep = "-";
        AddLog($"Connection failed: {error}");
    }

    private void AddLog(string message)
    {
        DiagnosticLog.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        SystemLogService.Add("ROBOT DIAGNOSTIC", message);
    }

    private void NotifyManualMotionStateChanged()
    {
        OnPropertyChanged(nameof(CanIssueManualMotion));
        OnPropertyChanged(nameof(CanUseServoSliders));
    }

    partial void OnIsConnectedChanged(bool value) =>
        NotifyManualMotionStateChanged();
    partial void OnIsBusyChanged(bool value) =>
        NotifyManualMotionStateChanged();
    partial void OnMotionEnabledChanged(bool value) =>
        NotifyManualMotionStateChanged();
    partial void OnFeedbackAvailableChanged(bool value) =>
        NotifyManualMotionStateChanged();
    partial void OnQueueSizeChanged(int value) =>
        NotifyManualMotionStateChanged();
    partial void OnCommandInProgressChanged(bool value) =>
        NotifyManualMotionStateChanged();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _liveServoMoves.Writer.TryComplete();
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
        _api.Dispose();
        // Do not dispose the gate here: an accepted asynchronous command may
        // still be unwinding its finally block after window close and must be
        // able to release the semaphore safely.
        GC.SuppressFinalize(this);
    }
}
