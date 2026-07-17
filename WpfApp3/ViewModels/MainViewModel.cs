namespace WpfApp3.ViewModels
{
    /// <summary>
    /// Main ViewModel for the Application Shell
    /// Coordinates between different component ViewModels
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        public CameraViewModel? CameraVM { get; set; }
        public ConveyorViewModel? ConveyorVM { get; set; }
        public AIDetectionViewModel? AIDetectionVM { get; set; }
        public RobotArmViewModel? RobotArmVM { get; set; }
        public StatisticsViewModel? StatisticsVM { get; set; }
        public EventLogViewModel? EventLogVM { get; set; }
        public SettingsViewModel? SettingsVM { get; set; }

        // Placeholder for Main ViewModel implementation
    }
}
