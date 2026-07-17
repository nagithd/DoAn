using System.Windows;
using System.Windows.Controls;

namespace WpfApp3.Controls
{
    /// <summary>
    /// Top Navigation Bar UserControl for the Battery Inspection System
    /// Displays application info, system status, and emergency controls
    /// </summary>
    public partial class TopNavigationBar : UserControl
    {
        public static readonly RoutedEvent EmergencyStopPressedEvent = EventManager.RegisterRoutedEvent(
            "EmergencyStopPressed", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TopNavigationBar));

        public event RoutedEventHandler EmergencyStopPressed
        {
            add { AddHandler(EmergencyStopPressedEvent, value); }
            remove { RemoveHandler(EmergencyStopPressedEvent, value); }
        }

        public TopNavigationBar()
        {
            InitializeComponent();
            if (EmergencyStopButton != null)
            {
                EmergencyStopButton.Click += EmergencyStopButton_Click;
            }
        }

        private void EmergencyStopButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(EmergencyStopPressedEvent));
        }

        /// <summary>
        /// Update current time display
        /// </summary>
        public void UpdateTime(string time)
        {
            var tb = this.FindName("CurrentTimeBlock") as TextBlock;
            if (tb != null)
            {
                tb.Text = time;
            }
        }

        /// <summary>
        /// Update system status display
        /// </summary>
        public void UpdateSystemStatus(string status, string color = "#4EC9B0")
        {
            var tb = this.FindName("SystemStatusBlock") as TextBlock;
            if (tb != null)
            {
                tb.Text = status;
                try
                {
                    tb.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                }
                catch
                {
                    // ignore invalid color
                }
            }
        }

        /// <summary>
        /// Update uptime display
        /// </summary>
        public void UpdateUptime(string uptime)
        {
            var tb = this.FindName("UptimeBlock") as TextBlock;
            if (tb != null)
            {
                tb.Text = uptime;
            }
        }

        /// <summary>
        /// Update connection status
        /// </summary>
        public void UpdateConnectionStatus(bool isOnline)
        {
            // Connection status UI was removed from the template. If present, update it.
            var status = this.FindName("ConnectionStatusIndicator") as FrameworkElement;
            if (status != null)
            {
                status.Visibility = isOnline ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Update user name display
        /// </summary>
        public void UpdateUserName(string userName)
        {
            var tb = this.FindName("UserNameBlock") as TextBlock;
            if (tb != null)
            {
                tb.Text = userName;
            }
        }

        /// <summary>
        /// Enable/Disable emergency stop button
        /// </summary>
        public void SetEmergencyStopEnabled(bool enabled)
        {
            EmergencyStopButton.IsEnabled = enabled;
        }
    }
}
