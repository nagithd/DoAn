using System.Text.Json.Serialization;

namespace WpfApp3.Models;

public sealed class VisionStatusResponse
{
    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("camera_open")]
    public bool CameraOpen { get; set; }

    [JsonPropertyName("frame_count")]
    public long FrameCount { get; set; }

    [JsonPropertyName("monitor_only")]
    public bool MonitorOnly { get; set; }

    [JsonPropertyName("vision_processing_enabled")]
    public bool VisionProcessingEnabled { get; set; }

    [JsonPropertyName("detector_available")]
    public bool DetectorAvailable { get; set; }

    [JsonPropertyName("last_inference_ms")]
    public double? LastInferenceMs { get; set; }

    [JsonPropertyName("last_detection")]
    public VisionDetection? LastDetection { get; set; }

    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }
}

public sealed class VisionDetection
{
    [JsonPropertyName("detector")]
    public string? Detector { get; set; }

    [JsonPropertyName("center_x")]
    public double CenterX { get; set; }

    [JsonPropertyName("center_y")]
    public double CenterY { get; set; }

    [JsonPropertyName("relative_angle_deg")]
    public double RelativeAngleDeg { get; set; }

    [JsonPropertyName("wrist_angle")]
    public double? WristAngle { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("inference_ms")]
    public double? InferenceMs { get; set; }
}

public sealed class VisionJobSummary
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("conveyor_release_allowed")]
    public bool ConveyorReleaseAllowed { get; set; }

    [JsonPropertyName("pose_confirmation")]
    public string? PoseConfirmation { get; set; }
    // Compatibility with Robot API revisions that wrap the actual job in an
    // API-style { "success": true, "job": { ... } } envelope.
    [JsonPropertyName("job")]
    public VisionJobSummary? NestedJob { get; set; }

    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }
}

public sealed class RobotJobResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("job")]
    public VisionJobSummary? Job { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
