using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels;

public partial class AIDetectionResultViewModel : ObservableObject
{
    [ObservableProperty]
    private InspectionResult? inspectionResult;

    [ObservableProperty]
    private BitmapSource? inspectionImage;

    [ObservableProperty]
    private ObservableCollection<BoundingBox> boundingBoxes = [];

    [ObservableProperty]
    private double score;

    [ObservableProperty]
    private string overallResultStatus = "WAITING";

    [ObservableProperty]
    private string statusIndicatorColor = "#858585";

    [ObservableProperty]
    private string modelStatus = "Awaiting Windows AI service result";

    [ObservableProperty]
    private string detectedClass = "-";

    [ObservableProperty]
    private string selectedRobotCycle = "-";

    [ObservableProperty]
    private string inferenceTime = "-";

    [ObservableProperty]
    private string inspectionTime = "-";

    public AIDetectionResultViewModel()
    {
        SetWaitingState();
    }

    public void LoadResult(InspectionResult result)
    {
        InspectionResult = result;
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
        InferenceTime = $"{result.InferenceTimeMs:0.0} ms";
        InspectionTime =
            result.InspectionTime.ToString("yyyy-MM-dd HH:mm:ss");
        ModelStatus = result.Status == InspectionResultStatus.Error
            ? result.Error ?? "Inference error"
            : "Result received from Windows AI";
        UpdateStatusIndicator(result.Status);
    }

    public void SetWaitingState()
    {
        InspectionResult = null;
        InspectionImage = null;
        BoundingBoxes = [];
        Score = 0;
        DetectedClass = "-";
        SelectedRobotCycle = "-";
        InferenceTime = "-";
        InspectionTime = "-";
        ModelStatus = "Awaiting Windows AI service result";
        UpdateStatusIndicator(InspectionResultStatus.Waiting);
    }

    private void UpdateStatusIndicator(InspectionResultStatus status)
    {
        switch (status)
        {
            case InspectionResultStatus.Detected:
                StatusIndicatorColor = "#4EC9B0";
                OverallResultStatus = "DETECTED";
                break;
            case InspectionResultStatus.Rejected:
                StatusIndicatorColor = "#FFD166";
                OverallResultStatus = "REJECTED";
                break;
            case InspectionResultStatus.Error:
                StatusIndicatorColor = "#F14C4C";
                OverallResultStatus = "ERROR";
                break;
            default:
                StatusIndicatorColor = "#858585";
                OverallResultStatus = "WAITING";
                break;
        }
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

    [RelayCommand]
    private void SaveImage()
    {
        if (InspectionImage == null)
        {
            SystemLogService.Add(
                "AI",
                "No inference image is available to save.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save AI inspection image",
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = ".png",
            FileName =
                $"ai_result_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
        };

        if (dialog.ShowDialog() != true)
            return;

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(InspectionImage));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
        SystemLogService.Add(
            "AI",
            $"Saved inference image: {dialog.FileName}");
    }
}
