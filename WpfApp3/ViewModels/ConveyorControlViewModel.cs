using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels;

/// <summary>
/// Serial integration for PhanLoaiPin_7826.ino. Running speed is represented
/// by the raw Arduino PWM value: a lower value is faster and 250 is STOP.
/// Automatic operation is limited to the firmware's calibrated 0..120 range.
/// </summary>
public partial class ConveyorControlViewModel : ObservableObject, IDisposable
{
    public const int ConveyorBaudRate = 9600;
    public const int MinimumRunningPwm = 0;
    public const int MaximumRunningPwm = 120;
    public const int StopPwm = 250;
    public const int DefaultRunningPwm = 100;

    private static readonly int[] CalibratedPwm =
        [0, 20, 40, 60, 80, 90, 100, 120];
    private static readonly int[] CalibratedRobotDelayMs =
        [2840, 3380, 3700, 4250, 4700, 4900, 5650, 6100];

    private readonly object _serialLock = new();
    private readonly StringBuilder _receiveBuffer = new();
    private SerialPort? _serialPort;
    private bool _disposed;

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public event EventHandler<ConveyorEventArgs>? ConveyorEventReceived;
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
    private string motorStatus = "Stopped / unknown";

    [ObservableProperty]
    private string lastReceived = "No Arduino response";

    [ObservableProperty]
    private string sensorStatus = "Waiting for sensor";

    [ObservableProperty]
    private string cycleStage = "Idle";

    [ObservableProperty]
    private int detectedBatteryCount;

    [ObservableProperty]
    private int selectedPwm = DefaultRunningPwm;

    [ObservableProperty]
    private int lastAppliedPwm = StopPwm;

    [ObservableProperty]
    private bool isWaitingForRobotCompletion;

    public string BaudRateText => $"{ConveyorBaudRate} baud, 8-N-1";
    public string StartCommand => BuildMotorCommand(SelectedPwm);
    public string StopCommand => BuildMotorCommand(StopPwm);
    public double SelectedSpeedPercent =>
        (StopPwm - SelectedPwm) / (double)StopPwm * 100.0;
    public double EstimatedRobotTravelSeconds =>
        EstimateRobotDelayMs(SelectedPwm) / 1000.0;

    public ConveyorControlViewModel() => RefreshPorts();

    partial void OnSelectedPwmChanged(int value)
    {
        int constrained = Math.Clamp(
            value,
            MinimumRunningPwm,
            MaximumRunningPwm);
        if (value != constrained)
        {
            SelectedPwm = constrained;
            return;
        }

        OnPropertyChanged(nameof(StartCommand));
        OnPropertyChanged(nameof(SelectedSpeedPercent));
        OnPropertyChanged(nameof(EstimatedRobotTravelSeconds));
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
            MotorStatus = "Arduino connected — awaiting state";
            SensorStatus = "Sensor armed";
            SystemLogService.Add(
                "CONVEYOR",
                $"Connected to {SelectedPortName} at {ConveyorBaudRate} baud. " +
                "Waiting for the Arduino reset sequence.");

            await Task.Delay(1500);
            if (IsConnected)
                SystemLogService.Add("CONVEYOR", "Serial event monitoring is ready.");
        }
        catch (Exception ex)
        {
            if (port != null)
            {
                port.DataReceived -= SerialPort_DataReceived;
                port.Dispose();
            }

            lock (_serialLock)
                _serialPort = null;
            IsConnected = false;
            ConnectionStatus = "Error";
            MotorStatus = "Unavailable";
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
        if (SendMotorPwm(SelectedPwm, "START"))
            MotorStatus = $"Running request — PWM {SelectedPwm}";
    }

    [RelayCommand]
    private void ApplySpeed()
    {
        if (SendMotorPwm(SelectedPwm, "SPEED"))
            MotorStatus = $"Speed request — PWM {SelectedPwm}";
    }

    [RelayCommand]
    private void StopMotor()
    {
        if (SendMotorPwm(StopPwm, "STOP"))
            MotorStatus = $"Stop request — PWM {StopPwm}";
    }

    [RelayCommand]
    private void Disconnect()
    {
        ClosePort(sendStopFirst: true);
        ConnectionStatus = "Disconnected";
        MotorStatus = "Stopped / disconnected";
        SensorStatus = "Disconnected";
        CycleStage = "Idle";
    }

    private bool SendMotorPwm(int pwm, string action)
    {
        int constrained = pwm == StopPwm
            ? StopPwm
            : Math.Clamp(pwm, MinimumRunningPwm, MaximumRunningPwm);
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

    private static int EstimateRobotDelayMs(int pwm)
    {
        int constrained = Math.Clamp(
            pwm,
            CalibratedPwm[0],
            CalibratedPwm[^1]);

        for (int index = 0; index < CalibratedPwm.Length - 1; index++)
        {
            int lowerPwm = CalibratedPwm[index];
            int upperPwm = CalibratedPwm[index + 1];
            if (constrained < lowerPwm || constrained > upperPwm)
                continue;

            double ratio = (constrained - lowerPwm) /
                (double)(upperPwm - lowerPwm);
            return CalibratedRobotDelayMs[index] +
                (int)Math.Round(
                    ratio *
                    (CalibratedRobotDelayMs[index + 1] -
                     CalibratedRobotDelayMs[index]));
        }

        return CalibratedRobotDelayMs[^1];
    }

    public async Task<bool> ConnectForSystemStartAsync()
    {
        if (!IsConnected)
        {
            RefreshPorts();
            await ConnectAsync();
        }

        return IsConnected;
    }

    public bool StartForSystem()
    {
        if (!IsConnected)
            return false;

        StartMotor();
        return LastAppliedPwm == SelectedPwm;
    }

    public bool StopForSystemStart()
    {
        if (!IsConnected)
            return false;

        StopMotor();
        return LastAppliedPwm == StopPwm;
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
        LastReceived = line;
        ConveyorEventKind kind = ClassifyArduinoMessage(line);

        switch (kind)
        {
            case ConveyorEventKind.SensorDetected:
                DetectedBatteryCount++;
                SensorStatus = "Battery detected";
                CycleStage = "Approaching IMITECH camera";
                break;
            case ConveyorEventKind.CameraStopped:
                SensorStatus = "Battery held at camera";
                CycleStage = "Camera stopped — capture requested";
                break;
            case ConveyorEventKind.MovingToRobot:
                CycleStage = "Transporting classified battery to DOFBOT";
                break;
            case ConveyorEventKind.RobotStopped:
                IsWaitingForRobotCompletion = true;
                CycleStage = "Battery held at robot checkpoint";
                break;
            case ConveyorEventKind.CycleCompleted:
                IsWaitingForRobotCompletion = false;
                SensorStatus = "Sensor armed";
                CycleStage = "Cycle completed — conveyor resumed";
                break;
            case ConveyorEventKind.MotorAcknowledged:
                MotorStatus = line;
                break;
        }

        var args = new ConveyorEventArgs(kind, line, DateTime.Now);
        ConveyorEventReceived?.Invoke(this, args);
        if (kind == ConveyorEventKind.CameraStopped)
            CameraStopReached?.Invoke(this, args);
        else if (kind == ConveyorEventKind.RobotStopped)
            RobotStopReached?.Invoke(this, args);

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
        IsWaitingForRobotCompletion = false;
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
