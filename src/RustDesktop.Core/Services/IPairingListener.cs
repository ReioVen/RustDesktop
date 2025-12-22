using RustDesktop.Core.Models;

namespace RustDesktop.Core.Services;

public interface IPairingListener
{
    event EventHandler<PairingPayload>? Paired;
    event EventHandler? Listening;
    event EventHandler? Stopped;
    event EventHandler<string>? Failed;
    event EventHandler<AlarmNotification>? AlarmReceived;
    event EventHandler<TeamChatMessage>? ChatReceived;
    
    bool IsRunning { get; }
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
}

public class AlarmNotification
{
    public DateTime Timestamp { get; set; }
    public string Server { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public uint? EntityId { get; set; }
    public string Message { get; set; } = "";

    public AlarmNotification(DateTime timestamp, string server, string deviceName, uint? entityId, string message)
    {
        Timestamp = timestamp;
        Server = server;
        DeviceName = deviceName;
        EntityId = entityId;
        Message = message;
    }
}

public class TeamChatMessage
{
    public DateTime Timestamp { get; set; }
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";

    public TeamChatMessage(DateTime timestamp, string author, string text)
    {
        Timestamp = timestamp;
        Author = author;
        Text = text;
    }
}







