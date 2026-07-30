using System.Reflection;
using System.Windows;

namespace PZServerManager;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version =
            typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)
            ?? "未知";
        LocalizationService.SetFormattedText(AboutVersionText, "版本 v{0}", version);
        LocalizationService.Apply(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
