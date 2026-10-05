namespace GamepadPilot.App.Platform;

public sealed class FileLogger
{
    private readonly object _gate = new();
    private readonly string _logFile;

    public FileLogger()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GamepadPilot",
            "logs");
        Directory.CreateDirectory(directory);
        _logFile = Path.Combine(directory, "gamepadpilot.log");
        TrimIfNeeded();
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_gate)
            {
                TrimIfNeeded();
                File.AppendAllText(_logFile, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }

    private void TrimIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFile) || new FileInfo(_logFile).Length < 2 * 1024 * 1024)
            {
                return;
            }

            var oldFile = _logFile + ".old";
            File.Move(_logFile, oldFile, true);
        }
        catch
        {
        }
    }
}
