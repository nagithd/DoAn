using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using WpfApp3.Helpers;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    /// <summary>
    /// Implementation of webcam camera service using OpenCvSharp.
    /// Captures frames from laptop/USB camera and converts them to WPF BitmapImage.
    /// Runs frame capture on a background thread to avoid blocking the UI.
    /// </summary>
    public class WebcamCameraService : IWebcamCameraService
    {
        private VideoCapture? _capture;
        private Thread? _captureThread;
        private bool _isRunning;
        private bool _shouldStop;
        private BitmapImage? _currentFrame;
        private Mat? _currentMat;
        private readonly object _frameLock = new object();
        private int _selectedCameraIndex;
        private readonly int _targetFps;
        private string _captureFolder;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Creates a new instance of WebcamCameraService.
        /// </summary>
        /// <param name="targetFps">Target frames per second for capture (default 30)</param>
        public WebcamCameraService(int targetFps = 30)
        {
            _selectedCameraIndex = 0;
            _targetFps = targetFps > 0 ? targetFps : 30;
            _isRunning = false;
            _shouldStop = false;
            _captureFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "WpfApp3_CapturedFrames"
            );

            // Create capture folder if it doesn't exist
            if (!Directory.Exists(_captureFolder))
            {
                Directory.CreateDirectory(_captureFolder);
            }
        }

        /// <summary>
        /// Enumerates all available webcam devices on the system.
        /// </summary>
        public static List<CameraDevice> EnumerateCameras()
        {
            var cameras = new List<CameraDevice>();

            // Try to open each camera index (0-9) to detect available cameras
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var cap = new VideoCapture(i);
                    if (cap.IsOpened())
                    {
                        // Get camera properties
                        double width = cap.Get(VideoCaptureProperties.FrameWidth);
                        double height = cap.Get(VideoCaptureProperties.FrameHeight);

                        string cameraName = $"Camera {i}";
                        if (width > 0 && height > 0)
                        {
                            cameraName += $" ({(int)width}x{(int)height})";
                        }

                        cameras.Add(new CameraDevice(i, cameraName));
                        cap.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking camera {i}: {ex.Message}");
                }
            }

            return cameras.Count > 0 ? cameras : new List<CameraDevice>();
        }

        /// <summary>
        /// Starts the specified camera and begins frame capture on a background thread.
        /// </summary>
        public void StartCamera(int cameraIndex = 0)
        {
            if (_isRunning)
                return;

            _selectedCameraIndex = cameraIndex;

            try
            {
                _capture = new VideoCapture(_selectedCameraIndex);

                if (!_capture.IsOpened())
                {
                    _capture?.Dispose();
                    _capture = null;
                    Debug.WriteLine("Failed to open camera");
                    return;
                }

                _shouldStop = false;
                _isRunning = true;

                // Start background thread for frame capture
                _captureThread = new Thread(CaptureFrames)
                {
                    IsBackground = true,
                    Name = "WebcamCaptureThread"
                };
                _captureThread.Start();

                Debug.WriteLine($"Camera {_selectedCameraIndex} started successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting camera: {ex.Message}");
                _isRunning = false;
                _capture?.Dispose();
                _capture = null;
            }
        }

        /// <summary>
        /// Stops the camera and halts frame capture.
        /// </summary>
        public void StopCamera()
        {
            if (!_isRunning)
                return;

            _shouldStop = true;

            // Wait for capture thread to finish
            if (_captureThread != null && _captureThread.IsAlive)
            {
                _captureThread.Join(TimeSpan.FromSeconds(5)); // 5 second timeout
            }

            _isRunning = false;
            _capture?.Dispose();
            _capture = null;

            lock (_frameLock)
            {
                _currentMat?.Dispose();
                _currentMat = null;
                _currentFrame = null;
            }

            Debug.WriteLine($"Camera {_selectedCameraIndex} stopped");
        }

        /// <summary>
        /// Gets the current camera frame as a BitmapImage.
        /// </summary>
        public BitmapImage? GetCurrentFrame()
        {
            lock (_frameLock)
            {
                return _currentFrame;
            }
        }

        /// <summary>
        /// Gets the current raw Mat frame in BGR format.
        /// Caller is responsible for disposing the returned Mat.
        /// </summary>
        public Mat? GetCurrentMatFrame()
        {
            lock (_frameLock)
            {
                return _currentMat?.Clone();
            }
        }

        /// <summary>
        /// Captures and saves the current frame to disk.
        /// </summary>
        public string? CaptureFrame()
        {
            if (_currentMat == null || _currentMat.Empty())
                return null;

            try
            {
                string filename = Path.Combine(
                    _captureFolder,
                    $"capture_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png"
                );

                lock (_frameLock)
                {
                    if (_currentMat != null && !_currentMat.Empty())
                    {
                        Cv2.ImWrite(filename, _currentMat);
                        Debug.WriteLine($"Frame captured: {filename}");
                        return filename;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing frame: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Background thread method for continuous frame capture.
        /// Stores the raw Mat frame for UI thread conversion.
        /// </summary>
        private void CaptureFrames()
        {
            if (_capture == null)
                return;

            Mat frame = new Mat();
            int frameDelayMs = 1000 / _targetFps;
            var stopwatch = new Stopwatch();

            try
            {
                while (!_shouldStop && _isRunning)
                {
                    stopwatch.Restart();

                    // Capture frame from camera
                    _capture.Read(frame);

                    if (frame.Empty())
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    // Store the frame (in BGR format from OpenCV)
                    lock (_frameLock)
                    {
                        _currentMat?.Dispose();
                        _currentMat = frame.Clone();
                    }

                    // Maintain target frame rate
                    long elapsedMs = stopwatch.ElapsedMilliseconds;
                    int remainingDelayMs = (int)(frameDelayMs - elapsedMs);
                    if (remainingDelayMs > 0)
                    {
                        Thread.Sleep(remainingDelayMs);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in capture thread: {ex.Message}");
            }
            finally
            {
                frame?.Dispose();
            }
        }

        /// <summary>
        /// Disposes resources held by this service.
        /// </summary>
        public void Dispose()
        {
            StopCamera();
            _capture?.Dispose();
            _capture = null;
            GC.SuppressFinalize(this);
        }

        ~WebcamCameraService()
        {
            Dispose();
        }
    }
}
