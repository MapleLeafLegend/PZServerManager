using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PZServerManager;

public static class ServerProfileService
{
    private static readonly string[] ConfigSuffixes =
    {
        ".ini", "_SandboxVars.lua", "_spawnregions.lua", "_spawnpoints.lua"
    };

    public static void CreateBuild42Defaults(string dataDirectory, string profileName)
    {
        ValidateProfileTarget(dataDirectory, profileName);
        var serverDirectory = Path.Combine(dataDirectory, "Server");
        Directory.CreateDirectory(serverDirectory);
        var resetId = RandomNumberGenerator.GetInt32(1, int.MaxValue);
        var ini = $"""
# Project Zomboid Build 42 Stable dedicated-server defaults.
# Options not listed here are intentionally left to the installed B42 server defaults.
PVP=true
PauseEmpty=true
GlobalChat=true
ChatStreams=s,r,a,w,y,sh,f,all
Open=true
AutoCreateUserInWhiteList=false
DisplayUserName=true
ShowFirstAndLastName=false
SpawnPoint=0,0,0
SafetySystem=true
ShowSafety=true
DefaultPort=16261
UDPPort=16262
ResetID={resetId}
DoLuaChecksum=true
Public=false
PublicName=
PublicDescription=
MaxPlayers=32
PingLimit=400
HoursForLootRespawn=0
SaveWorldEveryMinutes=0
BackupsCount=5
BackupsOnStart=true
BackupsOnVersionChange=true
BackupsPeriod=0
Map=Muldraugh, KY
Mods=
WorkshopItems=
Password=
RCONPort=27015
RCONPassword=
PlayerSafehouse=true
AdminSafehouse=true
SleepAllowed=false
SleepNeeded=false
SteamVAC=true
UPnP=true
VoiceEnable=true
VoiceMinDistance=10.0
VoiceMaxDistance=100.0
Voice3D=true
DenyLoginOnOverloadedServer=true
LoginQueueEnabled=false
LoginQueueConnectTimeout=60
""";
        var sandbox = """
SandboxVars = {
    VERSION = 6,
    DayLength = 3,
    StartMonth = 7,
    StartDay = 9,
    StartTime = 2,
    WaterShutModifier = 14,
    ElecShutModifier = 14,
    FoodLootNew = 0.6,
    CannedFoodLootNew = 0.6,
    LiteratureLootNew = 0.6,
    SurvivalGearsLootNew = 0.6,
    MedicalLootNew = 0.6,
    WeaponLootNew = 0.6,
    RangedWeaponLootNew = 0.6,
    AmmoLootNew = 0.6,
    MechanicsLootNew = 0.6,
    OtherLootNew = 0.6,
    HoursForLootRespawn = 0,
    MaxItemsForLootRespawn = 5,
    ConstructionPreventsLootRespawn = true,
    CharacterFreePoints = 0,
    StarterKit = false,
    StatsDecrease = 3,
    EndRegen = 3,
    Nutrition = true,
    InjurySeverity = 2,
    BoneFracture = true,
    ClothingDegradation = 3,
    MultiHitZombies = false,
    RearVulnerability = 3,
    BloodLevel = 3,
    PlayerDamageFromCrash = true,
    MultiplierConfig = {
        GlobalToggle = true,
        Global = 1.0,
    },
    ZombieLore = {
        Speed = 4,
        Strength = 2,
        Toughness = 2,
        Transmission = 1,
    },
    ZombieConfig = {
        PopulationMultiplier = 1.0,
        PopulationStartMultiplier = 1.0,
        PopulationPeakMultiplier = 1.5,
        PopulationPeakDay = 28,
        RespawnHours = 72.0,
        RespawnUnseenHours = 16.0,
        RespawnMultiplier = 0.1,
        RedistributeHours = 12.0,
    },
}
""";
        var iniPath = Path.Combine(serverDirectory, profileName + ".ini");
        var sandboxPath = Path.Combine(serverDirectory, profileName + "_SandboxVars.lua");
        try
        {
            ConfigFileEncoding.WritePreservingEncoding(iniPath, NormalizeNewlines(ini), "Utf8");
            ConfigFileEncoding.WritePreservingEncoding(sandboxPath, NormalizeNewlines(sandbox), "Utf8");
        }
        catch
        {
            try { if (File.Exists(iniPath)) File.Delete(iniPath); } catch { }
            try { if (File.Exists(sandboxPath)) File.Delete(sandboxPath); } catch { }
            throw;
        }
    }

    public static IReadOnlyList<string> CopyAndRename(string dataDirectory,
        string sourceName, string targetName, bool clearSecrets, bool adjustPorts)
    {
        ValidateProfileName(sourceName);
        ValidateProfileTarget(dataDirectory, targetName);
        var serverDirectory = Path.Combine(dataDirectory, "Server");
        var copied = new List<string>();
        try
        {
            foreach (var suffix in ConfigSuffixes)
            {
                var source = Path.Combine(serverDirectory, sourceName + suffix);
                if (!File.Exists(source)) continue;
                var target = Path.Combine(serverDirectory, targetName + suffix);
                if (suffix.Equals(".ini", StringComparison.OrdinalIgnoreCase))
                {
                    var loaded = ConfigFileEncoding.Read(source, "Auto");
                    var text = ReplaceIni(loaded.Text, "ResetID",
                        RandomNumberGenerator.GetInt32(1, int.MaxValue).ToString(CultureInfo.InvariantCulture));
                    if (clearSecrets)
                    {
                        text = ReplaceIni(text, "Password", "");
                        text = ReplaceIni(text, "RCONPassword", "");
                    }
                    if (adjustPorts)
                        text = AssignUnusedPorts(serverDirectory, text);
                    File.Copy(source, target, false);
                    copied.Add(target);
                    ConfigFileEncoding.WritePreservingEncoding(target, text, "Auto");
                    try
                    {
                        var backup = target + ".manager-backup";
                        if (File.Exists(backup)) File.Delete(backup);
                    }
                    catch { }
                }
                else
                {
                    File.Copy(source, target, false);
                    copied.Add(target);
                }
            }
        }
        catch
        {
            foreach (var path in copied)
                try { if (File.Exists(path)) File.Delete(path); } catch { }
            throw;
        }
        if (copied.Count == 0)
            throw new FileNotFoundException("找不到來源設定檔。", sourceName);
        return copied;
    }

    public static void ValidateProfileTarget(string dataDirectory, string profileName)
    {
        ValidateProfileName(profileName);
        var serverDirectory = Path.Combine(dataDirectory, "Server");
        foreach (var suffix in ConfigSuffixes)
            if (File.Exists(Path.Combine(serverDirectory, profileName + suffix)))
                throw new IOException($"設定檔名稱「{profileName}」已存在，管理器不會覆蓋。");
    }

    public static void ValidateProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) ||
            profileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            profileName.Contains(Path.DirectorySeparatorChar) ||
            profileName.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("設定檔名稱不可空白，且不得包含路徑或 Windows 保留字元。");
    }

    private static string ReplaceIni(string text, string key, string value)
    {
        var pattern = $@"(?im)^(\s*{Regex.Escape(key)}\s*=)[^\r\n]*";
        if (Regex.IsMatch(text, pattern)) return Regex.Replace(text, pattern, $"${{1}}{value}",
            RegexOptions.None, TimeSpan.FromSeconds(1));
        return text.TrimEnd() + $"\r\n{key}={value}\r\n";
    }

    private static string AssignUnusedPorts(string serverDirectory, string text)
    {
        var used = new HashSet<int>();
        foreach (var path in Directory.EnumerateFiles(serverDirectory, "*.ini"))
        {
            try
            {
                var other = ConfigFileEncoding.ReadText(path, "Auto");
                foreach (var key in new[] { "DefaultPort", "UDPPort", "RCONPort" })
                    if (TryReadIniInt(other, key, out var port)) used.Add(port);
            }
            catch { }
        }
        var main = TryReadIniInt(text, "DefaultPort", out var configuredMain)
            ? configuredMain : 16261;
        var udp = TryReadIniInt(text, "UDPPort", out var configuredUdp)
            ? configuredUdp : main + 1;
        while (main <= 65534 && (used.Contains(main) || used.Contains(udp)))
        {
            main += 2;
            udp = main + 1;
        }
        if (main > 65534) throw new IOException("找不到可用的主要／直接連線連接埠組合。");
        var rcon = TryReadIniInt(text, "RCONPort", out var configuredRcon)
            ? configuredRcon : 27015;
        while (rcon <= 65535 && (used.Contains(rcon) || rcon == main || rcon == udp)) rcon++;
        if (rcon > 65535) throw new IOException("找不到可用的 RCON 連接埠。");
        text = ReplaceIni(text, "DefaultPort", main.ToString(CultureInfo.InvariantCulture));
        text = ReplaceIni(text, "UDPPort", udp.ToString(CultureInfo.InvariantCulture));
        return ReplaceIni(text, "RCONPort", rcon.ToString(CultureInfo.InvariantCulture));
    }

    private static bool TryReadIniInt(string text, string key, out int value)
    {
        value = 0;
        var match = Regex.Match(text, $@"(?im)^\s*{Regex.Escape(key)}\s*=\s*(\d+)\s*$");
        return match.Success && int.TryParse(match.Groups[1].Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeNewlines(string text) =>
        text.Replace("\r\n", "\n").Replace("\n", "\r\n").TrimStart() + "\r\n";
}
