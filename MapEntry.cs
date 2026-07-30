namespace PZServerManager;

public sealed class MapEntry
{
    public bool Enabled { get; set; }
    public int Order { get; set; }
    public string MapFolder { get; set; } = "";
    public string ModId { get; set; } = "";
    public string WorkshopId { get; set; } = "";
    public bool SpawnEnabled { get; set; }
    public string SpawnPointsFile { get; set; } = "";
    public string Status { get; set; } = "";
}
