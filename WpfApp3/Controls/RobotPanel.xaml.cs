using System.Windows.Controls;

namespace WpfApp3.Controls
{
    /// <summary>
    /// RobotPanel.xaml の相互作用ロジック
    /// </summary>
    public partial class RobotPanel : UserControl
    {
        public RobotPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set the robot status indicator
        /// </summary>
        public void SetRobotStatus(string status)
        {
            RobotStatusValue.Text = status;
        }

        /// <summary>
        /// Set the current position (X, Y, Z coordinates)
        /// </summary>
        public void SetCurrentPosition(double x, double y, double z)
        {
            CurrentPositionValue.Text = $"X: {x:F1}, Y: {y:F1}, Z: {z:F1}";
        }

        /// <summary>
        /// Set the current action being performed
        /// </summary>
        public void SetCurrentAction(string action)
        {
            CurrentActionValue.Text = action;
        }

        /// <summary>
        /// Set the target bin
        /// </summary>
        public void SetTargetBin(string binId)
        {
            TargetBinValue.Text = binId;
        }

        /// <summary>
        /// Set the robot temperature and update the progress bar
        /// </summary>
        public void SetRobotTemperature(double celsius)
        {
            TemperatureValue.Text = $"{celsius:F1}°C";
            // Clamp temperature bar value between 0 and 100
            TemperatureBar.Value = System.Math.Min(celsius, 100);
        }

        /// <summary>
        /// Set servo status (1-3)
        /// </summary>
        public void SetServoStatus(int servoNumber, bool isOperational)
        {
            if (servoNumber >= 1 && servoNumber <= 3)
            {
                var servoIndicator = this.FindName($"ServoStatus{servoNumber}");
                if (servoIndicator is Border border)
                {
                    border.Background = isOperational 
                        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 201, 176)) // #4EC9B0
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 113, 113)); // #F47171
                }
            }
        }
    }
}
