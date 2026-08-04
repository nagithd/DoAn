using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace WpfApp3.Services;

/// <summary>
/// Shared application log used by the camera, robot and vision panels.
/// Entries are marshalled to the WPF dispatcher so background polling
/// tasks can report status safely.
/// </summary>
public static class SystemLogService
{
    private const int MaximumEntries = 500;
    private const long MaximumLogBytes = 5 * 1024 * 1024;
    private static readonly object FileLock = new();

    public static ObservableCollection<string> Entries { get; } = [];
    public static ObservableCollection<string> SessionEntries { get; } = [];
    public static string LogFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "WpfApp3",
        "system.log");

    static SystemLogService()
    {
        try
        {
            if (!File.Exists(LogFilePath))
                return;

            foreach (string line in File
                         .ReadLines(LogFilePath)
                         .TakeLast(MaximumEntries))
            {
                Entries.Add(line);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Cannot load application log: {ex.Message}");
        }
    }

    public static void Add(string source, string message)
    {
        string entry =
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  " +
            $"[{source}] {message}";
        Debug.WriteLine(entry);

        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(
                () => Add(source, message));
            return;
        }

        // SessionEntries intentionally excludes lines restored from disk.
        // It is used by the workflow page to show only the current run.
        SessionEntries.Add(entry);
        while (SessionEntries.Count > MaximumEntries)
            SessionEntries.RemoveAt(0);

        Entries.Add(entry);
        while (Entries.Count > MaximumEntries)
            Entries.RemoveAt(0);

        WriteEntryToDisk(entry);
    }

    public static void Clear()
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(Clear);
            return;
        }

        SessionEntries.Clear();
        Entries.Clear();
        try
        {
            lock (FileLock)
            {
                string? folder =
                    Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrWhiteSpace(folder))
                    Directory.CreateDirectory(folder);
                File.WriteAllText(
                    LogFilePath,
                    string.Empty,
                    Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Cannot clear application log: {ex.Message}");
        }
    }

    private static void WriteEntryToDisk(string entry)
    {
        try
        {
            lock (FileLock)
            {
                string? folder =
                    Path.GetDirectoryName(LogFilePath);
                if (string.IsNullOrWhiteSpace(folder))
                    return;

                Directory.CreateDirectory(folder);
                if (File.Exists(LogFilePath) &&
                    new FileInfo(LogFilePath).Length >
                    MaximumLogBytes)
                {
                    string archivePath = Path.Combine(
                        folder,
                        "system.previous.log");
                    File.Move(
                        LogFilePath,
                        archivePath,
                        true);
                }

                File.AppendAllText(
                    LogFilePath,
                    entry + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Cannot write application log: {ex.Message}");
        }
    }
}
