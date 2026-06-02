using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowSillAiLimits.Services;

public static class CommandStartInfoFactory
{
    public static ProcessStartInfo Create(string commandPath, IEnumerable<string> arguments)
    {
        var resolved = ResolveCommand(commandPath);
        var extension = System.IO.Path.GetExtension(resolved).ToLowerInvariant();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && extension is ".ps1")
        {
            var startInfo = BaseStartInfo(ResolveCommand("pwsh.exe"));
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(resolved);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }

        var direct = BaseStartInfo(resolved);
        foreach (var argument in arguments)
        {
            direct.ArgumentList.Add(argument);
        }

        return direct;
    }

    private static ProcessStartInfo BaseStartInfo(string fileName)
        => new()
        {
            FileName = fileName,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

    private static string ResolveCommand(string commandPath)
    {
        if (System.IO.Path.IsPathRooted(commandPath) ||
            commandPath.Contains(System.IO.Path.DirectorySeparatorChar) ||
            commandPath.Contains(System.IO.Path.AltDirectorySeparatorChar))
        {
            return commandPath;
        }

        var pathExts = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat", ".ps1", string.Empty }
            : [string.Empty];
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var directory in path.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in pathExts)
            {
                var candidate = System.IO.Path.Combine(directory, commandPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? commandPath : commandPath + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return commandPath;
    }

}
