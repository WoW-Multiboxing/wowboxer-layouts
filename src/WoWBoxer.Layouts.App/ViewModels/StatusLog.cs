using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.ViewModels;

public sealed record StatusLine(DateTime Time, StatusLevel Level, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");

    public Brush Brush => Level switch
    {
        StatusLevel.Ok => Brushes.LightGreen,
        StatusLevel.Warn => new SolidColorBrush(Color.FromRgb(0xF5, 0xC8, 0x6A)),
        StatusLevel.Error => new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x8A)),
        StatusLevel.Detail => Brushes.Gray,
        StatusLevel.Title => Brushes.White,
        _ => new SolidColorBrush(Color.FromRgb(0xC8, 0xCE, 0xDA)),
    };
}

/// <summary>Status lines for the UI (thread-safe) and a local log file. Nothing leaves the PC.</summary>
public sealed class StatusLog : IStatusSink
{
    private const int MaxLines = 400;
    private readonly string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private readonly object _fileLock = new();

    public ObservableCollection<StatusLine> Lines { get; } = [];

    public static string LogFolder => Demo.On ? Path.Combine(Demo.Root, "logs") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoWBoxer", "Layouts", "logs");
    public static string LogFile => Path.Combine(LogFolder, "app.log");

    public void Report(StatusLevel level, string message)
    {
        message = message.Replace(_userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        var line = new StatusLine(Demo.Now(), level, message);
        WriteFile(line);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (dispatcher.CheckAccess()) Add(line);
        else dispatcher.BeginInvoke(() => Add(line));
    }

    private void Add(StatusLine line)
    {
        Lines.Add(line);
        while (Lines.Count > MaxLines) Lines.RemoveAt(0);
    }

    private void WriteFile(StatusLine line)
    {
        try
        {
            lock (_fileLock)
            {
                Directory.CreateDirectory(LogFolder);
                File.AppendAllText(LogFile, $"{line.Time:yyyy-MM-dd HH:mm:ss} {line.Level,-6} {line.Message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the log is best effort
        }
    }
}
