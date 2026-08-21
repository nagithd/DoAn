using System.Text.Json.Serialization;

namespace WpfApp3.Models;

public sealed class VisionStatusResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("opencv_available")]
    public bool OpenCvAvailable { get; set; }

    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("camera_open")]
    public bool CameraOpen { get; set; }

    [JsonPropertyName("frame_count")]
    public long FrameCount { get; set; }

    [JsonPropertyName("trigger_latched")]
    public bool TriggerLatched { get; set; }

    [JsonPropertyName("stable_count")]
    public int StableCount { get; set; }

    [JsonPropertyName("auto_trigger")]
    public bool AutoTrigger { get; set; }

    [JsonPropertyName("monitor_only")]
    public bool MonitorOnly { get; set; }

    [JsonPropertyName("vision_processing_enabled")]
    public bool VisionProcessingEnabled { get; set; }

    [JsonPropertyName("detection_backend")]
    public string? DetectionBackend { get; set; }

    [JsonPropertyName("detector_available")]
    public bool DetectorAvailable { get; set; }

    [JsonPropertyName("last_inference_ms")]
    public double? LastInferenceMs { get; set; }

    [JsonPropertyName("robot_ready")]
    public bool RobotReady { get; set; }

    [JsonPropertyName("last_detection")]
    public VisionDetection? LastDetection { get; set; }

    [JsonPropertyName("last_trigger")]
    public VisionDetection? LastTrigger { get; set; }

    [JsonPropertyName("last_job")]
    public VisionJobSummary? LastJob { get; set; }

    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("classification_queue")]
    public VisionClassificationQueue? ClassificationQueue { get; set; }

    [JsonPropertyName("timing")]
    public VisionTiming? Timing { get; set; }

    [JsonPropertyName("config")]
    public VisionConfig? Config { get; set; }
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

    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }

    [JsonPropertyName("start_delay_ms")]
    public int StartDelayMs { get; set; }

    [JsonPropertyName("arrival_ms")]
    public int? ArrivalMs { get; set; }

    [JsonPropertyName("late")]
    public bool Late { get; set; }

    [JsonPropertyName("inspection_id")]
    public string? InspectionId { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("inference_ms")]
    public double? InferenceMs { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }
}

public sealed class VisionJobSummary
{
    // Compatibility with Robot API revisions that wrap the actual job in an
    // API-style { "success": true, "job": { ... } } envelope.
    [JsonPropertyName("job")]
    public VisionJobSummary? NestedJob { get; set; }

    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("current_step")]
    public string? CurrentStep { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }
}

public sealed class VisionTiming
{
    [JsonPropertyName("arrival_ms")]
    public int? ArrivalMs { get; set; }

    [JsonPropertyName("raw_start_delay_ms")]
    public int? RawStartDelayMs { get; set; }

    [JsonPropertyName("start_delay_ms")]
    public int StartDelayMs { get; set; }

    [JsonPropertyName("late")]
    public bool Late { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public sealed class VisionConfig
{
    [JsonPropertyName("belt_speed_mm_s")]
    public double BeltSpeedMmS { get; set; } = 40;

    [JsonPropertyName("distance_to_pick_mm")]
    public double DistanceToPickMm { get; set; } = 200;

    [JsonPropertyName("robot_time_to_grip_ms")]
    public int RobotTimeToGripMs { get; set; } = 4150;

    [JsonPropertyName("processing_margin_ms")]
    public int ProcessingMarginMs { get; set; } = 350;

    [JsonPropertyName("auto_trigger")]
    public bool AutoTrigger { get; set; }
}

public sealed class VisionClassification
{
    [JsonPropertyName("inspection_id")]
    public string InspectionId { get; set; } = "";

    [JsonPropertyName("class_name")]
    public string ClassName { get; set; } = "";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";
}

public sealed class VisionClassificationQueue
{
    [JsonPropertyName("queue_size")]
    public int QueueSize { get; set; }

    [JsonPropertyName("next_result")]
    public VisionClassification? NextResult { get; set; }

    [JsonPropertyName("expired_removed")]
    public int ExpiredRemoved { get; set; }
}

public sealed class VisionClassificationResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("accepted")]
    public VisionClassification? Accepted { get; set; }

    [JsonPropertyName("queue_size")]
    public int QueueSize { get; set; }

    [JsonPropertyName("next_result")]
    public VisionClassification? NextResult { get; set; }

    [JsonPropertyName("removed")]
    public int Removed { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class VisionCheckpointResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("duplicate")]
    public bool Duplicate { get; set; }

    [JsonPropertyName("trigger")]
    public VisionDetection? Trigger { get; set; }

    [JsonPropertyName("job")]
    public VisionJobSummary? Job { get; set; }

    [JsonPropertyName("queue_size")]
    public int QueueSize { get; set; }

    [JsonPropertyName("next_result")]
    public VisionClassification? NextResult { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
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

public sealed class VisionConfigResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("config")]
    public VisionConfig Config { get; set; } = new();
}
