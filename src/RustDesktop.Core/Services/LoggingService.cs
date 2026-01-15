using System.Collections.Concurrent;

namespace RustDesktop.Core.Services;

public class LoggingService : ILoggingService
{
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly object _lock = new();
    private readonly string _logFilePath;
    
    public event EventHandler<string>? LogAdded;

    public LoggingService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RustDesktop"
        );
        Directory.CreateDirectory(appDataPath);
        _logFilePath = Path.Combine(appDataPath, "rustdesktop.log");
    }

    public void LogInfo(string message)
    {
        Log("INFO", message);
    }

    public void LogWarning(string message)
    {
        Log("WARN", message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        var fullMessage = exception != null 
            ? $"{message}\nException: {exception.Message}\nStack: {exception.StackTrace}" 
            : message;
        Log("ERROR", fullMessage);
    }

    public void LogDebug(string message)
    {
        Log("DEBUG", message);
    }

    private void Log(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] {message}";
        
        _logs.Enqueue(logEntry);
        
        // Keep only last 1000 logs in memory to allow copying more at a time
        while (_logs.Count > 1000)
        {
            _logs.TryDequeue(out _);
        }

        // Write to file
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore file write errors
        }

        // Also output to console for debugging
        Console.WriteLine(logEntry);
        
        // Notify subscribers
        LogAdded?.Invoke(this, logEntry);
    }

    public List<string> GetRecentLogs(int count = 100)
    {
        return _logs.TakeLast(count).ToList();
    }
}

