using System.Reflection;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using PZServerManager;

internal static class Program
{
    private static readonly BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static MainWindow window = null!;
    private static Type windowType = null!;
    private static App application = null!;

    [STAThread]
    private static int Main()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            application = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.InitializeComponent();
            var freshDefaults = new ServerSettings();
            Assert(freshDefaults.PublicName == "", "Manager must not invent a public server name.");
            Assert(freshDefaults.Description == "", "Manager must not invent a server description.");
            Assert(freshDefaults.Password == "", "Manager must not invent a join password.");
            Assert(freshDefaults.WelcomeMessage == "", "Manager must not invent a welcome message.");
            Assert(freshDefaults.AutoWorkshopUpdate &&
                freshDefaults.WorkshopUpdateCheckMinutes == 5,
                "Workshop update checks must default to enabled every five minutes.");
            Assert(freshDefaults.WorkshopUpdateBroadcast &&
                freshDefaults.WorkshopUpdateAnnouncementMinutes == 30,
                "Workshop update broadcasts must default to enabled every thirty minutes.");
            window = new MainWindow();
            windowType = typeof(MainWindow);
            Invoke("BeginSetupActivity", "安裝狀態測試", "正在準備測試…");
            Assert(((System.Windows.FrameworkElement)Control("SetupActivityPanel")).Visibility ==
                System.Windows.Visibility.Visible, "Setup activity panel was not shown.");
            Assert(((TextBlock)Control("SetupActivityTitleText")).Text == "安裝狀態測試",
                "Setup activity title was not updated.");
            Invoke("AppendSetupOutput", "SteamCMD 即時輸出測試", false);
            Assert(((TextBox)Control("SetupLiveOutputBox")).Text.Contains("SteamCMD 即時輸出測試"),
                "Setup live output was not displayed.");
            Invoke("EndSetupActivity", true, "測試完成。");
            Assert(((TextBlock)Control("SetupActivityTitleText")).Text == "作業完成",
                "Setup completion state was not displayed.");
            ((System.Windows.FrameworkElement)Control("SetupActivityPanel")).Visibility =
                System.Windows.Visibility.Collapsed;
            Checked("AutoWorkshopUpdateCheck", false);
            Assert(!((TextBox)Control("WorkshopUpdateCheckMinutesBox")).IsEnabled,
                "The update interval must be disabled when update checks are disabled.");
            Assert(!((CheckBox)Control("WorkshopUpdateBroadcastCheck")).IsEnabled,
                "The broadcast option must be a disabled child of update checks.");
            Checked("AutoWorkshopUpdateCheck", true);
            Checked("WorkshopUpdateBroadcastCheck", false);
            Assert(((TextBox)Control("WorkshopUpdateCheckMinutesBox")).IsEnabled,
                "The update interval must be enabled with update checks.");
            Assert(!((TextBox)Control("WorkshopUpdateAnnouncementMinutesBox")).IsEnabled &&
                !((TextBox)Control("WorkshopUpdateMessageBox")).IsEnabled,
                "Broadcast fields must be disabled when broadcasts are disabled.");
            Checked("WorkshopUpdateBroadcastCheck", true);
            Assert(((TextBox)Control("WorkshopUpdateAnnouncementMinutesBox")).IsEnabled &&
                ((TextBox)Control("WorkshopUpdateMessageBox")).IsEnabled,
                "Broadcast fields must be enabled when both parent and child options are enabled.");
            var smokeRoot = Path.Combine(Directory.GetCurrentDirectory(), "smoke-all");
            Console.WriteLine($"SMOKE_ROOT={smokeRoot}");
            Assert(freshDefaults.CheckForManagerUpdates && freshDefaults.EnableManagerLog &&
                freshDefaults.ManagerLogRetentionDays == 14,
                "Update checks and persistent manager logging must use safe enabled defaults.");
            Assert(freshDefaults.ShowScheduledRestartInWelcome,
                "Scheduled restart information must default to enabled in the welcome message.");
            Assert(GitHubUpdateService.ParseVersion("v2.0.0") == new Version(2, 0, 0),
                "GitHub release version parsing failed.");
            Assert(ManagerLogService.Sanitize("Password=secret token=abc") ==
                "Password=<redacted> token=<redacted>", "Manager log secret scrubbing failed.");

            var profileRoot = Path.Combine(smokeRoot, "profile-tests-" + Guid.NewGuid().ToString("N"));
            ServerProfileService.CreateBuild42Defaults(profileRoot, "cleanb42");
            var cleanIni = File.ReadAllText(Path.Combine(profileRoot, "Server", "cleanb42.ini"));
            var cleanSandbox = File.ReadAllText(Path.Combine(profileRoot, "Server", "cleanb42_SandboxVars.lua"));
            Assert(cleanIni.Contains("PublicName=\r\n") && cleanIni.Contains("Mods=\r\n") &&
                cleanIni.Contains("WorkshopItems=\r\n") && cleanSandbox.Contains("VERSION = 6"),
                "New profile did not contain clean Build 42 defaults.");
            File.WriteAllText(Path.Combine(profileRoot, "Server", "cleanb42.ini"),
                cleanIni.Replace("Password=", "Password=private")
                    .Replace("RCONPassword=", "RCONPassword=private"));
            ServerProfileService.CopyAndRename(profileRoot, "cleanb42", "copiedb42", true, true);
            var copiedIni = File.ReadAllText(Path.Combine(profileRoot, "Server", "copiedb42.ini"));
            Assert(copiedIni.Contains("Password=\r\n") && copiedIni.Contains("RCONPassword=\r\n") &&
                !copiedIni.Contains("DefaultPort=16261\r\n"),
                "Copy-and-rename did not clear secrets or avoid source profile ports.");
            Directory.Delete(profileRoot, true);
            Text("InstallPathBox", Path.Combine(smokeRoot, "install"));
            Text("DataPathBox", Path.Combine(smokeRoot, "data"));
            Text("ServerNameBox", "alltest");
            Tag("ConfigEncodingCombo", "Auto");
            var fixtureIniPath = Path.Combine(smokeRoot, "data", "Server", "alltest.ini");
            var fixtureSandboxPath = Path.Combine(smokeRoot, "data", "Server", "alltest_SandboxVars.lua");
            var fixtureIni = File.ReadAllText(fixtureIniPath, Encoding.UTF8);
            if (!System.Text.RegularExpressions.Regex.IsMatch(fixtureIni, @"(?m)^AntiCheatSafety="))
                fixtureIni += "\r\n# Disables safety system anti-cheat protection.\r\nAntiCheatSafety=2\r\n";
            File.WriteAllText(fixtureIniPath, fixtureIni, new UTF8Encoding(false));
            var fixtureSandbox = File.ReadAllText(fixtureSandboxPath, Encoding.UTF8);
            if (!System.Text.RegularExpressions.Regex.IsMatch(fixtureSandbox, @"(?m)^\s*Temperature\s*="))
                fixtureSandbox = fixtureSandbox.Insert(fixtureSandbox.LastIndexOf('}'),
                    "    -- Default = Normal\r\n    -- 1 = Very Cold\r\n    -- 2 = Cold\r\n" +
                    "    -- 3 = Normal\r\n    -- 4 = Hot\r\n    -- 5 = Very Hot\r\n" +
                    "    Temperature = 3,\r\n    TestHarnessMod = {\r\n" +
                    "        -- Default = true\r\n        Enabled = true,\r\n    },\r\n");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fixtureSandbox, @"(?m)^\s*Memory\s*="))
                fixtureSandbox = fixtureSandbox.Replace("ZombieLore = {",
                    "ZombieLore = {\r\n        -- Default = Normal\r\n        -- 1 = Long\r\n" +
                    "        -- 2 = Normal\r\n        -- 3 = Short\r\n        Memory = 2,", StringComparison.Ordinal);
            if (!System.Text.RegularExpressions.Regex.IsMatch(fixtureSandbox, @"(?m)^\s*Sprinting\s*="))
                fixtureSandbox = fixtureSandbox.Replace("MultiplierConfig = {",
                    "MultiplierConfig = {\r\n        -- Min: 0.00 Max: 1000.00 Default: 1.00\r\n" +
                    "        Sprinting = 1.0,", StringComparison.Ordinal);
            File.WriteAllText(fixtureSandboxPath, fixtureSandbox, new UTF8Encoding(false));
            Assert((bool)Invoke("TryLoadExistingConfig", true)!, "Initial configuration load failed.");
            Assert(((TextBox)Control("DataPathBox")).Text == Path.Combine(smokeRoot, "data"),
                "Reading the current server reverted DataPathBox to a stale Manager path.");

            var featuredIniRows = ((System.Collections.IEnumerable)Field("featuredIniRows").GetValue(window)!)
                .Cast<ConfigValueRow>().ToList();
            var featuredSandboxRows = ((System.Collections.IEnumerable)Field("featuredSandboxRows").GetValue(window)!)
                .Cast<ConfigValueRow>().ToList();
            Assert(featuredIniRows.Any(row => row.Key == "AntiCheatSafety"),
                "Build 42 anti-cheat settings were not exposed in the advanced server GUI.");
            Assert(featuredSandboxRows.Any(row => row.Key == "Temperature") &&
                featuredSandboxRows.Any(row => row.Key == "ZombieLore.Memory") &&
                featuredSandboxRows.Any(row => row.Key == "MultiplierConfig.Sprinting") &&
                featuredSandboxRows.Any(row => row.Key == "TestHarnessMod.Enabled"),
                "Important world, zombie, player, or mod Sandbox settings were not exposed.");
            Assert(featuredSandboxRows.Single(row => row.Key == "Temperature").DisplayName == "氣溫" &&
                featuredSandboxRows.Single(row => row.Key == "ZombieLore.Memory").DisplayName == "殭屍記憶力" &&
                featuredSandboxRows.Single(row => row.Key == "MultiplierConfig.Sprinting").DisplayName == "衝刺經驗倍率",
                "Detailed Build 42 rows were not given Traditional Chinese display names.");
            Assert(featuredSandboxRows.Single(row => row.Key == "Temperature").LocalizedAllowedRange.Contains("寒冷") &&
                !featuredSandboxRows.Single(row => row.Key == "Temperature").LocalizedNotes.Contains("Default", StringComparison.OrdinalIgnoreCase),
                "Detailed Build 42 options or descriptions still exposed raw English in zh-TW mode.");
            Assert(featuredSandboxRows.Single(row => row.Key == "TestHarnessMod.Enabled").LocalizedNotes.Contains("模組作者"),
                "Unknown mod settings must receive a safe Chinese explanation instead of guessed English text.");
            var translationType = typeof(MainWindow).Assembly.GetType("PZServerManager.ConfigSettingLocalization")
                ?? throw new InvalidOperationException("ConfigSettingLocalization type was not found.");
            var translateOptions = translationType.GetMethod("TranslateOptions", Flags)
                ?? throw new MissingMethodException("TranslateOptions");
            var translatedOptions = (string)translateOptions.Invoke(null,
                new object[] { "1=Urban Focused;2=Very Fast (20 Days);3=Navigate and Use Doors;4=Endless Blizzard" })!;
            Assert(translatedOptions.Contains("集中於都市") && translatedOptions.Contains("非常快 (20 天)") &&
                translatedOptions.Contains("會導航並使用門") && translatedOptions.Contains("無盡暴風雪") &&
                !translatedOptions.Contains("Urban", StringComparison.OrdinalIgnoreCase) &&
                !translatedOptions.Contains("Days", StringComparison.OrdinalIgnoreCase),
                "Detailed option translation left common Build 42 English labels visible.");
            featuredIniRows.Single(row => row.Key == "AntiCheatSafety").CurrentValue = "3";
            featuredSandboxRows.Single(row => row.Key == "Temperature").CurrentValue = "4";
            featuredSandboxRows.Single(row => row.Key == "ZombieLore.Memory").CurrentValue = "3";
            featuredSandboxRows.Single(row => row.Key == "MultiplierConfig.Sprinting").CurrentValue = "1.5";
            featuredSandboxRows.Single(row => row.Key == "TestHarnessMod.Enabled").CurrentValue = "false";

            Text("PortBox", "17000");
            Text("UdpPortBox", "17001");
            Text("PlayersBox", "44");
            Text("MemoryBox", "12");
            Text("PublicNameBox", "整合測試伺服器");
            Text("DescriptionBox", "繁體中文寫入驗證");
            Password("PasswordBox", "join-pass");
            Password("AdminPasswordBox", "admin-pass");
            Checked("PublicCheck", true);
            Checked("OpenCheck", false);
            Checked("PauseCheck", false);
            Checked("AutoWorkshopUpdateCheck", true);
            Text("WorkshopUpdateCheckMinutesBox", "7");
            Checked("WorkshopUpdateBroadcastCheck", true);
            Text("WorkshopUpdateAnnouncementMinutesBox", "45");
            Text("WorkshopUpdateMessageBox", "模組已更新；全員離線後將安全重啟。");

            Checked("PvpCheck", false);
            Checked("SafetyCheck", false);
            Checked("SleepAllowedCheck", true);
            Checked("SleepNeededCheck", true);
            Checked("VoiceCheck", false);
            Checked("SafehouseCheck", false);
            Text("WelcomeBox", "<RGB:1,0,0>繁體中文歡迎<LINE>第二行");
            Text("PingLimitBox", "650");
            Text("SaveMinutesBox", "13");
            Text("BuiltInBackupsBox", "9");
            Text("LootRespawnHoursBox", "96");
            Text("RconPortBox", "28015");
            Password("RconPasswordBox", "new-rcon");

            Tag("DayLengthCombo", "4");
            Text("WaterDaysBox", "21");
            Text("ElectricDaysBox", "34");
            Text("XpBox", "2.5");
            Text("CharacterFreePointsBox", "12");
            Text("SpawnItemsBox", "Base.BaseballBat,Base.WaterBottle,Base.Chocolate");
            Tag("StatsDecreaseCombo", "4");
            Tag("EndRegenCombo", "1");
            Checked("NutritionCheck", false);
            Tag("InjurySeverityCombo", "3");
            Checked("BoneFractureCheck", false);
            Tag("ClothingDegradationCombo", "4");
            Checked("MultiHitZombiesCheck", true);
            Tag("RearVulnerabilityCombo", "2");
            Tag("BloodLevelCombo", "5");
            Checked("PlayerDamageFromCrashCheck", false);
            Text("FoodLootBox", "0.75");
            Text("WeaponLootBox", "0.8");
            Text("AmmoLootBox", "0.85");
            Text("MedicalLootBox", "0.9");
            Text("OtherLootBox", "0.95");

            Tag("ZombieSpeedCombo", "3");
            Tag("ZombieStrengthCombo", "3");
            Tag("ZombieToughnessCombo", "1");
            Tag("TransmissionCombo", "2");
            Text("PopulationBox", "1.25");
            Text("PeakPopulationBox", "2.25");
            Text("PeakDayBox", "45");
            Text("ZombieRespawnBox", "120");

            Checked("AllowNonAsciiUsernameCheck", true);
            Checked("AnnounceDeathCheck", true);
            Text("MaxAccountsPerUserBox", "6");
            Tag("MapRemotePlayerVisibilityBox", "3");
            Checked("PlayerRespawnWithSelfCheck", true);
            Checked("PlayerRespawnWithOtherCheck", true);
            Checked("SafehouseAllowRespawnCheck", true);
            Checked("FactionCheck", false);
            Text("FactionDaysBox", "11");
            Text("SafehouseDaysBox", "12");
            Text("SafehouseRemovalHoursBox", "240");
            Text("PvpFirearmDamageBox", "123.5");
            Text("PvpMeleeDamageBox", "234.5");
            Text("SpeedLimitBox", "99.5");
            Checked("DenyOverloadCheck", false);
            Checked("LoginQueueCheck", true);
            Text("LoginQueueTimeoutBox", "75");

            Assert((bool)Invoke("UiToSettings", true)!,
                "Valid UI values were rejected or mod application showed a blocking success dialog.");
            var automationSettings = Field("settings").GetValue(window)!;
            Assert((bool)automationSettings.GetType().GetProperty("AutoWorkshopUpdate")!
                    .GetValue(automationSettings)! &&
                (int)automationSettings.GetType().GetProperty("WorkshopUpdateCheckMinutes")!
                    .GetValue(automationSettings)! == 7,
                "Independent Workshop check settings were not read from the GUI.");
            Assert((bool)automationSettings.GetType().GetProperty("WorkshopUpdateBroadcast")!
                    .GetValue(automationSettings)! &&
                (int)automationSettings.GetType().GetProperty("WorkshopUpdateAnnouncementMinutes")!
                    .GetValue(automationSettings)! == 45,
                "Independent Workshop broadcast settings were not read from the GUI.");
            Field("explicitConfigWriteAuthorized").SetValue(window, true);
            try { Invoke("WriteServerConfig"); }
            finally { Field("explicitConfigWriteAuthorized").SetValue(window, false); }
            Assert((bool)Field("lastConfigWriteSucceeded").GetValue(window)!,
                "WriteServerConfig did not report success.");

            var settings = Field("settings").GetValue(window)!;
            var mismatches = ((System.Collections.IEnumerable)Invoke("CompareManagedPzFiles", settings)!)
                .Cast<object>().Select(x => x.ToString()).ToList();
            Assert(mismatches.Count == 0, "Direct disk verification: " + string.Join(" | ", mismatches));
            Assert((bool)Invoke("TryLoadExistingConfig", true)!, "Reload failed.");

            var expected = new Dictionary<string, string>
            {
                ["DefaultPort"] = "17000", ["UDPPort"] = "17001", ["MaxPlayers"] = "44",
                ["Description"] = "繁體中文寫入驗證", ["PingLimit"] = "650",
                ["SaveEveryMinutes"] = "13", ["BuiltInBackups"] = "9",
                ["LootRespawnHours"] = "96", ["RconPassword"] = "new-rcon",
                ["DayLength"] = "4", ["CharacterFreePoints"] = "12",
                ["SpawnItems"] = "Base.BaseballBat,Base.WaterBottle,Base.Chocolate",
                ["StatsDecrease"] = "4", ["EndRegen"] = "1", ["Nutrition"] = "False",
                ["InjurySeverity"] = "3", ["BoneFracture"] = "False",
                ["ClothingDegradation"] = "4", ["MultiHitZombies"] = "True",
                ["RearVulnerability"] = "2", ["BloodLevel"] = "5",
                ["PlayerDamageFromCrash"] = "False",
                ["SafehouseAllowRespawn"] = "True",
                ["PvpFirearmDamageModifier"] = "123.5", ["PvpMeleeDamageModifier"] = "234.5",
                ["SpeedLimit"] = "99.5", ["LoginQueueConnectTimeout"] = "75"
            };
            var reloaded = Field("settings").GetValue(window)!;
            foreach (var pair in expected)
            {
                var actual = reloaded.GetType().GetProperty(pair.Key)!.GetValue(reloaded)?.ToString();
                Assert(actual == pair.Value,
                    $"Reloaded {pair.Key}: expected {pair.Value}, actual {actual}");
            }

            var iniPath = Path.Combine(smokeRoot, "data", "Server", "alltest.ini");
            var sandboxPath = Path.Combine(smokeRoot, "data", "Server", "alltest_SandboxVars.lua");
            var goodIni = File.ReadAllText(iniPath, Encoding.UTF8);
            var goodSandbox = File.ReadAllText(sandboxPath, Encoding.UTF8);
            Assert(System.Text.RegularExpressions.Regex.IsMatch(goodIni,
                    @"(?m)^AntiCheatSafety=3\r?$") &&
                System.Text.RegularExpressions.Regex.IsMatch(goodSandbox,
                    @"(?m)^\s*Temperature\s*=\s*4,") &&
                System.Text.RegularExpressions.Regex.IsMatch(goodSandbox,
                    @"(?m)^\s*Memory\s*=\s*3,") &&
                System.Text.RegularExpressions.Regex.IsMatch(goodSandbox,
                    @"(?m)^\s*Sprinting\s*=\s*1\.5,") &&
                System.Text.RegularExpressions.Regex.IsMatch(goodSandbox,
                    @"(?m)^\s*Enabled\s*=\s*false,"),
                "Featured INI/Sandbox values were not written to disk.");
            Assert(System.Text.RegularExpressions.Regex.Matches(goodSandbox,
                    @"(?m)^    DayLength\s*=").Count == 1 &&
                System.Text.RegularExpressions.Regex.Matches(goodSandbox,
                    @"(?m)^        Global\s*=").Count == 1,
                "Saving an unchanged Lua value inserted or retained duplicate managed keys.");

            File.AppendAllText(iniPath, Environment.NewLine + "MaxPlayers=3" + Environment.NewLine,
                new UTF8Encoding(false));
            Field("explicitConfigWriteAuthorized").SetValue(window, true);
            try { Invoke("WriteServerConfig"); }
            finally { Field("explicitConfigWriteAuthorized").SetValue(window, false); }
            var duplicateValues = System.Text.RegularExpressions.Regex.Matches(
                    File.ReadAllText(iniPath, Encoding.UTF8), @"(?im)^\s*MaxPlayers\s*=(.*)$")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(match => match.Groups[1].Value.Trim()).ToList();
            Assert(duplicateValues.Count >= 2 && duplicateValues.All(value => value == "44"),
                "Duplicate INI keys were not all synchronized.");
            File.WriteAllText(iniPath, goodIni, new UTF8Encoding(false));

            var missingHours = System.Text.RegularExpressions.Regex.Replace(goodSandbox,
                @"(?m)^\s*HoursForLootRespawn\s*=.*\r?\n", "");
            File.WriteAllText(sandboxPath, missingHours, new UTF8Encoding(false));
            var missingMismatches = ((System.Collections.IEnumerable)
                    Invoke("CompareManagedPzFiles", Field("settings").GetValue(window)!)!)
                .Cast<object>().Select(x => x.ToString()).ToList();
            Assert(missingMismatches.Any(value => value?.Contains("HoursForLootRespawn") == true),
                "A missing Sandbox field was incorrectly accepted as the in-memory value.");
            File.WriteAllText(sandboxPath, goodSandbox, new UTF8Encoding(false));

            Invalid("CharacterFreePointsBox", "101", "12", "CharacterFreePoints=101");
            Invalid("PvpFirearmDamageBox", "501", "123.5", "PVPFirearmDamageModifier=501");
            Invalid("SpeedLimitBox", "9", "99.5", "SpeedLimit=9");
            Invalid("LoginQueueTimeoutBox", "19", "75", "LoginQueueConnectTimeout=19");
            Invalid("SpawnItemsBox", "Base.WaterBottleFull",
                "Base.BaseballBat,Base.WaterBottle,Base.Chocolate", "Base.WaterBottleFull");

            var workshopRoot = Path.Combine(smokeRoot, "install", "steamapps", "workshop",
                "content", "108600");
            var coreRoot = Path.Combine(workshopRoot, "100", "mods", "Core");
            var patchRoot = Path.Combine(workshopRoot, "200", "mods", "Patch");
            var variantARoot = Path.Combine(workshopRoot, "300", "mods", "VariantA");
            var variantBRoot = Path.Combine(workshopRoot, "300", "mods", "VariantB");
            Directory.CreateDirectory(coreRoot);
            Directory.CreateDirectory(patchRoot);
            Directory.CreateDirectory(variantARoot);
            Directory.CreateDirectory(variantBRoot);
            File.WriteAllText(Path.Combine(coreRoot, "mod.info"),
                "name=Core Framework\nid=CoreMod\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(patchRoot, "mod.info"),
                "name=Patch Module\nid=PatchMod\nrequire=\\CoreMod\nloadAfter=\\CoreMod\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(variantARoot, "mod.info"),
                "name=Variant A\nid=VariantA\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(variantBRoot, "mod.info"),
                "name=Variant B Patch\nid=VariantBPatch\n", new UTF8Encoding(false));
            var clientOnlyRoot = Path.Combine(variantARoot, "media", "lua", "client");
            Directory.CreateDirectory(clientOnlyRoot);
            File.WriteAllText(Path.Combine(clientOnlyRoot, "ClientOnly.lua"),
                "return {}\n", new UTF8Encoding(false));
            var mapRoot = Path.Combine(coreRoot, "media", "maps", "Test Map");
            Directory.CreateDirectory(mapRoot);
            File.WriteAllText(Path.Combine(mapRoot, "map.info"), "lots=lots\n",
                new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(mapRoot, "50_50.lotheader"), new byte[] { 0 });
            File.WriteAllText(Path.Combine(mapRoot, "spawnpoints.lua"),
                "function SpawnPoints()\n    return {}\nend\n", new UTF8Encoding(false));
            var inferredRoot = Path.Combine(workshopRoot, "400", "mods", "Inferred", "42");
            Directory.CreateDirectory(inferredRoot);
            File.WriteAllText(Path.Combine(inferredRoot, "mod.info"),
                "name=Inferred Workshop Mod\nid=UnlinkedMod\nversionMin=42.0\n",
                new UTF8Encoding(false));
            var localOnlyRoot = Path.Combine(smokeRoot, "data", "mods", "LocalOnly", "42");
            Directory.CreateDirectory(localOnlyRoot);
            File.WriteAllText(Path.Combine(localOnlyRoot, "mod.info"),
                "name=Local Only Mod\nid=LocalOnlyMod\nversionMin=42.0\n",
                new UTF8Encoding(false));
            var duplicateCoreRoot = Path.Combine(workshopRoot, "500", "mods", "CoreDuplicate", "42");
            Directory.CreateDirectory(duplicateCoreRoot);
            File.WriteAllText(Path.Combine(duplicateCoreRoot, "mod.info"),
                "name=Core Framework Alternate Source\nid=CoreMod\nversionMin=42.0\n",
                new UTF8Encoding(false));
            var cycleARoot = Path.Combine(workshopRoot, "600", "mods", "CycleA", "42");
            var cycleBRoot = Path.Combine(workshopRoot, "600", "mods", "CycleB", "42");
            Directory.CreateDirectory(cycleARoot);
            Directory.CreateDirectory(cycleBRoot);
            File.WriteAllText(Path.Combine(cycleARoot, "mod.info"),
                "name=Cycle A\nid=CycleA\nrequire=CycleB\nversionMin=42.0\n",
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(cycleBRoot, "mod.info"),
                "name=Cycle B\nid=CycleB\nrequire=CycleA\nversionMin=42.0\n",
                new UTF8Encoding(false));

            Text("WorkshopBox", "200;100;300");
            Text("ModsBox", "PatchMod;CoreMod");
            Text("MapFoldersBox", "Muldraugh, KY");
            Invoke("FindInstalledModEntries", new[] { "200", "100", "300" }, false);
            Invoke("SortModEntriesByDependencies");
            var modEntries = ((System.Collections.IEnumerable)
                Field("resolvedModEntries").GetValue(window)!).Cast<object>().ToList();
            string ModId(object entry) => entry.GetType().GetProperty("ModId")!.GetValue(entry)!.ToString()!;
            var coreEntry = modEntries.Single(entry => ModId(entry) == "CoreMod");
            var patchEntry = modEntries.Single(entry => ModId(entry) == "PatchMod");
            Assert(modEntries.IndexOf(coreEntry) < modEntries.IndexOf(patchEntry),
                "Dependency sorting did not place CoreMod before PatchMod.");
            Assert(modEntries.Count(entry => ModId(entry).StartsWith("Variant")) == 2,
                "Multiple Mod IDs from one Workshop item were not exposed separately.");
            Assert(modEntries.Where(entry => ModId(entry).StartsWith("Variant"))
                .All(entry => entry.GetType().GetProperty("Status")!.GetValue(entry)!.ToString()!
                    .Contains("多個 ID")), "Multi-ID Workshop entries were not flagged for review.");
            var variantA = modEntries.Single(entry => ModId(entry) == "VariantA");
            Assert(variantA.GetType().GetProperty("ClientPolicy")!.GetValue(variantA)!.ToString() ==
                "純客戶端候選", "Client-only mod was not classified as a whitelist candidate.");
            coreEntry.GetType().GetProperty("Enabled")!.SetValue(coreEntry, true);
            patchEntry.GetType().GetProperty("Enabled")!.SetValue(patchEntry, true);
            Assert((bool)Invoke("ApplyResolvedMods", false, true)!,
                "Valid dependency selection was rejected.");
            Assert(((TextBox)Control("ModsBox")).Text == "CoreMod;PatchMod",
                "Applied Mods did not preserve dependency order.");
            var mapEntries = ((System.Collections.IEnumerable)
                Field("resolvedMapEntries").GetValue(window)!).Cast<object>().ToList();
            var testMap = mapEntries.Single(entry =>
                entry.GetType().GetProperty("MapFolder")!.GetValue(entry)!.ToString() == "Test Map");
            Assert(!string.IsNullOrWhiteSpace(testMap.GetType().GetProperty("SpawnPointsFile")!
                    .GetValue(testMap)!.ToString()),
                "A map spawnpoints.lua file was not detected.");
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.WindowState = WindowState.Normal;
            window.Width = 1517;
            window.Height = 745;
            window.Left = -20000;
            window.Top = -20000;
            window.Opacity = 0;
            window.Show();
            ((System.Windows.FrameworkElement)Control("MainTabs")).Visibility =
                System.Windows.Visibility.Visible;
            SelectContainingTabs((System.Windows.DependencyObject)Control("AutoWorkshopUpdateCheck"));
            window.UpdateLayout();
            var workshopAutomationControl =
                (System.Windows.FrameworkElement)Control("AutoWorkshopUpdateCheck");
            var workshopAutomationPosition =
                workshopAutomationControl.TransformToAncestor(window)
                    .Transform(new System.Windows.Point(0, 0));
            Assert(workshopAutomationControl.ActualWidth > 0 &&
                workshopAutomationPosition.Y >= 0 &&
                workshopAutomationPosition.Y + workshopAutomationControl.ActualHeight <=
                    window.ActualHeight,
                "Workshop automation controls are outside the default visible window.");
            Assert(((FrameworkElement)Control("CliHealthBanner")).Visibility == Visibility.Collapsed,
                "CLI health banner must be hidden before a failure is detected.");
            var environmentRows = ((DataGrid)Control("EnvironmentCheckGrid")).ItemsSource as
                System.Collections.IEnumerable;
            Assert(environmentRows != null && environmentRows.Cast<object>().Count() >= 10,
                "Diagnostics tab did not populate the environment summary automatically.");
            Assert(environmentRows!.Cast<object>().Any(row =>
                    row.GetType().GetProperty("Item")?.GetValue(row)?.ToString() == "伺服器 INI"),
                "Environment summary is missing the active server INI check.");
            Assert((bool)Invoke("IsPlayerQueryTerminalLine", "Players connected (2):")! &&
                (bool)Invoke("IsPlayerQueryTerminalLine", "No players are connected")!,
                "The CLI watchdog does not recognize official players responses.");

            using (var watchdogProcess = Process.Start(new ProcessStartInfo("cmd.exe",
                       "/d /c ping -n 8 127.0.0.1 >nul")
                   { UseShellExecute = false, CreateNoWindow = true })!)
            {
                Field("serverProcess").SetValue(window, watchdogProcess);
                Invoke("UpdateWorkshopAutomationControlState");
                Assert(((CheckBox)Control("AutoWorkshopUpdateCheck")).IsEnabled &&
                    ((CheckBox)Control("WorkshopUpdateBroadcastCheck")).IsEnabled,
                    "Running server must not lock the emergency automation switches.");
                Assert(!((TextBox)Control("WorkshopUpdateCheckMinutesBox")).IsEnabled,
                    "Running server must keep automation timing fields locked.");
                Invoke("RegisterCliHealthFailure", "watchdog test 1");
                Assert(((FrameworkElement)Control("CliHealthBanner")).Visibility == Visibility.Visible &&
                    !((Button)Control("ForceTerminateFrozenServerButton")).IsEnabled,
                    "First CLI timeout must warn without enabling force termination.");
                Invoke("RegisterCliHealthFailure", "watchdog test 2");
                Assert((bool)Field("cliHealthAlarmActive").GetValue(window)! &&
                    (bool)Field("automationRuntimeSuspended").GetValue(window)! &&
                    ((Button)Control("ForceTerminateFrozenServerButton")).IsEnabled,
                    "Second CLI timeout must suspend automation and expose manual recovery.");
                Invoke("RegisterCliHealthSuccess");
                Assert(!(bool)Field("cliHealthAlarmActive").GetValue(window)! &&
                    !(bool)Field("automationRuntimeSuspended").GetValue(window)! &&
                    ((FrameworkElement)Control("CliHealthBanner")).Visibility == Visibility.Collapsed,
                    "A verified players response must clear the CLI health alarm.");
                Invoke("DisableAllAutomation_Click", new object(), new RoutedEventArgs());
                var runtimeSettings = Field("settings").GetValue(window)!;
                Assert(!((CheckBox)Control("AutoRestartCheck")).IsChecked!.Value &&
                    !((CheckBox)Control("AutoWorkshopUpdateCheck")).IsChecked!.Value &&
                    !(bool)runtimeSettings.GetType().GetProperty("AutoRestart")!.GetValue(runtimeSettings)! &&
                    !(bool)runtimeSettings.GetType().GetProperty("AutoWorkshopUpdate")!.GetValue(runtimeSettings)!,
                    "Emergency stop-all did not disable both automation paths immediately.");
                Checked("AutoRestartCheck", true);
                Checked("AutoWorkshopUpdateCheck", true);
                Checked("WorkshopUpdateBroadcastCheck", true);
                if (!watchdogProcess.HasExited) watchdogProcess.Kill(true);
                watchdogProcess.WaitForExit(3000);
                Field("serverProcess").SetValue(window, null);
                Invoke("UpdateWorkshopAutomationControlState");
            }
            Field("uiInitialized").SetValue(window, false);
            try
            {
                var languageCombo = (ComboBox)Control("UiLanguageCombo");
                Assert(languageCombo.Items.Cast<object>().Any(item =>
                        item.GetType().GetProperty("Code")?.GetValue(item)?.ToString() == "en-US"),
                    "External English language pack was not discovered.");
                languageCombo.SelectedValue = "en-US";
                var mainTabs = (TabControl)Control("MainTabs");
                Assert(((TabItem)mainTabs.Items[0]).Header?.ToString() == " Dashboard ",
                    "Dashboard tab did not switch to English.");
                Assert(((Button)Control("SaveWorldButton")).Content?.ToString() == "💾 Save now",
                    "Dashboard controls did not switch to English.");
                var englishRows = ((System.Collections.IEnumerable)Field("featuredSandboxRows").GetValue(window)!)
                    .Cast<ConfigValueRow>().ToList();
                Assert(englishRows.Single(row => row.Key == "Temperature").DisplayName == "Temperature",
                    "Detailed settings did not switch to their English presentation.");
                languageCombo.SelectedValue = "zh-TW";
                Assert(((TabItem)mainTabs.Items[0]).Header?.ToString() == " 儀表板 ",
                    "Dashboard tab did not switch back to Traditional Chinese.");
                var chineseRows = ((System.Collections.IEnumerable)Field("featuredSandboxRows").GetValue(window)!)
                    .Cast<ConfigValueRow>().ToList();
                Assert(chineseRows.Single(row => row.Key == "Temperature").DisplayName == "氣溫",
                    "Detailed settings did not switch back to Traditional Chinese.");

                Tag("UiFontCombo", "LXGWWenKaiTC");
                var selectedFont = (FontFamily)application.Resources["AppUiFontFamily"];
                Assert(selectedFont.Source.Contains("LXGW WenKai TC", StringComparison.Ordinal),
                    "Bundled LXGW WenKai TC font was not selected.");
                Assert(selectedFont.GetTypefaces().Any(),
                    "Bundled LXGW WenKai TC font did not expose a usable typeface.");
                Tag("UiFontCombo", "NotoSansTC");
            }
            finally
            {
                Field("uiInitialized").SetValue(window, true);
            }
            SelectContainingTabs((System.Windows.DependencyObject)Control("SettingsGrid"));
            window.Width = 1500;
            window.Height = 900;
            window.UpdateLayout();
            var settingsHeader = FindVisualChild<DataGridColumnHeader>(
                (System.Windows.DependencyObject)Control("SettingsGrid"));
            Assert(settingsHeader != null, "Settings table did not render a column header.");
            Assert(settingsHeader!.Background is SolidColorBrush headerBackground &&
                headerBackground.Color == Color.FromRgb(0x24, 0x31, 0x3A),
                "DataGrid header did not use the dark background.");
            Assert(settingsHeader.Foreground is SolidColorBrush headerForeground &&
                headerForeground.Color == Color.FromRgb(0xEA, 0xF0, 0xF2),
                "DataGrid header did not use the readable light foreground.");
            window.WindowState = WindowState.Normal;
            window.Width = 1517;
            window.Height = 745;
            var fullSettingsPages = (TabControl)Control("FullSettingsPages");
            Assert(fullSettingsPages.Items.Count == 2 &&
                fullSettingsPages.Items.Cast<TabItem>().Select(tab => tab.Header?.ToString())
                    .SequenceEqual(new[] { "基礎設定", "進階設定" }),
                "Settings were not split into the two requested top-level categories.");
            Assert(fullSettingsPages.TabStripPlacement == Dock.Left,
                "Top-level settings categories still consumed vertical settings space.");
            foreach (var category in fullSettingsPages.Items.Cast<TabItem>())
            {
                var categoryPages = category.Content as TabControl;
                Assert(categoryPages != null,
                    $"{category.Header} did not contain an independent category selector.");
                Assert(categoryPages!.Items.Count <= 6,
                    $"{category.Header} exceeded the six-category UI limit: {categoryPages.Items.Count}.");
                Assert(categoryPages.TabStripPlacement == Dock.Left,
                    $"{category.Header} categories still consumed vertical settings space.");
                Assert(categoryPages.Items.Cast<TabItem>().All(tab => double.IsNaN(tab.Width)),
                    $"{category.Header} categories were forced to fixed widths.");
            }
            foreach (var page in new[]
                     {
                         (Grid: "WorldDetailsGrid", Header: "世界詳細"),
                         (Grid: "PlayerDetailsGrid", Header: "玩家詳細"),
                         (Grid: "ZombieDetailsGrid", Header: "殭屍詳細"),
                         (Grid: "AntiCheatDetailsGrid", Header: "反作弊")
                     })
            {
                var featuredGrid = (DataGrid)Control(page.Grid);
                Assert(ContainingTabHeader(featuredGrid) == page.Header,
                    $"{page.Grid} was not moved to its own {page.Header} page.");
                SelectContainingTabs(featuredGrid);
                window.UpdateLayout();
                Assert(featuredGrid.ActualHeight >= 150,
                    $"{page.Grid} collapsed at 1517x745: {featuredGrid.ActualHeight}.");
            }
            SelectContainingTabs((System.Windows.DependencyObject)Control("ResolvedModsGrid"));
            window.UpdateLayout();
            Assert(ContainingTabHeader((System.Windows.DependencyObject)Control("ResolvedModsGrid")) == "伺服器模組",
                "Enabled MOD list was not split into its own page.");
            var resolvedGridHeight = ((DataGrid)Control("ResolvedModsGrid")).ActualHeight;
            Assert(resolvedGridHeight >= 300,
                $"Resolved MOD table collapsed during WPF layout: {resolvedGridHeight}.");
            SelectContainingTabs((System.Windows.DependencyObject)Control("MapCandidatesGrid"));
            window.UpdateLayout();
            Assert(ContainingTabHeader((System.Windows.DependencyObject)Control("MapCandidatesGrid")) == "地圖／重生點",
                "Map and spawn-point settings were not split into their own page.");
            var mapGridHeight = ((DataGrid)Control("MapCandidatesGrid")).ActualHeight;
            Assert(mapGridHeight >= 120,
                $"Map candidate table collapsed during WPF layout: {mapGridHeight}.");
            testMap.GetType().GetProperty("Enabled")!.SetValue(testMap, true);
            testMap.GetType().GetProperty("SpawnEnabled")!.SetValue(testMap, true);
            Assert((bool)Invoke("ApplyMapSelections", false)!,
                "Valid map and spawn-region selection was rejected.");
            Assert(((TextBox)Control("MapFoldersBox")).Text == "Test Map;Muldraugh, KY",
                "Selected map folder was not placed before the vanilla map. Actual: " +
                ((TextBox)Control("MapFoldersBox")).Text);
            var spawnRegionsPath = Path.Combine(smokeRoot, "data", "Server",
                "alltest_spawnregions.lua");
            File.WriteAllText(spawnRegionsPath,
                "function SpawnRegions()\n    return {\n" +
                "        { name = \"Muldraugh, KY\", file = \"media/maps/Muldraugh, KY/spawnpoints.lua\" },\n" +
                "    }\nend\n", new UTF8Encoding(false));
            Invoke("WriteManagedSpawnRegions", Path.Combine(smokeRoot, "data", "Server"));
            var spawnRegions = File.ReadAllText(spawnRegionsPath, Encoding.UTF8);
            Assert(spawnRegions.Contains("PZServerManager BEGIN MOD SPAWN REGIONS") &&
                spawnRegions.Contains("media/maps/Test Map/spawnpoints.lua") &&
                spawnRegions.Contains("media/maps/Muldraugh, KY/spawnpoints.lua"),
                "Selected map spawn region was not written to the managed Lua block.");
            coreEntry.GetType().GetProperty("Enabled")!.SetValue(coreEntry, false);
            Assert(!(bool)Invoke("ApplyResolvedMods", false, false)!,
                "A selected mod with a disabled dependency was incorrectly accepted.");
            coreEntry.GetType().GetProperty("Enabled")!.SetValue(coreEntry, true);

            const string dependencyHtml =
                "<div class=\"requiredItemsContainer\"><a href=\"https://steamcommunity.com/workshop/filedetails/?id=2950902979\"><div class=\"requiredItem\">Equipment UI</div></a></div></div>";
            var parsedDependencies = ((System.Collections.IEnumerable)
                Invoke("ParseRequiredWorkshopItemsHtml", dependencyHtml)!).Cast<object>()
                .Select(value => value.ToString()).ToList();
            Assert(parsedDependencies.SequenceEqual(new[] { "2950902979" }),
                "Steam required-item HTML was not parsed correctly.");
            using (var rateLimited = new System.Net.Http.HttpResponseMessage(
                       (System.Net.HttpStatusCode)429))
            {
                rateLimited.Headers.RetryAfter =
                    new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(12));
                var retryDelay = (TimeSpan)Invoke("CalculateWorkshopRetryDelay", rateLimited, 0)!;
                Assert(retryDelay >= TimeSpan.FromSeconds(12) &&
                    retryDelay <= TimeSpan.FromSeconds(61),
                    "Steam 429 Retry-After was not honored or safely capped.");
            }
            var rateLimitHandler = new QueueHttpHandler(
                new System.Net.Http.HttpResponseMessage((System.Net.HttpStatusCode)429));
            using (var rateLimitClient = new System.Net.Http.HttpClient(rateLimitHandler))
            {
                var fetchTask = (Task)Invoke("FetchWorkshopPageWithRetryAsync",
                    rateLimitClient, "100", true)!;
                fetchTask.GetAwaiter().GetResult();
                var fetchResult = fetchTask.GetType().GetProperty("Result")!.GetValue(fetchTask)!;
                var blocked = (bool)fetchResult.GetType().GetField("Item2")!.GetValue(fetchResult)!;
                Assert(blocked && rateLimitHandler.RequestCount == 1,
                    "A cached Workshop item did not stop immediately after Steam 429.");
            }
            const string workshopManifest =
                "\"WorkshopItemsInstalled\"\n{\n" +
                "  \"100\"\n  {\n    \"manifest\" \"500\"\n    \"timeupdated\" \"1700000000\"\n  }\n" +
                "  \"200\"\n  {\n    \"manifest\" \"600\"\n    \"timeupdated\" \"1800000000\"\n  }\n}";
            var installedTimes = (System.Collections.IDictionary)Invoke(
                "ParseInstalledWorkshopUpdateTimes", workshopManifest,
                new[] { "100", "200", "300" })!;
            Assert((long)installedTimes["100"]! == 1700000000L &&
                (long)installedTimes["200"]! == 1800000000L &&
                !installedTimes.Contains("300"),
                "Local appworkshop timestamp parsing is incorrect.");
            const string publishedDetails =
                "{\"response\":{\"publishedfiledetails\":[" +
                "{\"publishedfileid\":\"100\",\"result\":1,\"time_updated\":1700000100}," +
                "{\"publishedfileid\":\"200\",\"result\":9,\"time_updated\":1800000100}]}}";
            var remoteTimes = (System.Collections.IDictionary)Invoke(
                "ParsePublishedWorkshopUpdateTimes", publishedDetails)!;
            Assert((long)remoteTimes["100"]! == 1700000100L &&
                !remoteTimes.Contains("200"),
                "Steam published-file timestamp parsing is incorrect.");
            var requirements = (System.Collections.IDictionary)
                Field("workshopRequirements").GetValue(window)!;
            requirements["100"] = new List<string> { "900" };
            requirements["unrelated"] = new List<string> { "unrelated-child" };
            var reachable = ((System.Collections.IEnumerable)Invoke(
                    "CollectReachableWorkshopIds", (object)new[] { "100" })!).Cast<object>()
                .Select(value => value.ToString()).ToList();
            Assert(reachable.SequenceEqual(new[] { "100", "900" }) &&
                !reachable.Contains("unrelated-child"),
                "Workshop dependency graph included unrelated cached items.");
            var titles = (System.Collections.IDictionary)Field("workshopTitles").GetValue(window)!;
            titles["100"] = "Core Workshop";
            titles["900"] = "Optional Library";
            Invoke("RefreshWorkshopDependencyCandidates",
                new[] { "100" }, new[] { "100", "900" });
            var dependencyEntries = ((System.Collections.IEnumerable)
                Field("workshopDependencyEntries").GetValue(window)!).Cast<object>().ToList();
            Assert(dependencyEntries.Count == 1 &&
                dependencyEntries[0].GetType().GetProperty("WorkshopId")!
                    .GetValue(dependencyEntries[0])!.ToString() == "900",
                "Steam dependency was not exposed as a user-selectable candidate.");
            dependencyEntries[0].GetType().GetProperty("Include")!.SetValue(dependencyEntries[0], true);
            Text("WorkshopBox", "100");
            Invoke("AddSelectedWorkshopDependencies_Click", new object(),
                new System.Windows.RoutedEventArgs());
            Assert(((TextBox)Control("WorkshopBox")).Text == "100;900",
                "Selected Steam dependency candidate was not appended to the input list.");

            var managedWorkshopIds = (List<string>)Field("managedWorkshopIds").GetValue(window)!;
            managedWorkshopIds.Clear();
            managedWorkshopIds.AddRange(new[] { "200", "100", "300" });
            ((TextBox)Control("WorkshopBox")).Clear();
            Assert((string)Invoke("ManagedWorkshopItems")! == "200;100;300" &&
                ((TextBox)Control("WorkshopBox")).Text.Length == 0,
                "The transient add field was still coupled to the persistent Workshop list.");
            var disabledAfterRemoval = ((System.Collections.IEnumerable)
                Invoke("RemoveManagedWorkshop", "100")!).Cast<object>()
                .Select(value => value.ToString()).ToList();
            Assert(!((string)Invoke("ManagedWorkshopItems")!).Split(';').Contains("100") &&
                ((TextBox)Control("WorkshopBox")).Text.Length == 0,
                "Removing a Workshop did not update the persistent list independently.");
            Assert(disabledAfterRemoval.Contains("PatchMod") &&
                !(bool)patchEntry.GetType().GetProperty("Enabled")!.GetValue(patchEntry)!,
                "Removing a required Workshop did not disable dependent Mod IDs.");
            Assert(!((TextBox)Control("MapFoldersBox")).Text.Contains("Test Map",
                    StringComparison.OrdinalIgnoreCase),
                "Removing a Workshop left its exclusive map in the pending Map value.");

            managedWorkshopIds.Add("999999999999");
            Invoke("FindInstalledModEntries", managedWorkshopIds.ToArray(), false);
            var afterMissingScan = ((System.Collections.IEnumerable)
                Field("resolvedModEntries").GetValue(window)!).Cast<object>().ToList();
            var missingWorkshop = afterMissingScan.Single(entry =>
                entry.GetType().GetProperty("WorkshopId")!.GetValue(entry)!.ToString() ==
                "999999999999");
            Assert(!(bool)missingWorkshop.GetType().GetProperty("CanEnable")!
                    .GetValue(missingWorkshop)! &&
                missingWorkshop.GetType().GetProperty("Status")!.GetValue(missingWorkshop)!
                    .ToString()!.Contains("找不到"),
                "A missing Workshop item disappeared instead of remaining visible in the server list.");

            managedWorkshopIds.Clear();
            managedWorkshopIds.AddRange(new[] { "100", "500", "200" });
            Text("ModsBox", "CoreMod;PatchMod");
            Invoke("FindInstalledModEntries", managedWorkshopIds.ToArray(), false);
            var duplicateEntries = ((System.Collections.IEnumerable)
                Field("resolvedModEntries").GetValue(window)!).Cast<object>().ToList();
            var coreSources = duplicateEntries.Where(entry => ModId(entry) == "CoreMod").ToList();
            Assert(coreSources.Count == 2 && coreSources.Count(entry =>
                    (bool)entry.GetType().GetProperty("CanEnable")!.GetValue(entry)!) == 1,
                "Duplicate Mod IDs from separate Workshop sources were hidden or both enabled.");
            var disabledByAlternateRemoval = ((System.Collections.IEnumerable)
                Invoke("RemoveManagedWorkshop", "100")!).Cast<object>()
                .Select(value => value.ToString()).ToList();
            var afterAlternatePromotion = ((System.Collections.IEnumerable)
                Field("resolvedModEntries").GetValue(window)!).Cast<object>().ToList();
            Assert(disabledByAlternateRemoval.Count == 0 &&
                afterAlternatePromotion.Single(entry => ModId(entry) == "CoreMod")
                    .GetType().GetProperty("WorkshopId")!.GetValue(
                        afterAlternatePromotion.Single(entry => ModId(entry) == "CoreMod"))!
                    .ToString() == "500" &&
                afterAlternatePromotion.Single(entry => ModId(entry) == "PatchMod")
                    .GetType().GetProperty("Enabled")!.GetValue(
                        afterAlternatePromotion.Single(entry => ModId(entry) == "PatchMod")) is true,
                "Removing one duplicate source did not promote the remaining source safely.");

            managedWorkshopIds.Clear();
            managedWorkshopIds.Add("600");
            Text("ModsBox", "CycleA;CycleB");
            Invoke("FindInstalledModEntries", managedWorkshopIds.ToArray(), false);
            Invoke("SortModEntriesByDependencies");
            var cycleEntries = ((System.Collections.IEnumerable)
                Field("resolvedModEntries").GetValue(window)!).Cast<object>().ToList();
            Assert(cycleEntries.Where(entry => ModId(entry).StartsWith("Cycle"))
                    .All(entry => (bool)entry.GetType().GetProperty("HasOrderingCycle")!
                        .GetValue(entry)!),
                "Circular Mod dependencies were not retained and marked for review.");

            managedWorkshopIds.Clear();
            Text("WorkshopBox", "777777777777");
            Text("ModsBox", "UnlinkedMod;LocalOnlyMod");
            var runtimeSettingsForMods = Field("settings").GetValue(window)!;
            runtimeSettingsForMods.GetType().GetProperty("WorkshopItems")!
                .SetValue(runtimeSettingsForMods, "");
            runtimeSettingsForMods.GetType().GetProperty("Mods")!
                .SetValue(runtimeSettingsForMods, "UnlinkedMod;LocalOnlyMod");
            Invoke("FindInstalledModEntries", Array.Empty<string>(), false);
            var linkedEntries = ((System.Collections.IEnumerable)
                Field("resolvedModEntries").GetValue(window)!).Cast<object>().ToList();
            var inferredEntry = linkedEntries.Single(entry => ModId(entry) == "UnlinkedMod");
            var localOnlyEntry = linkedEntries.Single(entry => ModId(entry) == "LocalOnlyMod");
            Assert(inferredEntry.GetType().GetProperty("WorkshopId")!.GetValue(inferredEntry)!
                    .ToString() == "400" &&
                (bool)inferredEntry.GetType().GetProperty("InferredWorkshopLink")!
                    .GetValue(inferredEntry)! && managedWorkshopIds.SequenceEqual(new[] { "400" }),
                "An existing Mods entry was not reverse-linked to its downloaded Workshop ID.");
            Assert((bool)localOnlyEntry.GetType().GetProperty("IsLocalOnly")!
                    .GetValue(localOnlyEntry)! &&
                localOnlyEntry.GetType().GetProperty("DisplayWorkshopId")!
                    .GetValue(localOnlyEntry)!.ToString() == "—",
                "A true local mod was not clearly distinguished from a Workshop item.");
            Assert(((TextBox)Control("WorkshopBox")).Text == "777777777777",
                "Reverse-linking existing Mods unexpectedly changed the transient add field.");
            var pendingPartition = Invoke("ClassifyPendingWorkshopIds",
                new[] { "100", "999999999999", "888" }, new[] { "888" })!;
            var acceptedPending = ((System.Collections.IEnumerable)pendingPartition.GetType()
                    .GetField("Item1")!.GetValue(pendingPartition)!).Cast<object>()
                .Select(value => value.ToString()).ToList();
            var failedPending = ((System.Collections.IEnumerable)pendingPartition.GetType()
                    .GetField("Item2")!.GetValue(pendingPartition)!).Cast<object>()
                .Select(value => value.ToString()).ToList();
            Assert(acceptedPending.SequenceEqual(new[] { "100", "888" }) &&
                failedPending.SequenceEqual(new[] { "999999999999" }),
                "Pending Workshop IDs were cleared or accepted before a local download existed.");
            Assert((bool)Invoke("EnsureModIdsResolved")! &&
                ((TextBox)Control("ModsBox")).Text == "UnlinkedMod;LocalOnlyMod",
                "Mods without an original WorkshopItems value were cleared during validation.");

            SelectContainingTabs((System.Windows.DependencyObject)Control("ResolvedModsGrid"));
            ((DataGrid)Control("ResolvedModsGrid")).SelectedItem = inferredEntry;
            window.UpdateLayout();
            Assert(((TextBlock)Control("SelectedWorkshopIdText")).Text == "400" &&
                ((TextBlock)Control("SelectedModIdText")).Text == "UnlinkedMod",
                "The selected-item details did not show both Workshop ID and Mod ID.");
            Invoke("ModEnabledCheck_Click",
                new CheckBox { DataContext = inferredEntry, IsChecked = false },
                new RoutedEventArgs());
            Assert(ReferenceEquals(((DataGrid)Control("ResolvedModsGrid")).SelectedItem,
                    inferredEntry),
                "Refreshing an enabled state discarded the selected MOD and its details.");
            inferredEntry.GetType().GetProperty("Enabled")!.SetValue(inferredEntry, true);
            Invoke("ApplyUiLanguage", "en-US");
            window.UpdateLayout();
            Assert(((TextBlock)Control("SelectedWorkshopIdText")).Text == "400" &&
                ((TextBlock)Control("SelectedModIdText")).Text == "UnlinkedMod",
                "Language switching erased dynamic selected-item bindings.");
            Invoke("ApplyUiLanguage", "zh-TW");

            Invoke("RemoveLocalModEntry", localOnlyEntry);
            Assert(!((System.Collections.IEnumerable)Field("resolvedModEntries").GetValue(window)!)
                    .Cast<object>().Any(entry => ModId(entry) == "LocalOnlyMod") &&
                managedWorkshopIds.SequenceEqual(new[] { "400" }),
                "Removing a local mod affected the managed Workshop list or left the Mod behind.");
            Assert((bool)Invoke("UiToSettings", false)!,
                "The rebuilt MOD state could not be prepared for an explicit save.");
            Field("explicitConfigWriteAuthorized").SetValue(window, true);
            try { Invoke("WriteServerConfig"); }
            finally { Field("explicitConfigWriteAuthorized").SetValue(window, false); }
            Assert((bool)Field("lastConfigWriteSucceeded").GetValue(window)!,
                "The rebuilt MOD state failed its explicit disk write.");
            var modSavedIni = File.ReadAllText(iniPath, Encoding.UTF8);
            Assert(System.Text.RegularExpressions.Regex.IsMatch(modSavedIni,
                    @"(?m)^WorkshopItems=400\s*$") &&
                System.Text.RegularExpressions.Regex.IsMatch(modSavedIni,
                    @"(?m)^Mods=UnlinkedMod\s*$") &&
                !modSavedIni.Contains("777777777777", StringComparison.Ordinal),
                "Transient input leaked into INI or the managed Workshop/Mods state was not saved together.");
            Assert((bool)Invoke("TryLoadExistingConfig", true)! &&
                managedWorkshopIds.SequenceEqual(new[] { "400" }) &&
                ((System.Collections.IEnumerable)Field("resolvedModEntries").GetValue(window)!)
                    .Cast<object>().Any(entry => ModId(entry) == "UnlinkedMod" &&
                        entry.GetType().GetProperty("WorkshopId")!.GetValue(entry)!.ToString() == "400"),
                "Saved Workshop and Mod state did not survive a disk reload.");
            Assert((string)Invoke("NormalizeSemicolonList",
                    "WeaponSkillBooks CN;TacHold Pistol;Muldraugh, KY")! ==
                "WeaponSkillBooks CN;TacHold Pistol;Muldraugh, KY",
                "Mod IDs with spaces or map names with commas were altered.");
            Invoke("ApplyOnlinePlayerResponse", "Players connected (0)", "伺服器控制台");
            Assert(((TextBlock)Control("OnlinePlayerSummaryText")).Text == "在線玩家：0",
                "Player summary still exposes the meaningless console-source suffix.");

            var about = new AboutWindow();
            var aboutType = typeof(AboutWindow);
            var aboutVersion = (TextBlock)(aboutType.GetField("AboutVersionText", Flags)
                ?? throw new MissingFieldException("AboutVersionText")).GetValue(about)!;
            var creator = (TextBlock)(aboutType.GetField("CreatorText", Flags)
                ?? throw new MissingFieldException("CreatorText")).GetValue(about)!;
            var disclaimer = (TextBlock)(aboutType.GetField("DisclaimerText", Flags)
                ?? throw new MissingFieldException("DisclaimerText")).GetValue(about)!;
            Assert(aboutVersion.Text == "版本 v2.0.0",
                $"About version is incorrect: {aboutVersion.Text}");
            Assert(creator.Text == "MapleLeaf", "About creator is incorrect.");
            var disclaimerText = new System.Windows.Documents.TextRange(
                disclaimer.ContentStart, disclaimer.ContentEnd).Text;
            Assert(disclaimerText.Contains("非官方獨立管理工具") &&
                disclaimerText.Contains("自行承擔風險"), "About disclaimer is incomplete.");
            Assert(about.Icon != null, "About window icon was not loaded.");
            about.Close();
            var fakeRelease = new GitHubReleaseInfo(new Version(9, 9, 9), "v9.9.9",
                new Uri("https://github.com/MapleLeafLegend/PZServerManager/releases/tag/v9.9.9"),
                new Uri("https://github.com/MapleLeafLegend/PZServerManager/releases/download/v9.9.9/PZServerManager-Windows-x64-v9.9.9.zip"),
                "PZServerManager-Windows-x64-v9.9.9.zip", 123, null);
            var updateAbout = new AboutWindow(Task.FromResult<GitHubReleaseInfo?>(fakeRelease))
            {
                ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000, Opacity = 0
            };
            updateAbout.Show();
            var updateStatus = (TextBlock)(aboutType.GetField("UpdateStatusText", Flags)
                ?? throw new MissingFieldException("UpdateStatusText")).GetValue(updateAbout)!;
            var updateButton = (Button)(aboutType.GetField("DownloadUpdateButton", Flags)
                ?? throw new MissingFieldException("DownloadUpdateButton")).GetValue(updateAbout)!;
            var updateDot = (System.Windows.Shapes.Ellipse)(aboutType.GetField("UpdateStatusDot", Flags)
                ?? throw new MissingFieldException("UpdateStatusDot")).GetValue(updateAbout)!;
            Assert(PumpUntil(() => updateButton.Visibility == Visibility.Visible, 2000) &&
                updateStatus.Text.Contains("v9.9.9", StringComparison.Ordinal) &&
                updateDot.Fill is SolidColorBrush updateBrush && updateBrush.Color == Color.FromRgb(217, 105, 95),
                "A newer GitHub release did not produce the red status dot, version prompt, and download button.");
            updateAbout.Close();
            Assert(window.Icon != null, "Main window icon was not loaded.");

            var manualLogOffset = ((TextBox)Control("ConsoleBox")).Text.Length;
            ((Task)Invoke("StartServerAsync", true)!).GetAwaiter().GetResult();
            Assert(PumpUntil(() => Field("serverProcess").GetValue(window) == null, 5000),
                "Manual start did not finish without a blocking dialog.");
            Invoke("FlushPendingLogs");
            var manualLog = ((TextBox)Control("ConsoleBox")).Text[manualLogOffset..];
            Assert(manualLog.Contains("伺服器已啟動", StringComparison.Ordinal),
                "Manual start never launched the test server.");

            var validRestartHours = ((TextBox)Control("RestartHoursBox")).Text;
            Text("RestartHoursBox", "此值故意無效以確認自動啟動不重讀 GUI");
            var automaticLogOffset = ((TextBox)Control("ConsoleBox")).Text.Length;
            ((Task)Invoke("StartServerAsync", false)!).GetAwaiter().GetResult();
            Assert(PumpUntil(() => Field("serverProcess").GetValue(window) == null, 5000),
                "Automatic restart path did not finish without a blocking dialog.");
            Invoke("FlushPendingLogs");
            var automaticLog = ((TextBox)Control("ConsoleBox")).Text[automaticLogOffset..];
            Assert(automaticLog.Contains("自動啟動使用上次已驗證並儲存的設定", StringComparison.Ordinal) &&
                automaticLog.Contains("伺服器已啟動", StringComparison.Ordinal),
                "Automatic restart path re-read the GUI or failed to launch.");
            Assert(!(bool)Field("currentStartInteractive").GetValue(window)!,
                "Automatic restart was incorrectly marked as interactive.");
            Text("RestartHoursBox", validRestartHours);

            window.Close();
            application.Shutdown();
            Console.WriteLine("INTEGRATION_SAVE_RELOAD_PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            try { window?.Close(); } catch { }
            try { application?.Shutdown(); } catch { }
            return 1;
        }
    }

    private static FieldInfo Field(string name) =>
        windowType.GetField(name, Flags) ?? throw new MissingFieldException(name);

    private static object Control(string name) => Field(name).GetValue(window)!;

    private static object? Invoke(string name, params object[] arguments) =>
        (windowType.GetMethod(name, Flags) ?? throw new MissingMethodException(name))
        .Invoke(window, arguments);

    private static void Text(string name, string value) => ((TextBox)Control(name)).Text = value;

    private static void Password(string name, string value) =>
        ((PasswordBox)Control(name)).Password = value;

    private static void Checked(string name, bool value) =>
        ((CheckBox)Control(name)).IsChecked = value;

    private static void Tag(string name, string value)
    {
        var combo = (ComboBox)Control(name);
        combo.SelectedItem = combo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), value,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{name} has no tag {value}");
    }

    private static void Invalid(string field, string badValue, string restore, string label)
    {
        Text(field, badValue);
        Assert(!(bool)Invoke("UiToSettings", false)!, label + " was incorrectly accepted.");
        Text(field, restore);
    }

    private static T? FindVisualChild<T>(System.Windows.DependencyObject root)
        where T : System.Windows.DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                return match;
            var nested = FindVisualChild<T>(child);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool PumpUntil(Func<bool> condition, int timeoutMilliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(10);
        }
        return condition();
    }

    private static void SelectContainingTabs(System.Windows.DependencyObject child)
    {
        System.Windows.DependencyObject? current = child;
        while (current != null)
        {
            if (current is TabItem tab)
            {
                tab.IsSelected = true;
                if (ItemsControl.ItemsControlFromItemContainer(tab) is TabControl owner)
                    owner.SelectedItem = tab;
            }
            var logical = System.Windows.LogicalTreeHelper.GetParent(current);
            if (logical != null)
            {
                current = logical;
                continue;
            }
            try { current = System.Windows.Media.VisualTreeHelper.GetParent(current); }
            catch { current = null; }
        }
    }

    private static string ContainingTabHeader(System.Windows.DependencyObject child)
    {
        System.Windows.DependencyObject? current = child;
        while (current != null)
        {
            if (current is TabItem tab) return tab.Header?.ToString() ?? "";
            current = System.Windows.LogicalTreeHelper.GetParent(current);
        }
        return "";
    }

    private sealed class QueueHttpHandler(params System.Net.Http.HttpResponseMessage[] responses)
        : System.Net.Http.HttpMessageHandler
    {
        private readonly Queue<System.Net.Http.HttpResponseMessage> responses = new(responses);
        public int RequestCount { get; private set; }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (responses.Count == 0) throw new InvalidOperationException("No fake HTTP response remains.");
            return Task.FromResult(responses.Dequeue());
        }
    }
}
