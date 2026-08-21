using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Channels;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels;

public partial class RobotControlViewModel : ObservableObject, IDisposable
{
    private readonly RobotApiClient _robotApi = new();
    private readonly Channel<LiveServoMove> _liveServoMoves =
        Channel.CreateBounded<LiveServoMove>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    private readonly CancellationTokenSource _liveMoveCancellation = new();
    private CancellationTokenSource? _pollingCancellation;
    private bool _disposed;
    private readonly HashSet<int> _activeServoIds = [];
    private int _consecutivePollingFailures;
    private const int MaximumPollingFailures = 3;

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

    public ObservableCollection<string> RobotLog { get; } = [];

    [ObservableProperty]
    private string robotBaseUrl = "http://192.168.137.179:7000";

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private string robotState = "Unknown";

    [ObservableProperty]
    private string currentStep = "-";

    [ObservableProperty]
    private string lastError = "-";

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool motionEnabled;

    [ObservableProperty]
    private int servoMoveTimeMs = 250;

    [ObservableProperty]
    private int queueSize;

    public RobotControlViewModel()
    {
        _ = ProcessLiveServoMovesAsync(
            _liveMoveCancellation.Token);
    }

    /// <summary>
    /// Populates a static HOME state for report screenshots only.
    /// No Robot API request or servo command is sent.
    /// </summary>
    public void LoadReportPlaceholder()
    {
        ConnectionStatus = "Connected";
        RobotState = "HOME";
        CurrentStep = "Ready";
        LastError = "-";
        IsConnected = true;
        IsBusy = false;
        MotionEnabled = true;
        QueueSize = 0;

        double[] homeAngles = [180, 130, 0, 0, 90, 40];
        for (int index = 0; index < Servos.Count; index++)
        {
            Servos[index].CurrentAngle = homeAngles[index];
            Servos[index].TargetAngle = homeAngles[index];
            Servos[index].TargetInitialized = true;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            StopPolling();
            _consecutivePollingFailures = 0;
            ConnectionStatus = "Connecting...";
            _robotApi.Configure(RobotBaseUrl);

            var health = await _robotApi.GetHealthAsync();
            IsConnected = health.Status.Equals("ok", StringComparison.OrdinalIgnoreCase);
            MotionEnabled = health.MotionEnabled;
            ConnectionStatus = IsConnected ? "Connected" : health.Status;
            AddLog($"Robot API: {ConnectionStatus}");

            if (IsConnected)
            {
                await RefreshAsync();
                StartPolling();
            }
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
        IsBusy = false;
        MotionEnabled = false;
        QueueSize = 0;
        ConnectionStatus = "Disconnected";
        RobotState = "Unknown";
        CurrentStep = "-";
        AddLog("Đã ngắt theo dõi Robot API.");
    }

    public void StopMonitoringForSystem()
    {
        if (IsConnected || _pollingCancellation != null)
            Disconnect();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsConnected)
            return;

        try
        {
            await RefreshRobotDataAsync(
                CancellationToken.None,
                includeServoFeedback: true);
        }
        catch (Exception ex)
        {
            SetDisconnected(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SetServoAsync(ServoChannelViewModel? servo)
    {
        if (servo == null || !EnsureReady())
            return;

        try
        {
            double target = Math.Clamp(Math.Round(servo.TargetAngle, 1), 0, 180);
            AddLog($"Servo {servo.ServoId} → {target:0.0}°");
            await _robotApi.SetServoAsync(
                servo.ServoId,
                target,
                Math.Clamp(ServoMoveTimeMs, 100, 5000));
            await RefreshRobotDataAsync(
                CancellationToken.None,
                includeServoFeedback: true);
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    public void QueueLiveServoMove(
        ServoChannelViewModel servo,
        double targetAngle)
    {
        if (_disposed ||
            !IsConnected ||
            !MotionEnabled ||
            IsBusy)
        {
            return;
        }

        double target = Math.Clamp(
            Math.Round(targetAngle, 1),
            0,
            180);

        _liveServoMoves.Writer.TryWrite(
            new LiveServoMove(servo.ServoId, target));
    }

    public void BeginServoInteraction(int servoId) =>
        _activeServoIds.Add(servoId);

    public void EndServoInteraction(int servoId) =>
        _activeServoIds.Remove(servoId);

    [RelayCommand]
    private Task HomeAsync() => ExecuteSimpleCommandAsync("Đưa robot về HOME", _robotApi.HomeAsync);

    [RelayCommand]
    private Task ResetAsync() => ExecuteSimpleCommandAsync("Reset robot", _robotApi.ResetAsync);

    [RelayCommand]
    private void ClearRobotLog() => RobotLog.Clear();

    private async Task ExecuteSimpleCommandAsync(
        string description,
        Func<CancellationToken, Task<RobotCommandResponse>> action)
    {
        if (!EnsureReady())
            return;

        try
        {
            AddLog(description);
            await action(CancellationToken.None);
            await RefreshRobotDataAsync(
                CancellationToken.None,
                includeServoFeedback: true);
        }
        catch (Exception ex)
        {
            HandleCommandError(ex);
        }
    }

    private bool EnsureReady()
    {
        if (!IsConnected)
        {
            AddLog("Robot chưa kết nối.");
            return false;
        }

        if (IsBusy)
        {
            AddLog("Robot đang bận; lệnh thủ công bị từ chối.");
            return false;
        }

        return true;
    }

    private async Task ProcessLiveServoMovesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (
                LiveServoMove move in
                _liveServoMoves.Reader.ReadAllAsync(
                    cancellationToken))
            {
                if (!IsConnected ||
                    !MotionEnabled ||
                    IsBusy)
                {
                    continue;
                }

                try
                {
                    await _robotApi.SetServoAsync(
                        move.ServoId,
                        move.Angle,
                        Math.Clamp(
                            ServoMoveTimeMs,
                            100,
                            1000),
                        cancellationToken);

                    // About 10 updates per second. When the user moves
                    // faster, the bounded channel keeps the newest angle.
                    await Task.Delay(
                        80,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(
                        () => HandleCommandError(ex));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the robot control is closed.
        }
    }

    private void StartPolling()
    {
        StopPolling();
        _pollingCancellation = new CancellationTokenSource();
        _ = PollRobotAsync(_pollingCancellation.Token);
    }

    private void StopPolling()
    {
        _pollingCancellation?.Cancel();
        _pollingCancellation?.Dispose();
        _pollingCancellation = null;
    }

    private async Task PollRobotAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await RefreshRobotDataAsync(
                        cancellationToken);
                    bool recovered =
                        _consecutivePollingFailures > 0;
                    _consecutivePollingFailures = 0;
                    if (recovered)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(
                            () =>
                            {
                                ConnectionStatus = "Connected";
                                AddLog(
                                    "Robot API connection recovered.");
                            });
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _consecutivePollingFailures++;
                    int attempt =
                        _consecutivePollingFailures;

                    await Application.Current.Dispatcher.InvokeAsync(
                        () =>
                        {
                            LastError = ex.Message;
                            ConnectionStatus =
                                $"Connection unstable ({attempt}/" +
                                $"{MaximumPollingFailures})";
                            AddLog(
                                "Robot API did not respond; " +
                                $"retry {attempt}/" +
                                $"{MaximumPollingFailures}: " +
                                ex.Message);
                        });

                    if (attempt >= MaximumPollingFailures)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(
                            () => SetDisconnected(ex.Message));
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the user disconnects or closes the application.
        }
    }

    private async Task RefreshRobotDataAsync(
        CancellationToken cancellationToken,
        bool includeServoFeedback = false)
    {
        // Status is lightweight and remains available once per second.
        // Servo feedback touches the physical I2C bus and is intentionally
        // excluded from the periodic poll. It is read only on initial/manual
        // refresh and after an explicit robot command.
        var statusResponse =
            await _robotApi.GetStatusAsync(cancellationToken);
        var status = statusResponse.EffectiveState;

        RobotServosResponse? servoResponse = null;
        if (includeServoFeedback && !status.Busy)
        {
            servoResponse =
                await _robotApi.GetServosAsync(cancellationToken);
            if (!servoResponse.Success)
            {
                throw new InvalidOperationException(
                    servoResponse.Error ??
                    "Không đọc được góc servo.");
            }
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            RobotState = status.State;
            IsBusy = status.Busy;
            CurrentStep = status.CurrentStep ?? "-";
            LastError = status.LastError ?? "-";
            MotionEnabled = status.MotionEnabled;
            QueueSize = status.QueueSize;

            if (servoResponse == null)
                return;

            for (int index = 0; index < Servos.Count && index < servoResponse.Servos.Count; index++)
            {
                if (servoResponse.Servos[index] is not double angle)
                    continue;

                ServoChannelViewModel servo = Servos[index];
                servo.CurrentAngle = angle;
                if (!_activeServoIds.Contains(servo.ServoId))
                {
                    servo.TargetAngle = angle;
                    servo.TargetInitialized = true;
                }
            }
        });
    }

    private void HandleCommandError(Exception ex)
    {
        LastError = ex.Message;
        AddLog($"Lỗi: {ex.Message}");
    }

    private void SetDisconnected(string error)
    {
        StopPolling();
        IsConnected = false;
        IsBusy = false;
        MotionEnabled = false;
        QueueSize = 0;
        ConnectionStatus = "Disconnected";
        RobotState = "Unknown";
        CurrentStep = "-";
        LastError = error;
        AddLog($"Mất kết nối: {error}");
    }

    private void AddLog(string message)
    {
        string entry = $"{DateTime.Now:HH:mm:ss}  {message}";
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => AddLog(message));
            return;
        }

        RobotLog.Add(entry);
        SystemLogService.Add("ROBOT", message);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        StopPolling();
        _liveServoMoves.Writer.TryComplete();
        _liveMoveCancellation.Cancel();
        _liveMoveCancellation.Dispose();
        _robotApi.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
