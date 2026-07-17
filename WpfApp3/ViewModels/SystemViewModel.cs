using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for System Status monitoring
    /// Responsible for managing system component statuses
    /// </summary>
    public partial class SystemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string cameraStatus = "Green";

        [ObservableProperty]
        private string aiStatus = "Green";

        [ObservableProperty]
        private string robotStatus = "Green";

        [ObservableProperty]
        private string arduinoStatus = "Green";

        [ObservableProperty]
        private string conveyorStatus = "Green";

        [ObservableProperty]
        private string powerStatus = "Green";

        [ObservableProperty]
        private string overallSystemHealth = "Operational";

        [ObservableProperty]
        private int activeAlerts = 0;

        [ObservableProperty]
        private string lastStatusUpdate = "N/A";

        [ObservableProperty]
        private int systemUptime = 0;

        [ObservableProperty]
        private double cpuUsage = 0.0;

        [ObservableProperty]
        private double memoryUsage = 0.0;

        public SystemViewModel()
        {
            // Initialize default values
        }
    }
}
