using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WpfApp3.ViewModels;

namespace WpfApp3.Dialogs
{
    /// <summary>
    /// Interaction logic for AIDetectionResultDialog.xaml
    /// Displays AI detection results with bounding boxes overlay
    /// </summary>
    public partial class AIDetectionResultDialog : Window
    {
        private AIDetectionResultViewModel _viewModel;

        public AIDetectionResultDialog()
        {
            InitializeComponent();

            // Initialize view model
            _viewModel = new AIDetectionResultViewModel();
            this.DataContext = _viewModel;

            // Draw bounding boxes after content is loaded
            this.ContentRendered += AIDetectionResultDialog_ContentRendered;
        }

        /// <summary>
        /// Draw bounding boxes on the canvas after window is rendered
        /// </summary>
        private void AIDetectionResultDialog_ContentRendered(object? sender, System.EventArgs e)
        {
            DrawBoundingBoxes();
            UpdateStatusIndicator();
            HighlightLegendBox();
        }

        /// <summary>
        /// Draw bounding boxes on the canvas
        /// </summary>
        private void DrawBoundingBoxes()
        {
            if (_viewModel?.BoundingBoxes == null)
                return;

            // Clear existing rectangles (except the image)
            for (int i = ImageCanvas.Children.Count - 1; i >= 0; i--)
            {
                if (ImageCanvas.Children[i] is Rectangle rect)
                {
                    ImageCanvas.Children.RemoveAt(i);
                }
                if (ImageCanvas.Children[i] is TextBlock txt)
                {
                    ImageCanvas.Children.RemoveAt(i);
                }
            }

            // Draw rectangles for each bounding box
            foreach (var box in _viewModel.BoundingBoxes)
            {
                // Scale boxes to fit within the canvas (500x300)
                var scaledX = box.X * (500.0 / 640.0); // Assuming 640x480 source
                var scaledY = box.Y * (300.0 / 480.0);
                var scaledWidth = box.Width * (500.0 / 640.0);
                var scaledHeight = box.Height * (300.0 / 480.0);

                // Draw rectangle
                var rect = new Rectangle
                {
                    Width = scaledWidth,
                    Height = scaledHeight,
                    Stroke = GetColorForConfidence(box.Confidence),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Colors.Transparent)
                };

                Canvas.SetLeft(rect, scaledX);
                Canvas.SetTop(rect, scaledY);
                ImageCanvas.Children.Add(rect);

                // Draw label with component name and confidence
                var label = new TextBlock
                {
                    Text = $"{box.ComponentName}\n{box.Confidence:P0}",
                    FontSize = 10,
                    Foreground = GetColorForConfidence(box.Confidence),
                    Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                    Padding = new Thickness(4, 2, 4, 2)
                };

                Canvas.SetLeft(label, scaledX + 2);
                Canvas.SetTop(label, Math.Max(0, scaledY - 30));
                ImageCanvas.Children.Add(label);
            }
        }

        /// <summary>
        /// Update the status indicator color based on the inspection result
        /// </summary>
        private void UpdateStatusIndicator()
        {
            var color = _viewModel?.StatusIndicatorColor ?? "#4EC9B0";
            try
            {
                StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }
            catch
            {
                StatusIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"));
            }
        }

        /// <summary>
        /// Highlight the legend box corresponding to the current result status
        /// </summary>
        private void HighlightLegendBox()
        {
            // This will be implemented when XAML is fully compiled
            try
            {
                // Reset all legend boxes
                if (LegendMiss != null)
                    LegendMiss.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3D"));
                if (LegendBad != null)
                    LegendBad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3D"));
                if (LegendGood != null)
                    LegendGood.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A3D"));

                // Highlight the appropriate box
                if (_viewModel?.OverallResultStatus == "MISS" && LegendMiss != null)
                {
                    LegendMiss.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F14C4C"));
                }
                else if (_viewModel?.OverallResultStatus == "BAD" && LegendBad != null)
                {
                    LegendBad.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD166"));
                }
                else if (_viewModel?.OverallResultStatus == "GOOD" && LegendGood != null)
                {
                    LegendGood.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"));
                }
            }
            catch { }
        }

        /// <summary>
        /// Get stroke color based on confidence level
        /// </summary>
        private SolidColorBrush GetColorForConfidence(double confidence)
        {
            if (confidence >= 0.9)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0")); // Green
            else if (confidence >= 0.7)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD166")); // Yellow
            else
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F14C4C")); // Red
        }

        /// <summary>
        /// Handle Close button click
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
