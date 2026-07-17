using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for Robot Arm control and display
    /// Responsible for managing robot arm state and interactions
    /// </summary>
    public partial class RobotArmViewModel : ObservableObject
    {
        [ObservableProperty]
        private string robotStatus = "Idle";

        [ObservableProperty]
        private double positionX = 0.0;

        [ObservableProperty]
        private double positionY = 0.0;

        [ObservableProperty]
        private double positionZ = 0.0;

        [ObservableProperty]
        private string currentAction = "Idle";

        [ObservableProperty]
        private string targetBin = "None";

        [ObservableProperty]
        private double robotTemperature = 0.0;

        [ObservableProperty]
        private bool servo1Status = true;

        [ObservableProperty]
        private bool servo2Status = true;

        [ObservableProperty]
        private bool servo3Status = true;

        [ObservableProperty]
        private int actionProgress = 0;

        [ObservableProperty]
        private string lastCommand = "None";

        public RobotArmViewModel()
        {
            // Initialize default values
        }
    }
}
