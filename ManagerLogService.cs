using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PZServerManager;

public static class ManagerLogService
{
    private static readonly BlockingCollection<string> Queue = new();
    private static readonly object Gate = new();
    private static Task? writerTask;
    private static string logDirectory = "";
    private static int retentionDays = 14;

    public static string LogDirectory => logDirectory;

    public static void Initialize(int days)
    {
        lock (Gate)
        {
            retentionDays = Math.Clamp(days, 1, 365);
            if (writerTask != null)
            {
                CleanupOldFiles();
                return;
            }
            logDirectory = SelectWritableDirectory();
            Directory.CreateDirectory(logDirectory);
            CleanupOldFiles();
            writerTask = Task.Run(WriterLoop);
        }
    }

    public static void Write(string line)
    {
        if (writerTask == null) Initialize(retentionDays);
        Queue.TryAdd(Sanitize(line));
    }

    public static IReadOnlyList<string> RecentFiles(int count = 10)
    {
        try
        {
            return Directory.EnumerateFiles(logDirectory, "Manager-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc).Take(count).ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    public static async Task ShutdownAsync()
    {
        lock (Gate)
        {
            if (!Queue.IsAddingCompleted) Queue.CompleteAdding();
        }
        if (writerTask != null) await writerTask.ConfigureAwait(false);
    }

    public static string Sanitize(string value)
    {
        var text = value ?? "";
        text = Regex.Replace(text,
            @"(?i)(password|adminpassword|rconpassword|token|secret)\s*[:=]\s*[^\s;,]+",
            "$1=<redacted>");
        text = Regex.Replace(text, @"(?i)(\+login\s+\S+\s+)(\S+)", "$1<redacted>");
        return text;
    }

    private static void WriterLoop()
    {
        foreach (var line in Queue.GetConsumingEnumerable())
        {
            try
            {
                var path = Path.Combine(logDirectory, $"Manager-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line.TrimEnd()}\r\n",
                    new UTF8Encoding(false));
            }
            catch
            {
                // Logging must never affect the manager or server process.
            }
        }
    }

    private static string SelectWritableDirectory()
    {
        var preferred = Path.Combine(AppContext.BaseDirectory, "Logs");
        try
        {
            Directory.CreateDirectory(preferred);
            var probe = Path.Combine(preferred, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return preferred;
        }
        catch
        {
            return Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData), "PZServerManager", "Logs");
        }
    }

    private static void CleanupOldFiles()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            foreach (var path in Directory.EnumerateFiles(logDirectory, "Manager-*.log"))
                if (File.GetLastWriteTimeUtc(path) < cutoff) File.Delete(path);
        }
        catch
        {
            // Cleanup failures are non-fatal.
        }
    }
}
