using Microsoft.Win32;
using System.Windows;

namespace PZServerManager;

public partial class ProfileDialog : Window
{
    public string DataDirectory => DataDirectoryBox.Text.Trim();
    public string ProfileName => ProfileNameBox.Text.Trim();
    public bool ClearSecrets => ClearSecretsCheck.IsChecked == true;
    public bool AdjustPorts => AdjustPortsCheck.IsChecked == true;

    public ProfileDialog(string dataDirectory, bool copyMode)
    {
        InitializeComponent();
        DataDirectoryBox.Text = dataDirectory;
        HeadingText.Text = copyMode ? "複製並重新命名" : "建立 Build 42 預設設定檔";
        HelpText.Text = copyMode
            ? "只複製伺服器設定；不複製世界、地圖區塊、角色或玩家資料庫。ResetID 會自動更新。"
            : "建立乾淨的 Build 42 Stable VERSION=6 預設；不帶入目前 GUI 值、名稱、密碼或 MOD。";
        ClearSecretsCheck.Visibility = copyMode ? Visibility.Visible : Visibility.Collapsed;
        AdjustPortsCheck.Visibility = copyMode ? Visibility.Visible : Visibility.Collapsed;
        LocalizationService.Apply(this);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇 Project Zomboid 資料目錄",
            InitialDirectory = DataDirectoryBox.Text
        };
        if (dialog.ShowDialog(this) == true) DataDirectoryBox.Text = dialog.FolderName;
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ServerProfileService.ValidateProfileName(ProfileName);
            if (string.IsNullOrWhiteSpace(DataDirectory))
                throw new ArgumentException("請指定資料目錄。");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "設定檔名稱無效",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
