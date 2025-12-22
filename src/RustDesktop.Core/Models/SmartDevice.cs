namespace RustDesktop.Core.Models;

public class SmartDevice
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Switch, Alarm, etc.
    public string Name { get; set; } = string.Empty;
    public bool IsOn { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}











