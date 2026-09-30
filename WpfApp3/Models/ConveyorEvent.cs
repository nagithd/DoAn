namespace WpfApp3.Models;

public enum ConveyorEventKind
{
    SensorDetected,
    CameraStopped,
    MovingToRobot,
    RobotStopped,
    CycleCompleted,
    MotorAcknowledged,
    Heartbeat,
    Message
}

public sealed class ConveyorEventArgs(
    ConveyorEventKind kind,
    string rawMessage,
    DateTime receivedAt,
    string? cycleId = null) : EventArgs
{
    public string? CycleId { get; } = cycleId;
    public ConveyorEventKind Kind { get; } = kind;
    public string RawMessage { get; } = rawMessage;
    public DateTime ReceivedAt { get; } = receivedAt;
}
