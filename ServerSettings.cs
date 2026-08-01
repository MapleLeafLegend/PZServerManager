using System.IO;

namespace PZServerManager;

public sealed class ServerSettings
{
    public string SteamCmdPath { get; set; } = @"C:\steamcmd\steamcmd.exe";
    public string InstallDirectory { get; set; } = @"C:\PZServer";
    public string DataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid");
    public string ServerName { get; set; } = "servertest";
    public string PublicName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Password { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string BetaBranch { get; set; } = "";
    public string ConfigEncoding { get; set; } = "Auto";
    public string SettingsStorage { get; set; } = "ExeDirectory";
    public string UiFontFamily { get; set; } = "NotoSansTC";
    public string UiLanguage { get; set; } = "zh-TW";
    public int DefaultPort { get; set; } = 16261;
    public int UDPPort { get; set; } = 16262;
    public int MaxPlayers { get; set; } = 32;
    public int MemoryGb { get; set; } = 8;
    public bool Public { get; set; }
    public bool PauseEmpty { get; set; } = true;
    public bool Open { get; set; } = true;
    public bool Pvp { get; set; } = true;
    public bool SafetySystem { get; set; } = true;
    public bool SleepAllowed { get; set; }
    public bool SleepNeeded { get; set; }
    public bool VoiceEnable { get; set; } = true;
    public bool PlayerSafehouse { get; set; } = true;
    public bool AllowNonAsciiUsername { get; set; }
    public bool AnnounceDeath { get; set; }
    public int MaxAccountsPerUser { get; set; }
    public int MapRemotePlayerVisibility { get; set; } = 1;
    public bool PlayerRespawnWithSelf { get; set; }
    public bool PlayerRespawnWithOther { get; set; }
    public bool SafehouseAllowRespawn { get; set; }
    public bool Faction { get; set; } = true;
    public int FactionDaySurvivedToCreate { get; set; }
    public int SafehouseDaySurvivedToClaim { get; set; }
    public int SafeHouseRemovalTime { get; set; } = 144;
    public double PvpFirearmDamageModifier { get; set; } = 50.0;
    public double PvpMeleeDamageModifier { get; set; } = 30.0;
    public double SpeedLimit { get; set; } = 70.0;
    public bool DenyLoginOnOverloadedServer { get; set; } = true;
    public bool LoginQueueEnabled { get; set; }
    public int LoginQueueConnectTimeout { get; set; } = 60;
    public int PingLimit { get; set; } = 400;
    public int SaveEveryMinutes { get; set; }
    public int BuiltInBackups { get; set; } = 5;
    public int LootRespawnHours { get; set; }
    public int CharacterFreePoints { get; set; }
    public string SpawnItems { get; set; } = "";
    public bool StarterKit { get; set; }
    public int StatsDecrease { get; set; } = 3;
    public int EndRegen { get; set; } = 3;
    public bool Nutrition { get; set; } = true;
    public int InjurySeverity { get; set; } = 2;
    public bool BoneFracture { get; set; } = true;
    public int ClothingDegradation { get; set; } = 3;
    public bool MultiHitZombies { get; set; }
    public int RearVulnerability { get; set; } = 3;
    public int BloodLevel { get; set; } = 3;
    public bool PlayerDamageFromCrash { get; set; } = true;
    public string WelcomeMessage { get; set; } = "";
    public string WorkshopItems { get; set; } = "";
    public string Mods { get; set; } = "";
    public string MapFolders { get; set; } = "Muldraugh, KY";
    public int RconPort { get; set; } = 27015;
    public string RconPassword { get; set; } = "";
    public int DayLength { get; set; } = 3;
    public int WaterShutDays { get; set; } = 14;
    public int ElectricityShutDays { get; set; } = 14;
    public double XpMultiplier { get; set; } = 1.0;
    public double FoodLoot { get; set; } = 0.6;
    public double WeaponLoot { get; set; } = 0.6;
    public double AmmoLoot { get; set; } = 0.6;
    public double MedicalLoot { get; set; } = 0.6;
    public double OtherLoot { get; set; } = 0.6;
    public int ZombieSpeed { get; set; } = 4;
    public int ZombieStrength { get; set; } = 2;
    public int ZombieToughness { get; set; } = 2;
    public int Transmission { get; set; } = 1;
    public double PopulationMultiplier { get; set; } = 1.0;
    public double PopulationPeakMultiplier { get; set; } = 1.5;
    public int PopulationPeakDay { get; set; } = 28;
    public double RespawnHours { get; set; } = 72;
    public bool AutoRestart { get; set; }
    public int RestartHours { get; set; } = 6;
    public int WarningMinutes { get; set; } = 5;
    public int PlayerQueryMinutes { get; set; } = 30;
    public bool BackupBeforeRestart { get; set; } = true;
    public string RestartWarningMessage { get; set; } =
        "伺服器將在 {minutes} 分鐘後安全重啟，請移動到安全地點。";
    public bool AutoWorkshopUpdate { get; set; } = true;
    public int WorkshopUpdateCheckMinutes { get; set; } = 5;
    public bool WorkshopUpdateBroadcast { get; set; } = true;
    public int WorkshopUpdateAnnouncementMinutes { get; set; } = 30;
    public string WorkshopUpdateWarningMessage { get; set; } =
        "偵測到模組更新；伺服器將在所有玩家離線後自動重啟更新。";
}
