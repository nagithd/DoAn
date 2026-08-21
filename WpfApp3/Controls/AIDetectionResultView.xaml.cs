using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Input;
using WpfApp3.Models;
using WpfApp3.ViewModels;

namespace WpfApp3.Controls;

public partial class AIDetectionResultView : UserControl
{
    public AIDetectionResultViewModel ViewModel { get; } = new();

    public static readonly DependencyProperty ResultProperty =
        DependencyProperty.Register(
            nameof(Result),
            typeof(InspectionResult),
            typeof(AIDetectionResultView),
            new PropertyMetadata(null, OnResultChanged));

    public InspectionResult? Result
    {
        get => (InspectionResult?)GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    public static readonly DependencyProperty AiServiceStatusProperty =
        DependencyProperty.Register(
            nameof(AiServiceStatus),
            typeof(string),
            typeof(AIDetectionResultView),
            new PropertyMetadata("AI service status unavailable"));

    public string AiServiceStatus
    {
        get => (string)GetValue(AiServiceStatusProperty);
        set => SetValue(AiServiceStatusProperty, value);
    }

    public static readonly DependencyProperty CheckAiServiceCommandProperty =
        DependencyProperty.Register(
            nameof(CheckAiServiceCommand),
            typeof(ICommand),
            typeof(AIDetectionResultView),
            new PropertyMetadata(null));

    public ICommand? CheckAiServiceCommand
    {
        get => (ICommand?)GetValue(CheckAiServiceCommandProperty);
        set => SetValue(CheckAiServiceCommandProperty, value);
    }

    public AIDetectionResultView()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderResult();
    }

    private static void OnResultChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (AIDetectionResultView)sender;

        if (args.NewValue is InspectionResult result)
            view.ViewModel.LoadResult(result);
        else
            view.ViewModel.SetWaitingState();

        view.Dispatcher.BeginInvoke(
            view.RenderResult,
            DispatcherPriority.Loaded);
    }

    private void RenderResult()
    {
        ConfigureImageCanvas();
        DrawBoundingBoxes();
    }

    private void ConfigureImageCanvas()
    {
        BitmapSource? image = ViewModel.InspectionImage;
        double width = Math.Max(1, image?.PixelWidth ?? 640);
        double height = Math.Max(1, image?.PixelHeight ?? 480);

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

        foreach (BoundingBox box in ViewModel.BoundingBoxes)
        {
            double labelFontSize = Math.Clamp(
                ImageCanvas.Width / 28,
                32,
                150);
            double labelPadding = Math.Clamp(
                ImageCanvas.Width / 300,
                7,
                18);

            var rectangle = new Rectangle
            {
                Width = Math.Max(1, box.Width),
                Height = Math.Max(1, box.Height),
                Stroke = GetColorForConfidence(box.Confidence),
                StrokeThickness = Math.Max(3, ImageCanvas.Width / 900),
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(rectangle, box.X);
            Canvas.SetTop(rectangle, box.Y);
            ImageCanvas.Children.Add(rectangle);

            var label = new TextBlock
            {
                Text = $"{box.ClassName}  {box.Confidence:P1}",
                FontSize = labelFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(
                    Color.FromArgb(220, 30, 30, 30)),
                Padding = new Thickness(
                    labelPadding,
                    labelPadding * 0.55,
                    labelPadding,
                    labelPadding * 0.55)
            };
            Canvas.SetLeft(label, box.X);
            Canvas.SetTop(
                label,
                Math.Max(
                    0,
                    box.Y - labelFontSize - (labelPadding * 2)));
            ImageCanvas.Children.Add(label);
        }
    }

    private static SolidColorBrush GetColorForConfidence(double confidence)
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
}
