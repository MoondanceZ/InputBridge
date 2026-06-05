using System.Text;
using Avalonia.Threading;

namespace InputBridge;

internal static class AppExceptionHandler
{
    private static readonly object LogLock = new();

    public static void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log("AppDomain.UnhandledException", e.ExceptionObject as Exception, e.ExceptionObject?.ToString());
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log("Dispatcher.UIThread.UnhandledException", e.Exception);
            e.Handled = true;
        };
    }

    public static void Log(string source, Exception? exception, string? fallback = null)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InputBridge",
                "logs");
            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, $"InputBridge-{DateTime.Now:yyyyMMdd}.log");
            var builder = new StringBuilder()
                .AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source}");

            if (exception != null)
            {
                builder.AppendLine(exception.ToString());
            }
            else if (!string.IsNullOrWhiteSpace(fallback))
            {
                builder.AppendLine(fallback);
            }

            builder.AppendLine();
            lock (LogLock)
            {
                File.AppendAllText(path, builder.ToString());
            }
        }
        catch
        {
        }
    }
}
