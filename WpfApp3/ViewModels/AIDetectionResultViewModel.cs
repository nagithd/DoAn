using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using WpfApp3.Models;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for the AI Detection Result Dialog
    /// Manages inspection result data and user interactions
    /// </summary>
    public partial class AIDetectionResultViewModel : ObservableObject
    {
        [ObservableProperty]
        private InspectionResult? inspectionResult;

        [ObservableProperty]
        private BitmapSource? inspectionImage;

        [ObservableProperty]
        private ObservableCollection<ComponentInspection> components = new();

        [ObservableProperty]
        private ObservableCollection<BoundingBox> boundingBoxes = new();

        [ObservableProperty]
        private double score = 0.92;

        [ObservableProperty]
        private string overallResultStatus = "GOOD";

        [ObservableProperty]
        private string statusIndicatorColor = "#4EC9B0"; // Green (GOOD)

        public AIDetectionResultViewModel()
        {
            // Load sample data by default
            LoadSampleData();
        }

        /// <summary>
        /// Load sample inspection data for testing
        /// </summary>
        public void LoadSampleData()
        {
            var result = InspectionResult.CreateSampleData();

            InspectionResult = result;
            Components = result.Components;
            BoundingBoxes = result.BoundingBoxes;
            Score = result.Score;

            // Set status based on result type
            UpdateStatusIndicator(result.Result);

            // Load placeholder image
            LoadPlaceholderImage();
        }

        /// <summary>
        /// Update the status indicator and text based on the inspection result
        /// </summary>
        private void UpdateStatusIndicator(InspectionResultStatus status)
        {
            switch (status)
            {
                case InspectionResultStatus.GOOD:
                    StatusIndicatorColor = "#4EC9B0"; // Green - Good
                    OverallResultStatus = "GOOD";
                    break;
                case InspectionResultStatus.BAD:
                    StatusIndicatorColor = "#FFD166"; // Yellow - Bad
                    OverallResultStatus = "BAD";
                    break;
                case InspectionResultStatus.MISS:
                    StatusIndicatorColor = "#F14C4C"; // Red - Miss
                    OverallResultStatus = "MISS";
                    break;
                default:
                    StatusIndicatorColor = "#4EC9B0";
                    OverallResultStatus = "GOOD";
                    break;
            }
        }

        /// <summary>
        /// Load a placeholder image for display
        /// In real implementation, this would load from the actual camera/model output
        /// </summary>
        private void LoadPlaceholderImage()
        {
            try
            {
                // Create a placeholder bitmap (500x300 light gray)
                var bitmap = new WriteableBitmap(500, 300, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);

                // Fill with a light gray color to show the area
                int[] pixels = new int[500 * 300];
                for (int i = 0; i < pixels.Length; i++)
                {
                    // BGRA format: light gray (200, 200, 200) with full alpha
                    pixels[i] = (200 << 24) | (200 << 16) | (200 << 8) | 200;
                }

                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, 500, 300), pixels, 500 * 4, 0);
                InspectionImage = bitmap;
            }
            catch { }
        }

        /// <summary>
        /// Save the inspection image
        /// TODO: Implement actual file save functionality
        /// </summary>
        [RelayCommand]
        public void SaveImage()
        {
            // Placeholder for image save functionality
            System.Diagnostics.Debug.WriteLine("Save image clicked");
        }

        /// <summary>
        /// Close the dialog
        /// This is called from the dialog code-behind
        /// </summary>
        public void CloseDialog()
        {
            // Handled by dialog code-behind
        }
    }
}
