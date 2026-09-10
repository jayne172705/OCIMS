using System;
using System.Diagnostics;
using System.IO;

internal static class Launcher
{
    private static void Main(string[] args)
    {
        var rootDir = AppDomain.CurrentDomain.BaseDirectory;
        var currentDir = new DirectoryInfo(rootDir);
        while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "eSureHi.sln")))
        {
            currentDir = currentDir.Parent;
        }

        var baseDir = currentDir?.FullName ?? Directory.GetCurrentDirectory();
        var targetProject = Path.Combine(baseDir, "OCIMS", "eSureHi.csproj");

        var extraArgs = args != null && args.Length > 0 ? " " + string.Join(" ", args) : "";
        var psi = new ProcessStartInfo("dotnet", $"run --project \"{targetProject}\"{extraArgs}")
        {
            UseShellExecute = false,
            WorkingDirectory = baseDir
        };

        using var proc = Process.Start(psi);
        proc?.WaitForExit();
    }
}
