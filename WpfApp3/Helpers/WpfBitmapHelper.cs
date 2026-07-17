using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp3.Helpers
{
    /// <summary>
    /// Helper class for converting OpenCV Mat to WPF BitmapImage/WriteableBitmap.
    /// Optimized for real-time video streaming.
    /// </summary>
    public static class WpfBitmapHelper
    {
        /// <summary>
        /// Converts an OpenCV Mat directly to a WriteableBitmap.
        /// Fast, suitable for real-time video.
        /// Must be called from UI thread or marshaled via Dispatcher.
        /// </summary>
        public static WriteableBitmap MatToWriteableBitmap(Mat mat)
        {
            if (mat.Empty())
                throw new ArgumentException("Mat is empty", nameof(mat));

            int width = mat.Cols;
            int height = mat.Rows;
            int channels = mat.Channels();

            if (channels != 3 && channels != 4)
                throw new NotSupportedException($"Only 3 or 4-channel images are supported. Got {channels} channels.");

            // Determine pixel format
            PixelFormat pixelFormat = channels == 4 ? PixelFormats.Bgra32 : PixelFormats.Bgr24;
            int stride = width * channels;

            // Create WriteableBitmap
            var writeableBitmap = new WriteableBitmap(width, height, 96, 96, pixelFormat, null);

            // Get raw bytes from Mat
            byte[] imageData = new byte[height * stride];
            Marshal.Copy(mat.Data, imageData, 0, imageData.Length);

            // Copy image data to WriteableBitmap
            writeableBitmap.WritePixels(
                new System.Windows.Int32Rect(0, 0, width, height),
                imageData,
                stride,
                0
            );

            // Freeze for cross-thread access
            writeableBitmap.Freeze();

            return writeableBitmap;
        }

        /// <summary>
        /// Converts an OpenCV Mat to byte array for manual WriteableBitmap creation.
        /// Useful when you need more control over bitmap creation.
        /// </summary>
        public static byte[] MatToByteArray(Mat mat, out int width, out int height, out int stride)
        {
            if (mat.Empty())
                throw new ArgumentException("Mat is empty", nameof(mat));

            width = mat.Cols;
            height = mat.Rows;
            int channels = mat.Channels();
            stride = width * channels;

            byte[] imageData = new byte[height * stride];
            Marshal.Copy(mat.Data, imageData, 0, imageData.Length);

            return imageData;
        }
    }
}
