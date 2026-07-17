using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for Camera control and display
    /// Responsible for managing camera state and interactions
    /// </summary>
    public partial class CameraViewModel : ObservableObject
    {
        [ObservableProperty]
        private string cameraStatus = "Disconnected";

        [ObservableProperty]
        private string cameraResolution = "1920x1080";

        [ObservableProperty]
        private double cameraFrameRate = 30.0;

        [ObservableProperty]
        private string lensType = "Standard";

        [ObservableProperty]
        private double exposureTime = 0.0;

        [ObservableProperty]
        private double cameraTemperature = 0.0;

        [ObservableProperty]
        private int capturedFrames = 0;

        [ObservableProperty]
        private string lastCaptureTime = "N/A";

        [ObservableProperty]
        private bool isRecording = false;

        [ObservableProperty]
        private int recordingDuration = 0;

        [ObservableProperty]
        private ObservableCollection<string> availableCameras = new ObservableCollection<string>();

        [ObservableProperty]
        private ObservableCollection<string> systemLog = new ObservableCollection<string>();

        [RelayCommand]
        private void Connect()
        {
            CameraStatus = "Connected";
            SystemLog.Add($"{DateTime.Now:s}: Connected to camera.");
        }

        [RelayCommand]
        private void Refresh()
        {
            // Populate available cameras with placeholder entries for now
            AvailableCameras.Clear();
            AvailableCameras.Add("Camera 1 - USB");
            AvailableCameras.Add("Camera 2 - GigE");
            AvailableCameras.Add("Camera 3 - Virtual");
            SystemLog.Add($"{DateTime.Now:s}: Refreshed camera list ({AvailableCameras.Count} found).");
        }

        [RelayCommand]
        private void Disconnect()
        {
            CameraStatus = "Disconnected";
            SystemLog.Add($"{DateTime.Now:s}: Disconnected from camera.");
        }

        [RelayCommand]
        private void Capture()
        {
            CapturedFrames++;
            LastCaptureTime = DateTime.Now.ToString("s");
            SystemLog.Add($"{DateTime.Now:s}: Captured frame {CapturedFrames}.");
        }

        [RelayCommand]
        private void StartRecording()
        {
            if (!IsRecording)
            {
                IsRecording = true;
                RecordingDuration = 0;
                SystemLog.Add($"{DateTime.Now:s}: Started recording.");
            }
        }

        [RelayCommand]
        private void StopRecording()
        {
            if (IsRecording)
            {
                IsRecording = false;
                SystemLog.Add($"{DateTime.Now:s}: Stopped recording after {RecordingDuration} seconds.");
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            SystemLog.Clear();
        }

        public CameraViewModel()
        {
            // Initialize default values
        }
    }
}
