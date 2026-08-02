using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Reflection;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace PZServerManager;

public partial class MainWindow : Window
{
    private string settingsFile = "";
    private static string ExeSettingsFile => Path.Combine(AppContext.BaseDirectory, "manager-settings.json");
    private static string LocalSettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PZServerManager", "manager-settings.json");
    private readonly SemaphoreSlim stopLock = new(1, 1);
    private readonly DispatcherTimer scheduleTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer logFlushTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer setupActivityTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ConcurrentQueue<string> pendingLogLines = new();
    private readonly ConcurrentQueue<(Process Process, string Command)> pendingServerCommands = new();
    private readonly List<ModEntry> resolvedModEntries = new();
    private readonly List<WorkshopDependencyEntry> workshopDependencyEntries = new();
    private readonly List<MapEntry> resolvedMapEntries = new();
    private readonly Dictionary<string, string> workshopTitles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> workshopRequirements = new(StringComparer.OrdinalIgnoreCase);
    private string resolvedWorkshopIdentity = "";
    private ServerSettings settings = new();
    private Process? serverProcess;
    private DateTime? nextRestart;
    private DateTime? nextPlayerQuery;
    private DateTime? nextWorkshopUpdateCheck;
    private DateTime? nextWorkshopUpdateAnnouncement;
    private readonly HashSet<string> pendingWorkshopUpdateIds = new(StringComparer.OrdinalIgnoreCase);
    private int workshopUpdateCheckRunning;
    private int? lastKnownOnlinePlayerCount;
    private bool workshopRestartInProgress;
    private readonly object playerQueryLock = new();
    private readonly List<string> playerQueryOutput = new();
    private volatile bool capturePlayerQueryOutput;
    private int playerQueryRunning;
    private TaskCompletionSource<bool>? playerQueryResponseSignal;
    private CancellationTokenSource? commandWriterCancellation;
    private CancellationTokenSource automationCancellation = new();
    private int commandWriterRunning;
    private int scheduleTickRunning;
    private int consecutiveCliResponseFailures;
    private bool cliHealthAlarmActive;
    private bool automationRuntimeSuspended;
    private bool suppressAutomationOptionEvents;
    private bool restartAfterStopAutomated;
    private volatile bool serverReadyForCommands;
    private bool intentionalStop;
    private bool restartAfterStop;
    private volatile bool roleInitializationFailure;
    private bool lastConfigWriteSucceeded;
    private bool explicitConfigWriteAuthorized;
    private bool uiInitialized;
    private bool closeAfterServerStops;
    private bool setupOperationRunning;
    private bool spawnRegionSelectionTouched;
    private bool currentStartInteractive = true;
    private DateTime? setupActivityStarted;
    private string? startupConfigWarning;
    private string? loadedConfigIdentity;
    private readonly Dictionary<string, (long Length, DateTime LastWriteUtc)> loadedFileStates = new();
    private static readonly string AppVersion =
        typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "未知";
    private static readonly Uri EmbeddedFontBaseUri = new(
        "pack://application:,,,/PZServerManager;component/Assets/Fonts/", UriKind.Absolute);
    private static readonly Dictionary<string, string> B42Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PVP"]="true", ["PauseEmpty"]="true", ["GlobalChat"]="true", ["Open"]="true",
        ["AutoCreateUserInWhiteList"]="false", ["DisplayUserName"]="true", ["ShowFirstAndLastName"]="false",
        ["SpawnPoint"]="0,0,0", ["SafetySystem"]="true", ["ShowSafety"]="true",
        ["DefaultPort"]="16261", ["UDPPort"]="16262", ["DoLuaChecksum"]="true", ["Public"]="false",
        ["MaxPlayers"]="32", ["PingLimit"]="400", ["HoursForLootRespawn"]="0",
        ["SpawnItems"]="", ["CharacterFreePoints"]="0", ["StarterKit"]="false", ["Map"]="Muldraugh, KY",
        ["StatsDecrease"]="3", ["EndRegen"]="3", ["Nutrition"]="true",
        ["InjurySeverity"]="2", ["BoneFracture"]="true", ["ClothingDegradation"]="3",
        ["MultiHitZombies"]="false", ["RearVulnerability"]="3", ["BloodLevel"]="3",
        ["PlayerDamageFromCrash"]="true",
        ["MaxItemsForLootRespawn"]="5", ["ConstructionPreventsLootRespawn"]="true",
        ["MinutesPerPage"]="1.0", ["SaveWorldEveryMinutes"]="0", ["PlayerSafehouse"]="true",
        ["AdminSafehouse"]="true", ["RCONPort"]="27015", ["SleepAllowed"]="false",
        ["SleepNeeded"]="false", ["SteamVAC"]="true", ["UPnP"]="true", ["VoiceEnable"]="true",
        ["VoiceMinDistance"]="10.0", ["VoiceMaxDistance"]="100.0", ["Voice3D"]="true",
        ["BackupsCount"]="5", ["BackupsOnStart"]="true", ["BackupsOnVersionChange"]="true",
        ["BackupsPeriod"]="0", ["VERSION"]="6", ["DayLength"]="3", ["StartMonth"]="7",
        ["StartDay"]="9", ["StartTime"]="2", ["WaterShutModifier"]="14", ["ElecShutModifier"]="14",
        ["FoodLootNew"]="0.6", ["CannedFoodLootNew"]="0.6", ["LiteratureLootNew"]="0.6",
        ["SurvivalGearsLootNew"]="0.6", ["MedicalLootNew"]="0.6", ["WeaponLootNew"]="0.6",
        ["RangedWeaponLootNew"]="0.6", ["AmmoLootNew"]="0.6", ["MechanicsLootNew"]="0.6",
        ["OtherLootNew"]="0.6", ["MultiplierConfig.Global"]="1.0",
        ["AllowNonAsciiUsername"]="false", ["AnnounceDeath"]="false", ["MaxAccountsPerUser"]="0",
        ["MapRemotePlayerVisibility"]="1", ["PlayerRespawnWithSelf"]="false",
        ["PlayerRespawnWithOther"]="false", ["SafehouseAllowRespawn"]="false",
        ["Faction"]="true", ["FactionDaySurvivedToCreate"]="0",
        ["SafehouseDaySurvivedToClaim"]="0", ["SafeHouseRemovalTime"]="144",
        ["PVPFirearmDamageModifier"]="50.0", ["PVPMeleeDamageModifier"]="30.0", ["SpeedLimit"]="70.0",
        ["DenyLoginOnOverloadedServer"]="true", ["LoginQueueEnabled"]="false",
        ["LoginQueueConnectTimeout"]="60",
        ["MultiplierConfig.GlobalToggle"]="true", ["ZombieLore.Speed"]="4", ["ZombieLore.Strength"]="2",
        ["ZombieLore.Toughness"]="2", ["ZombieLore.Transmission"]="1",
        ["ZombieConfig.PopulationMultiplier"]="1.0", ["ZombieConfig.PopulationStartMultiplier"]="1.0",
        ["ZombieConfig.PopulationPeakMultiplier"]="1.5", ["ZombieConfig.PopulationPeakDay"]="28",
        ["ZombieConfig.RespawnHours"]="72.0", ["ZombieConfig.RespawnUnseenHours"]="16.0",
        ["ZombieConfig.RespawnMultiplier"]="0.1", ["ZombieConfig.RedistributeHours"]="12.0"
    };
    private static readonly Dictionary<string, (double Min, double Max, string Display)> B42Ranges =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DefaultPort"] = (0, 65535, "0–65535"),
            ["UDPPort"] = (0, 65535, "0–65535"),
            ["MaxPlayers"] = (1, 100, "1–100"),
            ["PingLimit"] = (100, int.MaxValue, $"100–{int.MaxValue}"),
            ["HoursForLootRespawn"] = (0, int.MaxValue, $"0–{int.MaxValue}"),
            ["SaveWorldEveryMinutes"] = (0, int.MaxValue, $"0–{int.MaxValue}"),
            ["BackupsCount"] = (1, 300, "1–300"),
            ["RCONPort"] = (0, 65535, "0–65535"),
            ["MaxAccountsPerUser"] = (0, int.MaxValue, $"0–{int.MaxValue}"),
            ["MapRemotePlayerVisibility"] = (1, 3, "1–3"),
            ["FactionDaySurvivedToCreate"] = (0, int.MaxValue, $"0–{int.MaxValue}"),
            ["SafehouseDaySurvivedToClaim"] = (0, int.MaxValue, $"0–{int.MaxValue}"),
            ["SafeHouseRemovalTime"] = (0, int.MaxValue, $"0–{int.MaxValue}"),
            ["PVPFirearmDamageModifier"] = (0, 500, "0.00–500.00"),
            ["PVPMeleeDamageModifier"] = (0, 500, "0.00–500.00"),
            ["SpeedLimit"] = (10, 150, "10.00–150.00"),
            ["LoginQueueConnectTimeout"] = (20, 1200, "20–1200"),
            ["WaterShutModifier"] = (-1, int.MaxValue, $"-1–{int.MaxValue}"),
            ["ElecShutModifier"] = (-1, int.MaxValue, $"-1–{int.MaxValue}"),
            ["MultiplierConfig.Global"] = (0, 1000, "0.00–1000.00"),
            ["FoodLootNew"] = (0, 4, "0.00–4.00"),
            ["WeaponLootNew"] = (0, 4, "0.00–4.00"),
            ["AmmoLootNew"] = (0, 4, "0.00–4.00"),
            ["MedicalLootNew"] = (0, 4, "0.00–4.00"),
            ["OtherLootNew"] = (0, 4, "0.00–4.00"),
            ["CharacterFreePoints"] = (-100, 100, "-100–100"),
            ["StatsDecrease"] = (1, 4, "1–4"),
            ["EndRegen"] = (1, 4, "1–4"),
            ["InjurySeverity"] = (1, 3, "1–3"),
            ["ClothingDegradation"] = (1, 4, "1–4"),
            ["RearVulnerability"] = (1, 3, "1–3"),
            ["BloodLevel"] = (1, 5, "1–5"),
            ["ZombieConfig.PopulationMultiplier"] = (0, 4, "0.00–4.00"),
            ["ZombieConfig.PopulationPeakMultiplier"] = (0, 4, "0.00–4.00"),
            ["ZombieConfig.PopulationPeakDay"] = (1, 365, "1–365"),
            ["ZombieConfig.RespawnHours"] = (0, 8760, "0.00–8760.00")
        };

    public MainWindow()
    {
        InitializeComponent();
        LocalizationService.Reload();
        MergeSettingsTabs();
        Title = $"PZ Build 42 伺服器管理器 v{AppVersion}";
        HeaderVersionText.Text = $"v{AppVersion}";
        InitializeSettingChoices();
        LoadSettings();
        uiInitialized = true;
        WizardSteamCmdPathBox.Text = settings.SteamCmdPath;
        WizardInstallPathBox.Text = settings.InstallDirectory;
        VerifyWindowsServerEnvironment();
        RefreshSetupStage();
        ApplyUiLanguage(settings.UiLanguage);
        if (IsPzServerInstalled()) TryLoadExistingConfigSafely(true);
        Loaded += (_, _) =>
        {
            if (IsPzServerInstalled()) ScanExistingServers();
            if (!string.IsNullOrWhiteSpace(startupConfigWarning))
            {
                MessageBox.Show(startupConfigWarning, "設定檔編碼已安全回復",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                startupConfigWarning = null;
            }
        };
        scheduleTimer.Tick += ScheduleTimer_Tick;
        scheduleTimer.Start();
        logFlushTimer.Tick += (_, _) => FlushPendingLogs();
        logFlushTimer.Start();
        setupActivityTimer.Tick += (_, _) => UpdateSetupElapsed();
        Closing += async (_, e) =>
        {
            if (serverProcess is { HasExited: false })
            {
                e.Cancel = true;
                if (MessageBox.Show("伺服器仍在執行。要先存檔並安全關服嗎？", "關閉管理器",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    closeAfterServerStops = true;
                    await SafeStopAsync(false);
                    var stopped = serverProcess is null;
                    try { stopped |= serverProcess?.HasExited == true; } catch { stopped = true; }
                    if (stopped)
                    {
                        closeAfterServerStops = false;
                        Close();
                    }
                }
            }
        };
    }

    private string SelectedEncodingMode() =>
        (ConfigEncodingCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Auto";

    private string SelectedSettingsStorage() =>
        (SettingsStorageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ExeDirectory";

    private string SelectedUiFont() =>
        (UiFontCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "NotoSansTC";

    private string SelectedUiLanguage() =>
        UiLanguageCombo.SelectedValue?.ToString() ?? "zh-TW";

    private static FontFamily ResolveUiFont(string? key)
    {
        try
        {
            return key switch
            {
                "NotoSerifTC" => new FontFamily(EmbeddedFontBaseUri, "./#Noto Serif TC"),
                "LXGWWenKaiTC" => new FontFamily(EmbeddedFontBaseUri, "./#LXGW WenKai TC"),
                "MicrosoftJhengHeiUI" => new FontFamily("Microsoft JhengHei UI"),
                _ => new FontFamily(EmbeddedFontBaseUri, "./#Noto Sans TC")
            };
        }
        catch
        {
            return new FontFamily("Microsoft JhengHei UI");
        }
    }

    private void ApplyUiFont(string? key)
    {
        var normalized = key is "NotoSerifTC" or "LXGWWenKaiTC" or "MicrosoftJhengHeiUI"
            ? key : "NotoSansTC";
        settings.UiFontFamily = normalized;
        if (Application.Current != null)
            Application.Current.Resources["AppUiFontFamily"] = ResolveUiFont(normalized);
        else
            FontFamily = ResolveUiFont(normalized);
    }

    private void ApplyUiLanguage(string? code)
    {
        LocalizationService.SetLanguage(code);
        settings.UiLanguage = LocalizationService.CurrentLanguage;
        LocalizationService.SetFormattedTitle(this,
            "PZ Build 42 伺服器管理器 v{0}", AppVersion);
        LocalizationService.Apply(this);
    }

    private void UiFontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UiFontCombo.SelectedItem == null) return;
        ApplyUiFont(SelectedUiFont());
        if (uiInitialized) PersistSettings();
    }

    private void UiLanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UiLanguageCombo.SelectedValue == null) return;
        ApplyUiLanguage(SelectedUiLanguage());
        if (uiInitialized) PersistSettings();
    }

    private void ReloadLanguages_Click(object sender, RoutedEventArgs e)
    {
        var requested = SelectedUiLanguage();
        LocalizationService.Reload();
        UiLanguageCombo.ItemsSource = LocalizationService.AvailableLanguages;
        UiLanguageCombo.SelectedValue = requested;
        if (UiLanguageCombo.SelectedValue == null)
            UiLanguageCombo.SelectedValue = "zh-TW";
        ApplyUiLanguage(SelectedUiLanguage());
        if (uiInitialized) PersistSettings();
    }

    private void OpenLanguagesFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(LocalizationService.LanguageDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", LocalizationService.LanguageDirectory)
                { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private void ConfigEncodingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!uiInitialized) return;
        settings.ConfigEncoding = SelectedEncodingMode();
        PersistSettings();
        if (!IsPzServerInstalled()) return;
        loadedConfigIdentity = null;
        loadedFileStates.Clear();
        try
        {
            TryLoadExistingConfig(false);
            ScanExistingServers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"無法用所選編碼讀取設定檔：\n{ex.Message}\n\n檔案未被修改。",
                "編碼不相符", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool TryLoadExistingConfigSafely(bool silent)
    {
        try { return TryLoadExistingConfig(silent); }
        catch (Exception firstError)
        {
            loadedConfigIdentity = null;
            loadedFileStates.Clear();
            uiInitialized = false;
            SelectByTag(ConfigEncodingCombo, "Auto");
            uiInitialized = true;
            settings.ConfigEncoding = "Auto";
            PersistSettings();
            Log($"原編碼模式無法安全讀取，已回復「自動」：{firstError.Message}");
            startupConfigWarning =
                $"上次保存的設定檔編碼無法安全讀取：\n{firstError.Message}\n\n" +
                "管理器已自動回復「自動」並重新讀取；PZ 設定檔未被修改。";
            try { return TryLoadExistingConfig(true); }
            catch (Exception fallbackError)
            {
                Log($"自動編碼仍無法讀取：{fallbackError.Message}");
                startupConfigWarning += $"\n\n自動模式仍無法讀取：{fallbackError.Message}\n請到「原始設定檔」檢查或還原備份。";
                return false;
            }
        }
    }

    private bool IsSteamCmdInstalled() => File.Exists(WizardSteamCmdPathBox.Text.Trim());

    private bool IsPzServerInstalled()
    {
        var path = WizardInstallPathBox.Text.Trim();
        return new[] { "StartServer64.bat", "start-server.bat", "StartServer32.bat" }
            .Any(x => File.Exists(Path.Combine(path, x)));
    }

    private void RefreshSetupStage()
    {
        if (setupOperationRunning) return;
        var steamReady = IsSteamCmdInstalled();
        var serverReady = steamReady && IsPzServerInstalled();
        MainTabs.Visibility = serverReady ? Visibility.Visible : Visibility.Collapsed;
        SetupOverlay.Visibility = serverReady ? Visibility.Collapsed : Visibility.Visible;
        StepSteamBadge.Background = BrushFrom(steamReady ? "#297052" : "#8A641F");
        StepServerBadge.Background = BrushFrom(serverReady ? "#297052" : steamReady ? "#8A641F" : "#26343B");
        StepReadyBadge.Background = BrushFrom(serverReady ? "#297052" : "#26343B");
        SteamSetupActions.Visibility = steamReady ? Visibility.Collapsed : Visibility.Visible;
        SteamSourceNotice.Visibility = steamReady ? Visibility.Collapsed : Visibility.Visible;
        BrowseSteamCmdButton.Visibility = steamReady ? Visibility.Collapsed : Visibility.Visible;
        ServerSetupActions.Visibility = steamReady && !serverReady ? Visibility.Visible : Visibility.Collapsed;
        LocalizationService.SetText(SetupTitleText,
            !steamReady ? "先準備 SteamCMD" : "安裝 Project Zomboid Dedicated Server");
        LocalizationService.SetText(SetupDescriptionText, !steamReady
            ? "尚未找到 steamcmd.exe。你可以選擇既有檔案，或由管理器從 Valve 官方來源下載並解壓縮。完成前不會顯示或讀寫伺服器設定。"
            : "SteamCMD 已就緒。選擇 Dedicated Server 安裝位置後執行安裝；確認啟動批次檔存在後，才會解鎖管理與設定介面。");
        if (SetupActivityPanel.Visibility != Visibility.Visible)
        {
            if (!steamReady)
                LocalizationService.SetText(SetupStatusText, "等待 SteamCMD");
            else
                LocalizationService.SetFormattedText(SetupStatusText, "SteamCMD：{0}",
                    WizardSteamCmdPathBox.Text.Trim());
        }
    }

    private static SolidColorBrush BrushFrom(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    private void RecheckSetup_Click(object sender, RoutedEventArgs e)
    {
        SyncWizardPaths();
        PersistSettings();
        RefreshSetupStage();
    }

    private void BrowseSteamCmd_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog {
            Title = "選擇 steamcmd.exe", Filter = "SteamCMD|steamcmd.exe|執行檔|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        WizardSteamCmdPathBox.Text = dialog.FileName;
        SyncWizardPaths();
        PersistSettings();
        RefreshSetupStage();
    }

    private void BrowseInstallFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog {
            Title = "選擇 Project Zomboid Dedicated Server 安裝資料夾",
            InitialDirectory = Directory.Exists(WizardInstallPathBox.Text) ? WizardInstallPathBox.Text : null
        };
        if (dialog.ShowDialog(this) != true) return;
        WizardInstallPathBox.Text = dialog.FolderName;
        SyncWizardPaths();
        PersistSettings();
    }

    private async void DownloadSteamCmd_Click(object sender, RoutedEventArgs e)
    {
        var executable = WizardSteamCmdPathBox.Text.Trim();
        if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            executable = Path.Combine(executable, "steamcmd.exe");
        var directory = Path.GetDirectoryName(executable);
        if (string.IsNullOrWhiteSpace(directory)) { MessageBox.Show("SteamCMD 路徑無效。"); return; }
        var temporaryZip = Path.Combine(Path.GetTempPath(), $"steamcmd-{Guid.NewGuid():N}.zip");
        var succeeded = false;
        var finalMessage = "SteamCMD 安裝失敗。";
        BeginSetupActivity("正在安裝 SteamCMD", "正在連線至 Valve 官方下載來源…");
        try
        {
            Directory.CreateDirectory(directory);
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await client.GetAsync(
                "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip",
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var destination = new FileStream(temporaryZip, FileMode.Create,
                             FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long downloaded = 0;
                var lastPercent = -1;
                while (true)
                {
                    var read = await source.ReadAsync(buffer);
                    if (read == 0) break;
                    await destination.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    var percent = totalBytes is > 0
                        ? (int)Math.Min(100, downloaded * 100 / totalBytes.Value)
                        : -1;
                    if (percent == lastPercent) continue;
                    lastPercent = percent;
                    if (percent >= 0)
                    {
                        SetupProgressBar.IsIndeterminate = false;
                        SetupProgressBar.Value = percent;
                        UpdateSetupActivity(
                            $"正在下載 SteamCMD：{percent}%（{downloaded / 1048576d:0.0} / {totalBytes!.Value / 1048576d:0.0} MB）",
                            false);
                    }
                    else
                    {
                        UpdateSetupActivity($"正在下載 SteamCMD：{downloaded / 1048576d:0.0} MB", false);
                    }
                }
            }
            UpdateSetupActivity("正在驗證下載檔案…");
            var signature = new byte[2];
            await using (var zipStream = File.OpenRead(temporaryZip))
                _ = await zipStream.ReadAsync(signature);
            if (signature[0] != (byte)'P' || signature[1] != (byte)'K')
                throw new InvalidDataException("Valve 下載結果不是有效的 ZIP 格式，已拒絕安裝。");
            SetupProgressBar.IsIndeterminate = true;
            UpdateSetupActivity("下載完成，正在解壓縮 SteamCMD…");
            ZipFile.ExtractToDirectory(temporaryZip, directory, true);
            var extracted = Path.Combine(directory, "steamcmd.exe");
            if (!File.Exists(extracted)) throw new InvalidDataException("壓縮檔內找不到 steamcmd.exe。");
            WizardSteamCmdPathBox.Text = extracted;
            SyncWizardPaths();
            PersistSettings();
            succeeded = true;
            finalMessage = "SteamCMD 安裝完成，現在可以安裝 PZ Dedicated Server。";
        }
        catch (Exception ex)
        {
            finalMessage = $"SteamCMD 安裝失敗：{ex.Message}";
            MessageBox.Show($"SteamCMD 下載或解壓失敗：\n{ex.Message}\n\n你仍可手動下載後使用「選擇 steamcmd.exe」。",
                "SteamCMD 安裝失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (File.Exists(temporaryZip)) File.Delete(temporaryZip);
            EndSetupActivity(succeeded, finalMessage);
            RefreshSetupStage();
        }
    }

    private void BeginSetupActivity(string title, string detail)
    {
        setupOperationRunning = true;
        setupActivityStarted = DateTime.Now;
        SetupActivityPanel.Visibility = Visibility.Visible;
        SetupActivityDot.Fill = BrushFrom("#E1A84B");
        LocalizationService.SetText(SetupActivityTitleText, title);
        LocalizationService.SetText(SetupActivityDetailText, detail);
        LocalizationService.SetText(SetupStatusText, "作業執行中，請勿關閉管理器。");
        SetupLiveOutputBox.Clear();
        SetupProgressBar.Value = 0;
        SetupProgressBar.IsIndeterminate = true;
        SetupProgressBar.Visibility = Visibility.Visible;
        SetSetupControlsEnabled(false);
        AppendSetupOutput(detail);
        UpdateSetupElapsed();
        setupActivityTimer.Start();
    }

    private void UpdateSetupActivity(string detail, bool addToOutput = true)
    {
        LocalizationService.SetText(SetupActivityDetailText, detail);
        if (addToOutput) AppendSetupOutput(detail);
    }

    private void AppendSetupOutput(string line, bool isError = false)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        void Append()
        {
            var text = $"[{DateTime.Now:HH:mm:ss}] {(isError ? "[錯誤] " : "")}{line.Trim()}{Environment.NewLine}";
            SetupLiveOutputBox.AppendText(text);
            if (SetupLiveOutputBox.Text.Length > 50000)
                SetupLiveOutputBox.Text = SetupLiveOutputBox.Text[^40000..];
            SetupLiveOutputBox.ScrollToEnd();
            SetupActivityDetailText.Text = line.Trim();
        }
        if (Dispatcher.CheckAccess()) Append();
        else Dispatcher.BeginInvoke(Append);
    }

    private void EndSetupActivity(bool success, string message)
    {
        setupActivityTimer.Stop();
        setupOperationRunning = false;
        UpdateSetupElapsed();
        SetupProgressBar.Visibility = Visibility.Collapsed;
        SetupProgressBar.IsIndeterminate = true;
        SetupActivityDot.Fill = BrushFrom(success ? "#4FC18B" : "#DF6B62");
        LocalizationService.SetText(SetupActivityTitleText, success ? "作業完成" : "作業失敗");
        LocalizationService.SetText(SetupActivityDetailText, message);
        LocalizationService.SetText(SetupStatusText, message);
        AppendSetupOutput(message, !success);
        SetSetupControlsEnabled(true);
    }

    private void UpdateSetupElapsed()
    {
        var elapsed = setupActivityStarted.HasValue
            ? DateTime.Now - setupActivityStarted.Value
            : TimeSpan.Zero;
        LocalizationService.SetFormattedText(SetupElapsedText, "已執行 {0}",
            elapsed.ToString(@"hh\:mm\:ss"));
    }

    private void SetSetupControlsEnabled(bool enabled)
    {
        SteamSetupActions.IsEnabled = enabled;
        ServerSetupActions.IsEnabled = enabled;
        BrowseSteamCmdButton.IsEnabled = enabled;
        WizardSteamCmdPathBox.IsEnabled = enabled;
        WizardInstallPathBox.IsEnabled = enabled;
        InstallButton.IsEnabled = enabled;
        WizardInstallServerButton.IsEnabled = enabled;
    }

    private void SyncWizardPaths()
    {
        settings.SteamCmdPath = WizardSteamCmdPathBox.Text.Trim();
        settings.InstallDirectory = WizardInstallPathBox.Text.Trim();
        SteamCmdPathBox.Text = settings.SteamCmdPath;
        InstallPathBox.Text = settings.InstallDirectory;
    }

    private void MergeSettingsTabs()
    {
        var basicContent = BasicSettingsTab.Content;
        var fullContent = FullSettingsTab.Content;
        BasicSettingsTab.Content = null;
        FullSettingsTab.Content = null;
        MainTabs.Items.Remove(BasicSettingsTab);
        MainTabs.Items.Remove(FullSettingsTab);
        var settingsPages = new TabControl { Margin = new Thickness(8) };
        settingsPages.Items.Add(new TabItem { Header = "基礎設定", Content = basicContent });
        settingsPages.Items.Add(new TabItem { Header = "完整設定", Content = fullContent });
        MainTabs.Items.Insert(1, new TabItem { Header = "伺服器設定", Content = settingsPages });
    }

    private void InitializeSettingChoices()
    {
        // Build 42 Stable 使用可直接輸入的 0.00–4.00 物資倍率。
        UiLanguageCombo.ItemsSource = LocalizationService.AvailableLanguages;
    }

    private void LoadSettings()
    {
        try
        {
            var candidates = new[] { ExeSettingsFile, LocalSettingsFile }
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
            settingsFile = candidates.FirstOrDefault()?.FullName ?? ExeSettingsFile;
            if (File.Exists(settingsFile))
                settings = JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(settingsFile)) ?? new();
            // 清除舊版管理器自行發明的文字；只比對完全相同的舊值，
            // 使用者自己的公開名稱與說明一律保留。
            if (settings.PublicName == "我的 Build 42 伺服器") settings.PublicName = "";
            if (settings.Description == "由 PZ Server Manager 管理") settings.Description = "";
            if (settings.WelcomeMessage == "歡迎來到伺服器！") settings.WelcomeMessage = "";
            settings.SettingsStorage = string.Equals(settingsFile, LocalSettingsFile, StringComparison.OrdinalIgnoreCase)
                ? "LocalAppData" : "ExeDirectory";
            Log($"Manager 設定來源：{settingsFile}");
        }
        catch (Exception ex)
        {
            settingsFile = ExeSettingsFile;
            Log($"讀取設定失敗：{ex.Message}");
        }
        SettingsToUi();
    }

    private void SettingsToUi()
    {
        SteamCmdPathBox.Text = settings.SteamCmdPath; InstallPathBox.Text = settings.InstallDirectory;
        DataPathBox.Text = settings.DataDirectory; ServerNameBox.Text = settings.ServerName;
        PublicNameBox.Text = settings.PublicName; DescriptionBox.Text = settings.Description;
        PasswordBox.Password = settings.Password; AdminPasswordBox.Password = settings.AdminPassword;
        BetaBox.Text = settings.BetaBranch; PortBox.Text = settings.DefaultPort.ToString();
        UdpPortBox.Text = settings.UDPPort.ToString(); PlayersBox.Text = settings.MaxPlayers.ToString();
        MemoryBox.Text = settings.MemoryGb.ToString(); PublicCheck.IsChecked = settings.Public;
        PauseCheck.IsChecked = settings.PauseEmpty; OpenCheck.IsChecked = settings.Open;
        AutoRestartCheck.IsChecked = settings.AutoRestart; RestartHoursBox.Text = settings.RestartHours.ToString();
        WarningMinutesBox.Text = settings.WarningMinutes.ToString(); BackupCheck.IsChecked = settings.BackupBeforeRestart;
        PlayerQueryMinutesBox.Text = settings.PlayerQueryMinutes.ToString();
        RestartMessageBox.Text = settings.RestartWarningMessage;
        AutoWorkshopUpdateCheck.IsChecked = settings.AutoWorkshopUpdate;
        WorkshopUpdateCheckMinutesBox.Text =
            settings.WorkshopUpdateCheckMinutes.ToString(CultureInfo.InvariantCulture);
        WorkshopUpdateBroadcastCheck.IsChecked = settings.WorkshopUpdateBroadcast;
        WorkshopUpdateAnnouncementMinutesBox.Text =
            settings.WorkshopUpdateAnnouncementMinutes.ToString(CultureInfo.InvariantCulture);
        WorkshopUpdateMessageBox.Text = settings.WorkshopUpdateWarningMessage;
        UpdateWorkshopAutomationControlState();
        SelectByTag(ConfigEncodingCombo, settings.ConfigEncoding);
        SelectByTag(SettingsStorageCombo, settings.SettingsStorage);
        SelectByTag(UiFontCombo, settings.UiFontFamily);
        UiLanguageCombo.SelectedValue = settings.UiLanguage;
        if (UiLanguageCombo.SelectedValue == null)
            UiLanguageCombo.SelectedValue = "zh-TW";
        ApplyUiFont(SelectedUiFont());
        ApplyUiLanguage(SelectedUiLanguage());
        PvpCheck.IsChecked = settings.Pvp; SafetyCheck.IsChecked = settings.SafetySystem;
        SleepAllowedCheck.IsChecked = settings.SleepAllowed; SleepNeededCheck.IsChecked = settings.SleepNeeded;
        VoiceCheck.IsChecked = settings.VoiceEnable; SafehouseCheck.IsChecked = settings.PlayerSafehouse;
        PingLimitBox.Text = settings.PingLimit.ToString(); SaveMinutesBox.Text = settings.SaveEveryMinutes.ToString();
        BuiltInBackupsBox.Text = settings.BuiltInBackups.ToString(); LootRespawnHoursBox.Text = settings.LootRespawnHours.ToString();
        CharacterFreePointsBox.Text = settings.CharacterFreePoints.ToString();
        SpawnItemsBox.Text = settings.SpawnItems; StarterKitCheck.IsChecked = settings.StarterKit;
        SelectByTag(StatsDecreaseCombo, settings.StatsDecrease);
        SelectByTag(EndRegenCombo, settings.EndRegen);
        NutritionCheck.IsChecked = settings.Nutrition;
        SelectByTag(InjurySeverityCombo, settings.InjurySeverity);
        BoneFractureCheck.IsChecked = settings.BoneFracture;
        SelectByTag(ClothingDegradationCombo, settings.ClothingDegradation);
        MultiHitZombiesCheck.IsChecked = settings.MultiHitZombies;
        SelectByTag(RearVulnerabilityCombo, settings.RearVulnerability);
        SelectByTag(BloodLevelCombo, settings.BloodLevel);
        PlayerDamageFromCrashCheck.IsChecked = settings.PlayerDamageFromCrash;
        WelcomeBox.Text = settings.WelcomeMessage; WorkshopBox.Text = settings.WorkshopItems; ModsBox.Text = settings.Mods;
        MapFoldersBox.Text = settings.MapFolders;
        ResolvedModsText.Text = string.IsNullOrWhiteSpace(settings.Mods) ? "尚未解析" : $"已解析：{settings.Mods}";
        RconPortBox.Text = settings.RconPort.ToString(); RconPasswordBox.Password = settings.RconPassword;
        SelectByTag(DayLengthCombo, settings.DayLength); WaterDaysBox.Text = settings.WaterShutDays.ToString();
        ElectricDaysBox.Text = settings.ElectricityShutDays.ToString(); XpBox.Text = LuaNumber(settings.XpMultiplier);
        FoodLootBox.Text = LuaNumber(settings.FoodLoot); WeaponLootBox.Text = LuaNumber(settings.WeaponLoot);
        AmmoLootBox.Text = LuaNumber(settings.AmmoLoot); MedicalLootBox.Text = LuaNumber(settings.MedicalLoot);
        OtherLootBox.Text = LuaNumber(settings.OtherLoot);
        SelectByTag(ZombieSpeedCombo, settings.ZombieSpeed); SelectByTag(ZombieStrengthCombo, settings.ZombieStrength);
        SelectByTag(ZombieToughnessCombo, settings.ZombieToughness); SelectByTag(TransmissionCombo, settings.Transmission);
        PopulationBox.Text = settings.PopulationMultiplier.ToString(); PeakPopulationBox.Text = settings.PopulationPeakMultiplier.ToString();
        PeakDayBox.Text = settings.PopulationPeakDay.ToString(); ZombieRespawnBox.Text = settings.RespawnHours.ToString();
        AllowNonAsciiUsernameCheck.IsChecked = settings.AllowNonAsciiUsername;
        AnnounceDeathCheck.IsChecked = settings.AnnounceDeath;
        MaxAccountsPerUserBox.Text = settings.MaxAccountsPerUser.ToString();
        SelectByTag(MapRemotePlayerVisibilityBox, settings.MapRemotePlayerVisibility);
        PlayerRespawnWithSelfCheck.IsChecked = settings.PlayerRespawnWithSelf;
        PlayerRespawnWithOtherCheck.IsChecked = settings.PlayerRespawnWithOther;
        SafehouseAllowRespawnCheck.IsChecked = settings.SafehouseAllowRespawn;
        FactionCheck.IsChecked = settings.Faction;
        FactionDaysBox.Text = settings.FactionDaySurvivedToCreate.ToString();
        SafehouseDaysBox.Text = settings.SafehouseDaySurvivedToClaim.ToString();
        SafehouseRemovalHoursBox.Text = settings.SafeHouseRemovalTime.ToString();
        PvpFirearmDamageBox.Text = LuaNumber(settings.PvpFirearmDamageModifier);
        PvpMeleeDamageBox.Text = LuaNumber(settings.PvpMeleeDamageModifier);
        SpeedLimitBox.Text = LuaNumber(settings.SpeedLimit);
        DenyOverloadCheck.IsChecked = settings.DenyLoginOnOverloadedServer;
        LoginQueueCheck.IsChecked = settings.LoginQueueEnabled;
        LoginQueueTimeoutBox.Text = settings.LoginQueueConnectTimeout.ToString();

        // The text fields and the resolved grid must always represent the same
        // on-disk INI state.  The constructor loads settings before the UI is
        // initialized, so defer the local Workshop scan until later reloads.
        if (uiInitialized && !string.IsNullOrWhiteSpace(settings.WorkshopItems))
        {
            var workshopIds = NormalizeWorkshopList(settings.WorkshopItems)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (workshopIds.Count > 0 && workshopIds.All(id => ulong.TryParse(id, out _)))
                FindInstalledModEntries(workshopIds, false);
        }
        else if (uiInitialized)
        {
            resolvedModEntries.Clear();
            ResolvedModsGrid.ItemsSource = null;
            ResolvedModsText.Text = "尚未設定 Workshop 模組。";
        }
    }

    private bool UiToSettings(bool showError = true)
    {
        if (resolvedModEntries.Count > 0 && !ApplyResolvedMods(false, showError)) return false;
        if (spawnRegionSelectionTouched && !ValidateMapSelections(showError)) return false;
        var autoWorkshopUpdate = AutoWorkshopUpdateCheck.IsChecked == true;
        var workshopUpdateBroadcast = WorkshopUpdateBroadcastCheck.IsChecked == true;
        var workshopCheckMinutesValid =
            int.TryParse(WorkshopUpdateCheckMinutesBox.Text, out var workshopCheckMinutes) &&
            workshopCheckMinutes is >= 1 and <= 1440;
        var workshopAnnouncementMinutesValid =
            int.TryParse(WorkshopUpdateAnnouncementMinutesBox.Text, out var workshopAnnouncementMinutes) &&
            workshopAnnouncementMinutes is >= 1 and <= 1440;
        if (!workshopCheckMinutesValid && !autoWorkshopUpdate)
            workshopCheckMinutes = settings.WorkshopUpdateCheckMinutes is >= 1 and <= 1440
                ? settings.WorkshopUpdateCheckMinutes : 5;
        if (!workshopAnnouncementMinutesValid &&
            (!autoWorkshopUpdate || !workshopUpdateBroadcast))
            workshopAnnouncementMinutes = settings.WorkshopUpdateAnnouncementMinutes is >= 1 and <= 1440
                ? settings.WorkshopUpdateAnnouncementMinutes : 30;
        if (!int.TryParse(PortBox.Text, out var port) || port is < 0 or > 65535 ||
            !int.TryParse(UdpPortBox.Text, out var udp) || udp is < 0 or > 65535 ||
            !int.TryParse(PlayersBox.Text, out var players) || players is < 1 or > 100 ||
            !int.TryParse(MemoryBox.Text, out var memory) || memory is < 2 or > 128 ||
            !int.TryParse(RestartHoursBox.Text, out var hours) || hours is < 1 or > 168 ||
            !int.TryParse(WarningMinutesBox.Text, out var warning) || warning is < 0 or > 60 ||
            !int.TryParse(PlayerQueryMinutesBox.Text, out var playerQueryMinutes) ||
                playerQueryMinutes is < 1 or > 1440 ||
            (autoWorkshopUpdate && !workshopCheckMinutesValid) ||
            (autoWorkshopUpdate && workshopUpdateBroadcast && !workshopAnnouncementMinutesValid))
        {
            if (showError) MessageBox.Show("請檢查連接埠、玩家數、記憶體與排程欄位。", "設定格式錯誤",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var name = ServerNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            if (showError) MessageBox.Show("設定檔名稱不可空白或包含非法檔名字元。");
            return false;
        }
        settings = new ServerSettings {
            SteamCmdPath = SteamCmdPathBox.Text.Trim(), InstallDirectory = InstallPathBox.Text.Trim(),
            DataDirectory = DataPathBox.Text.Trim(), ServerName = name, PublicName = PublicNameBox.Text.Trim(),
            Description = DescriptionBox.Text.Trim(), Password = PasswordBox.Password,
            AdminPassword = AdminPasswordBox.Password, BetaBranch = BetaBox.Text.Trim(),
            ConfigEncoding = SelectedEncodingMode(),
            SettingsStorage = SelectedSettingsStorage(),
            UiFontFamily = SelectedUiFont(),
            UiLanguage = SelectedUiLanguage(),
            DefaultPort = port, UDPPort = udp, MaxPlayers = players, MemoryGb = memory,
            Public = PublicCheck.IsChecked == true, PauseEmpty = PauseCheck.IsChecked == true,
            Open = OpenCheck.IsChecked == true, Pvp = PvpCheck.IsChecked == true,
            SafetySystem = SafetyCheck.IsChecked == true, SleepAllowed = SleepAllowedCheck.IsChecked == true,
            SleepNeeded = SleepNeededCheck.IsChecked == true, VoiceEnable = VoiceCheck.IsChecked == true,
            PlayerSafehouse = SafehouseCheck.IsChecked == true, WelcomeMessage = WelcomeBox.Text,
            WorkshopItems = NormalizeWorkshopList(WorkshopBox.Text),
            Mods = NormalizeSemicolonList(ModsBox.Text),
            MapFolders = NormalizeSemicolonList(MapFoldersBox.Text),
            RconPassword = RconPasswordBox.Password, AutoRestart = AutoRestartCheck.IsChecked == true,
            RestartHours = hours, WarningMinutes = warning, PlayerQueryMinutes = playerQueryMinutes,
            BackupBeforeRestart = BackupCheck.IsChecked == true,
            AutoWorkshopUpdate = autoWorkshopUpdate,
            WorkshopUpdateCheckMinutes = workshopCheckMinutes,
            WorkshopUpdateBroadcast = workshopUpdateBroadcast,
            WorkshopUpdateAnnouncementMinutes = workshopAnnouncementMinutes
        };
        settings.RestartWarningMessage = string.IsNullOrWhiteSpace(RestartMessageBox.Text)
            ? new ServerSettings().RestartWarningMessage : RestartMessageBox.Text.Trim();
        settings.WorkshopUpdateWarningMessage = string.IsNullOrWhiteSpace(WorkshopUpdateMessageBox.Text)
            ? new ServerSettings().WorkshopUpdateWarningMessage : WorkshopUpdateMessageBox.Text.Trim();
        if (ContainsIrrecoverableTextLoss(settings.Description) ||
            ContainsIrrecoverableTextLoss(settings.WelcomeMessage))
        {
            if (showError) MessageBox.Show(
                "「說明」或「玩家加入歡迎訊息」包含 � 或連續的 ???。\n\n" +
                "這代表先前轉碼已遺失字元，無法再由 UTF-8／Big5 推算原文。請從 `.manager-backup` 還原，或重新輸入正確文字；目前不會寫入設定。",
                "偵測到不可逆亂碼", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        if (!TryReadFullSettings(showError)) return false;
        return true;
    }

    private static bool ContainsIrrecoverableTextLoss(string text) =>
        text.Contains('\uFFFD') ||
        System.Text.RegularExpressions.Regex.IsMatch(text, @"\?{3,}");

    private bool TryReadFullSettings(bool showError)
    {
        int ping = 0, saveMinutes = 0, backups = 0, lootHours = 0, rconPort = 0;
        int water = 0, electric = 0, peakDay = 0, characterFreePoints = 0;
        int maxAccounts = 0, mapVisibility = 0, factionDays = 0, safehouseDays = 0;
        int safehouseRemoval = 0, loginQueueTimeout = 0;
        double xp = 0, population = 0, peak = 0, respawn = 0;
        double pvpFirearm = 0, pvpMelee = 0, speedLimit = 0;
        double foodLoot = 0, weaponLoot = 0, ammoLoot = 0, medicalLoot = 0, otherLoot = 0;
        var errors = new List<string>();

        CheckInt(PingLimitBox.Text, "Ping 上限", 100, int.MaxValue, out ping, errors);
        CheckInt(SaveMinutesBox.Text, "自動存檔分鐘", 0, int.MaxValue, out saveMinutes, errors);
        CheckInt(BuiltInBackupsBox.Text, "遊戲內建備份數", 1, 300, out backups, errors);
        CheckInt(LootRespawnHoursBox.Text, "物資重生小時", 0, int.MaxValue, out lootHours, errors);
        CheckInt(CharacterFreePointsBox.Text, "創角額外點數", -100, 100, out characterFreePoints, errors);
        CheckInt(RconPortBox.Text, "RCON TCP 連接埠", 0, 65535, out rconPort, errors);
        CheckInt(WaterDaysBox.Text, "停水天數", -1, int.MaxValue, out water, errors);
        CheckInt(ElectricDaysBox.Text, "停電天數", -1, int.MaxValue, out electric, errors);
        CheckDouble(XpBox.Text, "全域經驗倍率", 0, 1000, out xp, errors);
        CheckDouble(FoodLootBox.Text, "食物物資倍率", 0, 4, out foodLoot, errors);
        CheckDouble(WeaponLootBox.Text, "近戰／工具物資倍率", 0, 4, out weaponLoot, errors);
        CheckDouble(AmmoLootBox.Text, "彈藥物資倍率", 0, 4, out ammoLoot, errors);
        CheckDouble(MedicalLootBox.Text, "醫療物資倍率", 0, 4, out medicalLoot, errors);
        CheckDouble(OtherLootBox.Text, "其他物資倍率", 0, 4, out otherLoot, errors);
        CheckDouble(PopulationBox.Text, "殭屍人口倍率", 0, 4, out population, errors);
        CheckDouble(PeakPopulationBox.Text, "巔峰人口倍率", 0, 4, out peak, errors);
        CheckInt(PeakDayBox.Text, "人口巔峰日", 1, 365, out peakDay, errors);
        CheckDouble(ZombieRespawnBox.Text, "殭屍重生小時", 0, 8760, out respawn, errors);
        CheckInt(MaxAccountsPerUserBox.Text, "每位 Steam 使用者帳號上限", 0, int.MaxValue, out maxAccounts, errors);
        mapVisibility = SelectedTag(MapRemotePlayerVisibilityBox, 1);
        CheckInt(FactionDaysBox.Text, "建立派系所需生存天數", 0, int.MaxValue, out factionDays, errors);
        CheckInt(SafehouseDaysBox.Text, "建立安全屋所需生存天數", 0, int.MaxValue, out safehouseDays, errors);
        CheckInt(SafehouseRemovalHoursBox.Text, "安全屋未使用移除小時", 0, int.MaxValue, out safehouseRemoval, errors);
        CheckDouble(PvpFirearmDamageBox.Text, "PVP 槍械傷害倍率", 0, 500, out pvpFirearm, errors);
        CheckDouble(PvpMeleeDamageBox.Text, "PVP 近戰傷害倍率", 0, 500, out pvpMelee, errors);
        CheckDouble(SpeedLimitBox.Text, "車輛速限", 10, 150, out speedLimit, errors);
        CheckInt(LoginQueueTimeoutBox.Text, "登入佇列連線逾時", 20, 1200, out loginQueueTimeout, errors);

        if (errors.Count > 0)
        {
            if (showError) MessageBox.Show("請修正以下欄位：\n\n" + string.Join("\n", errors),
                "設定格式錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!TryValidateSpawnItems(SpawnItemsBox.Text, out var spawnItemsError))
        {
            if (showError) MessageBox.Show(spawnItemsError, "出生物品 ID 無效",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        settings.PingLimit = ping; settings.SaveEveryMinutes = saveMinutes; settings.BuiltInBackups = backups;
        settings.LootRespawnHours = lootHours; settings.RconPort = rconPort;
        settings.CharacterFreePoints = characterFreePoints;
        settings.SpawnItems = SpawnItemsBox.Text.Trim();
        settings.StarterKit = StarterKitCheck.IsChecked == true;
        settings.StatsDecrease = SelectedTag(StatsDecreaseCombo, 3);
        settings.EndRegen = SelectedTag(EndRegenCombo, 3);
        settings.Nutrition = NutritionCheck.IsChecked == true;
        settings.InjurySeverity = SelectedTag(InjurySeverityCombo, 2);
        settings.BoneFracture = BoneFractureCheck.IsChecked == true;
        settings.ClothingDegradation = SelectedTag(ClothingDegradationCombo, 3);
        settings.MultiHitZombies = MultiHitZombiesCheck.IsChecked == true;
        settings.RearVulnerability = SelectedTag(RearVulnerabilityCombo, 3);
        settings.BloodLevel = SelectedTag(BloodLevelCombo, 3);
        settings.PlayerDamageFromCrash = PlayerDamageFromCrashCheck.IsChecked == true;
        settings.DayLength = SelectedTag(DayLengthCombo, 3); settings.WaterShutDays = water;
        settings.ElectricityShutDays = electric; settings.XpMultiplier = xp;
        settings.FoodLoot = foodLoot; settings.WeaponLoot = weaponLoot;
        settings.AmmoLoot = ammoLoot; settings.MedicalLoot = medicalLoot; settings.OtherLoot = otherLoot;
        settings.ZombieSpeed = SelectedTag(ZombieSpeedCombo, 2);
        settings.ZombieStrength = SelectedTag(ZombieStrengthCombo, 2);
        settings.ZombieToughness = SelectedTag(ZombieToughnessCombo, 2);
        settings.Transmission = SelectedTag(TransmissionCombo, 1);
        settings.PopulationMultiplier = population; settings.PopulationPeakMultiplier = peak;
        settings.PopulationPeakDay = peakDay; settings.RespawnHours = respawn;
        settings.AllowNonAsciiUsername = AllowNonAsciiUsernameCheck.IsChecked == true;
        settings.AnnounceDeath = AnnounceDeathCheck.IsChecked == true;
        settings.MaxAccountsPerUser = maxAccounts;
        settings.MapRemotePlayerVisibility = mapVisibility;
        settings.PlayerRespawnWithSelf = PlayerRespawnWithSelfCheck.IsChecked == true;
        settings.PlayerRespawnWithOther = PlayerRespawnWithOtherCheck.IsChecked == true;
        settings.SafehouseAllowRespawn = SafehouseAllowRespawnCheck.IsChecked == true;
        settings.Faction = FactionCheck.IsChecked == true;
        settings.FactionDaySurvivedToCreate = factionDays;
        settings.SafehouseDaySurvivedToClaim = safehouseDays;
        settings.SafeHouseRemovalTime = safehouseRemoval;
        settings.PvpFirearmDamageModifier = pvpFirearm;
        settings.PvpMeleeDamageModifier = pvpMelee;
        settings.SpeedLimit = speedLimit;
        settings.DenyLoginOnOverloadedServer = DenyOverloadCheck.IsChecked == true;
        settings.LoginQueueEnabled = LoginQueueCheck.IsChecked == true;
        settings.LoginQueueConnectTimeout = loginQueueTimeout;
        return true;
    }

    private static void CheckInt(string text, string name, int minimum, int maximum,
        out int value, List<string> errors)
    {
        if (int.TryParse(text.Trim(), out value) && value >= minimum && value <= maximum) return;
        errors.Add($"• {name}：目前「{text}」，允許 {minimum}–{maximum}");
    }

    private static void CheckDouble(string text, string name, double minimum, double maximum,
        out double value, List<string> errors)
    {
        if (TryParseFlexibleDouble(text, out value) && value >= minimum && value <= maximum) return;
        errors.Add($"• {name}：目前「{text}」，允許 {LuaNumber(minimum)}–{LuaNumber(maximum)}");
    }

    private static bool TryParseFlexibleDouble(string text, out double value)
    {
        var normalized = text.Trim();
        if (normalized.Contains(',') && !normalized.Contains('.')) normalized = normalized.Replace(',', '.');
        return double.TryParse(normalized, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }
    private void PersistSettings()
    {
        var target = settings.SettingsStorage == "LocalAppData" ? LocalSettingsFile : ExeSettingsFile;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var temporary = target + ".tmp";
            File.WriteAllText(temporary,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            File.Move(temporary, target, true);

            if (settings.SettingsStorage == "LocalAppData" &&
                !string.Equals(target, ExeSettingsFile, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(ExeSettingsFile))
            {
                var backup = Path.Combine(AppContext.BaseDirectory, "manager-settings.exe-location.backup.json");
                File.Move(ExeSettingsFile, backup, true);
            }
            settingsFile = target;
            Log($"Manager 設定已儲存：{settingsFile}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"無法儲存 Manager 設定：\n{target}\n\n{ex.Message}\n\n若 EXE 資料夾不可寫，請改選 AppData。",
                "Manager 設定儲存失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void VerifyWindowsServerEnvironment()
    {
        if (!Environment.Is64BitOperatingSystem || !Environment.Is64BitProcess)
        {
            MessageBox.Show("此管理器與 Build 42 Dedicated Server 需要 64 位元 Windows。",
                "不支援的系統", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var os = Environment.OSVersion.Version;
        LocalizationService.SetFormattedText(FooterText,
            "管理器 v{0} • Build 42 Stable • App ID 380870 • Windows {1} x64",
            AppVersion, os);
        Log($"環境檢查：Windows {os}、64 位元程序、.NET {Environment.Version}。");
        if (!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")))
            Log("警告：未偵測到 Desktop Experience。Windows Server Core 無法顯示 WPF GUI。");
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var onboarding = SetupOverlay.Visibility == Visibility.Visible;
        var activityStarted = false;
        var succeeded = false;
        var finalMessage = "PZ Dedicated Server 安裝／更新失敗。";
        try
        {
            if (SetupOverlay.Visibility == Visibility.Visible) SyncWizardPaths();
            if (!UiToSettings()) return;
            if (!File.Exists(settings.SteamCmdPath))
            {
                MessageBox.Show("找不到 SteamCMD。請先下載 steamcmd.exe，並在「基礎設定」填入正確路徑。");
                return;
            }
            PersistSettings();
            Directory.CreateDirectory(settings.InstallDirectory);
            var beta = string.IsNullOrWhiteSpace(settings.BetaBranch) ? "" : $" -beta \"{settings.BetaBranch}\"";
            var args = $"+force_install_dir \"{settings.InstallDirectory}\" +login anonymous +app_update 380870{beta} validate +quit";
            InstallButton.IsEnabled = false;
            WizardInstallServerButton.IsEnabled = false;
            if (onboarding)
            {
                BeginSetupActivity("正在安裝 PZ Dedicated Server",
                    "SteamCMD 已啟動，正在登入並檢查 App ID 380870…");
                activityStarted = true;
            }
            SetStatus("正在安裝 / 更新", "#E1A84B");
            Log("開始透過 SteamCMD 安裝 / 更新 Project Zomboid Dedicated Server…");
            var code = await RunProcessAsync(settings.SteamCmdPath, args,
                Path.GetDirectoryName(settings.SteamCmdPath)!,
                onboarding ? AppendSetupOutput : null);
            Log(code == 0 ? "SteamCMD 作業完成。" : $"SteamCMD 結束，代碼 {code}。請查看上方輸出。");
            if (code == 0)
            {
                Log("更新完成；未自動改寫設定檔。請先讀取目前設定，再由設定頁儲存變更。");
                succeeded = true;
                finalMessage = "PZ Dedicated Server 安裝／更新完成。";
            }
            else
                finalMessage = $"SteamCMD 結束代碼為 {code}，請查看即時輸出。";
            if (IsPzServerInstalled())
            {
                ScanExistingServers();
                TryLoadExistingConfigSafely(true);
                MainTabs.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            finalMessage = $"PZ Dedicated Server 安裝／更新失敗：{ex.Message}";
            Log($"檢查／修復失敗：{ex}");
            MessageBox.Show($"檢查／修復失敗，但管理器會繼續執行：\n{ex.Message}",
                "SteamCMD 作業失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (activityStarted) EndSetupActivity(succeeded, finalMessage);
            else
            {
                InstallButton.IsEnabled = true;
                WizardInstallServerButton.IsEnabled = true;
            }
            RefreshSetupStage();
            SetStatus(serverProcess is { HasExited: false } ? "執行中" : "已停止",
                serverProcess is { HasExited: false } ? "#4FC18B" : "#71808A");
        }
    }

    private async Task<int> RunProcessAsync(string file, string args, string workingDirectory,
        Action<string, bool>? liveOutput = null)
    {
        using var p = new Process {
            StartInfo = new ProcessStartInfo(file, args) {
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            }
        };
        p.OutputDataReceived += (_, a) => {
            if (a.Data == null) return;
            Log(a.Data);
            liveOutput?.Invoke(a.Data, false);
        };
        p.ErrorDataReceived += (_, a) => {
            if (a.Data == null) return;
            Log("[錯誤] " + a.Data);
            liveOutput?.Invoke(a.Data, true);
        };
        try
        {
            if (!p.Start()) return -1;
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            Log($"無法執行外部作業：{ex.Message}");
            return -1;
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveConfigurationAndVerify();
    }

    private void WriteServerConfig()
    {
        EnsureExplicitConfigWriteAuthorized();
        lastConfigWriteSucceeded = false;
        var snapshots = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var serverDir = Path.Combine(settings.DataDirectory, "Server");
            Directory.CreateDirectory(serverDir);
            var iniPath = Path.Combine(serverDir, settings.ServerName + ".ini");
            var sandboxPath = Path.Combine(serverDir, settings.ServerName + "_SandboxVars.lua");
            var spawnRegionsPath = Path.Combine(serverDir, settings.ServerName + "_spawnregions.lua");
            snapshots[iniPath] = File.Exists(iniPath) ? File.ReadAllBytes(iniPath) : null;
            snapshots[sandboxPath] = File.Exists(sandboxPath) ? File.ReadAllBytes(sandboxPath) : null;
            if (spawnRegionSelectionTouched)
                snapshots[spawnRegionsPath] = File.Exists(spawnRegionsPath)
                    ? File.ReadAllBytes(spawnRegionsPath) : null;
            var iniExisted = File.Exists(iniPath);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["DefaultPort"] = settings.DefaultPort.ToString(), ["UDPPort"] = settings.UDPPort.ToString(),
                ["MaxPlayers"] = settings.MaxPlayers.ToString(), ["Public"] = Bool(settings.Public),
                ["PublicName"] = Clean(settings.PublicName), ["PublicDescription"] = Clean(settings.Description),
                ["Password"] = Clean(settings.Password), ["PauseEmpty"] = Bool(settings.PauseEmpty),
                ["Open"] = Bool(settings.Open), ["AutoCreateUserInWhiteList"] = Bool(settings.Open),
                ["DoLuaChecksum"] = "true", ["SteamVAC"] = "true", ["PVP"] = Bool(settings.Pvp),
                ["SafetySystem"] = Bool(settings.SafetySystem), ["SleepAllowed"] = Bool(settings.SleepAllowed),
                ["SleepNeeded"] = Bool(settings.SleepNeeded), ["VoiceEnable"] = Bool(settings.VoiceEnable),
                ["PlayerSafehouse"] = Bool(settings.PlayerSafehouse), ["PingLimit"] = settings.PingLimit.ToString(),
                ["SaveWorldEveryMinutes"] = settings.SaveEveryMinutes.ToString(),
                ["BackupsCount"] = settings.BuiltInBackups.ToString(),
                ["SpawnItems"] = settings.SpawnItems,
                ["ServerWelcomeMessage"] = Clean(settings.WelcomeMessage),
                ["WorkshopItems"] = settings.WorkshopItems, ["Mods"] = settings.Mods,
                ["Map"] = settings.MapFolders,
                ["RCONPort"] = settings.RconPort.ToString(), ["RCONPassword"] = Clean(settings.RconPassword),
                ["AllowNonAsciiUsername"] = Bool(settings.AllowNonAsciiUsername),
                ["AnnounceDeath"] = Bool(settings.AnnounceDeath),
                ["MaxAccountsPerUser"] = settings.MaxAccountsPerUser.ToString(),
                ["MapRemotePlayerVisibility"] = settings.MapRemotePlayerVisibility.ToString(),
                ["PlayerRespawnWithSelf"] = Bool(settings.PlayerRespawnWithSelf),
                ["PlayerRespawnWithOther"] = Bool(settings.PlayerRespawnWithOther),
                ["SafehouseAllowRespawn"] = Bool(settings.SafehouseAllowRespawn),
                ["Faction"] = Bool(settings.Faction),
                ["FactionDaySurvivedToCreate"] = settings.FactionDaySurvivedToCreate.ToString(),
                ["SafehouseDaySurvivedToClaim"] = settings.SafehouseDaySurvivedToClaim.ToString(),
                ["SafeHouseRemovalTime"] = settings.SafeHouseRemovalTime.ToString(),
                ["PVPFirearmDamageModifier"] = LuaNumber(settings.PvpFirearmDamageModifier),
                ["PVPMeleeDamageModifier"] = LuaNumber(settings.PvpMeleeDamageModifier),
                ["SpeedLimit"] = LuaNumber(settings.SpeedLimit),
                ["DenyLoginOnOverloadedServer"] = Bool(settings.DenyLoginOnOverloadedServer),
                ["LoginQueueEnabled"] = Bool(settings.LoginQueueEnabled),
                ["LoginQueueConnectTimeout"] = settings.LoginQueueConnectTimeout.ToString()
            };
            var lines = iniExisted ? ConfigFileEncoding.ReadAllLines(iniPath, SelectedEncodingMode()).ToList() : new List<string>();
            foreach (var pair in values)
            {
                var pattern = new System.Text.RegularExpressions.Regex(
                    $@"^\s*{System.Text.RegularExpressions.Regex.Escape(pair.Key)}\s*=",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var indices = Enumerable.Range(0, lines.Count).Where(i => pattern.IsMatch(lines[i])).ToList();
                if (indices.Count == 0) lines.Add($"{pair.Key}={pair.Value}");
                else
                {
                    // PZ 設定檔若有重複鍵，遊戲實際採用哪一筆並不適合依賴；
                    // 將所有同名鍵寫成一致值，避免 GUI 與伺服器各讀到不同結果。
                    foreach (var index in indices) lines[index] = $"{pair.Key}={pair.Value}";
                }
            }
            var expectedIni = string.Join(Environment.NewLine, lines);
            ConfigFileEncoding.WritePreservingEncoding(iniPath, expectedIni, SelectedEncodingMode());
            VerifyWrittenText(iniPath, expectedIni);
            WriteSandboxConfig(serverDir);
            WriteManagedSpawnRegions(serverDir);
            NormalizeEncodingModeAfterRepair();
            CaptureConfigState();
            UpdateMemoryConfig();
            lastConfigWriteSucceeded = true;
            spawnRegionSelectionTouched = false;
            Log($"已寫入基礎設定：{iniPath}");
        }
        catch (Exception ex)
        {
            foreach (var snapshot in snapshots)
            {
                try
                {
                    if (snapshot.Value == null)
                    {
                        if (File.Exists(snapshot.Key)) File.Delete(snapshot.Key);
                    }
                    else File.WriteAllBytes(snapshot.Key, snapshot.Value);
                }
                catch (Exception rollbackEx) { Log($"回復寫入前內容失敗：{snapshot.Key}：{rollbackEx.Message}"); }
            }
            // 整合測試沒有 WPF Application；直接回傳原始例外，避免 MessageBox 阻塞。
            if (Application.Current == null) throw;
            MessageBox.Show($"寫入設定失敗，已嘗試回復 INI、Sandbox 與重生區域的寫入前內容：\n{ex.Message}",
                "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WriteSandboxConfig(string serverDir)
    {
        var path = Path.Combine(serverDir, settings.ServerName + "_SandboxVars.lua");
        var sandboxExisted = File.Exists(path);
        if (!sandboxExisted)
        {
            File.WriteAllText(path, """
SandboxVars = {
    VERSION = 6,
    ZombieLore = {
    },
    ZombieConfig = {
    },
    MultiplierConfig = {
    },
}
""", new UTF8Encoding(false));
        }
        var text = ConfigFileEncoding.ReadText(path, SelectedEncodingMode());
        if (!IsBuild42StableSandbox(text))
            throw new InvalidDataException("完整設定只支援目前 Build 42 Sandbox VERSION = 6；檔案未被修改。");

        var top = new Dictionary<string, string> {
            ["DayLength"] = settings.DayLength.ToString(),
            ["WaterShutModifier"] = settings.WaterShutDays.ToString(),
            ["ElecShutModifier"] = settings.ElectricityShutDays.ToString(),
            ["FoodLootNew"] = LuaNumber(settings.FoodLoot),
            ["WeaponLootNew"] = LuaNumber(settings.WeaponLoot),
            ["AmmoLootNew"] = LuaNumber(settings.AmmoLoot),
            ["MedicalLootNew"] = LuaNumber(settings.MedicalLoot),
            ["OtherLootNew"] = LuaNumber(settings.OtherLoot),
            ["HoursForLootRespawn"] = settings.LootRespawnHours.ToString(),
            ["CharacterFreePoints"] = settings.CharacterFreePoints.ToString(),
            ["StarterKit"] = Bool(settings.StarterKit),
            ["StatsDecrease"] = settings.StatsDecrease.ToString(),
            ["EndRegen"] = settings.EndRegen.ToString(),
            ["Nutrition"] = Bool(settings.Nutrition),
            ["InjurySeverity"] = settings.InjurySeverity.ToString(),
            ["BoneFracture"] = Bool(settings.BoneFracture),
            ["ClothingDegradation"] = settings.ClothingDegradation.ToString(),
            ["MultiHitZombies"] = Bool(settings.MultiHitZombies),
            ["RearVulnerability"] = settings.RearVulnerability.ToString(),
            ["BloodLevel"] = settings.BloodLevel.ToString(),
            ["PlayerDamageFromCrash"] = Bool(settings.PlayerDamageFromCrash)
        };
        foreach (var pair in top) text = ReplaceLuaValue(text, pair.Key, pair.Value, null, true);
        text = ReplaceLuaValue(text, "Global", LuaNumber(settings.XpMultiplier), "MultiplierConfig", true);
        text = ReplaceLuaValue(text, "GlobalToggle", "true", "MultiplierConfig", true);
        text = ReplaceLuaValue(text, "Speed", settings.ZombieSpeed.ToString(), "ZombieLore", true);
        text = ReplaceLuaValue(text, "Strength", settings.ZombieStrength.ToString(), "ZombieLore", true);
        text = ReplaceLuaValue(text, "Toughness", settings.ZombieToughness.ToString(), "ZombieLore", true);
        text = ReplaceLuaValue(text, "Transmission", settings.Transmission.ToString(), "ZombieLore", true);
        text = ReplaceLuaValue(text, "PopulationMultiplier", LuaNumber(settings.PopulationMultiplier), "ZombieConfig", true);
        text = ReplaceLuaValue(text, "PopulationPeakMultiplier", LuaNumber(settings.PopulationPeakMultiplier), "ZombieConfig", true);
        text = ReplaceLuaValue(text, "PopulationPeakDay", settings.PopulationPeakDay.ToString(), "ZombieConfig", true);
        text = ReplaceLuaValue(text, "RespawnHours", LuaNumber(settings.RespawnHours), "ZombieConfig", true);
        ConfigFileEncoding.WritePreservingEncoding(path, text, SelectedEncodingMode());
        VerifyWrittenText(path, text);
        Log($"已寫入 Build 42 Sandbox（保留原 VERSION）設定：{path}");
    }

    private void WriteManagedSpawnRegions(string serverDir)
    {
        if (!spawnRegionSelectionTouched) return;
        const string beginMarker = "-- PZServerManager BEGIN MOD SPAWN REGIONS";
        const string endMarker = "-- PZServerManager END MOD SPAWN REGIONS";
        var path = Path.Combine(serverDir, settings.ServerName + "_spawnregions.lua");
        var selected = resolvedMapEntries.Where(entry =>
            entry.Enabled && entry.SpawnEnabled && !string.IsNullOrWhiteSpace(entry.SpawnPointsFile)).ToList();
        if (!File.Exists(path) && selected.Count == 0) return;
        var entries = selected.Select(entry =>
            $"        {{ name = \"{EscapeLuaString(entry.MapFolder)}\", " +
            $"file = \"media/maps/{EscapeLuaString(entry.MapFolder)}/spawnpoints.lua\" }},");
        var block = beginMarker + Environment.NewLine +
            string.Join(Environment.NewLine, entries) + Environment.NewLine + endMarker;

        string text;
        if (File.Exists(path))
            text = ConfigFileEncoding.ReadText(path, SelectedEncodingMode());
        else
            text = "function SpawnRegions()" + Environment.NewLine +
                   "    return {" + Environment.NewLine +
                   "    }" + Environment.NewLine +
                   "end" + Environment.NewLine;

        var managedPattern = Regex.Escape(beginMarker) + @"[\s\S]*?" + Regex.Escape(endMarker);
        var hasManagedBlock = Regex.IsMatch(text, managedPattern);
        if (!hasManagedBlock && selected.Count == 0) return;
        if (hasManagedBlock)
            text = Regex.Replace(text, managedPattern, _ => block);
        else
        {
            var tableEnds = Regex.Matches(text, @"(?m)^\s*}\s*$").Cast<Match>().ToList();
            var returnTableEnd = tableEnds.LastOrDefault();
            if (returnTableEnd == null)
                throw new InvalidDataException(
                    $"{Path.GetFileName(path)} 找不到 SpawnRegions 回傳表格結尾；未修改重生區域。");
            text = text.Insert(returnTableEnd.Index, "    " +
                block.Replace(Environment.NewLine, Environment.NewLine + "    ") +
                Environment.NewLine);
        }

        ConfigFileEncoding.WritePreservingEncoding(path, text, SelectedEncodingMode());
        VerifyWrittenText(path, text);
        Log($"已更新管理器重生區域區塊：{path}（{selected.Count} 個）");
    }

    private static string EscapeLuaString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private void VerifyWrittenText(string path, string expected)
    {
        var verificationMode = SelectedEncodingMode().Equals("RepairUtf8FromBig5", StringComparison.OrdinalIgnoreCase)
            ? "Auto" : SelectedEncodingMode();
        var actual = ConfigFileEncoding.ReadText(path, verificationMode);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new IOException($"{Path.GetFileName(path)} 寫入後由磁碟重讀不一致，已拒絕回報儲存成功。");
    }

    private void NormalizeEncodingModeAfterRepair()
    {
        if (!SelectedEncodingMode().Equals("RepairUtf8FromBig5", StringComparison.OrdinalIgnoreCase)) return;
        uiInitialized = false;
        SelectByTag(ConfigEncodingCombo, "Auto");
        uiInitialized = true;
        settings.ConfigEncoding = "Auto";
        PersistSettings();
        Log("可逆亂碼已以 UTF-8 寫回；設定檔編碼已自動切回「自動」。");
    }
    private static string ReplaceLuaValue(string text, string key, string value, string? section, bool allowInsert)
    {
        var start = 0; var end = text.Length;
        if (section != null)
        {
            start = text.IndexOf(section + " = {", StringComparison.Ordinal);
            if (start < 0) return text;
            end = FindLuaSectionEnd(text, text.IndexOf('{', start));
        }
        var segment = text[start..end];
        var pattern = $@"(?m)^(\s*{System.Text.RegularExpressions.Regex.Escape(key)}\s*=\s*)[^,\r\n]+";
        var replaced = new System.Text.RegularExpressions.Regex(pattern).Replace(segment, $"${{1}}{value}", 1);
        if (replaced == segment)
        {
            if (!allowInsert) return text;
            var insert = section == null ? text.LastIndexOf('}') : end - 1;
            return text.Insert(insert, $"    {key} = {value},{Environment.NewLine}");
        }
        return text[..start] + replaced + text[end..];
    }

    private static int FindLuaSectionEnd(string text, int opening)
    {
        var depth = 0;
        for (var i = opening; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i + 1;
        }
        return text.Length;
    }

    private void UpdateMemoryConfig()
    {
        var jsonPath = new[] { "ProjectZomboid64.json", "ProjectZomboid32.json" }
            .Select(x => Path.Combine(settings.InstallDirectory, x)).FirstOrDefault(File.Exists);
        if (jsonPath == null) return;
        var text = File.ReadAllText(jsonPath);
        text = System.Text.RegularExpressions.Regex.Replace(text, "\"-Xmx\\d+[gGmM]\"", $"\"-Xmx{settings.MemoryGb}g\"");
        File.WriteAllText(jsonPath, text, new UTF8Encoding(false));
        Log($"已將 JVM 最大記憶體設為 {settings.MemoryGb} GB。");
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await StartServerAsync(true);

    private Task StartServerAsync(bool interactive)
    {
        if (serverProcess is { HasExited: false }) return Task.CompletedTask;
        if (interactive && !UiToSettings(true))
        {
            return Task.CompletedTask;
        }
        if (interactive)
            PersistSettings();
        else
            Log("自動啟動使用上次已驗證並儲存的設定；不重新套用 GUI，也不顯示阻塞式對話框。");
        var launcher = new[] { "StartServer64.bat", "start-server.bat", "StartServer32.bat" }
            .Select(x => Path.Combine(settings.InstallDirectory, x)).FirstOrDefault(File.Exists);
        if (launcher == null)
        {
            ReportStartBlocker(interactive,
                "找不到伺服器啟動批次檔，請先執行「安裝 / 更新伺服器」。",
                "找不到啟動批次檔");
            return Task.CompletedTask;
        }
        var databasePath = Path.Combine(settings.DataDirectory, "db", settings.ServerName + ".db");
        if (!File.Exists(databasePath) && string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            ReportStartBlocker(interactive,
                "Build 42 首次建立伺服器資料庫時必須設定管理員密碼。\n\n" +
                "請在「基礎設定」填入管理員密碼後再啟動。管理器尚未修改任何 PZ 檔案。",
                "首次啟動需要管理員密碼");
            return Task.CompletedTask;
        }
        currentStartInteractive = interactive;
        intentionalStop = false;
        roleInitializationFailure = false;
        ResetRuntimeForNewServerProcess();
        var iniPath = Path.Combine(settings.DataDirectory, "Server", settings.ServerName + ".ini");
        var (serverTextEncoding, javaCharset) = DetectServerTextEncoding(iniPath);
        var args = $"/d /s /c \"\"{launcher}\" -servername \"{settings.ServerName}\"";
        if (!string.IsNullOrWhiteSpace(settings.AdminPassword))
            args += $" -adminpassword \"{settings.AdminPassword.Replace("\"", "")}\"";
        args += "\"";
        var startInfo = new ProcessStartInfo("cmd.exe", args) {
                WorkingDirectory = settings.InstallDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = serverTextEncoding,
                StandardOutputEncoding = serverTextEncoding,
                StandardErrorEncoding = serverTextEncoding
            };
        var inheritedJavaOptions = Environment.GetEnvironmentVariable("JAVA_TOOL_OPTIONS") ?? "";
        startInfo.Environment["JAVA_TOOL_OPTIONS"] =
            $"{inheritedJavaOptions} -Dfile.encoding={javaCharset}".Trim();
        Log($"PZ 文字編碼：{serverTextEncoding.WebName}；JVM -Dfile.encoding={javaCharset}。" +
            $" Windows 目前語系：{CultureInfo.CurrentCulture.Name}（ANSI {CultureInfo.CurrentCulture.TextInfo.ANSICodePage}）。");
        serverProcess = new Process {
            StartInfo = startInfo, EnableRaisingEvents = true
        };
        serverProcess.OutputDataReceived += (_, a) => { if (a.Data != null) HandleServerOutput(a.Data, false); };
        serverProcess.ErrorDataReceived += (_, a) => { if (a.Data != null) HandleServerOutput(a.Data, true); };
        serverProcess.Exited += ServerProcess_Exited;
        try
        {
            serverProcess.Start(); serverProcess.BeginOutputReadLine(); serverProcess.BeginErrorReadLine();
            StartButton.IsEnabled = false; StopButton.IsEnabled = true;
            SaveWorldButton.IsEnabled = true;
            PlayerQueryMinutesBox.IsEnabled = false;
            SetStatus("執行中", "#4FC18B"); Log($"伺服器已啟動（PID {serverProcess.Id}）。");
            if (settings.PauseEmpty)
                Log("Build 42 風險提示：目前 PauseEmpty=true；斷線或快速重連時若發生主迴圈卡死，建議關閉「無玩家時暫停世界」。");
            nextRestart = null;
            nextPlayerQuery = null;
            nextWorkshopUpdateCheck = null;
            UpdateWorkshopAutomationControlState();
        }
        catch (Exception ex)
        {
            Log($"啟動失敗：{ex.Message}");
            PlayerQueryMinutesBox.IsEnabled = true;
            AutoWorkshopUpdateCheck.IsEnabled = true;
            WorkshopUpdateCheckMinutesBox.IsEnabled = true;
            WorkshopUpdateBroadcastCheck.IsEnabled = true;
            WorkshopUpdateAnnouncementMinutesBox.IsEnabled = true;
            WorkshopUpdateMessageBox.IsEnabled = true;
            serverProcess.Dispose();
            serverProcess = null;
            nextWorkshopUpdateCheck = null;
            UpdateWorkshopAutomationControlState();
            UpdateWorkshopUpdateStatus();
        }
        return Task.CompletedTask;
    }

    private void HandleServerOutput(string line, bool isError)
    {
        Log(isError ? "[伺服器] " + line : line);
        if (capturePlayerQueryOutput)
        {
            lock (playerQueryLock) playerQueryOutput.Add(line);
            if (IsPlayerQueryTerminalLine(line))
                playerQueryResponseSignal?.TrySetResult(true);
        }
        var normalized = line.Trim();
        if (!serverReadyForCommands &&
            normalized.Contains("*** SERVER STARTED ****", StringComparison.OrdinalIgnoreCase))
        {
            serverReadyForCommands = true;
            _ = Dispatcher.InvokeAsync(() =>
            {
                Log("PZ 已回報 SERVER STARTED；現在才啟動 CLI 健康檢查與自動化計時，避免載入模組期間誤報。");
                ScheduleNextRestart();
                ScheduleWorkshopUpdateCheck();
                nextPlayerQuery = DateTime.Now;
            });
        }
        if (normalized.Contains("Roles.getDefaultForUser()", StringComparison.OrdinalIgnoreCase))
            roleInitializationFailure = true;
        if (!normalized.EndsWith(">PAUSE", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("Press any key to continue", StringComparison.OrdinalIgnoreCase) &&
            !normalized.Contains("請按任意鍵繼續", StringComparison.OrdinalIgnoreCase))
            return;
        var pausedProcess = serverProcess;
        if (pausedProcess == null) return;
        Log("偵測到啟動批次檔的 PAUSE；PZ JVM 已返回，正在關閉殘留的 cmd.exe 外殼。");
        try
        {
            if (!pausedProcess.HasExited) pausedProcess.Kill(false);
        }
        catch (InvalidOperationException) { }
        catch (Exception ex) { Log($"關閉批次外殼失敗：{ex.Message}"); }
    }

    private void ServerProcess_Exited(object? sender, EventArgs e)
    {
        var exitedProcess = sender as Process;
        var code = 0;
        try { code = exitedProcess?.ExitCode ?? 0; } catch { }
        _ = Dispatcher.InvokeAsync(async () =>
        {
            Log($"伺服器程序已結束（代碼 {code}）。");
            StartButton.IsEnabled = true; StopButton.IsEnabled = false;
            SaveWorldButton.IsEnabled = false;
            PlayerQueryMinutesBox.IsEnabled = true;
            AutoWorkshopUpdateCheck.IsEnabled = true;
            WorkshopUpdateCheckMinutesBox.IsEnabled = true;
            WorkshopUpdateBroadcastCheck.IsEnabled = true;
            WorkshopUpdateAnnouncementMinutesBox.IsEnabled = true;
            WorkshopUpdateMessageBox.IsEnabled = true;
            SetStatus("已停止", "#71808A"); nextRestart = null; UpdateNextRestartText();
            CancelRuntimePipelines();
            ClearCliHealthState();
            nextPlayerQuery = null;
            serverReadyForCommands = false;
            nextWorkshopUpdateCheck = null;
            lastKnownOnlinePlayerCount = null;
            OnlinePlayersListBox.ItemsSource = null;
            OnlinePlayerSummaryText.Text = "伺服器未啟動";
            if (ReferenceEquals(serverProcess, exitedProcess)) serverProcess = null;
            UpdateWorkshopAutomationControlState();
            try { exitedProcess?.Dispose(); } catch { }
            if (roleInitializationFailure)
            {
                roleInitializationFailure = false;
                var roleFailureMessage =
                    "Build 42 無法從目前資料庫取得預設使用者角色，因此伺服器在建立世界前退出。\n\n" +
                    "這通常是舊版或未完成初始化的 db\\<伺服器名稱>.db，不是 map_t.bin 或 Sandbox 編碼錯誤。\n\n" +
                    "請依序處理：\n" +
                    "1. 確認「基礎設定」的管理員密碼不是空白。\n" +
                    "2. 先改用一個全新的設定檔名稱啟動，確認 B42 能建立乾淨資料庫（不會刪除舊資料）。\n" +
                    "3. 若仍失敗，暫時清空 WorkshopItems 與 Mods，以純原版啟動。\n" +
                    "4. 純原版成功後，再逐項加入你要使用的 Workshop 模組，找出造成初始化失敗的項目。\n\n" +
                    "管理器沒有刪除、重建或覆寫你的資料庫與世界存檔。";
                if (currentStartInteractive)
                    MessageBox.Show(roleFailureMessage,
                        "Build 42 角色資料庫初始化失敗",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    Log("自動啟動失敗：" + roleFailureMessage.Replace("\n", " "));
            }
            if (restartAfterStop)
            {
                var automatedRestart = restartAfterStopAutomated;
                restartAfterStop = false;
                restartAfterStopAutomated = false;
                workshopRestartInProgress = false;
                if (automatedRestart && automationRuntimeSuspended)
                    Log("自動化已暫停，因此取消本次自動重新啟動。");
                else
                {
                    Log("5 秒後重新啟動…");
                    await Task.Delay(5000);
                    await StartServerAsync(false);
                }
            }
            else
            {
                workshopRestartInProgress = false;
                if (!intentionalStop) Log("偵測到非預期停止；未自動啟動，請檢查主控台。");
                UpdateWorkshopUpdateStatus();
            }
            if (closeAfterServerStops)
            {
                closeAfterServerStops = false;
                Close();
            }
        }, DispatcherPriority.Send);
    }

    private async void Stop_Click(object sender, RoutedEventArgs e) => await SafeStopAsync(false);

    private void ReportStartBlocker(bool interactive, string message, string title)
    {
        Log($"{title}：{message.Replace("\r", " ").Replace("\n", " ")}");
        if (interactive)
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            SetStatus("自動啟動失敗", "#D9695F");
    }

    private void SaveWorld_Click(object sender, RoutedEventArgs e)
    {
        if (serverProcess is not { HasExited: false })
        {
            MessageBox.Show("伺服器尚未啟動。");
            return;
        }
        SendCommand("save");
        Log("已手動送出存檔指令；伺服器會在背景完成磁碟寫入。");
    }

    private async Task SafeStopAsync(bool restart, bool automated = false,
        CancellationToken cancellationToken = default)
    {
        if (serverProcess is not { HasExited: false } || !await stopLock.WaitAsync(0)) return;
        try
        {
            intentionalStop = true;
            restartAfterStop = restart;
            restartAfterStopAutomated = automated;
            nextRestart = null;
            nextWorkshopUpdateAnnouncement = null;
            StopButton.IsEnabled = false; SetStatus(restart ? "正在安全重啟" : "正在安全關服", "#E1A84B");
            SendCommand("servermsg \"伺服器正在存檔，請稍候…\"");
            SendCommand("save");
            Log("已送出 save，等待磁碟寫入…");
            await Task.Delay(8000, cancellationToken);
            if (restart && settings.BackupBeforeRestart) await CreateBackupAsync();
            cancellationToken.ThrowIfCancellationRequested();
            SendCommand("quit");
            Log("已送出 quit，等待伺服器正常退出…");
            var waitUntil = DateTime.UtcNow.AddSeconds(45);
            while (serverProcess is { HasExited: false } && DateTime.UtcNow < waitUntil)
                await Task.Delay(500, cancellationToken);
            if (serverProcess is { HasExited: false })
            {
                restartAfterStop = false;
                restartAfterStopAutomated = false;
                Log("45 秒內未退出；為保護存檔，不強制終止程序。");
                ActivateCliHealthAlarm("安全關服已等待 45 秒，PZ 仍未退出。");
                StopButton.IsEnabled = true;
            }
        }
        catch (OperationCanceledException) when (automated)
        {
            restartAfterStop = false;
            restartAfterStopAutomated = false;
            workshopRestartInProgress = false;
            if (serverProcess is { HasExited: false })
            {
                StopButton.IsEnabled = true;
                SetStatus(cliHealthAlarmActive ? "PZ CLI 無回應／自動化已暫停" : "執行中",
                    cliHealthAlarmActive ? "#D9695F" : "#4FC18B");
            }
            Log("自動關服流程已取消；尚未再送出後續 quit 指令。");
        }
        finally { stopLock.Release(); }
    }

    private void SendCommand(string command)
    {
        var target = serverProcess;
        if (target is not { HasExited: false }) return;
        pendingServerCommands.Enqueue((target, command));
        StartCommandWriter();
    }

    private void StartCommandWriter()
    {
        if (Interlocked.CompareExchange(ref commandWriterRunning, 1, 0) != 0) return;
        _ = DrainServerCommandQueueAsync();
    }

    private async Task DrainServerCommandQueueAsync()
    {
        try
        {
            while (pendingServerCommands.TryDequeue(out var item))
            {
                var cancellation = commandWriterCancellation;
                if (cancellation == null || cancellation.IsCancellationRequested ||
                    !ReferenceEquals(serverProcess, item.Process) || item.Process.HasExited)
                    continue;
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    await item.Process.StandardInput.WriteLineAsync(
                        item.Command.AsMemory(), timeout.Token).ConfigureAwait(false);
                    await item.Process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
                    Log($"> {item.Command}");
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    ClearPendingServerCommands();
                    Log("PZ 標準輸入管線 3 秒內未接受指令；GUI 未被阻塞，已觸發 CLI 健康警報。");
                    await Dispatcher.InvokeAsync(() =>
                        ActivateCliHealthAlarm("PZ 標準輸入管線已阻塞，指令無法送入。"));
                    break;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log($"指令傳送失敗：{ex.Message}");
                    if (ReferenceEquals(serverProcess, item.Process) && !item.Process.HasExited)
                        await Dispatcher.InvokeAsync(() =>
                            RegisterCliHealthFailure("PZ 指令管線發生錯誤。"));
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref commandWriterRunning, 0);
            if (!pendingServerCommands.IsEmpty) StartCommandWriter();
        }
    }

    private void ClearPendingServerCommands()
    {
        while (pendingServerCommands.TryDequeue(out _)) { }
    }

    private static (Encoding StreamEncoding, string JavaCharset) DetectServerTextEncoding(string iniPath)
    {
        if (!File.Exists(iniPath)) return (new UTF8Encoding(false), "UTF-8");
        var detected = ConfigFileEncoding.Read(iniPath, "Auto").Encoding;
        return detected.CodePage switch
        {
            950 => (Encoding.GetEncoding(950), "Big5"),
            1200 => (Encoding.Unicode, "UTF-16LE"),
            1201 => (Encoding.BigEndianUnicode, "UTF-16BE"),
            _ => (new UTF8Encoding(false), "UTF-8")
        };
    }

    private void SendCommand_Click(object sender, RoutedEventArgs e) => SendTypedCommand();
    private void CommandBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { SendTypedCommand(); e.Handled = true; } }
    private void SendTypedCommand()
    {
        var command = CommandBox.Text.Trim();
        if (command.Length == 0) return;
        if (serverProcess is not { HasExited: false }) { MessageBox.Show("伺服器尚未啟動。"); return; }
        if (command.Equals("quit", StringComparison.OrdinalIgnoreCase))
            CancelCurrentSessionAutomationForManualQuit();
        SendCommand(command); CommandBox.Clear();
    }

    private void ResetRuntimeForNewServerProcess()
    {
        commandWriterCancellation?.Cancel();
        commandWriterCancellation?.Dispose();
        commandWriterCancellation = new CancellationTokenSource();
        ClearPendingServerCommands();
        RenewAutomationCancellation();
        consecutiveCliResponseFailures = 0;
        cliHealthAlarmActive = false;
        automationRuntimeSuspended = false;
        restartAfterStopAutomated = false;
        serverReadyForCommands = false;
        CliHealthBanner.Visibility = Visibility.Collapsed;
        ForceTerminateFrozenServerButton.IsEnabled = false;
    }

    private void RenewAutomationCancellation()
    {
        automationCancellation.Cancel();
        automationCancellation.Dispose();
        automationCancellation = new CancellationTokenSource();
    }

    private void CancelRuntimePipelines()
    {
        automationCancellation.Cancel();
        commandWriterCancellation?.Cancel();
        ClearPendingServerCommands();
        playerQueryResponseSignal?.TrySetCanceled();
    }

    private void ClearCliHealthState()
    {
        consecutiveCliResponseFailures = 0;
        cliHealthAlarmActive = false;
        automationRuntimeSuspended = false;
        CliHealthBanner.Visibility = Visibility.Collapsed;
        ForceTerminateFrozenServerButton.IsEnabled = false;
    }

    private static bool IsPlayerQueryTerminalLine(string line) =>
        Regex.IsMatch(line, @"(?i)players?\s+connected\s*\(\d+\)") ||
        Regex.IsMatch(line, @"(?i)no\s+players?\s+(?:are\s+)?connected");

    private void RegisterCliHealthFailure(string reason)
    {
        if (serverProcess is not { HasExited: false }) return;
        consecutiveCliResponseFailures++;
        if (consecutiveCliResponseFailures >= 2)
        {
            ActivateCliHealthAlarm(reason);
            return;
        }

        CliHealthBanner.Visibility = Visibility.Visible;
        CliHealthBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4B3B1E"));
        CliHealthBanner.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E1A84B"));
        LocalizationService.SetFormattedText(CliHealthText,
            "PZ CLI 第一次未回應：{0} 30 秒後重試；目前尚未判定卡死。",
            LocalizationService.Translate(reason));
        ForceTerminateFrozenServerButton.IsEnabled = false;
        nextPlayerQuery = DateTime.Now.AddSeconds(30);
        Log("CLI 健康檢查第一次逾時；30 秒後重試。LOG 持續輸出不代表 PZ 主迴圈仍正常。");
    }

    private void ActivateCliHealthAlarm(string reason)
    {
        if (serverProcess is not { HasExited: false }) return;
        var firstActivation = !cliHealthAlarmActive;
        consecutiveCliResponseFailures = Math.Max(2, consecutiveCliResponseFailures);
        cliHealthAlarmActive = true;
        automationRuntimeSuspended = true;
        automationCancellation.Cancel();
        nextRestart = null;
        nextWorkshopUpdateCheck = null;
        nextWorkshopUpdateAnnouncement = null;
        nextPlayerQuery = DateTime.Now.AddSeconds(30);
        workshopRestartInProgress = false;
        if (restartAfterStopAutomated)
        {
            restartAfterStop = false;
            restartAfterStopAutomated = false;
        }
        UpdateNextRestartText();
        CliHealthBanner.Visibility = Visibility.Visible;
        CliHealthBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#482724"));
        CliHealthBanner.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D9695F"));
        LocalizationService.SetFormattedText(CliHealthText,
            "PZ CLI 無回應：{0} 已暫停全部自動化。請先測試回應；必要時才手動強制終止。",
            LocalizationService.Translate(reason));
        ForceTerminateFrozenServerButton.IsEnabled = true;
        SetStatus("PZ CLI 無回應／自動化已暫停", "#D9695F");
        if (firstActivation)
            Log("警報：連續兩次 `players` 沒有實際回傳，已暫停定時重啟、Workshop 檢查與公告。管理器不會自行強制關閉程序。");
    }

    private void RegisterCliHealthSuccess()
    {
        var wasAbnormal = consecutiveCliResponseFailures > 0 || cliHealthAlarmActive;
        consecutiveCliResponseFailures = 0;
        if (cliHealthAlarmActive || automationRuntimeSuspended)
        {
            cliHealthAlarmActive = false;
            automationRuntimeSuspended = false;
            RenewAutomationCancellation();
            ScheduleNextRestart();
            ScheduleWorkshopUpdateCheck();
            SetStatus("執行中", "#4FC18B");
        }
        CliHealthBanner.Visibility = Visibility.Collapsed;
        ForceTerminateFrozenServerButton.IsEnabled = false;
        if (wasAbnormal) Log("PZ CLI 已重新回傳 `players` 結果；健康警報解除並恢復已啟用的自動化。");
    }

    private void CancelCurrentSessionAutomationForManualQuit()
    {
        intentionalStop = true;
        restartAfterStop = false;
        restartAfterStopAutomated = false;
        workshopRestartInProgress = false;
        automationCancellation.Cancel();
        nextRestart = null;
        nextWorkshopUpdateCheck = null;
        nextWorkshopUpdateAnnouncement = null;
        pendingWorkshopUpdateIds.Clear();
        UpdateNextRestartText();
        UpdateWorkshopUpdateStatus();
        Log("偵測到手動 quit：已取消本次工作階段的重啟倒數與模組更新自動化，不會在退出後自行重開。 ");
    }

    private async void TestCliResponse_Click(object sender, RoutedEventArgs e)
    {
        await QueryOnlinePlayersAsync();
    }

    private async void ForceTerminateFrozenServer_Click(object sender, RoutedEventArgs e)
    {
        var target = serverProcess;
        if (!cliHealthAlarmActive || target is not { HasExited: false }) return;
        var answer = MessageBox.Show(
            LocalizationService.Translate(
                "這只會終止目前由管理器啟動的 PZ 程序樹，不會關閉 Windows VM。\n\n" +
                "PZ CLI 已無法執行 save／quit；強制終止可能遺失最近一次成功存檔後的進度。確定繼續嗎？"),
            LocalizationService.Translate("強制終止卡死的 PZ 程序"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        intentionalStop = true;
        restartAfterStop = false;
        restartAfterStopAutomated = false;
        ForceTerminateFrozenServerButton.IsEnabled = false;
        SetStatus("正在強制終止卡死程序", "#D9695F");
        CancelRuntimePipelines();
        Log($"使用者已確認強制終止卡死的 PZ 程序樹（PID {target.Id}）。");
        try
        {
            await Task.Run(() =>
            {
                if (!target.HasExited) target.Kill(entireProcessTree: true);
                target.WaitForExit(15000);
            });
        }
        catch (Exception ex)
        {
            Log($"強制終止失敗：{ex.Message}");
            if (target is { HasExited: false }) ForceTerminateFrozenServerButton.IsEnabled = true;
        }
    }

    private async void Backup_Click(object sender, RoutedEventArgs e) => await CreateBackupAsync(true);

    private async Task CreateBackupAsync(bool notify = false)
    {
        try
        {
            var source = Path.Combine(settings.DataDirectory, "Saves", "Multiplayer", settings.ServerName);
            if (!Directory.Exists(source)) { if (notify) MessageBox.Show("尚未找到此伺服器的存檔資料夾。"); return; }
            var backupDir = Path.Combine(settings.DataDirectory, "Backups");
            Directory.CreateDirectory(backupDir);
            var target = Path.Combine(backupDir, $"{settings.ServerName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            await Task.Run(() => ZipFile.CreateFromDirectory(source, target, CompressionLevel.Fastest, false));
            Log($"備份完成：{target}");
            if (notify) MessageBox.Show($"備份完成：\n{target}", "完成");
        }
        catch (Exception ex) { Log($"備份失敗：{ex.Message}"); if (notify) MessageBox.Show(ex.Message, "備份失敗"); }
    }

    private void SaveSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (!UiToSettings()) return;
        PersistSettings();
        ScheduleNextRestart();
        ScheduleWorkshopUpdateCheck();
        MessageBox.Show("自動化設定已儲存。");
    }

    private void SaveFullConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveConfigurationAndVerify();
    }

    private void SaveConfigurationAndVerify()
    {
        if (!UiToSettings()) return;
        if (!EnsureModIdsResolved()) return;
        if (!CanWriteConfiguration()) return;
        var expected = CloneSettings(settings);
        RunExplicitConfigWrite(WriteServerConfig);
        if (!lastConfigWriteSucceeded) return;
        var mismatches = CompareManagedPzFiles(expected);
        if (mismatches.Count > 0)
        {
            settings = expected;
            SettingsToUi();
            lastConfigWriteSucceeded = false;
            MessageBox.Show("檔案寫入後直接由磁碟逐欄驗證失敗，因此不會顯示成功，GUI 也會保留你輸入的值：\n\n" +
                string.Join("\n", mismatches.Take(12)) +
                (mismatches.Count > 12 ? $"\n…另有 {mismatches.Count - 12} 項" : ""),
                "寫入後值不一致", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (!TryLoadExistingConfig(true))
        {
            settings = expected;
            SettingsToUi();
            MessageBox.Show("檔案已寫入，但無法由磁碟重新載入；GUI 保留你輸入的值，未回報儲存成功。",
                "寫入後驗證失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        mismatches = CompareManagedPzSettings(expected, settings);
        if (mismatches.Count > 0)
        {
            settings = expected;
            SettingsToUi();
            lastConfigWriteSucceeded = false;
            MessageBox.Show("寫入後由磁碟讀回的值與輸入不一致，因此不會顯示成功，也不會讓 GUI 跳回舊值：\n\n" +
                string.Join("\n", mismatches.Take(12)) +
                (mismatches.Count > 12 ? $"\n…另有 {mismatches.Count - 12} 項" : ""),
                "寫入後值不一致", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        PersistSettings();
        MessageBox.Show("INI 與 SandboxVars 已寫入，且所有 GUI 管理欄位均已由磁碟重新讀回核對一致。",
            "儲存與驗證完成", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private List<string> CompareManagedPzFiles(ServerSettings expected)
    {
        var expectedValues = ManagedPzValues(expected);
        var actualValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var iniPath = Path.Combine(expected.DataDirectory, "Server", expected.ServerName + ".ini");
        var sandboxPath = Path.Combine(expected.DataDirectory, "Server", expected.ServerName + "_SandboxVars.lua");
        var iniText = File.Exists(iniPath) ? ConfigFileEncoding.ReadText(iniPath, SelectedEncodingMode()) : "";
        var sandboxText = File.Exists(sandboxPath) ? ConfigFileEncoding.ReadText(sandboxPath, SelectedEncodingMode()) : "";

        foreach (var key in expectedValues.Keys)
        {
            if (key.StartsWith("Sandbox.", StringComparison.OrdinalIgnoreCase))
            {
                TryReadLuaValue(sandboxText, key["Sandbox.".Length..], null, out var value);
                if (value != null) actualValues[key] = value;
            }
            else if (key.StartsWith("MultiplierConfig.", StringComparison.OrdinalIgnoreCase))
            {
                TryReadLuaValue(sandboxText, key["MultiplierConfig.".Length..], "MultiplierConfig", out var value);
                if (value != null) actualValues[key] = value;
            }
            else if (key.StartsWith("ZombieLore.", StringComparison.OrdinalIgnoreCase))
            {
                TryReadLuaValue(sandboxText, key["ZombieLore.".Length..], "ZombieLore", out var value);
                if (value != null) actualValues[key] = value;
            }
            else if (key.StartsWith("ZombieConfig.", StringComparison.OrdinalIgnoreCase))
            {
                TryReadLuaValue(sandboxText, key["ZombieConfig.".Length..], "ZombieConfig", out var value);
                if (value != null) actualValues[key] = value;
            }
            else if (TryReadIniValue(iniText, key, out var value))
            {
                actualValues[key] = value;
            }
        }

        return expectedValues
            .Where(pair => !actualValues.TryGetValue(pair.Key, out var actualValue) ||
                !string.Equals(pair.Value, actualValue, StringComparison.OrdinalIgnoreCase))
            .Select(pair => $"• {pair.Key}：輸入「{pair.Value}」／磁碟「" +
                (actualValues.TryGetValue(pair.Key, out var value) ? value : "缺少此設定") + "」")
            .ToList();
    }

    private static bool TryReadIniValue(string text, string key, out string value)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(text,
            $@"(?im)^\s*{System.Text.RegularExpressions.Regex.Escape(key)}\s*=(.*)$");
        if (matches.Count == 0)
        {
            value = "";
            return false;
        }
        value = matches[^1].Groups[1].Value.TrimEnd('\r');
        return true;
    }

    private static bool TryReadLuaValue(string text, string key, string? section, out string? value)
    {
        value = null;
        if (section != null)
        {
            var start = text.IndexOf(section + " = {", StringComparison.Ordinal);
            if (start < 0) return false;
            var opening = text.IndexOf('{', start);
            if (opening < 0) return false;
            text = text[start..FindLuaSectionEnd(text, opening)];
        }
        var matches = System.Text.RegularExpressions.Regex.Matches(text,
            $@"(?m)^\s*{System.Text.RegularExpressions.Regex.Escape(key)}\s*=\s*([^,\r\n]+)");
        if (matches.Count == 0) return false;
        value = matches[^1].Groups[1].Value.Trim();
        return true;
    }

    private static ServerSettings CloneSettings(ServerSettings source) =>
        JsonSerializer.Deserialize<ServerSettings>(JsonSerializer.Serialize(source)) ?? new ServerSettings();

    private static List<string> CompareManagedPzSettings(ServerSettings expected, ServerSettings actual)
    {
        var expectedValues = ManagedPzValues(expected);
        var actualValues = ManagedPzValues(actual);
        return expectedValues
            .Where(pair => !actualValues.TryGetValue(pair.Key, out var actualValue) ||
                !string.Equals(pair.Value, actualValue, StringComparison.OrdinalIgnoreCase))
            .Select(pair => $"• {pair.Key}：輸入「{pair.Value}」／磁碟「" +
                (actualValues.TryGetValue(pair.Key, out var value) ? value : "未讀取") + "」")
            .ToList();
    }

    private static Dictionary<string, string> ManagedPzValues(ServerSettings value) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DefaultPort"] = value.DefaultPort.ToString(),
            ["UDPPort"] = value.UDPPort.ToString(),
            ["MaxPlayers"] = value.MaxPlayers.ToString(),
            ["Public"] = Bool(value.Public),
            ["PublicName"] = Clean(value.PublicName),
            ["PublicDescription"] = Clean(value.Description),
            ["Password"] = Clean(value.Password),
            ["PauseEmpty"] = Bool(value.PauseEmpty),
            ["Open"] = Bool(value.Open),
            ["PVP"] = Bool(value.Pvp),
            ["SafetySystem"] = Bool(value.SafetySystem),
            ["SleepAllowed"] = Bool(value.SleepAllowed),
            ["SleepNeeded"] = Bool(value.SleepNeeded),
            ["VoiceEnable"] = Bool(value.VoiceEnable),
            ["PlayerSafehouse"] = Bool(value.PlayerSafehouse),
            ["PingLimit"] = value.PingLimit.ToString(),
            ["SaveWorldEveryMinutes"] = value.SaveEveryMinutes.ToString(),
            ["BackupsCount"] = value.BuiltInBackups.ToString(),
            ["SpawnItems"] = value.SpawnItems,
            ["ServerWelcomeMessage"] = Clean(value.WelcomeMessage),
            ["WorkshopItems"] = value.WorkshopItems,
            ["Mods"] = value.Mods,
            ["Map"] = value.MapFolders,
            ["RCONPort"] = value.RconPort.ToString(),
            ["RCONPassword"] = Clean(value.RconPassword),
            ["AllowNonAsciiUsername"] = Bool(value.AllowNonAsciiUsername),
            ["AnnounceDeath"] = Bool(value.AnnounceDeath),
            ["MaxAccountsPerUser"] = value.MaxAccountsPerUser.ToString(),
            ["MapRemotePlayerVisibility"] = value.MapRemotePlayerVisibility.ToString(),
            ["PlayerRespawnWithSelf"] = Bool(value.PlayerRespawnWithSelf),
            ["PlayerRespawnWithOther"] = Bool(value.PlayerRespawnWithOther),
            ["SafehouseAllowRespawn"] = Bool(value.SafehouseAllowRespawn),
            ["Faction"] = Bool(value.Faction),
            ["FactionDaySurvivedToCreate"] = value.FactionDaySurvivedToCreate.ToString(),
            ["SafehouseDaySurvivedToClaim"] = value.SafehouseDaySurvivedToClaim.ToString(),
            ["SafeHouseRemovalTime"] = value.SafeHouseRemovalTime.ToString(),
            ["PVPFirearmDamageModifier"] = LuaNumber(value.PvpFirearmDamageModifier),
            ["PVPMeleeDamageModifier"] = LuaNumber(value.PvpMeleeDamageModifier),
            ["SpeedLimit"] = LuaNumber(value.SpeedLimit),
            ["DenyLoginOnOverloadedServer"] = Bool(value.DenyLoginOnOverloadedServer),
            ["LoginQueueEnabled"] = Bool(value.LoginQueueEnabled),
            ["LoginQueueConnectTimeout"] = value.LoginQueueConnectTimeout.ToString(),
            ["Sandbox.DayLength"] = value.DayLength.ToString(),
            ["Sandbox.WaterShutModifier"] = value.WaterShutDays.ToString(),
            ["Sandbox.ElecShutModifier"] = value.ElectricityShutDays.ToString(),
            ["Sandbox.FoodLootNew"] = LuaNumber(value.FoodLoot),
            ["Sandbox.WeaponLootNew"] = LuaNumber(value.WeaponLoot),
            ["Sandbox.AmmoLootNew"] = LuaNumber(value.AmmoLoot),
            ["Sandbox.MedicalLootNew"] = LuaNumber(value.MedicalLoot),
            ["Sandbox.OtherLootNew"] = LuaNumber(value.OtherLoot),
            ["Sandbox.HoursForLootRespawn"] = value.LootRespawnHours.ToString(),
            ["Sandbox.CharacterFreePoints"] = value.CharacterFreePoints.ToString(),
            ["Sandbox.StarterKit"] = Bool(value.StarterKit),
            ["Sandbox.StatsDecrease"] = value.StatsDecrease.ToString(),
            ["Sandbox.EndRegen"] = value.EndRegen.ToString(),
            ["Sandbox.Nutrition"] = Bool(value.Nutrition),
            ["Sandbox.InjurySeverity"] = value.InjurySeverity.ToString(),
            ["Sandbox.BoneFracture"] = Bool(value.BoneFracture),
            ["Sandbox.ClothingDegradation"] = value.ClothingDegradation.ToString(),
            ["Sandbox.MultiHitZombies"] = Bool(value.MultiHitZombies),
            ["Sandbox.RearVulnerability"] = value.RearVulnerability.ToString(),
            ["Sandbox.BloodLevel"] = value.BloodLevel.ToString(),
            ["Sandbox.PlayerDamageFromCrash"] = Bool(value.PlayerDamageFromCrash),
            ["MultiplierConfig.Global"] = LuaNumber(value.XpMultiplier),
            ["ZombieLore.Speed"] = value.ZombieSpeed.ToString(),
            ["ZombieLore.Strength"] = value.ZombieStrength.ToString(),
            ["ZombieLore.Toughness"] = value.ZombieToughness.ToString(),
            ["ZombieLore.Transmission"] = value.Transmission.ToString(),
            ["ZombieConfig.PopulationMultiplier"] = LuaNumber(value.PopulationMultiplier),
            ["ZombieConfig.PopulationPeakMultiplier"] = LuaNumber(value.PopulationPeakMultiplier),
            ["ZombieConfig.PopulationPeakDay"] = value.PopulationPeakDay.ToString(),
            ["ZombieConfig.RespawnHours"] = LuaNumber(value.RespawnHours)
        };

    private async void ResolveMods_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPrepareModResolver()) return;
        var roots = ParseWorkshopIds(WorkshopBox.Text);
        if (roots == null) return;
        if (roots.Count == 0)
        {
            resolvedModEntries.Clear();
            workshopDependencyEntries.Clear();
            resolvedMapEntries.Clear();
            WorkshopDependencyGrid.ItemsSource = null;
            MapCandidatesGrid.ItemsSource = null;
            RefreshModGrid();
            settings.Mods = "";
            ModsBox.Text = "";
            ResolvedModsText.Text = "未設定 Workshop 項目";
            return;
        }

        ResolveModsButton.IsEnabled = false;
        ModResolveProgress.Visibility = Visibility.Visible;
        try
        {
            var discoveredIds = await DiscoverWorkshopDependenciesAsync(roots);
            RefreshWorkshopDependencyCandidates(roots, discoveredIds);
            var workshopIds = roots;
            WorkshopBox.Text = string.Join(';', workshopIds);
            settings.WorkshopItems = WorkshopBox.Text;
            var (_, missingBefore) = FindInstalledModEntries(workshopIds, false);
            foreach (var id in missingBefore)
            {
                ResolvedModsText.Text = $"正在下載 Workshop {id}…";
                var args = $"+force_install_dir \"{settings.InstallDirectory}\" +login anonymous " +
                    $"+workshop_download_item 108600 {id} validate +quit";
                var code = await RunProcessAsync(settings.SteamCmdPath, args,
                    Path.GetDirectoryName(settings.SteamCmdPath)!);
                if (code != 0) Log($"Workshop {id} 下載結束代碼：{code}");
            }

            var (_, missingAfter) = FindInstalledModEntries(workshopIds, true);
            SortModEntriesByDependencies();
            ApplyResolvedMods(false, false);
            if (missingAfter.Count > 0)
                MessageBox.Show(
                    $"下列 Workshop 項目下載後仍找不到 mod.info／id：\n{string.Join("\n", missingAfter)}\n\n" +
                    "可能是純素材、地圖、下載失敗，或項目尚未支援 Build 42。",
                    "部分項目無法解析", MessageBoxButton.OK, MessageBoxImage.Warning);
            else if (resolvedModEntries.Any(entry =>
                         entry.Status.Contains("缺少依賴") ||
                         entry.Status.Contains("依賴尚未勾選")))
                MessageBox.Show(
                    "解析完成，但部分已勾選 Mod ID 尚未滿足 mod.info 的硬依賴。\n\n" +
                    "請從 Steam 依賴候選中選擇要加入的 Workshop，或取消不需要的相容補丁。",
                    "需要選擇依賴", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("解析完成。請檢查多 ID 項目的勾選，再按「套用勾選與順序」。\n尚未寫入 PZ INI。",
                    "模組管理清單已更新");
        }
        catch (Exception ex)
        {
            Log($"模組解析失敗：{ex}");
            MessageBox.Show($"模組解析失敗，但管理器會繼續執行：\n{ex.Message}",
                "模組解析失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ResolveModsButton.IsEnabled = true;
            ModResolveProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshLocalMods_Click(object sender, RoutedEventArgs e)
    {
        if (!TryPrepareModResolver(false)) return;
        var ids = ParseWorkshopIds(WorkshopBox.Text);
        if (ids == null) return;
        FindInstalledModEntries(ids, false);
    }

    private void AddSelectedWorkshopDependencies_Click(object sender, RoutedEventArgs e)
    {
        WorkshopDependencyGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        WorkshopDependencyGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var selected = workshopDependencyEntries.Where(entry => entry.Include)
            .Select(entry => entry.WorkshopId).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("請先勾選要加入的 Steam 依賴候選。", "尚未選擇依賴");
            return;
        }

        var current = ParseWorkshopIds(WorkshopBox.Text);
        if (current == null) return;
        var combined = current.Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        WorkshopBox.Text = string.Join(';', combined);
        foreach (var entry in workshopDependencyEntries.Where(entry => entry.Include))
        {
            entry.Include = false;
            entry.Status = "已加入輸入清單；等待重新解析";
        }
        WorkshopDependencyGrid.Items.Refresh();
        ResolvedModsText.Text = $"已加入 {selected.Count} 個候選；請再按「下載／解析並檢查依賴」。";
    }

    private bool TryPrepareModResolver(bool requireSteamCmd = true)
    {
        var steamCmdPath = SteamCmdPathBox.Text.Trim();
        var installDirectory = InstallPathBox.Text.Trim();
        if (requireSteamCmd && !File.Exists(steamCmdPath))
        {
            MessageBox.Show("找不到 SteamCMD。請先完成前置安裝，或指定正確的 steamcmd.exe。",
                "找不到 SteamCMD", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            MessageBox.Show("請先指定 PZ Dedicated Server 目錄。",
                "缺少伺服器目錄", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        settings.SteamCmdPath = steamCmdPath;
        settings.InstallDirectory = installDirectory;
        return true;
    }

    private List<string>? ParseWorkshopIds(string text)
    {
        var ids = NormalizeWorkshopList(text).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (ids.Any(id => !ulong.TryParse(id, out _)))
        {
            MessageBox.Show("Workshop ID 只能包含數字，請以分號分隔。", "格式錯誤");
            return null;
        }
        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<List<string>> DiscoverWorkshopDependenciesAsync(List<string> roots)
    {
        workshopRequirements.Clear();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PZServerManager/" + AppVersion);
        var queue = new Queue<string>(roots);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (queue.Count > 0 && visited.Count < 100)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id)) continue;
            ResolvedModsText.Text = $"正在向 Steam 檢查依賴：{id}（{visited.Count}）";
            try
            {
                string html;
                using (var response = await client.GetAsync(
                    $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}"))
                {
                    if ((int)response.StatusCode == 429)
                    {
                        await Task.Delay(1500);
                        using var retry = await client.GetAsync(
                            $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}");
                        retry.EnsureSuccessStatusCode();
                        html = await retry.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                        html = await response.Content.ReadAsStringAsync();
                    }
                }
                var title = ParseWorkshopTitleHtml(html);
                if (!string.IsNullOrWhiteSpace(title)) workshopTitles[id] = title;
                var dependencies = ParseRequiredWorkshopItemsHtml(html);
                workshopRequirements[id] = dependencies;
                foreach (var dependency in dependencies)
                    if (!visited.Contains(dependency)) queue.Enqueue(dependency);
                await Task.Delay(220);
            }
            catch (Exception ex)
            {
                workshopRequirements[id] = new List<string>();
                Log($"無法從 Steam 取得 Workshop {id} 依賴：{ex.Message}");
            }
        }

        var ordered = new List<string>();
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var complete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string id)
        {
            if (complete.Contains(id)) return;
            if (!active.Add(id))
            {
                Log($"Workshop 依賴形成循環：{id}。保留目前相對順序。");
                return;
            }
            if (workshopRequirements.TryGetValue(id, out var dependencies))
                foreach (var dependency in dependencies) Visit(dependency);
            active.Remove(id);
            complete.Add(id);
            if (!ordered.Contains(id, StringComparer.OrdinalIgnoreCase)) ordered.Add(id);
        }
        foreach (var root in roots) Visit(root);
        foreach (var discovered in visited) Visit(discovered);
        return ordered;
    }

    private void RefreshWorkshopDependencyCandidates(
        IReadOnlyCollection<string> roots, IEnumerable<string> discoveredIds)
    {
        var rootSet = roots.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requiredBy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in workshopRequirements)
        {
            foreach (var dependency in pair.Value)
            {
                if (!requiredBy.TryGetValue(dependency, out var owners))
                    requiredBy[dependency] = owners = new List<string>();
                var owner = workshopTitles.GetValueOrDefault(pair.Key, pair.Key);
                if (!owners.Contains(owner, StringComparer.OrdinalIgnoreCase)) owners.Add(owner);
            }
        }

        workshopDependencyEntries.Clear();
        foreach (var id in discoveredIds.Where(id => !rootSet.Contains(id))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            workshopDependencyEntries.Add(new WorkshopDependencyEntry
            {
                WorkshopId = id,
                Title = workshopTitles.GetValueOrDefault(id, $"Workshop {id}"),
                RequiredBy = requiredBy.TryGetValue(id, out var owners)
                    ? string.Join("；", owners) : "Steam Required Items",
                Status = "候選；尚未加入"
            });
        }
        WorkshopDependencyGrid.ItemsSource = null;
        WorkshopDependencyGrid.ItemsSource = workshopDependencyEntries;
    }

    private static List<string> ParseRequiredWorkshopItemsHtml(string html)
    {
        var container = Regex.Match(html,
            @"<div class=""requiredItemsContainer""[^>]*>([\s\S]*?)</div>\s*</div>",
            RegexOptions.IgnoreCase).Groups[1].Value;
        return Regex.Matches(container, @"filedetails/\?id=(\d+)", RegexOptions.IgnoreCase)
            .Cast<Match>().Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ParseWorkshopTitleHtml(string html)
    {
        var match = Regex.Match(html,
            @"<div class=""workshopItemTitle"">\s*([\s\S]*?)\s*</div>",
            RegexOptions.IgnoreCase);
        return WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<[^>]+>", "")).Trim();
    }

    private bool EnsureModIdsResolved()
    {
        var workshopIds = ParseWorkshopIds(settings.WorkshopItems);
        if (workshopIds == null) return false;
        if (workshopIds.Count == 0)
        {
            settings.Mods = "";
            settings.MapFolders = new ServerSettings().MapFolders;
            ModsBox.Text = "";
            MapFoldersBox.Text = settings.MapFolders;
            ResolvedModsText.Text = "未設定 Workshop 項目";
            return true;
        }
        if (!string.Equals(resolvedWorkshopIdentity, string.Join(';', workshopIds),
                StringComparison.OrdinalIgnoreCase))
            FindInstalledModEntries(workshopIds, false);
        if (resolvedModEntries.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(settings.Mods))
            {
                Log("本機尚未解析到 mod.info；保留 INI 目前 Mods，不自動清空。");
                return true;
            }
            MessageBox.Show("尚未解析任何 Mod ID。請先按「下載／解析並檢查依賴」。",
                "需要解析模組", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return ApplyResolvedMods(false, true);
    }

    private (List<ModEntry> Entries, List<string> MissingWorkshopIds) FindInstalledModEntries(
        IEnumerable<string> workshopIds, bool selectNewSingles)
    {
        var ids = workshopIds.ToList();
        var previousOrder = NormalizeSemicolonList(ModsBox.Text)
            .Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        var previousSet = previousOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entriesById = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        var obsoleteBuildVariantIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        var steamCmdDirectory = Path.GetDirectoryName(settings.SteamCmdPath) ?? "";

        foreach (var workshopId in ids)
        {
            var foundForItem = false;
            var itemEntries = new List<ModEntry>();
            var itemRoots = new[]
            {
                Path.Combine(settings.InstallDirectory, "steamapps", "workshop", "content", "108600", workshopId),
                Path.Combine(steamCmdDirectory, "steamapps", "workshop", "content", "108600", workshopId)
            }.Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var itemRoot in itemRoots.Where(Directory.Exists))
            {
                List<string> infoFiles;
                try
                {
                    infoFiles = Directory.EnumerateFiles(itemRoot, "mod.info", SearchOption.AllDirectories)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
                }
                catch (Exception ex) { Log($"無法掃描 {itemRoot}：{ex.Message}"); continue; }
                var selectedInfoFiles = SelectBuild42ModInfoFiles(infoFiles);
                foreach (var excluded in infoFiles.Except(selectedInfoFiles,
                             StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var excludedId = ModInfoValue(
                            ConfigFileEncoding.ReadText(excluded, "Auto"), "id");
                        if (!string.IsNullOrWhiteSpace(excludedId))
                            obsoleteBuildVariantIds.Add(excludedId);
                    }
                    catch { }
                }
                foreach (var infoFile in selectedInfoFiles)
                {
                    try
                    {
                        var text = ConfigFileEncoding.ReadText(infoFile, "Auto");
                        var modId = ModInfoValue(text, "id");
                        if (string.IsNullOrWhiteSpace(modId)) continue;
                        foundForItem = true;
                        if (!entriesById.TryGetValue(modId, out var entry))
                        {
                            entry = new ModEntry {
                                WorkshopId = workshopId,
                                WorkshopTitle = workshopTitles.GetValueOrDefault(workshopId, ""),
                                ModName = ModInfoValue(text, "name"),
                                ModId = modId,
                                Variant = Build42VariantLabel(infoFile, text),
                                Enabled = previousSet.Contains(modId),
                                SourceFile = infoFile
                            };
                            InspectModRuntime(entry);
                            entriesById[modId] = entry;
                            itemEntries.Add(entry);
                        }
                        MergeDistinct(entry.Requires, SplitModInfoList(ModInfoValue(text, "require")));
                        MergeDistinct(entry.LoadBefore, SplitModInfoList(ModInfoValue(text, "loadBefore")));
                        MergeDistinct(entry.LoadBefore, SplitModInfoList(ModInfoValue(text, "loadModBefore")));
                        MergeDistinct(entry.LoadAfter, SplitModInfoList(ModInfoValue(text, "loadAfter")));
                        MergeDistinct(entry.LoadAfter, SplitModInfoList(ModInfoValue(text, "loadModAfter")));
                        MergeDistinct(entry.MapFolders, FindMapFolders(infoFile));
                    }
                    catch (Exception ex) { Log($"讀取 {infoFile} 失敗：{ex.Message}"); }
                }
            }
            if (!foundForItem) missing.Add(workshopId);
            MarkMutuallyExclusiveAlternatives(itemEntries);
            if (selectNewSingles && itemEntries.Count == 1 &&
                !previousSet.Any(id => itemEntries.Any(entry =>
                    string.Equals(entry.ModId, id, StringComparison.OrdinalIgnoreCase))))
                itemEntries[0].Enabled = true;
        }

        foreach (var oldId in previousOrder.Where(id =>
                     !entriesById.ContainsKey(id) && !obsoleteBuildVariantIds.Contains(id)))
            entriesById[oldId] = new ModEntry {
                ModId = oldId, ModName = oldId, Enabled = true,
                Category = "未解析", Status = "目前找不到 mod.info；保留既有值"
            };

        resolvedModEntries.Clear();
        resolvedModEntries.AddRange(entriesById.Values
            .OrderBy(entry =>
            {
                var index = previousOrder.FindIndex(id =>
                    string.Equals(id, entry.ModId, StringComparison.OrdinalIgnoreCase));
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(entry => ids.FindIndex(id =>
                string.Equals(id, entry.WorkshopId, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(entry => entry.ModId, StringComparer.OrdinalIgnoreCase));
        resolvedWorkshopIdentity = string.Join(';', ids);
        UpdateModEntryDiagnostics();
        RefreshModGrid();
        RefreshMapCandidates(false);
        foreach (var obsolete in previousOrder.Where(obsoleteBuildVariantIds.Contains))
            Log($"已排除非目前 Build 42 變體 Mod ID：{obsolete}");
        return (resolvedModEntries, missing);
    }

    private static void MarkMutuallyExclusiveAlternatives(List<ModEntry> entries)
    {
        foreach (var left in entries)
        {
            var alternatives = entries.Where(right =>
                    !ReferenceEquals(left, right) &&
                    (string.Equals(right.ModId, left.ModId + " AR",
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(left.ModId, right.ModId + " AR",
                         StringComparison.OrdinalIgnoreCase)))
                .Select(right => right.ModId).ToList();
            if (alternatives.Count > 0)
                left.SelectionPolicy = "與 " + string.Join(", ", alternatives) + " 擇一";
        }
    }

    private static string ModInfoValue(string text, string key)
    {
        var matches = Regex.Matches(text,
            $@"(?im)^\s*{Regex.Escape(key)}\s*=\s*(.+?)\s*$");
        return matches.Count == 0 ? "" : matches[^1].Groups[1].Value.Trim();
    }

    private static List<string> SelectBuild42ModInfoFiles(IEnumerable<string> infoFiles)
    {
        return infoFiles.GroupBy(GetModPackageRoot, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var candidates = group.Select(path =>
                {
                    var text = ConfigFileEncoding.ReadText(path, "Auto");
                    var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
                    var directoryVersion = ParseBuildVersion(folder);
                    var minimum = ParseBuildVersion(ModInfoValue(text, "versionMin"));
                    var maximum = ParseBuildVersion(ModInfoValue(text, "versionMax"));
                    var buildVersion = directoryVersion?.Major == 42 ? directoryVersion :
                        minimum?.Major == 42 ? minimum :
                        folder.Equals("common", StringComparison.OrdinalIgnoreCase)
                            ? new Version(42, 0) : null;
                    return new { Path = path, Folder = folder, Minimum = minimum,
                        Maximum = maximum, BuildVersion = buildVersion };
                }).ToList();
                var build42 = candidates.Where(candidate => candidate.BuildVersion?.Major == 42)
                    .ToList();
                if (build42.Count == 0)
                    return candidates.OrderByDescending(candidate =>
                            candidate.Folder.Equals("common", StringComparison.OrdinalIgnoreCase))
                        .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                        .First().Path;

                var target = build42.Max(candidate => candidate.BuildVersion)!;
                var compatible = build42.Where(candidate =>
                        (candidate.Minimum == null || candidate.Minimum <= target) &&
                        (candidate.Maximum == null || candidate.Maximum >= target))
                    .ToList();
                var pool = compatible.Count > 0 ? compatible : build42;
                return pool
                    .OrderByDescending(candidate => candidate.BuildVersion)
                    .ThenByDescending(candidate => candidate.Minimum)
                    .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                    .First().Path;
            }).ToList();
    }

    private static string GetModPackageRoot(string infoFile)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(infoFile) ?? "");
        if (directory.Parent != null &&
            (directory.Name.Equals("common", StringComparison.OrdinalIgnoreCase) ||
             ParseBuildVersion(directory.Name)?.Major == 42))
            return directory.Parent.FullName;
        return directory.FullName;
    }

    private static Version? ParseBuildVersion(string value)
    {
        var match = Regex.Match(value.Trim(), @"^(\d+)(?:\.(\d+))?(?:\.(\d+))?");
        if (!match.Success) return null;
        return new Version(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            match.Groups[2].Success
                ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0,
            match.Groups[3].Success
                ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 0);
    }

    private static string Build42VariantLabel(string infoFile, string text)
    {
        var folder = Path.GetFileName(Path.GetDirectoryName(infoFile)) ?? "";
        var minimum = ModInfoValue(text, "versionMin");
        var maximum = ModInfoValue(text, "versionMax");
        var label = ParseBuildVersion(folder)?.Major == 42 ? folder :
            folder.Equals("common", StringComparison.OrdinalIgnoreCase) ? "common" : "通用";
        if (!string.IsNullOrWhiteSpace(minimum)) label += $" ≥{minimum}";
        if (!string.IsNullOrWhiteSpace(maximum)) label += $" ≤{maximum}";
        return label;
    }

    private static List<string> SplitModInfoList(string value) =>
        value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeModReference).Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string NormalizeModReference(string value) =>
        value.Trim().Trim('"', '\'').TrimStart('\\', '/').Trim();

    private static void MergeDistinct(List<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
            if (!target.Contains(value, StringComparer.OrdinalIgnoreCase)) target.Add(value);
    }

    private static List<string> GetBuild42ContentRoots(string infoFile)
    {
        var packageRoot = GetModPackageRoot(infoFile);
        var selectedRoot = Path.GetDirectoryName(infoFile) ?? packageRoot;
        return new[]
            {
                string.Equals(packageRoot, selectedRoot, StringComparison.OrdinalIgnoreCase)
                    ? packageRoot : "",
                Path.Combine(packageRoot, "common"),
                selectedRoot
            }
            .Where(path => path.Length > 0 && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> FindMapFolders(string infoFile)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var contentRoot in GetBuild42ContentRoots(infoFile))
            {
                var mapsRoot = Path.Combine(contentRoot, "media", "maps");
                if (!Directory.Exists(mapsRoot)) continue;
                foreach (var mapRoot in Directory.EnumerateDirectories(mapsRoot))
                {
                    if (!File.Exists(Path.Combine(mapRoot, "map.info")) ||
                        !ContainsWorldMapCells(mapRoot)) continue;
                    var name = Path.GetFileName(mapRoot);
                    if (!string.IsNullOrWhiteSpace(name)) result.Add(name);
                }
            }
        }
        catch
        {
            // A partial or locked Workshop download is ignored instead of crashing the GUI.
        }
        return result.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool ContainsWorldMapCells(string mapRoot)
    {
        return Directory.EnumerateFiles(mapRoot, "*", SearchOption.AllDirectories)
            .Any(path =>
            {
                var name = Path.GetFileName(path);
                var extension = Path.GetExtension(path);
                return extension.Equals(".lotheader", StringComparison.OrdinalIgnoreCase) ||
                       extension.Equals(".lotpack", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith("chunkdata_", StringComparison.OrdinalIgnoreCase) &&
                       extension.Equals(".bin", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static string FindSpawnPointsFile(string infoFile, string mapFolder)
    {
        foreach (var contentRoot in GetBuild42ContentRoots(infoFile))
        {
            var candidate = Path.Combine(contentRoot, "media", "maps", mapFolder,
                "spawnpoints.lua");
            if (File.Exists(candidate)) return candidate;
        }
        return "";
    }

    private static void InspectModRuntime(ModEntry entry)
    {
        try
        {
            foreach (var modRoot in GetBuild42ContentRoots(entry.SourceFile))
            {
                foreach (var file in Directory.EnumerateFiles(modRoot, "*",
                             SearchOption.AllDirectories))
                {
                    var relative = "/" + Path.GetRelativePath(modRoot, file).Replace('\\', '/');
                    if (relative.Contains("/lua/client/", StringComparison.OrdinalIgnoreCase))
                        entry.HasClientLua = true;
                    else if (relative.Contains("/lua/shared/", StringComparison.OrdinalIgnoreCase))
                        entry.HasSharedLua = true;
                    else if (relative.Contains("/lua/server/", StringComparison.OrdinalIgnoreCase))
                        entry.HasServerLua = true;

                    if (relative.Contains("/scripts/", StringComparison.OrdinalIgnoreCase) ||
                        relative.Contains("/maps/", StringComparison.OrdinalIgnoreCase) ||
                        relative.Contains("/vehicles/", StringComparison.OrdinalIgnoreCase) ||
                        relative.EndsWith("map.info", StringComparison.OrdinalIgnoreCase))
                        entry.HasGameData = true;
                }
            }
        }
        catch
        {
            // A locked Workshop file must not crash the GUI. Diagnostics remain conservative.
            entry.HasGameData = true;
        }
    }

    private void UpdateModEntryDiagnostics()
    {
        var known = resolvedModEntries.Select(entry => entry.ModId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enabled = resolvedModEntries.Where(entry => entry.Enabled)
            .Select(entry => entry.ModId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groupCounts = resolvedModEntries.Where(entry => entry.WorkshopId.Length > 0)
            .GroupBy(entry => entry.WorkshopId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var requiredByOthers = resolvedModEntries.SelectMany(entry => entry.Requires)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in resolvedModEntries)
        {
            var missing = entry.Requires.Where(id => !known.Contains(id)).ToList();
            var inactive = entry.Requires.Where(id => known.Contains(id) && !enabled.Contains(id)).ToList();
            var missingOrderTargets = entry.LoadAfter.Concat(entry.LoadBefore)
                .Where(id => !known.Contains(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var enabledExclusive = resolvedModEntries.Where(other =>
                    other.Enabled && !ReferenceEquals(other, entry) &&
                    entry.SelectionPolicy.Contains(other.ModId, StringComparison.OrdinalIgnoreCase))
                .Select(other => other.ModId).ToList();
            entry.Dependencies = entry.Requires.Count == 0 ? "無" : string.Join(", ", entry.Requires);
            var ordering = new List<string>();
            if (entry.LoadAfter.Count > 0)
                ordering.Add("後於 " + string.Join(", ", entry.LoadAfter));
            if (entry.LoadBefore.Count > 0)
                ordering.Add("先於 " + string.Join(", ", entry.LoadBefore));
            entry.Ordering = ordering.Count == 0 ? "無" : string.Join("；", ordering);
            if (missing.Count > 0)
                entry.Status = "缺少依賴：" + string.Join(", ", missing);
            else if (entry.Enabled && inactive.Count > 0)
                entry.Status = "依賴尚未勾選：" + string.Join(", ", inactive);
            else if (entry.Enabled && enabledExclusive.Count > 0)
                entry.Status = "互斥選項不可同時啟用：" + string.Join(", ", enabledExclusive);
            else if (entry.ModId.Equals("BetterGeneratorInfo", StringComparison.OrdinalIgnoreCase))
                entry.Status = "作者要求 DoLuaChecksum=false；不建議啟用";
            else if (missingOrderTargets.Count > 0)
                entry.Status = "作者排序目標未安裝（非硬依賴）：" +
                    string.Join(", ", missingOrderTargets);
            else if (entry.WorkshopId.Length > 0 && groupCounts.GetValueOrDefault(entry.WorkshopId) > 1)
                entry.Status = "同一 Workshop 有多個 ID，請確認本體／補丁／擇一版本";
            else entry.Status = "可用";

            var searchable = (entry.ModName + " " + entry.ModId).ToLowerInvariant();
            entry.Category = entry.MapFolders.Count > 0 ? "地圖" :
                searchable.Contains("patch") || searchable.Contains("authz") ? "相容補丁" :
                requiredByOthers.Contains(entry.ModId) ? "框架／依賴" :
                Regex.IsMatch(searchable, @"ui|interface|error|symbol|categor|read|pin|translation|lang|hold|visible")
                    ? "介面／QoL 候選" : "伺服器 MOD";

            entry.ClientPolicy =
                entry.HasClientLua && !entry.HasSharedLua && !entry.HasServerLua &&
                !entry.HasGameData && entry.MapFolders.Count == 0
                    ? "純客戶端候選"
                    : entry.HasSharedLua || entry.HasServerLua || entry.HasGameData ||
                      entry.MapFolders.Count > 0
                        ? "伺服器／雙端必需"
                        : "無 Lua／需人工確認";
        }
    }

    private void RefreshModGrid()
    {
        for (var i = 0; i < resolvedModEntries.Count; i++)
            resolvedModEntries[i].Order = i + 1;
        ResolvedModsGrid.ItemsSource = null;
        ResolvedModsGrid.ItemsSource = resolvedModEntries;
        var enabled = resolvedModEntries.Count(entry => entry.Enabled);
        var warnings = resolvedModEntries.Count(entry => entry.Status != "可用");
        ResolvedModsText.Text = $"共 {resolvedModEntries.Count} 個 Mod ID；已勾選 {enabled}；需確認 {warnings}";
    }

    private void RefreshMapCandidates(bool preservePendingChoices)
    {
        var previous = preservePendingChoices
            ? resolvedMapEntries.ToDictionary(
                entry => $"{entry.WorkshopId}|{entry.ModId}|{entry.MapFolder}",
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, MapEntry>(StringComparer.OrdinalIgnoreCase);
        var currentMaps = NormalizeSemicolonList(MapFoldersBox.Text)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var currentSet = currentMaps.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingSpawnMaps = ReadExistingSpawnRegionMaps();
        var enabledModIds = resolvedModEntries.Where(entry => entry.Enabled)
            .Select(entry => entry.ModId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        resolvedMapEntries.Clear();
        foreach (var mod in resolvedModEntries)
        {
            foreach (var mapFolder in mod.MapFolders)
            {
                var key = $"{mod.WorkshopId}|{mod.ModId}|{mapFolder}";
                var spawnFile = FindSpawnPointsFile(mod.SourceFile, mapFolder);
                var hasSpawn = spawnFile.Length > 0;
                previous.TryGetValue(key, out var pending);
                resolvedMapEntries.Add(new MapEntry
                {
                    Enabled = pending?.Enabled ?? currentSet.Contains(mapFolder),
                    MapFolder = mapFolder,
                    ModId = mod.ModId,
                    WorkshopId = mod.WorkshopId,
                    SpawnEnabled = hasSpawn &&
                        (pending?.SpawnEnabled ?? existingSpawnMaps.Contains(mapFolder)),
                    SpawnPointsFile = hasSpawn ? spawnFile : "",
                    Status = !enabledModIds.Contains(mod.ModId)
                        ? "來源 Mod 尚未勾選"
                        : hasSpawn ? "可加入重生選單" : "僅地圖；無 spawnpoints.lua"
                });
            }
        }

        resolvedMapEntries.Sort((left, right) =>
        {
            var leftIndex = currentMaps.FindIndex(map =>
                string.Equals(map, left.MapFolder, StringComparison.OrdinalIgnoreCase));
            var rightIndex = currentMaps.FindIndex(map =>
                string.Equals(map, right.MapFolder, StringComparison.OrdinalIgnoreCase));
            if (leftIndex < 0) leftIndex = int.MaxValue;
            if (rightIndex < 0) rightIndex = int.MaxValue;
            return leftIndex != rightIndex ? leftIndex.CompareTo(rightIndex) :
                resolvedModEntries.FindIndex(entry =>
                    string.Equals(entry.ModId, left.ModId, StringComparison.OrdinalIgnoreCase))
                .CompareTo(resolvedModEntries.FindIndex(entry =>
                    string.Equals(entry.ModId, right.ModId, StringComparison.OrdinalIgnoreCase)));
        });
        RefreshMapGrid();
    }

    private HashSet<string> ReadExistingSpawnRegionMaps()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(DataPathBox.Text.Trim(), "Server",
            ServerNameBox.Text.Trim() + "_spawnregions.lua");
        if (!File.Exists(path)) return result;
        try
        {
            var text = ConfigFileEncoding.ReadText(path, SelectedEncodingMode());
            foreach (Match match in Regex.Matches(text,
                         @"file\s*=\s*[""']media/maps/(.*?)/spawnpoints\.lua[""']",
                         RegexOptions.IgnoreCase))
                result.Add(match.Groups[1].Value);
        }
        catch (Exception ex) { Log($"無法讀取重生區域：{ex.Message}"); }
        return result;
    }

    private void RefreshMapGrid()
    {
        for (var i = 0; i < resolvedMapEntries.Count; i++)
            resolvedMapEntries[i].Order = i + 1;
        MapCandidatesGrid.ItemsSource = null;
        MapCandidatesGrid.ItemsSource = resolvedMapEntries;
    }

    private void MoveMapUp_Click(object sender, RoutedEventArgs e) => MoveSelectedMap(-1);
    private void MoveMapDown_Click(object sender, RoutedEventArgs e) => MoveSelectedMap(1);

    private void MoveSelectedMap(int direction)
    {
        MapCandidatesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        if (MapCandidatesGrid.SelectedItem is not MapEntry entry) return;
        var index = resolvedMapEntries.IndexOf(entry);
        var target = index + direction;
        if (target < 0 || target >= resolvedMapEntries.Count) return;
        (resolvedMapEntries[index], resolvedMapEntries[target]) =
            (resolvedMapEntries[target], resolvedMapEntries[index]);
        RefreshMapGrid();
        MapCandidatesGrid.SelectedItem = entry;
        MapCandidatesGrid.ScrollIntoView(entry);
    }

    private void ApplyMapSelections_Click(object sender, RoutedEventArgs e) =>
        ApplyMapSelections(true);

    private bool ApplyMapSelections(bool showMessage)
    {
        MapCandidatesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MapCandidatesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (!ValidateMapSelections(showMessage)) return false;

        var detectedSet = resolvedMapEntries.Select(entry => entry.MapFolder)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var manualMaps = NormalizeSemicolonList(MapFoldersBox.Text)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(map => !detectedSet.Contains(map)).ToList();
        if (!manualMaps.Contains("Muldraugh, KY", StringComparer.OrdinalIgnoreCase))
            manualMaps.Add("Muldraugh, KY");
        var selectedMaps = resolvedMapEntries.Where(entry => entry.Enabled)
            .Select(entry => entry.MapFolder);
        MapFoldersBox.Text = string.Join(';', selectedMaps.Concat(manualMaps)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        settings.MapFolders = MapFoldersBox.Text;
        spawnRegionSelectionTouched = true;
        if (showMessage)
            MessageBox.Show(
                $"已套用地圖順序；選擇 {resolvedMapEntries.Count(entry => entry.SpawnEnabled)} 個重生區域。\n" +
                "目前尚未寫入 INI 或 _spawnregions.lua。",
                "已套用地圖與重生點");
        return true;
    }

    private bool ValidateMapSelections(bool showMessage)
    {
        var enabledModIds = resolvedModEntries.Where(entry => entry.Enabled)
            .Select(entry => entry.ModId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalidMaps = resolvedMapEntries.Where(entry =>
            entry.Enabled && !enabledModIds.Contains(entry.ModId)).Select(entry => entry.MapFolder).ToList();
        var invalidSpawns = resolvedMapEntries.Where(entry =>
            entry.SpawnEnabled && (!entry.Enabled || string.IsNullOrWhiteSpace(entry.SpawnPointsFile)))
            .Select(entry => entry.MapFolder).ToList();
        if (invalidMaps.Count > 0 || invalidSpawns.Count > 0)
        {
            if (showMessage)
                MessageBox.Show(
                    (invalidMaps.Count > 0
                        ? "下列地圖的來源 Mod 尚未勾選：\n" + string.Join("\n", invalidMaps) + "\n\n"
                        : "") +
                    (invalidSpawns.Count > 0
                        ? "下列重生區域未加入地圖或沒有 spawnpoints.lua：\n" +
                          string.Join("\n", invalidSpawns)
                        : ""),
                    "地圖選擇尚未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private void MoveModUp_Click(object sender, RoutedEventArgs e) => MoveSelectedMod(-1);
    private void MoveModDown_Click(object sender, RoutedEventArgs e) => MoveSelectedMod(1);

    private void MoveSelectedMod(int direction)
    {
        ResolvedModsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        if (ResolvedModsGrid.SelectedItem is not ModEntry entry) return;
        var index = resolvedModEntries.IndexOf(entry);
        var target = index + direction;
        if (target < 0 || target >= resolvedModEntries.Count) return;
        (resolvedModEntries[index], resolvedModEntries[target]) =
            (resolvedModEntries[target], resolvedModEntries[index]);
        RefreshModGrid();
        ResolvedModsGrid.SelectedItem = entry;
        ResolvedModsGrid.ScrollIntoView(entry);
    }

    private void SortModsByDependencies_Click(object sender, RoutedEventArgs e)
    {
        SortModEntriesByDependencies();
        MessageBox.Show("已依 mod.info 的 require、loadBefore/loadAfter 與 loadModBefore/loadModAfter 排序。\n" +
            "循環依賴會保留原本相對順序；尚未寫入 PZ INI。", "依賴排序完成");
    }

    private void SortModEntriesByDependencies()
    {
        var byId = resolvedModEntries.ToDictionary(entry => entry.ModId,
            StringComparer.OrdinalIgnoreCase);
        var edges = resolvedModEntries.ToDictionary(entry => entry.ModId,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var indegree = resolvedModEntries.ToDictionary(entry => entry.ModId, _ => 0,
            StringComparer.OrdinalIgnoreCase);
        void AddEdge(string before, string after)
        {
            if (!byId.ContainsKey(before) || !byId.ContainsKey(after) ||
                string.Equals(before, after, StringComparison.OrdinalIgnoreCase)) return;
            if (edges[before].Add(after)) indegree[after]++;
        }
        foreach (var entry in resolvedModEntries)
        {
            foreach (var dependency in entry.Requires.Concat(entry.LoadAfter))
                AddEdge(dependency, entry.ModId);
            foreach (var target in entry.LoadBefore) AddEdge(entry.ModId, target);
        }
        var originalIndex = resolvedModEntries.Select((entry, index) => (entry.ModId, index))
            .ToDictionary(item => item.ModId, item => item.index, StringComparer.OrdinalIgnoreCase);
        var ready = indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key)
            .OrderBy(id => originalIndex[id]).ToList();
        var sorted = new List<ModEntry>();
        while (ready.Count > 0)
        {
            var id = ready[0];
            ready.RemoveAt(0);
            sorted.Add(byId[id]);
            foreach (var next in edges[id].OrderBy(next => originalIndex[next]))
            {
                if (--indegree[next] == 0)
                {
                    ready.Add(next);
                    ready.Sort((left, right) => originalIndex[left].CompareTo(originalIndex[right]));
                }
            }
        }
        foreach (var entry in resolvedModEntries.Where(entry => !sorted.Contains(entry)))
            sorted.Add(entry);
        resolvedModEntries.Clear();
        resolvedModEntries.AddRange(sorted);
        UpdateModEntryDiagnostics();
        RefreshModGrid();
        RefreshMapCandidates(true);
    }

    private void ApplySelectedMods_Click(object sender, RoutedEventArgs e) =>
        ApplyResolvedMods(true, true);

    private bool ApplyResolvedMods(bool showSuccessMessage, bool showErrorMessage)
    {
        ResolvedModsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ResolvedModsGrid.CommitEdit(DataGridEditingUnit.Row, true);
        UpdateModEntryDiagnostics();
        RefreshModGrid();
        var enabled = resolvedModEntries.Where(entry => entry.Enabled).ToList();
        var enabledIds = enabled.Select(entry => entry.ModId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = enabled.SelectMany(entry => entry.Requires.Select(required => (entry, required)))
            .Where(pair => !enabledIds.Contains(pair.required))
            .Select(pair => $"{pair.entry.ModId} → {pair.required}").Distinct().ToList();
        if (missing.Count > 0)
        {
            if (showErrorMessage) MessageBox.Show("下列已啟用模組缺少或未勾選依賴：\n\n" +
                string.Join("\n", missing), "模組依賴未完成",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var exclusive = enabled.SelectMany(entry => enabled
                .Where(other => !ReferenceEquals(other, entry) &&
                    entry.SelectionPolicy.Contains(other.ModId, StringComparison.OrdinalIgnoreCase))
                .Select(other => string.Compare(entry.ModId, other.ModId,
                    StringComparison.OrdinalIgnoreCase) < 0
                    ? $"{entry.ModId} ↔ {other.ModId}" : $"{other.ModId} ↔ {entry.ModId}"))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (exclusive.Count > 0)
        {
            if (showErrorMessage) MessageBox.Show(
                "下列 Mod ID 是同一功能的互斥版本，請只勾選其中一個：\n\n" +
                string.Join("\n", exclusive), "模組互斥選項",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (enabledIds.Contains("BetterGeneratorInfo"))
        {
            if (showErrorMessage) MessageBox.Show(
                "Better Generator Info 的作者要求 Dedicated Server 關閉 DoLuaChecksum，" +
                "但本管理器維持 Lua 校驗以避免客戶端腳本不一致。\n\n請停用此 Mod，或改由原始設定檔自行承擔風險。",
                "Lua 校驗衝突", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var mods = string.Join(';', enabled.Select(entry => entry.ModId));
        ModsBox.Text = mods;
        settings.Mods = mods;
        RefreshMapCandidates(true);
        ResolvedModsText.Text = $"待儲存：{enabled.Count} 個 Mod；Map={settings.MapFolders}";
        if (showSuccessMessage) MessageBox.Show(
            $"已套用 {enabled.Count} 個 Mod ID。\n" +
            "地圖請在下方逐項選擇，再按「套用地圖與重生點」。目前尚未寫入 PZ INI。",
            "已套用模組清單");
        return true;
    }

    private void ScanServers_Click(object sender, RoutedEventArgs e) => ScanExistingServers();

    private void ScanExistingServers()
    {
        var dataRoots = DiscoverDataRoots();
        var installations = DiscoverInstallations();
        var servers = new List<ExistingServer>();
        foreach (var dataRoot in dataRoots)
        {
            var serverDir = Path.Combine(dataRoot, "Server");
            if (!Directory.Exists(serverDir)) continue;
            try
            {
                servers.AddRange(Directory.GetFiles(serverDir, "*.ini", SearchOption.TopDirectoryOnly)
                    .Select(path => new ExistingServer {
                        Name = Path.GetFileNameWithoutExtension(path), DataDirectory = dataRoot,
                        IniPath = path,
                        SandboxPath = Path.Combine(serverDir, Path.GetFileNameWithoutExtension(path) + "_SandboxVars.lua")
                    }));
            }
            catch (UnauthorizedAccessException) { Log($"沒有權限掃描：{serverDir}"); }
        }
        servers = servers.DistinctBy(x => x.IniPath, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DataDirectory).ToList();
        ServerListBox.ItemsSource = servers;
        ScanSummaryText.Text = $"找到 {servers.Count} 個設定實例、{installations.Count} 個程式安裝目錄";
        foreach (var install in installations) Log($"找到 Dedicated Server 程式：{install}");
        if (servers.Count > 0) ServerListBox.SelectedIndex = 0;
    }

    private List<string> DiscoverDataRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExistingDirectory(roots, DataPathBox.Text.Trim());
        AddExistingDirectory(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid"));
        var usersRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Users");
        if (Directory.Exists(usersRoot))
        {
            try
            {
                foreach (var profile in Directory.GetDirectories(usersRoot))
                    AddExistingDirectory(roots, Path.Combine(profile, "Zomboid"));
            }
            catch (UnauthorizedAccessException) { Log($"沒有權限掃描使用者資料夾：{usersRoot}"); }
        }
        return roots.ToList();
    }

    private List<string> DiscoverInstallations()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddServerInstallation(roots, InstallPathBox.Text.Trim());
        foreach (var programRoot in new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        }.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var steam = Path.Combine(programRoot, "Steam");
            AddServerInstallation(roots, Path.Combine(steam, "steamapps", "common", "Project Zomboid Dedicated Server"));
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            try
            {
                var text = File.ReadAllText(vdf);
                foreach (System.Text.RegularExpressions.Match match in
                    System.Text.RegularExpressions.Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                {
                    var library = match.Groups[1].Value.Replace(@"\\", @"\");
                    AddServerInstallation(roots, Path.Combine(library, "steamapps", "common",
                        "Project Zomboid Dedicated Server"));
                }
            }
            catch (Exception ex) { Log($"讀取 Steam Library 清單失敗：{ex.Message}"); }
        }
        return roots.ToList();
    }

    private static void AddExistingDirectory(HashSet<string> roots, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (Directory.Exists(path)) roots.Add(Path.GetFullPath(path)); } catch { }
    }

    private static void AddServerInstallation(HashSet<string> roots, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var full = Path.GetFullPath(path);
            if (new[] { "StartServer64.bat", "start-server.bat" }.Any(x => File.Exists(Path.Combine(full, x))))
                roots.Add(full);
        }
        catch { }
    }

    private void ServerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowSelectedServerValues();

    private void RefreshServerValues_Click(object sender, RoutedEventArgs e) => ShowSelectedServerValues();

    private void ShowSelectedServerValues()
    {
        if (ServerListBox.SelectedItem is not ExistingServer server)
        {
            SettingsGrid.ItemsSource = null;
            return;
        }
        var rows = new List<ConfigValueRow>();
        rows.AddRange(ParseIniForInspection(server.IniPath));
        if (File.Exists(server.SandboxPath)) rows.AddRange(ParseLuaForInspection(server.SandboxPath));
        SettingsGrid.ItemsSource = rows;
        ScanSummaryText.Text = $"{server.Name}：共 {rows.Count} 個設定；自訂 {rows.Count(x => x.Status == "已修改")}";
    }

    private void UseSelectedServer_Click(object sender, RoutedEventArgs e)
    {
        if (ServerListBox.SelectedItem is not ExistingServer server)
        {
            MessageBox.Show("請先選擇一個伺服器。");
            return;
        }
        DataPathBox.Text = server.DataDirectory;
        ServerNameBox.Text = server.Name;
        loadedConfigIdentity = null;
        loadedFileStates.Clear();
        TryLoadExistingConfig(false);
    }

    private void ResetSelectedDefault_Click(object sender, RoutedEventArgs e)
    {
        SettingsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        if (SettingsGrid.SelectedItem is not ConfigValueRow row)
        {
            MessageBox.Show("請先在表格中選擇一個設定格。");
            return;
        }
        if (!row.CanReset)
        {
            MessageBox.Show("這個欄位的 Build 42 設定檔只提供文字說明，沒有可靠的原始預設值，因此不會猜測寫入。");
            return;
        }
        row.CurrentValue = row.DefaultValue;
        row.Status = "待恢復";
        SettingsGrid.Items.Refresh();
        ScanSummaryText.Text = $"已暫存：{row.Key} → {row.DefaultValue}（尚未寫入檔案）";
    }

    private void ResetAllDefaults_Click(object sender, RoutedEventArgs e)
    {
        SettingsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        if (SettingsGrid.ItemsSource is not IEnumerable<ConfigValueRow> source) return;
        var count = 0;
        foreach (var row in source.Where(x => x.CanReset))
        {
            row.CurrentValue = row.DefaultValue;
            row.Status = "待恢復";
            count++;
        }
        SettingsGrid.Items.Refresh();
        ScanSummaryText.Text = $"已暫存 {count} 個可靠預設值；尚未寫入檔案";
    }

    private void SaveInspectedValues_Click(object sender, RoutedEventArgs e)
    {
        SettingsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        if (ServerListBox.SelectedItem is not ExistingServer server ||
            SettingsGrid.ItemsSource is not IEnumerable<ConfigValueRow> rows) return;
        var changedRows = rows.Where(row =>
            !string.Equals(row.CurrentValue, row.OriginalValue, StringComparison.Ordinal)).ToList();
        if (changedRows.Count == 0) { MessageBox.Show("目前沒有待套用的設定變更。"); return; }
        var invalidRows = changedRows.Where(row =>
            row.MinimumValue.HasValue && row.MaximumValue.HasValue &&
            (!TryParseFlexibleDouble(row.CurrentValue, out var value) ||
             value < row.MinimumValue.Value || value > row.MaximumValue.Value)).ToList();
        if (invalidRows.Count > 0)
        {
            MessageBox.Show("以下數值超出目前設定檔註記或 Build 42 規格，未寫入：\n\n" +
                string.Join("\n", invalidRows.Select(row =>
                    $"• {row.Key}：{row.CurrentValue}（允許 {row.AllowedRange}）")),
                "拒絕超出範圍的設定", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show("確定要把表格中的待套用值寫入所選伺服器嗎？\n寫入前會建立 .manager-backup。",
            "確認儲存", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        DataPathBox.Text = server.DataDirectory;
        ServerNameBox.Text = server.Name;
        LoadRawFiles();
        var ini = RawIniBox.Text;
        var lua = RawSandboxBox.Text;
        foreach (var row in changedRows)
        {
            if (row.Category == "伺服器 INI")
                ini = ReplaceIniValue(ini, row.Key, row.CurrentValue);
            else
            {
                var dot = row.Key.IndexOf('.');
                var section = dot > 0 ? row.Key[..dot] : null;
                var key = dot > 0 ? row.Key[(dot + 1)..] : row.Key;
                lua = ReplaceLuaWholeLineValue(lua, key, row.CurrentValue, section);
            }
        }
        if (!ValidateLuaStructure(lua, out var validationError))
        {
            MessageBox.Show($"SandboxVars.lua 驗證失敗，原檔不會被修改：\n{validationError}",
                "拒絕寫入", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        RawIniBox.Text = ini;
        RawSandboxBox.Text = lua;
        RunExplicitConfigWrite(SaveRawCore);
        ShowSelectedServerValues();
    }

    private static string ReplaceIniValue(string text, string key, string value)
    {
        var pattern = $@"(?m)^({System.Text.RegularExpressions.Regex.Escape(key)}=).*$";
        return new System.Text.RegularExpressions.Regex(pattern)
            .Replace(text, $"${{1}}{value}", 1);
    }

    private static string ReplaceLuaWholeLineValue(string text, string key, string value, string? section)
    {
        var start = 0; var end = text.Length;
        if (section != null)
        {
            start = text.IndexOf(section + " = {", StringComparison.Ordinal);
            if (start < 0) return text;
            end = FindLuaSectionEnd(text, text.IndexOf('{', start));
        }
        var segment = text[start..end];
        var pattern = $@"(?m)^(\s*{System.Text.RegularExpressions.Regex.Escape(key)}\s*=\s*).*(,\s*)$";
        var regex = new System.Text.RegularExpressions.Regex(pattern);
        var replaced = regex.Replace(segment,
            match => match.Groups[1].Value + value + match.Groups[2].Value, 1);
        return text[..start] + replaced + text[end..];
    }

    private IEnumerable<ConfigValueRow> ParseIniForInspection(string path)
    {
        var comments = new List<string>();
        foreach (var raw in ConfigFileEncoding.ReadAllLines(path, SelectedEncodingMode()))
        {
            var line = raw.Trim();
            if (line.StartsWith('#') || line.StartsWith(';'))
            {
                comments.Add(line[1..].Trim());
                continue;
            }
            if (line.Length == 0) { comments.Clear(); continue; }
            var equals = line.IndexOf('=');
            if (equals <= 0) { comments.Clear(); continue; }
            var key = line[..equals].Trim();
            var current = line[(equals + 1)..].Trim();
            var fallback = ExtractCommentDefault(comments);
            var defaultValue = fallback ??
                (B42Defaults.TryGetValue(key, out var known) ? known : "遊戲未註明");
            var commentRange = ExtractCommentRange(comments);
            var range = commentRange.Display.Length > 0 ? commentRange :
                B42Ranges.TryGetValue(key, out var knownRange)
                    ? (knownRange.Min, knownRange.Max, knownRange.Display)
                    : (null, null, "未註明");
            var source = fallback != null || commentRange.Display.Length > 0
                ? "目前設定檔註記"
                : B42Defaults.ContainsKey(key) || B42Ranges.ContainsKey(key)
                    ? "內建 B42 參考"
                    : "遊戲未註明";
            yield return MakeInspectionRow("伺服器 INI", key, current, defaultValue,
                range.Item3, source, range.Item1, range.Item2);
            comments.Clear();
        }
    }

    private IEnumerable<ConfigValueRow> ParseLuaForInspection(string path)
    {
        var comments = new List<string>();
        var sections = new Stack<string>();
        foreach (var raw in ConfigFileEncoding.ReadAllLines(path, SelectedEncodingMode()))
        {
            var line = raw.Trim();
            if (line.StartsWith("--"))
            {
                comments.Add(line[2..].Trim());
                continue;
            }
            var sectionMatch = System.Text.RegularExpressions.Regex.Match(line, @"^([A-Za-z0-9_]+)\s*=\s*\{$");
            if (sectionMatch.Success)
            {
                var section = sectionMatch.Groups[1].Value;
                if (!section.Equals("SandboxVars", StringComparison.OrdinalIgnoreCase)) sections.Push(section);
                comments.Clear();
                continue;
            }
            if (line.StartsWith('}'))
            {
                if (sections.Count > 0) sections.Pop();
                comments.Clear();
                continue;
            }
            var valueMatch = System.Text.RegularExpressions.Regex.Match(line, @"^([A-Za-z0-9_]+)\s*=\s*(.+?),?\s*$");
            if (!valueMatch.Success) { if (line.Length > 0) comments.Clear(); continue; }
            var key = valueMatch.Groups[1].Value;
            var current = valueMatch.Groups[2].Value.TrimEnd(',').Trim();
            var fullKey = sections.Count > 0 ? sections.Peek() + "." + key : key;
            var fallback = ExtractCommentDefault(comments);
            var defaultValue = fallback ??
                (B42Defaults.TryGetValue(fullKey, out var nested) ? nested :
                    B42Defaults.TryGetValue(key, out var known) ? known : "遊戲未註明");
            var commentRange = ExtractCommentRange(comments);
            var range = commentRange.Display.Length > 0 ? commentRange :
                B42Ranges.TryGetValue(fullKey, out var nestedRange) ? nestedRange :
                B42Ranges.TryGetValue(key, out var knownRange)
                    ? knownRange
                    : (null, null, "未註明");
            var source = fallback != null || commentRange.Display.Length > 0
                ? "目前設定檔註記"
                : B42Defaults.ContainsKey(fullKey) || B42Defaults.ContainsKey(key) ||
                  B42Ranges.ContainsKey(fullKey) || B42Ranges.ContainsKey(key)
                    ? "內建 B42 參考"
                    : "遊戲未註明";
            yield return MakeInspectionRow(sections.Count > 0 ? sections.Peek() : "沙盒世界",
                fullKey, current, defaultValue, range.Item3, source, range.Item1, range.Item2);
            comments.Clear();
        }
    }

    private static string? ExtractCommentDefault(IEnumerable<string> comments)
    {
        foreach (var comment in comments.Reverse())
        {
            var match = System.Text.RegularExpressions.Regex.Match(comment,
                @"(?:預設|Default)\s*[=:]\s*([^\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value.Trim().TrimEnd('.', '。');
        }
        return null;
    }

    private static (double? Min, double? Max, string Display) ExtractCommentRange(IEnumerable<string> comments)
    {
        var text = string.Join("\n", comments);
        var minMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:Minimum|Min|最小值?)\s*[=:]\s*(-?\d+(?:\.\d+)?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var maxMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(?:Maximum|Max|最大值?)\s*[=:]\s*(-?\d+(?:\.\d+)?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!minMatch.Success || !maxMatch.Success ||
            !double.TryParse(minMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var min) ||
            !double.TryParse(maxMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
            return (null, null, "");
        return (min, max, $"{minMatch.Groups[1].Value}–{maxMatch.Groups[1].Value}");
    }

    private static ConfigValueRow MakeInspectionRow(string category, string key, string current,
        string defaultValue, string allowedRange, string source, double? minimum, double? maximum)
    {
        var comparable = defaultValue != "遊戲未註明" &&
            System.Text.RegularExpressions.Regex.IsMatch(defaultValue, @"^(true|false|-?\d+(?:\.\d+)?)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var same = comparable && string.Equals(NormalizeConfigValue(current), NormalizeConfigValue(defaultValue),
            StringComparison.OrdinalIgnoreCase);
        return new ConfigValueRow {
            Category = category, Key = key, CurrentValue = current, OriginalValue = current,
            DefaultValue = defaultValue, AllowedRange = allowedRange, MetadataSource = source,
            MinimumValue = minimum, MaximumValue = maximum,
            Status = !comparable ? "供參考" : same ? "預設" : "已修改", CanReset = comparable
        };
    }

    private static string NormalizeConfigValue(string value)
    {
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var number))
            return number.ToString("0.########", System.Globalization.CultureInfo.InvariantCulture);
        return value.Trim().Trim('"');
    }

    private void LoadRaw_Click(object sender, RoutedEventArgs e) => LoadRawFiles();

    private void LoadRawFiles()
    {
        var serverDir = Path.Combine(DataPathBox.Text.Trim(), "Server");
        var name = ServerNameBox.Text.Trim();
        var ini = Path.Combine(serverDir, name + ".ini");
        var sandbox = Path.Combine(serverDir, name + "_SandboxVars.lua");
        if (File.Exists(ini))
        {
            var loaded = ConfigFileEncoding.Read(ini, SelectedEncodingMode());
            RawIniBox.Text = loaded.Text;
            Log($"讀取 {Path.GetFileName(ini)}：{loaded.Encoding.WebName}，儲存時保持相同編碼。");
        }
        else RawIniBox.Text = "";
        if (File.Exists(sandbox))
        {
            var loaded = ConfigFileEncoding.Read(sandbox, SelectedEncodingMode());
            RawSandboxBox.Text = loaded.Text;
            Log($"讀取 {Path.GetFileName(sandbox)}：{loaded.Encoding.WebName}，儲存時保持相同編碼。");
        }
        else RawSandboxBox.Text = "";
        if (File.Exists(ini) || File.Exists(sandbox)) CaptureConfigState();
        Log($"已載入 {name} 的原始設定檔。");
    }

    private void SaveRaw_Click(object sender, RoutedEventArgs e) => RunExplicitConfigWrite(SaveRawCore);

    private void SaveRawCore()
    {
        if (serverProcess is { HasExited: false })
        {
            MessageBox.Show("請先存檔並關閉伺服器，再儲存原始設定，避免被遊戲覆寫。");
            return;
        }
        if (!CanWriteConfiguration()) return;
        try
        {
            EnsureExplicitConfigWriteAuthorized();
            var serverDir = Path.Combine(DataPathBox.Text.Trim(), "Server");
            var name = ServerNameBox.Text.Trim();
            Directory.CreateDirectory(serverDir);
            if (!string.IsNullOrWhiteSpace(RawIniBox.Text))
                ConfigFileEncoding.WritePreservingEncoding(
                    Path.Combine(serverDir, name + ".ini"), RawIniBox.Text, SelectedEncodingMode());
            if (!string.IsNullOrWhiteSpace(RawSandboxBox.Text))
            {
                if (!RawSandboxBox.Text.Contains("SandboxVars", StringComparison.Ordinal) ||
                    !IsBuild42StableSandbox(RawSandboxBox.Text))
                {
                    MessageBox.Show("SandboxVars 必須包含 SandboxVars 表格，以及目前 Build 42 VERSION = 6。");
                    return;
                }
                if (!ValidateLuaStructure(RawSandboxBox.Text, out var validationError))
                {
                    MessageBox.Show($"SandboxVars.lua 結構驗證失敗，原檔不會被修改：\n{validationError}",
                        "拒絕寫入", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                ConfigFileEncoding.WritePreservingEncoding(
                    Path.Combine(serverDir, name + "_SandboxVars.lua"), RawSandboxBox.Text, SelectedEncodingMode());
            }
            CaptureConfigState();
            Log("已儲存原始 INI 與 SandboxVars.lua。");
            MessageBox.Show("原始設定檔已儲存。");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "儲存失敗"); }
    }

    private static bool ValidateLuaStructure(string text, out string error)
    {
        var braces = 0; var line = 1; var quoteStartLine = 0;
        var inString = false; var escaped = false; var inComment = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\n') { line++; inComment = false; }
            if (inComment) continue;
            if (!inString && c == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                inComment = true; i++; continue;
            }
            if (inString)
            {
                if (escaped) { escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (c == '"') { inString = true; quoteStartLine = line; }
            else if (c == '{') braces++;
            else if (c == '}' && --braces < 0)
            {
                error = $"第 {line} 行出現多餘的右大括號。"; return false;
            }
        }
        if (inString) { error = $"第 {quoteStartLine} 行開始的字串沒有結束引號。"; return false; }
        if (braces != 0) { error = $"大括號不平衡，差值為 {braces}。"; return false; }
        error = "";
        return true;
    }

    private void RestoreConfigBackup_Click(object sender, RoutedEventArgs e)
    {
        var basePath = Path.Combine(DataPathBox.Text.Trim(), "Server", ServerNameBox.Text.Trim());
        var targets = new[] { basePath + ".ini", basePath + "_SandboxVars.lua" };
        var available = targets.Where(path => File.Exists(path + ".manager-backup")).ToList();
        if (available.Count == 0)
        {
            MessageBox.Show("目前伺服器找不到 `.manager-backup` 設定備份。");
            return;
        }
        var names = string.Join("\n", available.Select(Path.GetFileName));
        if (MessageBox.Show($"將從管理器寫入前備份還原：\n{names}\n\n目前損壞檔會另存為 `.broken-時間.bak`。",
            "確認還原設定", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            foreach (var path in available)
            {
                if (File.Exists(path)) File.Copy(path, path + $".broken-{stamp}.bak", false);
                File.Copy(path + ".manager-backup", path, true);
                Log($"已還原：{path}");
            }
            loadedConfigIdentity = null;
            loadedFileStates.Clear();
            TryLoadExistingConfig(false);
        }
        catch (Exception ex) { MessageBox.Show($"還原失敗：{ex.Message}", "錯誤"); }
    }

    private void RunExplicitConfigWrite(Action action)
    {
        if (explicitConfigWriteAuthorized) throw new InvalidOperationException("設定寫入授權不可巢狀使用。");
        explicitConfigWriteAuthorized = true;
        try { action(); }
        finally { explicitConfigWriteAuthorized = false; }
    }

    private void EnsureExplicitConfigWriteAuthorized()
    {
        if (!explicitConfigWriteAuthorized)
            throw new InvalidOperationException("設定寫入已被阻擋：必須由使用者明確按下儲存按鈕。");
    }

    private void ReadConfig_Click(object sender, RoutedEventArgs e) => TryLoadExistingConfig(false);

    private bool TryLoadExistingConfig(bool silent)
    {
        // 使用者可能剛在基礎設定切換安裝位置、資料目錄或設定檔名稱。
        // 必須先同步定位值，否則 SettingsToUi() 會在讀檔後覆蓋回舊路徑。
        var requestedDataDirectory = DataPathBox.Text.Trim();
        var requestedServerName = ServerNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(requestedDataDirectory) ||
            string.IsNullOrWhiteSpace(requestedServerName))
        {
            if (!silent) MessageBox.Show("資料目錄與設定檔名稱不可空白。");
            return false;
        }
        settings.SteamCmdPath = SteamCmdPathBox.Text.Trim();
        settings.InstallDirectory = InstallPathBox.Text.Trim();
        settings.DataDirectory = requestedDataDirectory;
        settings.ServerName = requestedServerName;
        LoadRawFiles();
        if (string.IsNullOrWhiteSpace(RawIniBox.Text) && string.IsNullOrWhiteSpace(RawSandboxBox.Text))
        {
            if (!silent) MessageBox.Show("找不到目前伺服器名稱所對應的設定檔；將視為新伺服器。");
            loadedConfigIdentity = null;
            loadedFileStates.Clear();
            return false;
        }
        var sandboxCompatible = string.IsNullOrWhiteSpace(RawSandboxBox.Text) ||
            IsBuild42StableSandbox(RawSandboxBox.Text);
        if (!sandboxCompatible)
        {
            if (!silent) MessageBox.Show("此管理器只支援目前 Build 42 Sandbox VERSION = 6。\n目前檔案只會顯示於原始設定頁，不會開放 GUI 覆寫。",
                "不相容的 Sandbox 版本", MessageBoxButton.OK, MessageBoxImage.Warning);
            Log("Sandbox 版本不相容；仍會從 INI 讀取基礎設定，沙盒欄位維持目前值。");
        }
        // 讀檔時缺少欄位一律回到管理器內建的 Build 42 預設，
        // 不可沿用 manager-settings.json 的舊值，否則 GUI 會看似「亂跳」。
        var defaults = new ServerSettings();
        settings.PublicName = IniString(RawIniBox.Text, "PublicName", defaults.PublicName);
        settings.Description = IniString(RawIniBox.Text, "PublicDescription", defaults.Description);
        settings.Password = IniString(RawIniBox.Text, "Password", defaults.Password);
        settings.DefaultPort = IniInt(RawIniBox.Text, "DefaultPort", defaults.DefaultPort);
        settings.UDPPort = IniInt(RawIniBox.Text, "UDPPort", defaults.UDPPort);
        settings.MaxPlayers = IniInt(RawIniBox.Text, "MaxPlayers", defaults.MaxPlayers);
        settings.Public = IniBool(RawIniBox.Text, "Public", defaults.Public);
        settings.Open = IniBool(RawIniBox.Text, "Open", defaults.Open);
        settings.PauseEmpty = IniBool(RawIniBox.Text, "PauseEmpty", defaults.PauseEmpty);
        settings.Pvp = IniBool(RawIniBox.Text, "PVP", defaults.Pvp);
        settings.SafetySystem = IniBool(RawIniBox.Text, "SafetySystem", defaults.SafetySystem);
        settings.SleepAllowed = IniBool(RawIniBox.Text, "SleepAllowed", defaults.SleepAllowed);
        settings.SleepNeeded = IniBool(RawIniBox.Text, "SleepNeeded", defaults.SleepNeeded);
        settings.VoiceEnable = IniBool(RawIniBox.Text, "VoiceEnable", defaults.VoiceEnable);
        settings.PlayerSafehouse = IniBool(RawIniBox.Text, "PlayerSafehouse", defaults.PlayerSafehouse);
        settings.PingLimit = IniInt(RawIniBox.Text, "PingLimit", defaults.PingLimit);
        settings.SaveEveryMinutes = IniInt(RawIniBox.Text, "SaveWorldEveryMinutes", defaults.SaveEveryMinutes);
        settings.BuiltInBackups = IniInt(RawIniBox.Text, "BackupsCount", defaults.BuiltInBackups);
        settings.SpawnItems = IniString(RawIniBox.Text, "SpawnItems", defaults.SpawnItems);
        settings.WelcomeMessage = IniString(RawIniBox.Text, "ServerWelcomeMessage", defaults.WelcomeMessage);
        settings.WorkshopItems = IniString(RawIniBox.Text, "WorkshopItems", defaults.WorkshopItems);
        settings.Mods = IniString(RawIniBox.Text, "Mods", defaults.Mods);
        settings.MapFolders = IniString(RawIniBox.Text, "Map", defaults.MapFolders);
        settings.RconPort = IniInt(RawIniBox.Text, "RCONPort", defaults.RconPort);
        settings.RconPassword = IniString(RawIniBox.Text, "RCONPassword", defaults.RconPassword);
        settings.AllowNonAsciiUsername = IniBool(RawIniBox.Text, "AllowNonAsciiUsername", defaults.AllowNonAsciiUsername);
        settings.AnnounceDeath = IniBool(RawIniBox.Text, "AnnounceDeath", defaults.AnnounceDeath);
        settings.MaxAccountsPerUser = IniInt(RawIniBox.Text, "MaxAccountsPerUser", defaults.MaxAccountsPerUser);
        settings.MapRemotePlayerVisibility = IniInt(RawIniBox.Text, "MapRemotePlayerVisibility", defaults.MapRemotePlayerVisibility);
        settings.PlayerRespawnWithSelf = IniBool(RawIniBox.Text, "PlayerRespawnWithSelf", defaults.PlayerRespawnWithSelf);
        settings.PlayerRespawnWithOther = IniBool(RawIniBox.Text, "PlayerRespawnWithOther", defaults.PlayerRespawnWithOther);
        settings.SafehouseAllowRespawn = IniBool(RawIniBox.Text, "SafehouseAllowRespawn", defaults.SafehouseAllowRespawn);
        settings.Faction = IniBool(RawIniBox.Text, "Faction", defaults.Faction);
        settings.FactionDaySurvivedToCreate = IniInt(RawIniBox.Text, "FactionDaySurvivedToCreate", defaults.FactionDaySurvivedToCreate);
        settings.SafehouseDaySurvivedToClaim = IniInt(RawIniBox.Text, "SafehouseDaySurvivedToClaim", defaults.SafehouseDaySurvivedToClaim);
        settings.SafeHouseRemovalTime = IniInt(RawIniBox.Text, "SafeHouseRemovalTime", defaults.SafeHouseRemovalTime);
        settings.PvpFirearmDamageModifier = IniDouble(RawIniBox.Text, "PVPFirearmDamageModifier", defaults.PvpFirearmDamageModifier);
        settings.PvpMeleeDamageModifier = IniDouble(RawIniBox.Text, "PVPMeleeDamageModifier", defaults.PvpMeleeDamageModifier);
        settings.SpeedLimit = IniDouble(RawIniBox.Text, "SpeedLimit", defaults.SpeedLimit);
        settings.DenyLoginOnOverloadedServer = IniBool(RawIniBox.Text, "DenyLoginOnOverloadedServer", defaults.DenyLoginOnOverloadedServer);
        settings.LoginQueueEnabled = IniBool(RawIniBox.Text, "LoginQueueEnabled", defaults.LoginQueueEnabled);
        settings.LoginQueueConnectTimeout = IniInt(RawIniBox.Text, "LoginQueueConnectTimeout", defaults.LoginQueueConnectTimeout);
        if (sandboxCompatible)
        {
            settings.DayLength = LuaInt(RawSandboxBox.Text, "DayLength", defaults.DayLength);
            settings.WaterShutDays = LuaInt(RawSandboxBox.Text, "WaterShutModifier", defaults.WaterShutDays);
            settings.ElectricityShutDays = LuaInt(RawSandboxBox.Text, "ElecShutModifier", defaults.ElectricityShutDays);
            settings.XpMultiplier = LuaDouble(RawSandboxBox.Text, "Global", defaults.XpMultiplier, "MultiplierConfig");
            settings.FoodLoot = LuaDouble(RawSandboxBox.Text, "FoodLootNew", defaults.FoodLoot);
            settings.WeaponLoot = LuaDouble(RawSandboxBox.Text, "WeaponLootNew", defaults.WeaponLoot);
            settings.AmmoLoot = LuaDouble(RawSandboxBox.Text, "AmmoLootNew", defaults.AmmoLoot);
            settings.MedicalLoot = LuaDouble(RawSandboxBox.Text, "MedicalLootNew", defaults.MedicalLoot);
            settings.OtherLoot = LuaDouble(RawSandboxBox.Text, "OtherLootNew", defaults.OtherLoot);
            settings.LootRespawnHours = LuaInt(RawSandboxBox.Text, "HoursForLootRespawn", defaults.LootRespawnHours);
            settings.CharacterFreePoints = LuaInt(RawSandboxBox.Text, "CharacterFreePoints", defaults.CharacterFreePoints);
            settings.StarterKit = LuaBool(RawSandboxBox.Text, "StarterKit", defaults.StarterKit);
            settings.StatsDecrease = LuaInt(RawSandboxBox.Text, "StatsDecrease", defaults.StatsDecrease);
            settings.EndRegen = LuaInt(RawSandboxBox.Text, "EndRegen", defaults.EndRegen);
            settings.Nutrition = LuaBool(RawSandboxBox.Text, "Nutrition", defaults.Nutrition);
            settings.InjurySeverity = LuaInt(RawSandboxBox.Text, "InjurySeverity", defaults.InjurySeverity);
            settings.BoneFracture = LuaBool(RawSandboxBox.Text, "BoneFracture", defaults.BoneFracture);
            settings.ClothingDegradation = LuaInt(RawSandboxBox.Text, "ClothingDegradation", defaults.ClothingDegradation);
            settings.MultiHitZombies = LuaBool(RawSandboxBox.Text, "MultiHitZombies", defaults.MultiHitZombies);
            settings.RearVulnerability = LuaInt(RawSandboxBox.Text, "RearVulnerability", defaults.RearVulnerability);
            settings.BloodLevel = LuaInt(RawSandboxBox.Text, "BloodLevel", defaults.BloodLevel);
            settings.PlayerDamageFromCrash = LuaBool(RawSandboxBox.Text, "PlayerDamageFromCrash", defaults.PlayerDamageFromCrash);
            settings.ZombieSpeed = LuaInt(RawSandboxBox.Text, "Speed", defaults.ZombieSpeed, "ZombieLore");
            settings.ZombieStrength = LuaInt(RawSandboxBox.Text, "Strength", defaults.ZombieStrength, "ZombieLore");
            settings.ZombieToughness = LuaInt(RawSandboxBox.Text, "Toughness", defaults.ZombieToughness, "ZombieLore");
            settings.Transmission = LuaInt(RawSandboxBox.Text, "Transmission", defaults.Transmission, "ZombieLore");
            settings.PopulationMultiplier = LuaDouble(RawSandboxBox.Text, "PopulationMultiplier", defaults.PopulationMultiplier, "ZombieConfig");
            settings.PopulationPeakMultiplier = LuaDouble(RawSandboxBox.Text, "PopulationPeakMultiplier", defaults.PopulationPeakMultiplier, "ZombieConfig");
            settings.PopulationPeakDay = LuaInt(RawSandboxBox.Text, "PopulationPeakDay", defaults.PopulationPeakDay, "ZombieConfig");
            settings.RespawnHours = LuaDouble(RawSandboxBox.Text, "RespawnHours", defaults.RespawnHours, "ZombieConfig");
        }
        SettingsToUi();
        CaptureConfigState();
        Log("已先讀取現有 INI 與 Build 42 SandboxVars；現在可以安全修改。");
        if (!silent) MessageBox.Show("已從目前 INI 與 Build 42 SandboxVars 讀入 GUI。");
        return true;
    }

    private string CurrentConfigIdentity() =>
        Path.GetFullPath(Path.Combine(DataPathBox.Text.Trim(), "Server", ServerNameBox.Text.Trim()))
            .TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();

    private IEnumerable<string> CurrentConfigFiles()
    {
        var basePath = Path.Combine(DataPathBox.Text.Trim(), "Server", ServerNameBox.Text.Trim());
        yield return basePath + ".ini";
        yield return basePath + "_SandboxVars.lua";
    }

    private void CaptureConfigState()
    {
        loadedConfigIdentity = CurrentConfigIdentity();
        loadedFileStates.Clear();
        foreach (var path in CurrentConfigFiles())
        {
            if (!File.Exists(path)) continue;
            var file = new FileInfo(path);
            loadedFileStates[path] = (file.Length, file.LastWriteTimeUtc);
        }
    }

    private bool CanWriteConfiguration()
    {
        var sandboxPath = Path.Combine(DataPathBox.Text.Trim(), "Server", ServerNameBox.Text.Trim() + "_SandboxVars.lua");
        if (File.Exists(sandboxPath))
        {
            try
            {
                var sandboxText = ConfigFileEncoding.ReadText(sandboxPath, SelectedEncodingMode());
                if (!IsBuild42StableSandbox(sandboxText))
                {
                    MessageBox.Show("完整設定只支援目前 Build 42 Sandbox VERSION = 6。\n為避免部分寫入，INI 與 Sandbox 均未修改。",
                        "不相容的 Sandbox 版本", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法驗證 Sandbox 版本：{ex.Message}\n設定未修改。", "讀取失敗");
                return false;
            }
        }
        var existing = CurrentConfigFiles().Where(File.Exists).ToList();
        if (existing.Count == 0) return true;
        if (!string.Equals(loadedConfigIdentity, CurrentConfigIdentity(), StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("這組設定檔已存在。請先按「從目前檔案讀取 GUI」，確認讀取完成後再修改與儲存。",
                "尚未讀取現有設定", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        foreach (var path in existing)
        {
            var file = new FileInfo(path);
            if (!loadedFileStates.TryGetValue(path, out var oldState) ||
                oldState.Length != file.Length || oldState.LastWriteUtc != file.LastWriteTimeUtc)
            {
                MessageBox.Show($"{Path.GetFileName(path)} 在讀取後又被其他程式修改。\n請重新讀取，管理器不會覆蓋較新的內容。",
                    "偵測到設定衝突", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        return true;
    }


    private void ScheduleNextRestart()
    {
        nextRestart = settings.AutoRestart && !automationRuntimeSuspended &&
            serverProcess is { HasExited: false }
            ? DateTime.Now.AddHours(settings.RestartHours) : null;
        UpdateNextRestartText();
    }

    private void ScheduleWorkshopUpdateCheck()
    {
        if (serverProcess is not { HasExited: false })
        {
            nextWorkshopUpdateCheck = null;
            UpdateWorkshopUpdateStatus();
            return;
        }
        if (!settings.AutoWorkshopUpdate || automationRuntimeSuspended)
        {
            nextWorkshopUpdateCheck = null;
            pendingWorkshopUpdateIds.Clear();
            nextWorkshopUpdateAnnouncement = null;
            UpdateWorkshopUpdateStatus();
            return;
        }
        nextWorkshopUpdateCheck = DateTime.Now.AddMinutes(
            settings.WorkshopUpdateCheckMinutes);
        if (!settings.WorkshopUpdateBroadcast)
            nextWorkshopUpdateAnnouncement = null;
        UpdateWorkshopUpdateStatus();
    }

    private void AutomationOptionChanged(object sender, RoutedEventArgs e)
    {
        if (suppressAutomationOptionEvents) return;
        var checkEnabled = AutoWorkshopUpdateCheck?.IsChecked == true;
        if (!checkEnabled && WorkshopUpdateBroadcastCheck?.IsChecked == true)
        {
            suppressAutomationOptionEvents = true;
            WorkshopUpdateBroadcastCheck.IsChecked = false;
            suppressAutomationOptionEvents = false;
        }
        UpdateWorkshopAutomationControlState();
        if (!uiInitialized) return;

        settings.AutoRestart = AutoRestartCheck.IsChecked == true;
        settings.AutoWorkshopUpdate = checkEnabled;
        settings.WorkshopUpdateBroadcast = checkEnabled &&
            WorkshopUpdateBroadcastCheck?.IsChecked == true;
        PersistSettings();

        if (serverProcess is { HasExited: false })
        {
            RenewAutomationCancellation();
            if (restartAfterStopAutomated)
            {
                restartAfterStop = false;
                restartAfterStopAutomated = false;
            }
            workshopRestartInProgress = false;
            if (!settings.AutoWorkshopUpdate)
            {
                pendingWorkshopUpdateIds.Clear();
                nextWorkshopUpdateAnnouncement = null;
            }
            ScheduleNextRestart();
            ScheduleWorkshopUpdateCheck();
            Log($"自動化已即時更新：定時重啟={(settings.AutoRestart ? "開" : "關")}、" +
                $"模組檢查={(settings.AutoWorkshopUpdate ? "開" : "關")}、" +
                $"玩家公告={(settings.WorkshopUpdateBroadcast ? "開" : "關")}。");
        }
    }

    private void UpdateWorkshopAutomationControlState()
    {
        if (AutoWorkshopUpdateCheck == null ||
            WorkshopUpdateCheckMinutesBox == null ||
            WorkshopUpdateBroadcastCheck == null ||
            WorkshopUpdateAnnouncementMinutesBox == null ||
            WorkshopUpdateMessageBox == null)
            return;
        var serverRunning = serverProcess is { HasExited: false };
        var checkEnabled = AutoWorkshopUpdateCheck.IsChecked == true;
        var broadcastEnabled = checkEnabled && WorkshopUpdateBroadcastCheck.IsChecked == true;
        AutoWorkshopUpdateCheck.IsEnabled = true;
        WorkshopUpdateCheckMinutesBox.IsEnabled = !serverRunning && checkEnabled;
        WorkshopUpdateBroadcastCheck.IsEnabled = checkEnabled;
        WorkshopUpdateAnnouncementMinutesBox.IsEnabled = !serverRunning && broadcastEnabled;
        WorkshopUpdateMessageBox.IsEnabled = !serverRunning && broadcastEnabled;
    }

    private void DisableAllAutomation_Click(object sender, RoutedEventArgs e)
    {
        suppressAutomationOptionEvents = true;
        AutoRestartCheck.IsChecked = false;
        AutoWorkshopUpdateCheck.IsChecked = false;
        WorkshopUpdateBroadcastCheck.IsChecked = false;
        suppressAutomationOptionEvents = false;
        settings.AutoRestart = false;
        settings.AutoWorkshopUpdate = false;
        settings.WorkshopUpdateBroadcast = false;
        automationCancellation.Cancel();
        if (restartAfterStopAutomated)
        {
            restartAfterStop = false;
            restartAfterStopAutomated = false;
        }
        workshopRestartInProgress = false;
        nextRestart = null;
        nextWorkshopUpdateCheck = null;
        nextWorkshopUpdateAnnouncement = null;
        pendingWorkshopUpdateIds.Clear();
        UpdateWorkshopAutomationControlState();
        UpdateNextRestartText();
        UpdateWorkshopUpdateStatus();
        PersistSettings();
        Log("使用者已立即停用全部自動化；目前 PZ 程序不會被自動重啟或因模組更新而關服。");
    }

    private async Task CheckWorkshopUpdatesAsync(CancellationToken cancellationToken)
    {
        if (serverProcess is not { HasExited: false } ||
            !settings.AutoWorkshopUpdate ||
            Interlocked.Exchange(ref workshopUpdateCheckRunning, 1) != 0)
            return;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ids = NormalizeWorkshopList(settings.WorkshopItems)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(id => ulong.TryParse(id, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ids.Count == 0)
            {
                pendingWorkshopUpdateIds.Clear();
                nextWorkshopUpdateAnnouncement = null;
                LocalizationService.SetText(WorkshopUpdateStatusText,
                    "模組更新：尚未設定 Workshop 項目");
                return;
            }

            var installed = ReadInstalledWorkshopUpdateTimes(ids);
            if (installed.Count == 0)
            {
                LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                    "模組更新：找不到本機版本紀錄；下次檢查 {0}",
                    nextWorkshopUpdateCheck?.ToString("HH:mm") ?? "—");
                Log("找不到 appworkshop_108600.acf 的本機 timeupdated；為避免誤判，不會自動重啟。");
                return;
            }

            var remote = await FetchPublishedWorkshopUpdateTimesAsync(ids, cancellationToken);
            var outdated = ids.Where(id =>
                    installed.TryGetValue(id, out var localTime) &&
                    remote.TryGetValue(id, out var remoteTime) &&
                    remoteTime > localTime)
                .ToList();
            var previouslyPending = pendingWorkshopUpdateIds.Count > 0;
            pendingWorkshopUpdateIds.Clear();
            foreach (var id in outdated) pendingWorkshopUpdateIds.Add(id);

            var missingLocal = ids.Count(id => !installed.ContainsKey(id));
            if (missingLocal > 0)
                Log($"{missingLocal} 個 Workshop 項目沒有本機 timeupdated，已略過以避免誤判。");

            if (pendingWorkshopUpdateIds.Count == 0)
            {
                if (previouslyPending) Log("待更新模組已完成更新，本次不再需要重啟。");
                nextWorkshopUpdateAnnouncement = null;
                lastKnownOnlinePlayerCount = null;
                LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                    "模組更新：已是最新；下次檢查 {0}",
                    nextWorkshopUpdateCheck?.ToString("HH:mm") ?? "—");
                return;
            }

            if (!previouslyPending)
            {
                nextWorkshopUpdateAnnouncement =
                    settings.WorkshopUpdateBroadcast ? DateTime.Now : null;
                Log($"偵測到 {pendingWorkshopUpdateIds.Count} 個 Workshop 更新：{string.Join(", ", pendingWorkshopUpdateIds)}");
            }
            await EvaluatePendingWorkshopUpdatesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log("Workshop 更新自動化已取消。");
        }
        catch (Exception ex)
        {
            Log($"Workshop 更新檢查失敗：{ex.Message}");
            LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                "模組更新：檢查失敗，將於 {0} 重試",
                nextWorkshopUpdateCheck?.ToString("HH:mm") ?? "—");
            if (pendingWorkshopUpdateIds.Count > 0)
                await EvaluatePendingWorkshopUpdatesAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref workshopUpdateCheckRunning, 0);
        }
    }

    private async Task EvaluatePendingWorkshopUpdatesAsync(CancellationToken cancellationToken)
    {
        if (pendingWorkshopUpdateIds.Count == 0 ||
            serverProcess is not { HasExited: false } ||
            workshopRestartInProgress)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        var onlinePlayers = await QueryOnlinePlayersCoreAsync(cancellationToken, true);
        if (onlinePlayers is null)
        {
            if (settings.WorkshopUpdateBroadcast)
                nextWorkshopUpdateAnnouncement = DateTime.Now.AddMinutes(
                    Math.Min(settings.WorkshopUpdateCheckMinutes,
                        settings.WorkshopUpdateAnnouncementMinutes));
            LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                "模組更新：無法確認在線人數，將於 {0} 再檢查",
                (nextWorkshopUpdateAnnouncement ?? nextWorkshopUpdateCheck)
                    ?.ToString("HH:mm") ?? "—");
            return;
        }

        if (onlinePlayers.Value == 0)
        {
            workshopRestartInProgress = true;
            LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                "模組更新：偵測到 {0} 個更新，正在安全重啟",
                pendingWorkshopUpdateIds.Count);
            Log("模組有更新且已確認目前無玩家；開始安全存檔、關服與重啟。");
            await SafeStopAsync(true, true, cancellationToken);
            if (serverProcess is { HasExited: false } && !restartAfterStop)
            {
                workshopRestartInProgress = false;
                nextWorkshopUpdateCheck = DateTime.Now.AddMinutes(
                    settings.WorkshopUpdateCheckMinutes);
            }
            return;
        }

        if (settings.WorkshopUpdateBroadcast &&
            (nextWorkshopUpdateAnnouncement is null ||
             DateTime.Now >= nextWorkshopUpdateAnnouncement.Value))
        {
            SendCommand($"servermsg \"{FormatWorkshopUpdateMessage()}\"");
            nextWorkshopUpdateAnnouncement = DateTime.Now.AddMinutes(
                settings.WorkshopUpdateAnnouncementMinutes);
            Log($"已公告模組更新；仍有 {onlinePlayers.Value} 位玩家，" +
                $"{settings.WorkshopUpdateAnnouncementMinutes} 分鐘後才會再次公告。");
        }
        if (settings.WorkshopUpdateBroadcast)
            LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                "模組更新：等待 {0} 位玩家離線；下次公告 {1}",
                onlinePlayers.Value,
                nextWorkshopUpdateAnnouncement?.ToString("HH:mm") ?? "—");
        else
            LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                "模組更新：等待 {0} 位玩家離線；公告已停用",
                onlinePlayers.Value);
    }

    private Dictionary<string, long> ReadInstalledWorkshopUpdateTimes(IEnumerable<string> ids)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var steamCmdDirectory = Path.GetDirectoryName(settings.SteamCmdPath) ?? "";
        var manifests = new[]
        {
            Path.Combine(settings.InstallDirectory, "steamapps", "workshop", "appworkshop_108600.acf"),
            Path.Combine(steamCmdDirectory, "steamapps", "workshop", "appworkshop_108600.acf")
        }.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in manifests)
        {
            if (!File.Exists(manifest)) continue;
            try
            {
                string text;
                using (var stream = new FileStream(manifest, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    text = reader.ReadToEnd();
                foreach (var pair in ParseInstalledWorkshopUpdateTimes(text, ids))
                    if (!result.TryGetValue(pair.Key, out var existing) || pair.Value > existing)
                        result[pair.Key] = pair.Value;
            }
            catch (Exception ex)
            {
                Log($"無法讀取 Workshop manifest {manifest}：{ex.Message}");
            }
        }
        return result;
    }

    private static Dictionary<string, long> ParseInstalledWorkshopUpdateTimes(
        string manifestText, IEnumerable<string> ids)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (Match idMatch in Regex.Matches(manifestText,
                         $"\"{Regex.Escape(id)}\"\\s*\\{{",
                         RegexOptions.CultureInvariant))
            {
                var opening = manifestText.IndexOf('{', idMatch.Index + idMatch.Length - 1);
                if (opening < 0) continue;
                var end = FindBalancedBlockEnd(manifestText, opening);
                if (end <= opening) continue;
                var block = manifestText[opening..end];
                var timeMatch = Regex.Match(block,
                    "\"timeupdated\"\\s*\"(?<value>\\d+)\"",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!timeMatch.Success ||
                    !long.TryParse(timeMatch.Groups["value"].Value,
                        NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    continue;
                if (!result.TryGetValue(id, out var existing) || value > existing)
                    result[id] = value;
            }
        }
        return result;
    }

    private async Task<Dictionary<string, long>> FetchPublishedWorkshopUpdateTimesAsync(
        IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PZServerManager/" + AppVersion);
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in ids.Chunk(100))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new List<KeyValuePair<string, string>>
            {
                new("itemcount", batch.Length.ToString(CultureInfo.InvariantCulture))
            };
            for (var index = 0; index < batch.Length; index++)
                values.Add(new($"publishedfileids[{index}]", batch[index]));
            using var response = await client.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                new FormUrlEncodedContent(values), cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            foreach (var pair in ParsePublishedWorkshopUpdateTimes(json))
                result[pair.Key] = pair.Value;
        }
        return result;
    }

    private static Dictionary<string, long> ParsePublishedWorkshopUpdateTimes(string json)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("response", out var response) ||
            !response.TryGetProperty("publishedfiledetails", out var details) ||
            details.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var item in details.EnumerateArray())
        {
            if (!item.TryGetProperty("result", out var itemResult) ||
                itemResult.GetInt32() != 1 ||
                !item.TryGetProperty("publishedfileid", out var idElement) ||
                !item.TryGetProperty("time_updated", out var timeElement))
                continue;
            var id = idElement.GetString() ?? "";
            if (id.Length == 0 || !timeElement.TryGetInt64(out var time)) continue;
            result[id] = time;
        }
        return result;
    }

    private static int FindBalancedBlockEnd(string text, int opening)
    {
        var depth = 0;
        for (var index = opening; index < text.Length; index++)
        {
            if (text[index] == '{') depth++;
            else if (text[index] == '}' && --depth == 0) return index + 1;
        }
        return -1;
    }

    private void UpdateWorkshopUpdateStatus()
    {
        if (serverProcess is not { HasExited: false })
        {
            LocalizationService.SetText(WorkshopUpdateStatusText,
                "模組更新：伺服器未啟動");
            return;
        }
        if (automationRuntimeSuspended)
        {
            LocalizationService.SetText(WorkshopUpdateStatusText,
                "模組更新：PZ CLI 無回應，自動化已暫停");
            return;
        }
        if (!settings.AutoWorkshopUpdate)
        {
            LocalizationService.SetText(WorkshopUpdateStatusText,
                "模組更新：監控已停用");
            return;
        }
        if (pendingWorkshopUpdateIds.Count > 0)
        {
            LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
                "模組更新：偵測到 {0} 個更新；下次確認 {1}",
                pendingWorkshopUpdateIds.Count,
                nextWorkshopUpdateCheck?.ToString("HH:mm") ?? "—");
            return;
        }
        LocalizationService.SetFormattedText(WorkshopUpdateStatusText,
            "模組更新：下次檢查 {0}",
            nextWorkshopUpdateCheck?.ToString("HH:mm") ?? "—");
    }

    private async void ScheduleTimer_Tick(object? sender, EventArgs e)
    {
        if (Interlocked.Exchange(ref scheduleTickRunning, 1) != 0) return;
        try
        {
            UpdateNextRestartText();
            if (serverProcess is not { HasExited: false }) return;
            if (!serverReadyForCommands) return;
            var automationToken = automationCancellation.Token;
            if (!automationRuntimeSuspended && settings.AutoWorkshopUpdate &&
                nextWorkshopUpdateCheck is not null &&
                DateTime.Now >= nextWorkshopUpdateCheck.Value)
            {
                nextWorkshopUpdateCheck = DateTime.Now.AddMinutes(
                    settings.WorkshopUpdateCheckMinutes);
                await CheckWorkshopUpdatesAsync(automationToken);
            }
            if (!automationRuntimeSuspended && settings.AutoWorkshopUpdate &&
                settings.WorkshopUpdateBroadcast &&
                pendingWorkshopUpdateIds.Count > 0 &&
                nextWorkshopUpdateAnnouncement is not null &&
                DateTime.Now >= nextWorkshopUpdateAnnouncement.Value)
            {
                await EvaluatePendingWorkshopUpdatesAsync(automationToken);
            }
            if (nextPlayerQuery is not null && DateTime.Now >= nextPlayerQuery.Value)
            {
                nextPlayerQuery = DateTime.Now.AddMinutes(settings.PlayerQueryMinutes);
                await QueryOnlinePlayersAsync();
            }
            if (automationRuntimeSuspended || nextRestart is null) return;
            var remaining = nextRestart.Value - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                nextRestart = null;
                await SafeStopAsync(true, true, automationToken);
            }
            else
            {
                var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
                if (minutes <= settings.WarningMinutes && minutes > 0 && Math.Abs(remaining.TotalSeconds % 60) < 16)
                    SendCommand($"servermsg \"{FormatRestartMessage(minutes)}\"");
            }
        }
        catch (OperationCanceledException)
        {
            Log("自動化排程已取消。");
        }
        catch (Exception ex)
        {
            Log($"自動化排程發生錯誤但 GUI 已繼續運作：{ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref scheduleTickRunning, 0);
        }
    }

    private async void RefreshPlayers_Click(object sender, RoutedEventArgs e) =>
        await QueryOnlinePlayersAsync();

    private Task<int?> QueryOnlinePlayersAsync() =>
        QueryOnlinePlayersCoreAsync(CancellationToken.None, true);

    private async Task<int?> QueryOnlinePlayersCoreAsync(
        CancellationToken cancellationToken, bool trackHealth)
    {
        var queriedProcess = serverProcess;
        if (queriedProcess is not { HasExited: false } || Interlocked.Exchange(ref playerQueryRunning, 1) != 0)
            return null;
        if (!serverReadyForCommands)
        {
            Interlocked.Exchange(ref playerQueryRunning, 0);
            Log("PZ 尚未回報 SERVER STARTED；暫不送出 players，以免在模組／世界載入期間誤判卡死。");
            return null;
        }
        try
        {
            lock (playerQueryLock) playerQueryOutput.Clear();
            playerQueryResponseSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            capturePlayerQueryOutput = true;
            SendCommand("players");
            try
            {
                await playerQueryResponseSignal.Task.WaitAsync(
                    TimeSpan.FromSeconds(10), cancellationToken);
                await Task.Delay(600, cancellationToken);
            }
            catch (TimeoutException)
            {
                if (trackHealth) RegisterCliHealthFailure(
                    "`players` 已送出，但 10 秒內沒有回傳玩家人數。");
                return null;
            }
            capturePlayerQueryOutput = false;
            if (!ReferenceEquals(serverProcess, queriedProcess) || queriedProcess.HasExited)
                return null;
            string response;
            lock (playerQueryLock) response = string.Join(Environment.NewLine, playerQueryOutput);
            var count = ApplyOnlinePlayerResponse(response, "伺服器控制台");
            if (count is null)
            {
                if (trackHealth) RegisterCliHealthFailure("`players` 回傳內容不完整，無法確認玩家人數。");
                return null;
            }
            if (trackHealth) RegisterCliHealthSuccess();
            nextPlayerQuery = DateTime.Now.AddMinutes(settings.PlayerQueryMinutes);
            return count;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            capturePlayerQueryOutput = false;
            playerQueryResponseSignal = null;
            Interlocked.Exchange(ref playerQueryRunning, 0);
        }
    }

    private int? ApplyOnlinePlayerResponse(string response, string source)
    {
        var players = ParseOnlinePlayers(response, out var reportedCount);
        var verifiedCount = reportedCount >= 0 ? reportedCount :
            players.Count > 0 ? players.Count : (int?)null;
        lastKnownOnlinePlayerCount = verifiedCount;
        OnlinePlayersListBox.ItemsSource = players;
        if (players.Count > 0)
            LocalizationService.SetFormattedText(OnlinePlayerSummaryText,
                "在線 {0} 人 • {1} • {2}", players.Count, source, DateTime.Now.ToString("HH:mm"));
        else if (reportedCount > 0)
            LocalizationService.SetFormattedText(OnlinePlayerSummaryText,
                "在線 {0} 人（名稱未能解析）• {1}", reportedCount, source);
        else if (reportedCount == 0)
            LocalizationService.SetFormattedText(OnlinePlayerSummaryText,
                "目前無玩家 • {0} • {1}", source, DateTime.Now.ToString("HH:mm"));
        else
            LocalizationService.SetFormattedText(OnlinePlayerSummaryText,
                "無法確認在線人數 • {0} • {1}", source, DateTime.Now.ToString("HH:mm"));
        Log(verifiedCount is null
            ? $"在線玩家查詢沒有完整回應（{source}）；不會據此自動重啟。"
            : $"在線玩家已更新：{verifiedCount.Value} 人（{source}）。");
        return verifiedCount;
    }

    private static List<string> ParseOnlinePlayers(string response, out int reportedCount)
    {
        reportedCount = -1;
        var countMatch = System.Text.RegularExpressions.Regex.Match(response,
            @"(?i)players?\s+connected\s*\((\d+)\)");
        if (countMatch.Success) int.TryParse(countMatch.Groups[1].Value, out reportedCount);
        if (System.Text.RegularExpressions.Regex.IsMatch(response,
            @"(?i)no\s+players?\s+(?:are\s+)?connected"))
            reportedCount = 0;

        var players = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in response.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            var marker = line.LastIndexOf('>');
            if (marker >= 0) line = line[(marker + 1)..].Trim();
            var match = System.Text.RegularExpressions.Regex.Match(line, @"^-\s*(.+?)\s*$");
            if (!match.Success) continue;
            var name = match.Groups[1].Value.Trim();
            if (name.Length > 0 && !name.Contains("players connected", StringComparison.OrdinalIgnoreCase))
                players.Add(name);
        }
        return players.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private async void RestartNow_Click(object sender, RoutedEventArgs e)
    {
        if (serverProcess is not { HasExited: false }) { MessageBox.Show("伺服器尚未啟動。"); return; }
        if (!UiToSettings()) return;
        SendCommand($"servermsg \"{FormatRestartMessage(1)}\"");
        await Task.Delay(TimeSpan.FromSeconds(50));
        SendCommand($"servermsg \"{FormatRestartMessage(0)}\"");
        await Task.Delay(TimeSpan.FromSeconds(10));
        await SafeStopAsync(true);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(settings.InstallDirectory); Process.Start(new ProcessStartInfo("explorer.exe", settings.InstallDirectory) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private void UpdateNextRestartText()
    {
        if (nextRestart is null)
            LocalizationService.SetText(NextRestartText, "下次重啟：未排程");
        else
            LocalizationService.SetFormattedText(NextRestartText,
                "下次重啟：{0}", nextRestart.Value.ToString("yyyy/MM/dd HH:mm:ss"));
    }

    private void SetStatus(string text, string color)
    {
        LocalizationService.SetText(StatusText, text);
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private void Log(string text)
    {
        pendingLogLines.Enqueue($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }

    private void FlushPendingLogs()
    {
        if (pendingLogLines.IsEmpty) return;
        var batch = new StringBuilder();
        for (var i = 0; i < 100 && pendingLogLines.TryDequeue(out var line); i++)
            batch.Append(line);
        ConsoleBox.AppendText(batch.ToString());
        const int maximumConsoleCharacters = 200_000;
        if (ConsoleBox.Text.Length > maximumConsoleCharacters)
        {
            var remove = ConsoleBox.Text.Length - 150_000;
            var newline = ConsoleBox.Text.IndexOf('\n', remove);
            ConsoleBox.Text = newline >= 0 ? ConsoleBox.Text[(newline + 1)..] : ConsoleBox.Text[^150_000..];
            ConsoleBox.CaretIndex = ConsoleBox.Text.Length;
        }
        ConsoleBox.ScrollToEnd();
    }

    private static string Bool(bool value) => value ? "true" : "false";
    private static string Clean(string value) => value.Replace("\r", " ").Replace("\n", " ");
    private static string NormalizeWorkshopList(string value) =>
        string.Join(';', value.Split(new[] { ';', ',', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries));
    private static string NormalizeSemicolonList(string value) =>
        string.Join(';', value.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim()).Where(item => item.Length > 0));
    private void ApplyStarterLoadout_Click(object sender, RoutedEventArgs e)
    {
        SpawnItemsBox.Text = "Base.BaseballBat,Base.WaterBottle,Base.Chocolate";
        SpawnItemsStatusText.Text = "已由你明確套用 Build 42 範例；尚未儲存。";
    }

    private bool TryValidateSpawnItems(string rawValue, out string error)
    {
        error = "";
        var value = rawValue.Trim();
        if (value.Length == 0)
        {
            SpawnItemsStatusText.Text = "未設定出生物品（Build 42 預設）。";
            return true;
        }
        if (value.Contains(';') || value.Contains('\r') || value.Contains('\n'))
        {
            error = "SpawnItems 必須使用半形逗號分隔；管理器不會替你改寫分隔符號。";
            SpawnItemsStatusText.Text = error;
            return false;
        }
        var ids = value.Split(',', StringSplitOptions.None).Select(id => id.Trim()).ToList();
        var malformed = ids.Where(id => !System.Text.RegularExpressions.Regex.IsMatch(id,
            @"^[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*$")).ToList();
        if (malformed.Count > 0)
        {
            error = "以下物品 ID 格式不正確（必須為 Module.Item）：\n" +
                string.Join("\n", malformed.Select(id => $"• {id}"));
            SpawnItemsStatusText.Text = "物品 ID 格式錯誤；未自動修改。";
            return false;
        }
        if (ids.Any(id => id.Equals("Base.WaterBottleFull", StringComparison.OrdinalIgnoreCase)))
        {
            error = "Base.WaterBottleFull 已於 Build 42 移除；目前 Build 42 水瓶 ID 是 Base.WaterBottle。\n\n" +
                "管理器沒有自動替換你的輸入，請確認後自行修改。";
            SpawnItemsStatusText.Text = "發現 Build 41 舊物品 ID；未自動修改。";
            return false;
        }

        var scriptsDirectory = Path.Combine(InstallPathBox.Text.Trim(), "media", "scripts");
        if (!Directory.Exists(scriptsDirectory))
        {
            SpawnItemsStatusText.Text = "格式已通過；尚未找到伺服器 scripts，無法核對 Base 物品是否存在。";
            return true;
        }
        try
        {
            var baseItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateFiles(scriptsDirectory, "*.txt", SearchOption.AllDirectories))
            {
                var text = ConfigFileEncoding.ReadText(path, "Auto");
                foreach (System.Text.RegularExpressions.Match match in
                    System.Text.RegularExpressions.Regex.Matches(text, @"(?im)^\s*item\s+([A-Za-z_][A-Za-z0-9_]*)\b"))
                    baseItems.Add(match.Groups[1].Value);
            }
            var missingBaseIds = ids.Where(id => id.StartsWith("Base.", StringComparison.OrdinalIgnoreCase))
                .Where(id => !baseItems.Contains(id[(id.IndexOf('.') + 1)..])).ToList();
            if (missingBaseIds.Count > 0)
            {
                error = "目前已安裝的 Build 42 伺服器 scripts 找不到以下 Base 物品；未寫入：\n" +
                    string.Join("\n", missingBaseIds.Select(id => $"• {id}")) +
                    "\n\n非 Base 模組物品不會由這項檢查阻擋。";
                SpawnItemsStatusText.Text = "有 Base 物品不存在；未自動修改。";
                return false;
            }
            SpawnItemsStatusText.Text = $"已核對目前伺服器 scripts：{ids.Count} 個 ID；內容保持原樣。";
            return true;
        }
        catch (Exception ex)
        {
            error = $"無法核對伺服器物品 scripts：{ex.Message}\n物品 ID 未被修改。";
            SpawnItemsStatusText.Text = "物品核對失敗；未自動修改。";
            return false;
        }
    }
    private static string LuaNumber(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    private static bool IsBuild42StableSandbox(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text, @"(?im)^\s*VERSION\s*=\s*6\s*,?");
    private static bool TryParseInvariant(string text, out double value) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    private string FormatRestartMessage(int minutes) =>
        settings.RestartWarningMessage.Replace("{minutes}", minutes.ToString())
            .Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");
    private string FormatWorkshopUpdateMessage() =>
        (string.IsNullOrWhiteSpace(settings.WorkshopUpdateWarningMessage)
            ? new ServerSettings().WorkshopUpdateWarningMessage
            : settings.WorkshopUpdateWarningMessage)
        .Replace("\r", " ").Replace("\n", " ").Replace("\"", "'");
    private static void SelectByTag(System.Windows.Controls.ComboBox combo, int value)
    {
        combo.SelectedItem = combo.Items.Cast<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(x => x.Tag?.ToString() == value.ToString());
        if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
    }
    private static void SelectByTag(System.Windows.Controls.ComboBox combo, string value)
    {
        combo.SelectedItem = combo.Items.Cast<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase));
        if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
    }
    private static int SelectedTag(System.Windows.Controls.ComboBox combo, int fallback) =>
        int.TryParse((combo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString(), out var value) ? value : fallback;
    private static string IniString(string text, string key, string fallback)
    {
        var match = System.Text.RegularExpressions.Regex.Match(text,
            $@"(?m)^{System.Text.RegularExpressions.Regex.Escape(key)}=(.*)$");
        return match.Success ? match.Groups[1].Value.TrimEnd('\r') : fallback;
    }
    private static int IniInt(string text, string key, int fallback) =>
        int.TryParse(IniString(text, key, ""), out var value) ? value : fallback;
    private static double IniDouble(string text, string key, double fallback) =>
        double.TryParse(IniString(text, key, ""), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static bool IniBool(string text, string key, bool fallback) =>
        bool.TryParse(IniString(text, key, ""), out var value) ? value : fallback;
    private static string LuaValue(string text, string key, string? section)
    {
        if (section != null)
        {
            var start = text.IndexOf(section + " = {", StringComparison.Ordinal);
            if (start < 0) return "";
            text = text[start..FindLuaSectionEnd(text, text.IndexOf('{', start))];
        }
        var match = System.Text.RegularExpressions.Regex.Match(text,
            $@"(?m)^\s*{System.Text.RegularExpressions.Regex.Escape(key)}\s*=\s*([^,\r\n]+)");
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }
    private static int LuaInt(string text, string key, int fallback, string? section = null) =>
        int.TryParse(LuaValue(text, key, section), out var value) ? value : fallback;
    private static double LuaDouble(string text, string key, double fallback, string? section = null) =>
        double.TryParse(LuaValue(text, key, section), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static bool LuaBool(string text, string key, bool fallback, string? section = null) =>
        bool.TryParse(LuaValue(text, key, section), out var value) ? value : fallback;
}
