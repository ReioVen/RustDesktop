namespace RustDesktop.Core.Models;

public class MapInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public List<MapMarker> Markers { get; set; } = new();
}

public class MapMarker
{
    public string Type { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}










