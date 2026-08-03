using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace PZServerManager;

public partial class App : Application
{
    static App() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += HandleDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrashLog(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteCrashLog(args.Exception);
            args.SetObserved();
        };
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { ManagerLogService.ShutdownAsync().GetAwaiter().GetResult(); }
        catch { }
        base.OnExit(e);
    }

    private static void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        MessageBox.Show(
            $"管理器發生未預期錯誤，但已攔截以避免直接退出。\n{e.Exception.Message}\n\n" +
            "詳細紀錄位於 %LOCALAPPDATA%\\PZServerManager\\crash.log。",
            "PZ 伺服器管理器錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void WriteCrashLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PZServerManager");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{exception}\n\n", new UTF8Encoding(false));
        }
        catch
        {
            // Crash logging must never cause a second crash.
        }
    }
}
