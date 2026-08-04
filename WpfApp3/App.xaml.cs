using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp3
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly string _crashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WpfApp3",
            "crash.log");

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnDispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrashLog(e.Exception);
            MessageBox.Show(
                $"Ứng dụng không thể tiếp tục:\n\n{e.Exception.Message}\n\n" +
                $"Chi tiết đã được lưu tại:\n{_crashLogPath}",
                "WpfApp3 - Application Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
            Shutdown(-1);
        }

        private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception exception)
                WriteCrashLog(exception);
        }

        private void WriteCrashLog(Exception exception)
        {
            try
            {
                string? folder = Path.GetDirectoryName(_crashLogPath);
                if (!string.IsNullOrWhiteSpace(folder))
                    Directory.CreateDirectory(folder);

                File.AppendAllText(
                    _crashLogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{exception}\n\n");
            }
            catch
            {
                // Do not hide the original application error.
            }
        }
    }

}
