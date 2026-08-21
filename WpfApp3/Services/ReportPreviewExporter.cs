using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfApp3.Dialogs;
using WpfApp3.Models;
using WpfApp3.ViewModels;

namespace WpfApp3.Services;

/// <summary>
/// Exports deterministic UI screenshots for project documentation.
/// It is opt-in through environment variables and is not part of the
/// production camera/AI/robot workflow.
/// </summary>
public static class ReportPreviewExporter
{
    private const string OutputDirectoryVariable =
        "WPFAPP3_REPORT_PREVIEW_DIR";
    private const string SampleImageVariable =
        "WPFAPP3_REPORT_SAMPLE_IMAGE";
    private const string KeepOpenVariable =
        "WPFAPP3_REPORT_PREVIEW_KEEP_OPEN";
    private const string DofbotImageVariable =
        "WPFAPP3_REPORT_DOFbot_IMAGE";
    private const string FocusVariable =
        "WPFAPP3_REPORT_PREVIEW_FOCUS";

    public static bool IsRequested =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable(OutputDirectoryVariable));

    public static async Task ExportAsync(
        MainWindow owner,
        CameraViewModel cameraViewModel)
    {
        string outputDirectory =
            Environment.GetEnvironmentVariable(OutputDirectoryVariable)!;
        string sampleImage =
            Environment.GetEnvironmentVariable(SampleImageVariable) ?? "";
        bool keepOpen = string.Equals(
            Environment.GetEnvironmentVariable(KeepOpenVariable),
            "1",
            StringComparison.OrdinalIgnoreCase);
        string dofbotImage =
            Environment.GetEnvironmentVariable(DofbotImageVariable) ?? "";
        string focus =
            Environment.GetEnvironmentVariable(FocusVariable) ?? "result";

        try
        {
            if (!File.Exists(sampleImage))
            {
                throw new FileNotFoundException(
                    "The report preview sample image was not found.",
                    sampleImage);
            }
            if (!File.Exists(dofbotImage))
            {
                throw new FileNotFoundException(
                    "The DOFBOT report preview image was not found.",
                    dofbotImage);
            }

            Directory.CreateDirectory(outputDirectory);
            InspectionResult result =
                cameraViewModel.LoadReportPlaceholder(sampleImage);

            await owner.Dispatcher.InvokeAsync(
                () =>
                {
                    owner.Activate();
                    owner.UpdateLayout();
                },
                DispatcherPriority.ContextIdle);

            SaveElement(
                owner,
                Path.Combine(
                    outputDirectory,
                    "ai-camera-tab-placeholder.png"));

            var dialog = new AIDetectionResultDialog(
                result,
                cameraViewModel.AiServiceStatus,
                cameraViewModel.CheckAiServiceCommand)
            {
                Owner = owner
            };

            var rendered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            dialog.ContentRendered += (_, _) => rendered.TrySetResult();
            dialog.Show();
            await rendered.Task;
            await dialog.Dispatcher.InvokeAsync(
                dialog.UpdateLayout,
                DispatcherPriority.ContextIdle);

            SaveElement(
                dialog,
                Path.Combine(
                    outputDirectory,
                    "ai-result-placeholder.png"));

            owner.LoadDofbotReportPlaceholder(dofbotImage);
            await owner.Dispatcher.InvokeAsync(
                () =>
                {
                    owner.Activate();
                    owner.UpdateLayout();
                },
                DispatcherPriority.ContextIdle);

            SaveElement(
                owner,
                Path.Combine(
                    outputDirectory,
                    "dofbot-camera-tab-placeholder.png"));

            owner.SelectReportTab(2);
            await owner.Dispatcher.InvokeAsync(
                owner.UpdateLayout,
                DispatcherPriority.ContextIdle);
            SaveElement(
                owner,
                Path.Combine(
                    outputDirectory,
                    "workflow-conveyor-tab.png"));

            if (!keepOpen)
            {
                dialog.Close();
                Application.Current.Shutdown();
            }
            else if (focus.Equals(
                         "dofbot",
                         StringComparison.OrdinalIgnoreCase))
            {
                dialog.Close();
                owner.SelectReportTab(1);
                owner.Activate();
            }
            else if (focus.Equals(
                         "ai",
                         StringComparison.OrdinalIgnoreCase))
            {
                dialog.Close();
                owner.SelectReportTab(0);
                owner.Activate();
            }
            else if (focus.Equals(
                         "workflow",
                         StringComparison.OrdinalIgnoreCase))
            {
                dialog.Close();
                owner.SelectReportTab(2);
                owner.Activate();
            }
            else
            {
                owner.SelectReportTab(0);
                dialog.Activate();
            }
        }
        catch (Exception ex)
        {
            SystemLogService.Add(
                "REPORT",
                $"Cannot export report preview images: {ex.Message}");
            Application.Current.Shutdown(1);
        }
    }

    private static void SaveElement(
        FrameworkElement element,
        string outputPath)
    {
        element.UpdateLayout();
        int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));

        var bitmap = new RenderTargetBitmap(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
