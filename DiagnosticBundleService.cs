using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PZServerManager;

public static class DiagnosticBundleService
{
    public static string Preview(ServerSettings settings) =>
        "將包含：管理器版本、Windows/.NET 版本、已遮蔽的 Manager 設定，以及最近的 GUI Log。\n" +
        "不包含：密碼、Token、SteamID、IP/MAC、Windows 使用者名稱、Workshop/Mod 清單、世界或玩家資料。";

    public static string Export(ServerSettings settings, string managerVersion)
    {
        var downloads = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var baseTarget = Path.Combine(downloads,
            $"PZServerManager-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var target = baseTarget;
        for (var index = 1; File.Exists(target); index++)
            target = Path.Combine(downloads,
                $"{Path.GetFileNameWithoutExtension(baseTarget)}-{index}.zip");
        using var archive = ZipFile.Open(target, ZipArchiveMode.Create);
        WriteEntry(archive, "summary.txt", Sanitize(
            $"Manager={managerVersion}\nOS={Environment.OSVersion.Version}\n" +
            $"Runtime={Environment.Version}\n64Bit={Environment.Is64BitProcess}\n"));

        var safe = JsonSerializer.Deserialize<ServerSettings>(JsonSerializer.Serialize(settings))
                   ?? new ServerSettings();
        safe.Password = safe.AdminPassword = safe.RconPassword = "<redacted>";
        safe.WorkshopItems = safe.Mods = safe.MapFolders = "<excluded>";
        safe.ServerName = safe.PublicName = safe.Description = safe.WelcomeMessage = "<redacted>";
        safe.RestartWarningMessage = safe.WorkshopUpdateWarningMessage = "<redacted>";
        safe.SteamCmdPath = RedactPath(safe.SteamCmdPath);
        safe.InstallDirectory = RedactPath(safe.InstallDirectory);
        safe.DataDirectory = RedactPath(safe.DataDirectory);
        WriteEntry(archive, "manager-settings-redacted.json",
            JsonSerializer.Serialize(safe, new JsonSerializerOptions { WriteIndented = true }));

        foreach (var path in ManagerLogService.RecentFiles(3))
        {
            try { WriteEntry(archive, "Logs/" + Path.GetFileName(path), Sanitize(File.ReadAllText(path))); }
            catch { }
        }
        return target;
    }

    public static string Sanitize(string value)
    {
        var text = ManagerLogService.Sanitize(value);
        text = Regex.Replace(text, @"\b7656119\d{10}\b", "<steam-id>");
        text = Regex.Replace(text, @"\b(?:\d{1,3}\.){3}\d{1,3}\b", "<ip>");
        text = Regex.Replace(text, @"(?i)\b(?:[0-9A-F]{2}[:-]){5}[0-9A-F]{2}\b", "<mac>");
        text = Regex.Replace(text, @"(?i)C:\\Users\\[^\\\r\n]+", @"C:\Users\<user>");
        text = Regex.Replace(text, @"(?i)(Workshop(?:Items)?\s*[:=]?\s*)\d+(?:[;,]\d+)*",
            "$1<excluded>");
        text = Regex.Replace(text, @"(?i)(Mods\s*[:=]\s*)[^\r\n]+", "$1<excluded>");
        return text;
    }

    private static string RedactPath(string path) => Sanitize(path);

    private static void WriteEntry(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
