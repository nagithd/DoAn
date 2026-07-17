using System.Windows.Controls;
using System.Windows.Media;
using WpfApp3.Enums;

namespace WpfApp3.Controls
{
    /// <summary>
    /// SystemStatus.xaml の相互作用ロジック
    /// </summary>
    public partial class SystemStatus : UserControl
    {
        // Color definitions for status indicators
        private static readonly Color GreenColor = Color.FromRgb(78, 201, 176);      // #4EC9B0
        private static readonly Color YellowColor = Color.FromRgb(255, 184, 108);    // #FFB86C
        private static readonly Color RedColor = Color.FromRgb(244, 113, 113);       // #F47171

        public SystemStatus()
        {
            InitializeComponent();
            // Initialize all statuses to Green
            SetAllGreen();
        }

        /// <summary>
        /// Set all components to green status
        /// </summary>
        public void SetAllGreen()
        {
            SetCameraStatus(StatusLevel.Green);
            SetAIStatus(StatusLevel.Green);
            SetRobotStatus(StatusLevel.Green);
            SetArduinoStatus(StatusLevel.Green);
            SetConveyorStatus(StatusLevel.Green);
            SetPowerStatus(StatusLevel.Green);
        }

        /// <summary>
        /// Set camera status indicator
        /// </summary>
        public void SetCameraStatus(StatusLevel status)
        {
            CameraIndicator.Background = new SolidColorBrush(GetStatusColor(status));
        }

        /// <summary>
        /// Set AI status indicator
        /// </summary>
        public void SetAIStatus(StatusLevel status)
        {
            AIIndicator.Background = new SolidColorBrush(GetStatusColor(status));
        }

        /// <summary>
        /// Set robot status indicator
        /// </summary>
        public void SetRobotStatus(StatusLevel status)
        {
            RobotIndicator.Background = new SolidColorBrush(GetStatusColor(status));
        }

        /// <summary>
        /// Set Arduino status indicator
        /// </summary>
        public void SetArduinoStatus(StatusLevel status)
        {
            ArduinoIndicator.Background = new SolidColorBrush(GetStatusColor(status));
        }

        /// <summary>
        /// Set conveyor status indicator
        /// </summary>
        public void SetConveyorStatus(StatusLevel status)
        {
            ConveyorIndicator.Background = new SolidColorBrush(GetStatusColor(status));
        }

        /// <summary>
        /// Set power status indicator
        /// </summary>
        public void SetPowerStatus(StatusLevel status)
        {
            PowerIndicator.Background = new SolidColorBrush(GetStatusColor(status));
        }

        /// <summary>
        /// Get color based on status level
        /// </summary>
        private Color GetStatusColor(StatusLevel status)
        {
            return status switch
            {
                StatusLevel.Green => GreenColor,
                StatusLevel.Yellow => YellowColor,
                StatusLevel.Red => RedColor,
                _ => GreenColor
            };
        }
    }
}
