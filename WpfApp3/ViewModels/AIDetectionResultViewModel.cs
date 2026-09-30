using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using WpfApp3.Models;

namespace WpfApp3.ViewModels;

public partial class AIDetectionResultViewModel : ObservableObject
{
    [ObservableProperty]
    private BitmapSource? inspectionImage;

    [ObservableProperty]
    private ObservableCollection<BoundingBox> boundingBoxes = [];

    [ObservableProperty]
    private double score;

    [ObservableProperty]
    private string detectedClass = "-";

    [ObservableProperty]
    private string selectedRobotCycle = "-";

    public AIDetectionResultViewModel()
    {
        SetWaitingState();
    }

    public void LoadResult(InspectionResult result)
    {
        InspectionImage = result.AnnotatedImage;
        BoundingBoxes = result.BoundingBoxes;
        Score = result.Confidence;
        DetectedClass = string.IsNullOrWhiteSpace(result.DetectedClass)
            ? "-"
            : result.DetectedClass;
        SelectedRobotCycle =
            string.IsNullOrWhiteSpace(result.RecommendedRobotCycle)
                ? RoutingActionFor(result.DetectedClass)
                : result.RecommendedRobotCycle;
    }

    public void SetWaitingState()
    {
        InspectionImage = null;
        BoundingBoxes = [];
        Score = 0;
        DetectedClass = "-";
        SelectedRobotCycle = "-";
    }

    private static string RoutingActionFor(string className) =>
        className.Trim().ToLowerInvariant() switch
        {
            "normal" => "PASS - continue to end of conveyor",
            "dented" => "ROBOT PICK - DENTED bin",
            "scratched" => "ROBOT PICK - SCRATCHED bin",
            "swollen" => "ROBOT PICK - SWOLLEN bin",
            _ => "-"
        };

}
