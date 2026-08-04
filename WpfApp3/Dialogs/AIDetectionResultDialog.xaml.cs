using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WpfApp3.Models;
using WpfApp3.ViewModels;

namespace WpfApp3.Dialogs;

public partial class AIDetectionResultDialog : Window
{
    private readonly AIDetectionResultViewModel _viewModel;

    public AIDetectionResultDialog(
        InspectionResult? result = null)
    {
        InitializeComponent();
        _viewModel = new AIDetectionResultViewModel();
        if (result != null)
            _viewModel.LoadResult(result);
        DataContext = _viewModel;
        ContentRendered += (_, _) => RenderResult();
    }

    private void RenderResult()
    {
        ConfigureImageCanvas();
        DrawBoundingBoxes();
        UpdateStatusIndicator();
    }

    private void ConfigureImageCanvas()
    {
        BitmapSource? image = _viewModel.InspectionImage;
        if (image == null)
            return;

        double width = Math.Max(1, image.PixelWidth);
        double height = Math.Max(1, image.PixelHeight);
        ImageCanvas.Width = width;
        ImageCanvas.Height = height;
        InspectionImage.Width = width;
        InspectionImage.Height = height;
    }

    private void DrawBoundingBoxes()
    {
        for (int index = ImageCanvas.Children.Count - 1;
             index >= 0;
             index--)
        {
            if (ImageCanvas.Children[index] is not Image)
                ImageCanvas.Children.RemoveAt(index);
        }

        foreach (BoundingBox box in _viewModel.BoundingBoxes)
        {
            var rectangle = new Rectangle
            {
                Width = Math.Max(1, box.Width),
                Height = Math.Max(1, box.Height),
                Stroke = GetColorForConfidence(box.Confidence),
                StrokeThickness = 3,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(rectangle, box.X);
            Canvas.SetTop(rectangle, box.Y);
            ImageCanvas.Children.Add(rectangle);

            var label = new TextBlock
            {
                Text = $"{box.ClassName}  {box.Confidence:P1}",
                FontSize = 13,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(
                    Color.FromArgb(220, 30, 30, 30)),
                Padding = new Thickness(5, 3, 5, 3)
            };
            Canvas.SetLeft(label, box.X);
            Canvas.SetTop(label, Math.Max(0, box.Y - 28));
            ImageCanvas.Children.Add(label);
        }
    }

    private void UpdateStatusIndicator()
    {
        try
        {
            StatusIndicator.Fill = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(
                    _viewModel.StatusIndicatorColor));
        }
        catch
        {
            StatusIndicator.Fill = Brushes.Gray;
        }
    }

    private static SolidColorBrush GetColorForConfidence(
        double confidence)
    {
        if (confidence >= 0.9)
            return new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#4EC9B0"));
        if (confidence >= 0.7)
            return new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FFD166"));
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString("#F14C4C"));
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e) =>
        Close();
}
