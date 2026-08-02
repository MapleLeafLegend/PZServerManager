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
            Text("InstallPathBox", Path.Combine(smokeRoot, "install"));
            Text("DataPathBox", Path.Combine(smokeRoot, "data"));
            Text("ServerNameBox", "alltest");
            Tag("ConfigEncodingCombo", "Auto");
            Assert((bool)Invoke("TryLoadExistingConfig", true)!, "Initial configuration load failed.");
            Assert(((TextBox)Control("DataPathBox")).Text == Path.Combine(smokeRoot, "data"),
                "Reading the current server reverted DataPathBox to a stale Manager path.");

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
                languageCombo.SelectedValue = "zh-TW";
                Assert(((TabItem)mainTabs.Items[0]).Header?.ToString() == " 儀表板 ",
                    "Dashboard tab did not switch back to Traditional Chinese.");

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
            SelectContainingTabs((System.Windows.DependencyObject)Control("ResolvedModsGrid"));
            window.UpdateLayout();
            var resolvedGridHeight = ((DataGrid)Control("ResolvedModsGrid")).ActualHeight;
            var mapGridHeight = ((DataGrid)Control("MapCandidatesGrid")).ActualHeight;
            Assert(resolvedGridHeight >= 300,
                $"Resolved MOD table collapsed during WPF layout: {resolvedGridHeight}.");
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
            Assert((string)Invoke("NormalizeSemicolonList",
                    "WeaponSkillBooks CN;TacHold Pistol;Muldraugh, KY")! ==
                "WeaponSkillBooks CN;TacHold Pistol;Muldraugh, KY",
                "Mod IDs with spaces or map names with commas were altered.");

            var about = new AboutWindow();
            var aboutType = typeof(AboutWindow);
            var aboutVersion = (TextBlock)(aboutType.GetField("AboutVersionText", Flags)
                ?? throw new MissingFieldException("AboutVersionText")).GetValue(about)!;
            var creator = (TextBlock)(aboutType.GetField("CreatorText", Flags)
                ?? throw new MissingFieldException("CreatorText")).GetValue(about)!;
            var disclaimer = (TextBlock)(aboutType.GetField("DisclaimerText", Flags)
                ?? throw new MissingFieldException("DisclaimerText")).GetValue(about)!;
            Assert(aboutVersion.Text == "版本 v1.9.6",
                $"About version is incorrect: {aboutVersion.Text}");
            Assert(creator.Text == "MapleLeaf", "About creator is incorrect.");
            var disclaimerText = new System.Windows.Documents.TextRange(
                disclaimer.ContentStart, disclaimer.ContentEnd).Text;
            Assert(disclaimerText.Contains("非官方獨立管理工具") &&
                disclaimerText.Contains("自行承擔風險"), "About disclaimer is incomplete.");
            Assert(about.Icon != null, "About window icon was not loaded.");
            about.Close();
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
}
