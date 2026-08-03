using System.IO;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace PZServerManager;

public sealed record EnvironmentCheckRow(string Item, string Result, string Status);

public static class EnvironmentInspection
{
    public static IReadOnlyList<EnvironmentCheckRow> Run(ServerSettings settings)
    {
        var steamCmdPath = settings.SteamCmdPath ?? "";
        var installDirectory = settings.InstallDirectory ?? "";
        var dataDirectory = settings.DataDirectory ?? "";
        var serverDirectory = Path.Combine(dataDirectory, "Server");
        var iniPath = Path.Combine(serverDirectory, (settings.ServerName ?? "") + ".ini");
        var sandboxPath = Path.Combine(serverDirectory,
            (settings.ServerName ?? "") + "_SandboxVars.lua");
        var launcherPath = Path.Combine(installDirectory, "StartServer64.bat");
        var rows = new List<EnvironmentCheckRow>
        {
            new("Windows／程序架構",
                $"Windows {Environment.OSVersion.Version}／{(Environment.Is64BitProcess ? "x64" : "x86")}",
                Environment.Is64BitOperatingSystem && Environment.Is64BitProcess ? "正常" : "錯誤"),
            CheckFile("SteamCMD", steamCmdPath),
            CheckDirectory("PZ Server 目錄", installDirectory),
            CheckFile("PZ Server 啟動器", launcherPath),
            CheckWritableDirectory("資料目錄", dataDirectory),
            CheckFile("伺服器 INI", iniPath),
            CheckSandbox(sandboxPath),
            new("設定檔編碼", string.IsNullOrWhiteSpace(settings.ConfigEncoding)
                ? "Auto" : settings.ConfigEncoding, "資訊"),
            CheckPort("主要連接埠", settings.DefaultPort),
            CheckPort("UDP 連接埠", settings.UDPPort),
            CheckDisk(dataDirectory),
            new("記憶體設定", $"{settings.MemoryGb} GB",
                settings.MemoryGb is >= 2 and <= 128 ? "正常" : "警告")
        };
        return rows;
    }

    private static EnvironmentCheckRow CheckFile(string item, string path) =>
        new(item, File.Exists(path) ? path : $"找不到：{path}", File.Exists(path) ? "正常" : "錯誤");

    private static EnvironmentCheckRow CheckDirectory(string item, string path) =>
        new(item, Directory.Exists(path) ? path : $"找不到：{path}", Directory.Exists(path) ? "正常" : "錯誤");

    private static EnvironmentCheckRow CheckWritableDirectory(string item, string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return new(item, $"找不到：{path}", "錯誤");
            var probe = Path.Combine(path, ".pz-manager-write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return new(item, $"可讀寫：{path}", "正常");
        }
        catch (Exception ex) { return new(item, ex.Message, "錯誤"); }
    }

    private static EnvironmentCheckRow CheckSandbox(string path)
    {
        if (!File.Exists(path)) return new("Sandbox 設定", $"找不到：{path}", "錯誤");
        try
        {
            var match = Regex.Match(File.ReadAllText(path), @"\bVERSION\s*=\s*(\d+)",
                RegexOptions.CultureInvariant);
            if (!match.Success) return new("Sandbox 設定", $"{path}／找不到 VERSION", "警告");
            return new("Sandbox 設定", $"{path}／VERSION = {match.Groups[1].Value}",
                match.Groups[1].Value == "6" ? "正常" : "警告");
        }
        catch (Exception ex) { return new("Sandbox 設定", ex.Message, "錯誤"); }
    }

    private static EnvironmentCheckRow CheckPort(string item, int port)
    {
        if (port is < 0 or > 65535) return new(item, port.ToString(), "錯誤");
        try
        {
            var used = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
                .Any(endpoint => endpoint.Port == port) ||
                IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                    .Any(endpoint => endpoint.Port == port);
            return new(item, used ? $"{port} 已被占用" : $"{port} 可用", used ? "警告" : "正常");
        }
        catch { return new(item, $"{port}（無法查詢占用狀態）", "警告"); }
    }

    private static EnvironmentCheckRow CheckDisk(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path))!;
            var drive = new DriveInfo(root);
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            return new("磁碟剩餘空間", $"{freeGb:0.0} GB", freeGb >= 10 ? "正常" : "警告");
        }
        catch (Exception ex) { return new("磁碟剩餘空間", ex.Message, "警告"); }
    }
}
