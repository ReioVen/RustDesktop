namespace RustDesktop.Core.Models;

public class TeamInfo
{
    public ulong LeaderSteamId { get; set; }
    public List<TeamMember> Members { get; set; } = new();
}

public class TeamMember
{
    public ulong SteamId { get; set; }
    public string? Name { get; set; }
    public bool Online { get; set; }
    public bool Dead { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
}







