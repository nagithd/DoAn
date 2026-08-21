using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;

namespace WpfApp3.Models;

/// <summary>
/// Bounding box returned by the future Windows AI inference service.
/// Coordinates are expressed in source-image pixels.
/// </summary>
public class BoundingBox
{
    public string ClassName { get; set; } = "";
    public double Confidence { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public enum InspectionResultStatus
{
    Waiting,
    Detected,
    Rejected,
    Error
}

/// <summary>
/// UI contract for the trained model. The Python integration can populate
/// this object later without changing the result window.
/// </summary>
public class InspectionResult
{
    public string InspectionId { get; set; } = "";

    public InspectionResultStatus Status { get; set; } =
        InspectionResultStatus.Waiting;

    public string DetectedClass { get; set; } = "";

    public double Confidence { get; set; }

    public string RecommendedRobotCycle { get; set; } = "";

    public double InferenceTimeMs { get; set; }

    public BitmapSource? AnnotatedImage { get; set; }

    public ObservableCollection<BoundingBox> BoundingBoxes { get; set; } = [];

    public DateTime InspectionTime { get; set; } = DateTime.Now;

    public string? Error { get; set; }
}
