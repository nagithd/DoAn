using System;
using WpfApp3.ViewModels;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for Top Navigation Bar
    /// Provides data binding and placeholder for navigation bar state management
    /// </summary>
    public class TopNavigationBarViewModel : ViewModelBase
    {
        private string _applicationTitle = "Battery Inspection System";
        private string _applicationVersion = "1.0.0";
        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
        private string _currentUser = "Administrator";
        private string _userRole = "System Admin";
        private string _systemStatus = "Ready";
        private string _systemStatusColor = "#4EC9B0";
        private string _uptime = "00:00:00";
        private bool _isOnline = true;

        /// <summary>
        /// Application title
        /// </summary>
        public string ApplicationTitle
        {
            get => _applicationTitle;
            set => SetProperty(ref _applicationTitle, value);
        }

        /// <summary>
        /// Application version
        /// </summary>
        public string ApplicationVersion
        {
            get => _applicationVersion;
            set => SetProperty(ref _applicationVersion, value);
        }

        /// <summary>
        /// Current system time
        /// </summary>
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        /// <summary>
        /// Logged-in user name
        /// </summary>
        public string CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        /// <summary>
        /// User's role/title
        /// </summary>
        public string UserRole
        {
            get => _userRole;
            set => SetProperty(ref _userRole, value);
        }

        /// <summary>
        /// Current system status (Ready, Warning, Error, etc.)
        /// </summary>
        public string SystemStatus
        {
            get => _systemStatus;
            set => SetProperty(ref _systemStatus, value);
        }

        /// <summary>
        /// Color for system status display
        /// </summary>
        public string SystemStatusColor
        {
            get => _systemStatusColor;
            set => SetProperty(ref _systemStatusColor, value);
        }

        /// <summary>
        /// System uptime
        /// </summary>
        public string Uptime
        {
            get => _uptime;
            set => SetProperty(ref _uptime, value);
        }

        /// <summary>
        /// Connection status
        /// </summary>
        public bool IsOnline
        {
            get => _isOnline;
            set => SetProperty(ref _isOnline, value);
        }

        public TopNavigationBarViewModel()
        {
            // Initialize with current values
            UpdateCurrentTime();
        }

        /// <summary>
        /// Update the current time (placeholder for timer implementation)
        /// </summary>
        public void UpdateCurrentTime()
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        }

        /// <summary>
        /// Update system status with appropriate color
        /// </summary>
        public void SetSystemStatus(string status, string color)
        {
            SystemStatus = status;
            SystemStatusColor = color;
        }

        /// <summary>
        /// Set connection status
        /// </summary>
        public void SetConnectionStatus(bool isOnline)
        {
            IsOnline = isOnline;
        }

        /// <summary>
        /// Update user information
        /// </summary>
        public void SetUserInfo(string userName, string userRole)
        {
            CurrentUser = userName;
            UserRole = userRole;
        }

        /// <summary>
        /// Update uptime display
        /// </summary>
        public void UpdateUptime(TimeSpan elapsed)
        {
            Uptime = string.Format("{0:D2}:{1:D2}:{2:D2}", 
                elapsed.Hours, 
                elapsed.Minutes, 
                elapsed.Seconds);
        }
    }
}
