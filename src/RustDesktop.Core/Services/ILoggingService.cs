namespace RustDesktop.Core.Services;

public interface ILoggingService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? exception = null);
    void LogDebug(string message);
    List<string> GetRecentLogs(int count = 50);
    event EventHandler<string>? LogAdded;
}

