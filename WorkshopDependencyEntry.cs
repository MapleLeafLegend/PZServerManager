namespace PZServerManager;

public sealed class WorkshopDependencyEntry
{
    public bool Include { get; set; }
    public string WorkshopId { get; set; } = "";
    public string Title { get; set; } = "";
    public string RequiredBy { get; set; } = "";
    public string Status { get; set; } = "候選；尚未加入";
}
