using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for the Dashboard screen
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private int goodCount;

        [ObservableProperty]
        private int scratchCount;

        [ObservableProperty]
        private int dentCount;

        [ObservableProperty]
        private int otherCount;

        [ObservableProperty]
        private int totalCount;

        [ObservableProperty]
        private int todaysProduction;

        [ObservableProperty]
        private int goodTrend;

        [ObservableProperty]
        private int scratchTrend;

        [ObservableProperty]
        private int dentTrend;

        [ObservableProperty]
        private int otherTrend;

        [ObservableProperty]
        private int totalTrend;

        [ObservableProperty]
        private int productionTrend;

        [ObservableProperty]
        private string robotStatus = "Idle";

        [ObservableProperty]
        private double robotTemperature = 0.0;

        [ObservableProperty]
        private string systemHealth = "Good";

        public DashboardViewModel()
        {
            // Initialize default values
        }
    }
}
