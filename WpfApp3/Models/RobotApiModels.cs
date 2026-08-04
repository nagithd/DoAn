using System.Text.Json.Serialization;

namespace WpfApp3.Models;

public sealed class RobotHealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("motion_enabled")]
    public bool MotionEnabled { get; set; }

    [JsonPropertyName("robot_initialized")]
    public bool RobotInitialized { get; set; }
}

public sealed class RobotStatusResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    // Newer robot_api.py versions wrap the live state in "robot".
    // Keeping the flat fields below also supports the earlier response shape.
    [JsonPropertyName("robot")]
    public RobotStatusResponse? Robot { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "unknown";

    [JsonPropertyName("busy")]
    public bool Busy { get; set; }

    [JsonPropertyName("current_step")]
    public string? CurrentStep { get; set; }

    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("motion_enabled")]
    public bool MotionEnabled { get; set; }

    [JsonPropertyName("queue_size")]
    public int QueueSize { get; set; }

    [JsonIgnore]
    public RobotStatusResponse EffectiveState => Robot ?? this;
}

public sealed class RobotServosResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("servos")]
    public List<double?> Servos { get; set; } = [];

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class RobotCommandResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
