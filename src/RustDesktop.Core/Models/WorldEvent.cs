using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RustDesktop.Core.Models;

public class WorldEvent : INotifyPropertyChanged
{
    private string _eventType = string.Empty;
    private float _x;
    private float _y;
    private DateTime _spawnTime;
    private string _gridCoordinate = string.Empty;

    public string EventType
    {
        get => _eventType;
        set { _eventType = value; OnPropertyChanged(); }
    }

    public float X
    {
        get => _x;
        set { _x = value; OnPropertyChanged(); }
    }

    public float Y
    {
        get => _y;
        set { _y = value; OnPropertyChanged(); }
    }

    public DateTime SpawnTime
    {
        get => _spawnTime;
        set { _spawnTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeAgo)); }
    }

    public string GridCoordinate
    {
        get => _gridCoordinate;
        set { _gridCoordinate = value; OnPropertyChanged(); }
    }

    public string TimeAgo
    {
        get
        {
            var elapsed = DateTime.Now - SpawnTime;
            if (elapsed.TotalMinutes < 1)
                return $"{(int)elapsed.TotalSeconds}s ago";
            if (elapsed.TotalHours < 1)
                return $"{(int)elapsed.TotalMinutes}m ago";
            return $"{(int)elapsed.TotalHours}h ago";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


