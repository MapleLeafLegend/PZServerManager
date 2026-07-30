using System.IO;

namespace PZServerManager;

public sealed class ExistingServer
{
    public required string Name { get; init; }
    public required string DataDirectory { get; init; }
    public required string IniPath { get; init; }
    public string SandboxPath { get; init; } = "";
    public string DisplayName => $"{Name}  —  {DataDirectory}" +
        (File.Exists(SandboxPath) ? "" : "（缺少 SandboxVars）");
}

public sealed class ConfigValueRow
{
    public required string Category { get; init; }
    public required string Key { get; init; }
    public required string CurrentValue { get; set; }
    public required string OriginalValue { get; init; }
    public required string DefaultValue { get; init; }
    public required string AllowedRange { get; init; }
    public required string MetadataSource { get; init; }
    public required string Status { get; set; }
    public bool CanReset { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
}
