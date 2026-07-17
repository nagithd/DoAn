using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    /// <summary>
    /// Interface for webcam camera service.
    /// Abstracts camera hardware communication for laptop/USB cameras.
    /// Can be replaced with IIndustrialCameraService for GigE cameras.
    /// </summary>
    public interface IWebcamCameraService : IDisposable
    {
        /// <summary>
        /// Starts the specified camera and begins frame capture.
        /// </summary>
        void StartCamera(int cameraIndex = 0);

        /// <summary>
        /// Stops the camera and halts frame capture.
        /// </summary>
        void StopCamera();

        /// <summary>
        /// Gets the current camera frame as a BitmapImage.
        /// Returns null if no frame is available.
        /// </summary>
        BitmapImage? GetCurrentFrame();

        /// <summary>
        /// Gets the current raw Mat frame in BGR format.
        /// Returns null if no frame is available.
        /// Caller is responsible for disposing the returned Mat.
        /// </summary>
        Mat? GetCurrentMatFrame();

        /// <summary>
        /// Captures and saves the current frame to disk.
        /// Returns the file path if successful, null otherwise.
        /// </summary>
        string? CaptureFrame();

        /// <summary>
        /// Indicates whether the camera is currently running.
        /// </summary>
        bool IsRunning { get; }
    }

    /// <summary>
    /// Legacy interface for camera service.
    /// Kept for backward compatibility.
    /// </summary>
    public interface ICameraService
    {
        // Placeholder for Camera Service interface
    }
}
