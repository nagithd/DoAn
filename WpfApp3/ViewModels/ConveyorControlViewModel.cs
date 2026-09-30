using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels;

public partial class ConveyorControlViewModel : ObservableObject, IDisposable
{
    public const int ConveyorBaudRate = 9600;
    public const int MinimumPwm = 0;
    // Motion timing to the camera and DOFBOT checkpoint is calibrated only
    // in this range. PWM 255 is reserved for STOP and is handled separately.
    public const int MaximumPwm = 170;
    public const int StopPwm = 255;
    public const int DefaultRunningPwm = 100;

    private readonly object _serialLock = new();
    private readonly StringBuilder _receiveBuffer = new();
    private SerialPort? _serialPort;
    private bool _disposed;
    private readonly DispatcherTimer _heartbeatTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, TaskCompletionSource<bool>> _acknowledgements = new();
    private string? _hostSession;
    private uint _lastCycleNumber;
    private long _lastHeartbeatTick;
    public bool HandshakeReady { get; private set; }
    public string? ActiveCycleId { get; private set; }
    public event EventHandler? HandshakeInvalidated;

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public event EventHandler<ConveyorEventArgs>? CameraStopReached;
    public event EventHandler<ConveyorEventArgs>? RobotStopReached;

    [ObservableProperty]
    private string? selectedPortName;

    [ObservableProperty]
    private bool isConnected;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string connectionStatus = "Disconnected";

    [ObservableProperty]
    private int selectedPwm = DefaultRunningPwm;

    [ObservableProperty]
    private int lastAppliedPwm = StopPwm;

    public ConveyorControlViewModel()
    {
        RefreshPorts();
        _heartbeatTimer.Tick += (_, _) =>
        {
            if (!HandshakeReady) return;
            if (Environment.TickCount64 - _lastHeartbeatTick > 5000)
            {
                StopMotor();
                InvalidateHandshake("Arduino heartbeat lost; reconnect to recover.");
                return;
            }
            if (!WriteProtocol($"PING:{_hostSession}@"))
                InvalidateHandshake("Cannot send host heartbeat.");
        };
    }

    private void InvalidateCycle()
    {
        ActiveCycleId = null;
        HandshakeInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void InvalidateHandshake(string reason)
    {
        HandshakeReady = false;
        _heartbeatTimer.Stop();
        InvalidateCycle();
        foreach (var ack in _acknowledgements.Values.ToArray()) ack.TrySetResult(false);
        _acknowledgements.Clear();
        SystemLogService.Add("SAFETY", reason);
    }

    private bool WriteProtocol(string command)
    {
        try
        {
            lock (_serialLock)
            {
                if (_serialPort?.IsOpen != true) return false;
                _serialPort.Write(command);
            }
            return true;
        }
        catch (Exception ex)
        {
            SystemLogService.Add("CONVEYOR", ex.Message);
            return false;
        }
    }

    private async Task<bool> SendAcknowledgedAsync(string command, CancellationToken token = default,
        string? acknowledgement = null)
    {
        string key = acknowledgement ?? "ACK:" + command;
        if (_acknowledgements.ContainsKey(key)) return false;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _acknowledgements[key] = completion;
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (!WriteProtocol(command + "@")) return false;
                if (await Task.WhenAny(completion.Task, Task.Delay(1200, token)) == completion.Task)
                    return await completion.Task;
            }
            return false;
        }
        finally { _acknowledgements.Remove(key); }
    }

    public bool IsCurrentCycle(string cycleId) => HandshakeReady && ActiveCycleId == cycleId;

    public async Task<bool> ReleaseCheckpointAsync(string cycleId, bool robotCompleted, CancellationToken token)
    {
        if (!IsCurrentCycle(cycleId)) return false;
        bool ok = await SendAcknowledgedAsync(
            $"{(robotCompleted ? "ROBOT_DONE" : "CAMERA_READY")}:{cycleId}", token);
        // CYCLE_COMPLETED may arrive before ACK; Arduino's ACK still identifies this exact cycle.
        return ok && HandshakeReady;
    }

    partial void OnSelectedPwmChanged(int value)
    {
        int constrained = Math.Clamp(
            value,
            MinimumPwm,
            MaximumPwm);
        if (value != constrained)
        {
            SelectedPwm = constrained;
            return;
        }

    }

    [RelayCommand]
    private void RefreshPorts()
    {
        string? previous = SelectedPortName;
        string[] ports = SerialPort.GetPortNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        AvailablePorts.Clear();
        foreach (string port in ports)
            AvailablePorts.Add(port);

        SelectedPortName =
            previous != null && AvailablePorts.Contains(previous)
                ? previous
                : AvailablePorts.FirstOrDefault();

        SystemLogService.Add(
            "CONVEYOR",
            ports.Length == 0
                ? "No serial COM port detected."
                : $"Found {ports.Length} serial port(s): {string.Join(", ", ports)}.");
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnected || IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(SelectedPortName))
        {
            SystemLogService.Add("CONVEYOR", "Select a COM port before connecting.");
            return;
        }

        IsBusy = true;
        ConnectionStatus = "Connecting...";
        SerialPort? port = null;
        try
        {
            port = new SerialPort(
                SelectedPortName,
                ConveyorBaudRate,
                Parity.None,
                8,
                StopBits.One)
            {
                Handshake = Handshake.None,
                Encoding = Encoding.UTF8,
                ReadTimeout = 500,
                WriteTimeout = 500,
                DtrEnable = false,
                RtsEnable = false
            };
            port.DataReceived += SerialPort_DataReceived;
            port.Open();

            lock (_serialLock)
            {
                _serialPort = port;
                _receiveBuffer.Clear();
            }

            IsConnected = true;
            ConnectionStatus = $"Connected — {SelectedPortName}";
            SystemLogService.Add(
                "CONVEYOR",
                $"Connected to {SelectedPortName} at {ConveyorBaudRate} baud. " +
                "Waiting for the Arduino reset sequence.");

            await Task.Delay(1500);
            if (IsConnected)
            {
                _hostSession = Guid.NewGuid().ToString("N")[..16];
                _lastCycleNumber = 0;
                HandshakeReady = await SendAcknowledgedAsync("HELLO:" + _hostSession);
                if (!HandshakeReady)
                    throw new InvalidOperationException("Arduino handshake v1 is required. Flash conveyor_battery_sort_v3 first.");
                _lastHeartbeatTick = Environment.TickCount64;
                _heartbeatTimer.Start();
                SystemLogService.Add("CONVEYOR", "Handshake established; conveyor remains stopped.");
            }
        }
        catch (Exception ex)
        {
            InvalidateHandshake("Conveyor connection failed.");
            if (port != null)
            {
                port.DataReceived -= SerialPort_DataReceived;
                port.Dispose();
            }

            lock (_serialLock)
                _serialPort = null;
            IsConnected = false;
            ConnectionStatus = "Error";
            SystemLogService.Add("CONVEYOR", $"COM connection failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void StartMotor()
    {
        SendMotorPwm(SelectedPwm, "START");
    }

    [RelayCommand]
    private void ApplySpeed()
    {
        SendMotorPwm(SelectedPwm, "SPEED");
    }

    [RelayCommand]
    private void StopMotor()
    {
        InvalidateCycle();
        SendMotorPwm(StopPwm, "STOP");
    }

    [RelayCommand]
    private void Disconnect()
    {
        ClosePort(sendStopFirst: true);
        ConnectionStatus = "Disconnected";
    }

    private bool SendMotorPwm(int pwm, string action)
    {
        if (pwm != StopPwm && (!HandshakeReady || ActiveCycleId != null))
        {
            SystemLogService.Add("SAFETY", "RUN/SPEED is unavailable until handshake is ready and cycle is clear.");
            return false;
        }
        int constrained = pwm == StopPwm ? StopPwm : Math.Clamp(
            pwm,
            MinimumPwm,
            MaximumPwm);
        string command = BuildMotorCommand(constrained);

        try
        {
            lock (_serialLock)
            {
                if (_serialPort?.IsOpen != true)
                {
                    SystemLogService.Add("CONVEYOR", "Arduino is not connected.");
                    return false;
                }

                _serialPort.Write(command);
            }

            LastAppliedPwm = constrained;
            SystemLogService.Add(
                "CONVEYOR",
                $"{action} command sent: {command} (awaiting Arduino confirmation)." );
            return true;
        }
        catch (Exception ex)
        {
            SystemLogService.Add("CONVEYOR", $"Cannot send {action}: {ex.Message}");
            return false;
        }
    }

    private static string BuildMotorCommand(int pwm) => $"motor = {pwm}@";

    public async Task<bool> ConnectForSystemStartAsync()
    {
        if (!IsConnected)
        {
            RefreshPorts();
            await ConnectAsync();
        }

        return IsConnected && HandshakeReady;
    }

    public bool StartForSystem()
    {
        if (!IsConnected)
            return false;

        return SendMotorPwm(SelectedPwm, "START SYSTEM");
    }

    public async Task<bool> StartForSystemAsync(CancellationToken token = default)
    {
        if (!IsConnected || !HandshakeReady || ActiveCycleId != null) return false;
        int pwm = Math.Clamp(SelectedPwm, MinimumPwm, MaximumPwm);
        bool ok = await SendAcknowledgedAsync($"motor = {pwm}", token, $"ACK:MOTOR_PWM:{pwm}");
        if (ok) LastAppliedPwm = pwm;
        else StopForSystemStart();
        return ok;
    }

    public bool StopForSystemStart()
    {
        if (!IsConnected)
            return false;

        InvalidateCycle();
        return SendMotorPwm(StopPwm, "STOP SYSTEM");
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (sender is not SerialPort port)
                return;

            string chunk = port.ReadExisting();
            if (chunk.Length == 0)
                return;

            List<string> completedLines = [];
            lock (_serialLock)
            {
                foreach (char character in chunk)
                {
                    if (character is '\r' or '\n')
                    {
                        if (_receiveBuffer.Length > 0)
                        {
                            completedLines.Add(_receiveBuffer.ToString().Trim());
                            _receiveBuffer.Clear();
                        }
                    }
                    else
                    {
                        _receiveBuffer.Append(character);
                    }
                }
            }

            foreach (string line in completedLines.Where(line => line.Length > 0))
            {
                if (Application.Current?.Dispatcher.CheckAccess() == false)
                    Application.Current.Dispatcher.BeginInvoke(() => ProcessArduinoLine(line));
                else
                    ProcessArduinoLine(line);
            }
        }
        catch (Exception ex)
        {
            SystemLogService.Add("CONVEYOR", $"Cannot read Arduino response: {ex.Message}");
        }
    }

    private void ProcessArduinoLine(string line)
    {
        if (_acknowledgements.TryGetValue(line, out var ack)) ack.TrySetResult(true);
        if (line == "HEARTBEAT:" + _hostSession) { _lastHeartbeatTick = Environment.TickCount64; return; }
        if (line.StartsWith("EVENT:FAULT:", StringComparison.Ordinal) ||
            (HandshakeReady && line == "READY:CONVEYOR_HANDSHAKE:1"))
        {
            InvalidateHandshake("Arduino stopped: " + line);
            return;
        }
        string? cycleId = null;
        string classifiedLine = line;
        if (line.StartsWith("EVENT:", StringComparison.Ordinal))
        {
            string[] parts = line.Split(':');
            if (parts.Length != 4 || parts[2] != _hostSession || !HandshakeReady ||
                !uint.TryParse(parts[3], out uint number) || number == 0) return;
            cycleId = parts[2] + ":" + parts[3];
            classifiedLine = parts[0] + ":" + parts[1];
            if (parts[1] == "SENSOR_DETECTED")
            {
                if (number <= _lastCycleNumber) return;
                if (ActiveCycleId != null && ActiveCycleId != cycleId) { StopMotor(); return; }
                _lastCycleNumber = number;
                ActiveCycleId = cycleId;
            }
            else if (ActiveCycleId != cycleId) return;
        }
        ConveyorEventKind kind = ClassifyArduinoMessage(classifiedLine);

        var args = new ConveyorEventArgs(kind, line, DateTime.Now, cycleId);
        if (kind == ConveyorEventKind.CameraStopped && cycleId != null)
            CameraStopReached?.Invoke(this, args);
        else if (kind == ConveyorEventKind.RobotStopped && cycleId != null)
            RobotStopReached?.Invoke(this, args);
        else if (classifiedLine == "EVENT:READY_FOR_NEXT")
            ActiveCycleId = null;

        if (kind != ConveyorEventKind.Heartbeat)
            SystemLogService.Add("CONVEYOR", $"Arduino: {line}");
    }

    private static ConveyorEventKind ClassifyArduinoMessage(string line)
    {
        if (line.Equals("EVENT:SENSOR_DETECTED", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.SensorDetected;
        if (line.Equals("EVENT:CAMERA_STOPPED", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.CameraStopped;
        if (line.Equals("EVENT:MOVING_TO_ROBOT", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.MovingToRobot;
        if (line.Equals("EVENT:ROBOT_STOPPED", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.RobotStopped;
        if (line.Equals("EVENT:CYCLE_COMPLETED", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.CycleCompleted;
        if (line.StartsWith("ACK:MOTOR_PWM:", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.MotorAcknowledged;
        if (line.Equals("HEARTBEAT", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.Heartbeat;

        if (line.Contains("Phat hien PIN", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.SensorDetected;
        if (line.Contains("Dung truoc Camera", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.CameraStopped;
        if (line.Contains("Dua pin den checkpoint Robot", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.MovingToRobot;
        if (line.Contains("Dung Robot gap pin", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.RobotStopped;
        if (line.Contains("Robot gap xong", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.CycleCompleted;
        if (line.StartsWith("motor = ", StringComparison.OrdinalIgnoreCase) &&
            line.Contains("- ok", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.MotorAcknowledged;
        if (line.Contains("Tao dang chay", StringComparison.OrdinalIgnoreCase))
            return ConveyorEventKind.Heartbeat;
        return ConveyorEventKind.Message;
    }

    private void ClosePort(bool sendStopFirst)
    {
        InvalidateHandshake("Serial connection closed; active cycle cancelled.");
        SerialPort? port;
        lock (_serialLock)
        {
            port = _serialPort;
            _serialPort = null;
            _receiveBuffer.Clear();
        }

        if (port == null)
        {
            IsConnected = false;
            return;
        }

        try
        {
            if (port.IsOpen && sendStopFirst)
            {
                port.Write(BuildMotorCommand(StopPwm));
                SystemLogService.Add(
                    "CONVEYOR",
                    $"Safety stop sent before disconnect: PWM {StopPwm}." );
            }
        }
        catch (Exception ex)
        {
            SystemLogService.Add("CONVEYOR", $"Safety stop could not be sent: {ex.Message}");
        }
        finally
        {
            port.DataReceived -= SerialPort_DataReceived;
            try
            {
                if (port.IsOpen)
                    port.Close();
            }
            finally
            {
                port.Dispose();
            }
        }

        IsConnected = false;
        SystemLogService.Add("CONVEYOR", "Serial connection closed.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ClosePort(sendStopFirst: true);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
