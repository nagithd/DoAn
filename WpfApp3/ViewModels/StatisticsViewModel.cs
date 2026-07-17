using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for Statistics display and management
    /// Responsible for aggregating and presenting statistical data
    /// </summary>
    public partial class StatisticsViewModel : ObservableObject
    {
        [ObservableProperty]
        private int goodCount = 0;

        [ObservableProperty]
        private int scratchCount = 0;

        [ObservableProperty]
        private int dentCount = 0;

        [ObservableProperty]
        private int otherCount = 0;

        [ObservableProperty]
        private int totalCount = 0;

        [ObservableProperty]
        private int todaysProduction = 0;

        [ObservableProperty]
        private int goodTrend = 0;

        [ObservableProperty]
        private int scratchTrend = 0;

        [ObservableProperty]
        private int dentTrend = 0;

        [ObservableProperty]
        private int otherTrend = 0;

        [ObservableProperty]
        private int totalTrend = 0;

        [ObservableProperty]
        private int productionTrend = 0;

        [ObservableProperty]
        private double defectRate = 0.0;

        [ObservableProperty]
        private double passRate = 0.0;

        [ObservableProperty]
        private string peakHour = "N/A";

        [ObservableProperty]
        private int averageProcessingTime = 0;

        public StatisticsViewModel()
        {
            // Initialize default values
        }
    }
}
