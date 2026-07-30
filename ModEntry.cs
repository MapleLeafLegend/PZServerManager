namespace PZServerManager;

public sealed class ModEntry
{
    public bool Enabled { get; set; }
    public int Order { get; set; }
    public string WorkshopId { get; set; } = "";
    public string WorkshopTitle { get; set; } = "";
    public string ModName { get; set; } = "";
    public string ModId { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Category { get; set; } = "伺服器 MOD";
    public string ClientPolicy { get; set; } = "伺服器必需";
    public string Dependencies { get; set; } = "無";
    public string Ordering { get; set; } = "無";
    public string SelectionPolicy { get; set; } = "可獨立選擇";
    public string Status { get; set; } = "可用";
    public List<string> Requires { get; set; } = new();
    public List<string> LoadBefore { get; set; } = new();
    public List<string> LoadAfter { get; set; } = new();
    public List<string> MapFolders { get; set; } = new();
    public string SourceFile { get; set; } = "";
    public bool HasClientLua { get; set; }
    public bool HasSharedLua { get; set; }
    public bool HasServerLua { get; set; }
    public bool HasGameData { get; set; }
}
