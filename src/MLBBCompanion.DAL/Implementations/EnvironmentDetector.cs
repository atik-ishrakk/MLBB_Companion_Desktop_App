using MLBBCompanion.BLL.Interfaces;
using MLBBCompanion.BLL.Models;

namespace MLBBCompanion.DAL.Implementations;

public class EnvironmentDetector : IEnvironmentDetector
{
    private readonly string?[] _searchRoots =
    [
        Environment.GetEnvironmentVariable("ProgramFiles"),
        Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
        Environment.GetEnvironmentVariable("LocalAppData")
    ];

    public string? DetectBrowser()
    {
        var candidates = new List<string>();

        foreach (var root in _searchRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            candidates.Add(Path.Combine(root, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"));
            candidates.Add(Path.Combine(root, "Google", "Chrome", "Application", "chrome.exe"));
            candidates.Add(Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe"));
        }

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public string? DetectBlueStacksHdPlayer()
    {
        foreach (var root in _searchRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var path = Path.Combine(root, "BlueStacks_nxt", "HD-Player.exe");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public string? DetectBlueStacksLauncher()
    {
        foreach (var root in _searchRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var path = Path.Combine(root, "BlueStacks_nxt", "Bluestacks.exe");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public string? DetectAdbPath()
    {
        foreach (var root in _searchRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            var path = Path.Combine(root, "BlueStacks_nxt", "HD-Adb.exe");
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Check system PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "adb.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public EnvironmentSummary GetSummary()
    {
        return new EnvironmentSummary
        {
            BrowserPath = DetectBrowser(),
            BlueStacksHdPlayer = DetectBlueStacksHdPlayer(),
            BlueStacksLauncher = DetectBlueStacksLauncher(),
            AdbPath = DetectAdbPath()
        };
    }
}
