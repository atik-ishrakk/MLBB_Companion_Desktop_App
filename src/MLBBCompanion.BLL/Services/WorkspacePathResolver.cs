namespace MLBB.Core.Services;

/// <summary>
/// Discovers and caches the workspace root directory dynamically across runtime environments.
/// Eliminates hardcoded machine-specific absolute file paths so the application is portable.
/// </summary>
public static class WorkspacePathResolver
{
    private static string? _cachedRoot;

    public static string GetWorkspaceRoot()
    {
        if (_cachedRoot != null) return _cachedRoot;

        // 1. Traverse upward from AppContext.BaseDirectory
        string? current = AppContext.BaseDirectory;
        for (int i = 0; i < 7 && !string.IsNullOrEmpty(current); i++)
        {
            if (Directory.Exists(Path.Combine(current, "Assets", "heroes")) ||
                (Directory.Exists(Path.Combine(current, "Assets")) &&
                 (Directory.Exists(Path.Combine(current, "src")) ||
                  File.Exists(Path.Combine(current, "MLBBCompanion.slnx")) ||
                  File.Exists(Path.Combine(current, "agent.md")))))
            {
                _cachedRoot = Path.GetFullPath(current);
                return _cachedRoot;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // 2. Traverse upward from Directory.GetCurrentDirectory()
        current = Directory.GetCurrentDirectory();
        for (int i = 0; i < 7 && !string.IsNullOrEmpty(current); i++)
        {
            if (Directory.Exists(Path.Combine(current, "Assets", "heroes")) ||
                (Directory.Exists(Path.Combine(current, "Assets")) &&
                 (Directory.Exists(Path.Combine(current, "src")) ||
                  File.Exists(Path.Combine(current, "MLBBCompanion.slnx")) ||
                  File.Exists(Path.Combine(current, "agent.md")))))
            {
                _cachedRoot = Path.GetFullPath(current);
                return _cachedRoot;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        _cachedRoot = Directory.GetCurrentDirectory();
        return _cachedRoot;
    }

    public static string ResolvePath(params string[] subPaths)
    {
        return Path.Combine(GetWorkspaceRoot(), Path.Combine(subPaths));
    }
}
