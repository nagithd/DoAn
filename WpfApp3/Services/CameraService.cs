using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    /// <summary>
    /// Common interface for the active camera backend.
    /// The production AI camera uses the MVS industrial camera service.
    /// </summary>
    public interface ICameraService : IDisposable
    {
        /// <summary>
        /// Starts the specified camera and begins frame capture.
        /// </summary>
        bool StartCamera(CameraDevice camera);

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
        /// Gets a resized copy for live display without cloning the full
        /// source frame into the UI layer. The source frame remains unchanged
        /// for Capture/AI.
        /// </summary>
        Mat? GetPreviewMatFrame(int maximumWidth);

        /// <summary>
        /// Captures and saves the current frame to disk.
        /// Returns the file path if successful, null otherwise.
        /// </summary>
        string? CaptureFrame();

        /// <summary>
        /// Indicates whether the camera is currently running.
        /// </summary>
        bool IsRunning { get; }

        int FrameWidth { get; }

        int FrameHeight { get; }

        double FramesPerSecond { get; }

        /// <summary>
        /// Monotonically increasing identifier for frames delivered by the
        /// camera backend. Consumers use it to avoid analysing the same frame
        /// more than once when the UI refresh rate is higher than camera FPS.
        /// </summary>
        long FrameSequence { get; }
    }

}
