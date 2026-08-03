using System.IO;
using System.Text.Json;

namespace PZServerManager;

public sealed class WorkshopCacheItem
{
    public string Title { get; set; } = "";
    public List<string> Requirements { get; set; } = new();
    public DateTime CheckedUtc { get; set; }
}

public sealed class WorkshopMetadataCache
{
    public Dictionary<string, WorkshopCacheItem> Items { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PZServerManager", "workshop-metadata-cache.json");

    public static WorkshopMetadataCache Load(string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            if (!File.Exists(file)) return new WorkshopMetadataCache();
            var cache = JsonSerializer.Deserialize<WorkshopMetadataCache>(File.ReadAllText(file))
                        ?? new WorkshopMetadataCache();
            cache.Items = new Dictionary<string, WorkshopCacheItem>(cache.Items,
                StringComparer.OrdinalIgnoreCase);
            return cache;
        }
        catch { return new WorkshopMetadataCache(); }
    }

    public void Save(string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temporary = file + ".tmp";
            File.WriteAllText(temporary,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, file, true);
        }
        catch
        {
            // Cache failures only make the next scan slower.
        }
    }
}
