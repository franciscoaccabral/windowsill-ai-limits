using System.Diagnostics;
using System.Text;

namespace WindowSillAiLimits.Services;

internal static class AiLimitsDiagnostics
{
    private const int MaxLogBytes = 64 * 1024;

    public static string LogPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(localAppData, "WindowSillAiLimits", "diagnostics.log");
        }
    }

    public static void Info(string message)
        => Write("INFO", message);

    public static void Error(string message, Exception exception)
        => Write("ERROR", $"{message}: {exception.GetType().Name}: {UsageMessageSanitizer.Sanitize(exception.ToString())}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {UsageMessageSanitizer.Sanitize(message)}{Environment.NewLine}";
        Debug.Write(line);

        try
        {
            var path = LogPath;
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            RotateIfNeeded(path);
            File.AppendAllText(path, line, Encoding.UTF8);
        }
        catch
        {
            // Diagnostics must never break the sill host.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length <= MaxLogBytes)
        {
            return;
        }

        File.Move(path, path + ".old", overwrite: true);
    }
}
