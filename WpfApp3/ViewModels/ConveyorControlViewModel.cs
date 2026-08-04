using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Windows;
using WpfApp3.Services;

namespace WpfApp3.ViewModels;

/// <summary>
/// Serial connection used by the partially completed Arduino conveyor.
/// Commands are intentionally editable because some firmware revisions use
/// the opposite 0/1 convention.
/// </summary>
public partial class ConveyorControlViewModel : ObservableObject, IDisposable
{
    private const int ConveyorBaudRate = 9600;
    private const string StartMotorSignal = "motor = 1@";
    private const string StopMotorSignal = "motor = 0@";
    private readonly object _serialLock = new();
    private SerialPort? _serialPort;
    private bool _disposed;

    public ObservableCollection<string> AvailablePorts { get; } = [];

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

    public string BaudRateText => $"{ConveyorBaudRate} baud, 8-N-1";
    public string StartCommand => StartMotorSignal;
    public string StopCommand => StopMotorSignal;

    public ConveyorControlViewModel() => RefreshPorts();

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
                Encoding = Encoding.ASCII,
                ReadTimeout = 500,
                WriteTimeout = 500,
                DtrEnable = false,
                RtsEnable = false
            };
            port.DataReceived += SerialPort_DataReceived;
            port.Open();

            lock (_serialLock)
                _serialPort = port;

            IsConnected = true;
            ConnectionStatus = $"Connected — {SelectedPortName}";
            MotorStatus = "Ready — state not confirmed";
            SystemLogService.Add(
                "CONVEYOR",
                $"Connected to {SelectedPortName} at {ConveyorBaudRate} baud. " +
                "Waiting for a possible Arduino reset.");

            // Many Arduino boards reset when a serial connection is opened.
            await Task.Delay(1500);
            if (IsConnected)
                SystemLogService.Add("CONVEYOR", "Serial connection is ready for motor commands.");
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
        if (SendCommand(StartMotorSignal, "START"))
            MotorStatus = $"Start command sent: {StartMotorSignal}";
    }

    [RelayCommand]
    private void StopMotor()
    {
        if (SendCommand(StopMotorSignal, "STOP"))
            MotorStatus = $"Stop command sent: {StopMotorSignal}";
    }

    [RelayCommand]
    private void Disconnect()
    {
        ClosePort(sendStopFirst: true);
        ConnectionStatus = "Disconnected";
        MotorStatus = "Stopped / disconnected";
    }

    private bool SendCommand(string command, string action)
    {
        if (command is not (StartMotorSignal or StopMotorSignal))
        {
            SystemLogService.Add(
                "CONVEYOR",
                $"Rejected invalid {action} command. Only raw '1' and '0' are allowed.");
            return false;
        }

        try
        {
            lock (_serialLock)
            {
                if (_serialPort?.IsOpen != true)
                {
                    SystemLogService.Add("CONVEYOR", "Arduino is not connected.");
                    return false;
                }

                // The Arduino protocol accepts exactly one raw character:
                // '1' starts the motor and '0' stops it. Write is used instead
                // of WriteLine so no CR/LF is appended to the command.
                _serialPort.Write(command);
            }

            SystemLogService.Add(
                "CONVEYOR",
                $"{action} command sent: {command} (awaiting hardware confirmation)." );
            return true;
        }
        catch (Exception ex)
        {
            SystemLogService.Add("CONVEYOR", $"Cannot send {action}: {ex.Message}");
            return false;
        }
    }

    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (sender is not SerialPort port)
                return;

            string response = port.ReadExisting().Trim();
            if (response.Length == 0)
                return;

            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                Application.Current.Dispatcher.Invoke(
                    () => LastReceived = response);
            }
            else
            {
                LastReceived = response;
            }
            SystemLogService.Add("CONVEYOR", $"Arduino response: {response}");
        }
        catch (Exception ex)
        {
            SystemLogService.Add("CONVEYOR", $"Cannot read Arduino response: {ex.Message}");
        }
    }

    private void ClosePort(bool sendStopFirst)
    {
        SerialPort? port;
        lock (_serialLock)
        {
            port = _serialPort;
            _serialPort = null;
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
                port.Write(StopMotorSignal);
                SystemLogService.Add(
                    "CONVEYOR",
                    $"Safety stop sent before disconnect: {StopMotorSignal}." );
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
