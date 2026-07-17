using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using WpfApp3.Helpers;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels
{
    /// <summary>
    /// ViewModel for Camera control and display
    /// Responsible for managing camera state and interactions
    /// </summary>
    public partial class CameraViewModel : ObservableObject
    {
        private readonly IWebcamCameraService _cameraService;

        [ObservableProperty]
        private string cameraStatus = "Disconnected";

        [ObservableProperty]
        private string cameraResolution = "N/A";

        [ObservableProperty]
        private double cameraFrameRate = 0.0;

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

        private string? _selectedCameraName;
        public string? SelectedCameraName
        {
            get => _selectedCameraName;
            set
            {
                if (SetProperty(ref _selectedCameraName, value))
                {
                    // Update SelectedCamera when the name changes
                    if (!string.IsNullOrEmpty(value) && _cameraLookup.TryGetValue(value, out var camera))
                    {
                        SelectedCamera = camera;
                    }
                }
            }
        }

        private CameraDevice? _selectedCamera;
        public CameraDevice? SelectedCamera
        {
            get => _selectedCamera;
            set => SetProperty(ref _selectedCamera, value);
        }

        [ObservableProperty]
        private BitmapSource? cameraFrame;

        [ObservableProperty]
        private ObservableCollection<string> systemLog = new ObservableCollection<string>();

        private Dictionary<string, CameraDevice> _cameraLookup = new();

        public CameraViewModel()
        {
            // Initialize camera service
            _cameraService = new WebcamCameraService(targetFps: 30);

            // Subscribe to frame updates
            StartFrameRefresh();
        }

        /// <summary>
        /// Start a background task to pull frames from the service and convert them on UI thread
        /// </summary>
        private void StartFrameRefresh()
        {
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(33); // ~30 fps
            timer.Tick += (s, e) =>
            {
                try
                {
                    // Get the raw Mat frame from camera service
                    var matFrame = _cameraService.GetCurrentMatFrame();
                    if (matFrame == null || matFrame.Empty())
                        return;

                    // Convert Mat to WriteableBitmap on UI thread (DispatcherTimer runs on UI thread)
                    var bitmap = WpfBitmapHelper.MatToWriteableBitmap(matFrame);
                    if (bitmap != null)
                    {
                        CameraFrame = bitmap;
                    }

                    // Dispose the Mat after conversion
                    matFrame?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in frame refresh: {ex.Message}");
                }
            };
            timer.Start();
        }

        [RelayCommand]
        private async void Connect()
        {
            if (SelectedCamera == null)
            {
                LogMessage("No camera selected");
                return;
            }

            try
            {
                _cameraService.StartCamera(SelectedCamera.Index);
                CameraStatus = "Connected";
                LogMessage($"Connected to {SelectedCamera.Name}");
            }
            catch (Exception ex)
            {
                CameraStatus = "Error";
                LogMessage($"Error connecting to camera: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void Disconnect()
        {
            try
            {
                _cameraService.StopCamera();
                CameraStatus = "Disconnected";
                CameraFrame = null;
                LogMessage("Disconnected from camera");
            }
            catch (Exception ex)
            {
                LogMessage($"Error disconnecting: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void Refresh()
        {
            try
            {
                AvailableCameras.Clear();
                _cameraLookup.Clear();
                var cameras = WebcamCameraService.EnumerateCameras();

                if (cameras.Count == 0)
                {
                    LogMessage("No cameras detected");
                    SelectedCamera = null;
                    SelectedCameraName = null;
                    return;
                }

                foreach (var camera in cameras)
                {
                    AvailableCameras.Add(camera.Name);
                    _cameraLookup[camera.Name] = camera;
                }

                // Select first camera by default
                if (AvailableCameras.Count > 0)
                {
                    SelectedCameraName = AvailableCameras[0];
                }
                LogMessage($"Found {cameras.Count} camera(s)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error refreshing cameras: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void Capture()
        {
            if (!_cameraService.IsRunning)
            {
                LogMessage("Camera is not running");
                return;
            }

            try
            {
                string? filepath = _cameraService.CaptureFrame();
                if (filepath != null)
                {
                    CapturedFrames++;
                    LastCaptureTime = DateTime.Now.ToString("s");
                    LogMessage($"Frame captured: {filepath}");
                }
                else
                {
                    LogMessage("Failed to capture frame");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error capturing frame: {ex.Message}");
                Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private void StartRecording()
        {
            if (!IsRecording)
            {
                IsRecording = true;
                RecordingDuration = 0;
                LogMessage("Started recording");
            }
        }

        [RelayCommand]
        private void StopRecording()
        {
            if (IsRecording)
            {
                IsRecording = false;
                LogMessage($"Stopped recording after {RecordingDuration} seconds");
            }
        }

        [RelayCommand]
        private void ClearLog()
        {
            SystemLog.Clear();
        }

        private void LogMessage(string message)
        {
            SystemLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
        }

        ~CameraViewModel()
        {
            _cameraService?.StopCamera();
            _cameraService?.Dispose();
        }
    }
}
