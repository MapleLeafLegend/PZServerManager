using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace PZServerManager;

public partial class AboutWindow : Window
{
    private readonly Task<GitHubReleaseInfo?> updateCheckTask;
    private GitHubReleaseInfo? availableRelease;
    private CancellationTokenSource? downloadCancellation;

    public AboutWindow(Task<GitHubReleaseInfo?>? checkTask = null)
    {
        InitializeComponent();
        var version =
            typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)
            ?? "未知";
        LocalizationService.SetFormattedText(AboutVersionText, "版本 v{0}", version);
        LocalizationService.SetFormattedText(UpdateStatusText, "目前版本 v{0}", version);
        LocalizationService.Apply(this);
        updateCheckTask = checkTask ?? SafeCheckAsync();
        Loaded += AboutWindow_Loaded;
        Closed += (_, _) => downloadCancellation?.Cancel();
    }

    private static async Task<GitHubReleaseInfo?> SafeCheckAsync()
    {
        try { return await GitHubUpdateService.CheckForUpdateAsync(); }
        catch { return null; }
    }

    private async void AboutWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try { availableRelease = await updateCheckTask; }
        catch { availableRelease = null; }
        if (!IsLoaded || availableRelease == null) return;
        UpdateStatusDot.Fill = new SolidColorBrush(Color.FromRgb(217, 105, 95));
        LocalizationService.SetFormattedText(UpdateStatusText,
            "已偵測到新版本 v{0}，可前往下載。", availableRelease.Version.ToString(3));
        DownloadUpdateButton.Visibility = Visibility.Visible;
    }

    private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (availableRelease == null || downloadCancellation != null) return;
        downloadCancellation = new CancellationTokenSource();
        DownloadUpdateButton.IsEnabled = false;
        var progress = new Progress<int>(percent =>
            LocalizationService.SetFormattedText(UpdateStatusText,
                "正在下載 v{0}：{1}%", availableRelease.Version.ToString(3), percent));
        try
        {
            var result = await GitHubUpdateService.DownloadAsync(availableRelease, progress,
                downloadCancellation.Token);
            LocalizationService.SetFormattedText(UpdateStatusText,
                result.DigestVerified
                    ? "下載完成且 SHA-256 驗證成功：{0}"
                    : "下載完成；GitHub 未提供可核對的 SHA-256：{0}",
                result.FilePath);
            DownloadUpdateButton.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LocalizationService.SetFormattedText(UpdateStatusText,
                "下載失敗，現有版本不受影響：{0}", ex.Message);
            DownloadUpdateButton.IsEnabled = true;
        }
        finally { downloadCancellation?.Dispose(); downloadCancellation = null; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
