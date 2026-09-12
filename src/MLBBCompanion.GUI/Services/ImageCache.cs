using System.Drawing.Drawing2D;
using MLBB.Core.Services;

namespace MLBBCompanion.GUI.Services;

/// <summary>
/// High-performance memory-cached image provider with dynamic asset resolution,
/// high-DPI scaling, and antialiased circular/rounded cropping.
/// </summary>
public static class ImageCache
{
    private static readonly Dictionary<string, Image> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    public static Image? GetHeroImage(string? heroId, int width = 64, int height = 64, bool circular = false)
    {
        if (string.IsNullOrWhiteSpace(heroId)) return null;

        string key = $"hero_{heroId}_{width}x{height}_{(circular ? "circle" : "rect")}";
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var normalized = heroId.ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace("'", "");
            var path = WorkspacePathResolver.ResolvePath("Assets", "heroes", $"{normalized}.png");
            if (!File.Exists(path))
            {
                path = WorkspacePathResolver.ResolvePath("Assets", "heroes", $"{heroId}.png");
            }

            if (File.Exists(path))
            {
                try
                {
                    using var original = Image.FromFile(path);
                    var processed = ProcessImage(original, width, height, circular);
                    _cache[key] = processed;
                    return processed;
                }
                catch
                {
                    // Fall through to placeholder
                }
            }

            return null;
        }
    }

    public static Image? GetItemImage(string? itemId, int width = 36, int height = 36, bool circular = true)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;

        string key = $"item_{itemId}_{width}x{height}_{(circular ? "circle" : "rect")}";
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var normalized = itemId.ToLowerInvariant().Replace(" ", "_").Replace("-", "_").Replace("'", "");
            var path = WorkspacePathResolver.ResolvePath("Assets", "items", $"{normalized}.png");
            if (!File.Exists(path))
            {
                path = WorkspacePathResolver.ResolvePath("Assets", "items", $"{itemId}.png");
            }

            if (File.Exists(path))
            {
                try
                {
                    using var original = Image.FromFile(path);
                    var processed = ProcessImage(original, width, height, circular, roundedCornerRadius: circular ? 0 : 4);
                    _cache[key] = processed;
                    return processed;
                }
                catch
                {
                    // Fall through
                }
            }

            return null;
        }
    }

    public static Image? GetLaneImage(string? lane, int width = 24, int height = 24, bool circular = true)
    {
        if (string.IsNullOrWhiteSpace(lane)) return null;

        string key = $"lane_{lane}_{width}x{height}_{(circular ? "circle" : "rect")}";
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var normalized = lane.ToLowerInvariant();
            if (normalized.Contains("exp")) normalized = "exp";
            else if (normalized.Contains("jug") || normalized.Contains("jungle")) normalized = "jungle";
            else if (normalized.Contains("mid")) normalized = "mid";
            else if (normalized.Contains("gold")) normalized = "gold";
            else if (normalized.Contains("roam")) normalized = "roam";

            var path = WorkspacePathResolver.ResolvePath("Assets", "lanes", $"{normalized}.png");
            if (File.Exists(path))
            {
                try
                {
                    using var original = Image.FromFile(path);
                    var processed = ProcessImage(original, width, height, circular);
                    _cache[key] = processed;
                    return processed;
                }
                catch { }
            }

            return null;
        }
    }

    public static Image? GetSpellImage(string? spell, int width = 50, int height = 50, bool circular = true)
    {
        if (string.IsNullOrWhiteSpace(spell)) return null;

        string key = $"spell_{spell}_{width}x{height}_{(circular ? "circle" : "rect")}";
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var normalized = spell.ToLowerInvariant().Replace(" ", "_").Replace("-", "");
            var path = WorkspacePathResolver.ResolvePath("Assets", "spells", $"{normalized}.png");
            if (!File.Exists(path))
            {
                path = WorkspacePathResolver.ResolvePath("Assets", "spells", $"{spell.ToLowerInvariant()}.png");
            }

            if (File.Exists(path))
            {
                try
                {
                    using var original = Image.FromFile(path);
                    var processed = ProcessImage(original, width, height, circular);
                    _cache[key] = processed;
                    return processed;
                }
                catch { }
            }

            return null;
        }
    }

    private static Bitmap ProcessImage(Image original, int width, int height, bool circular, int roundedCornerRadius = 0)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        if (circular)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, width, height);
            g.SetClip(path);
            g.DrawImage(original, 0, 0, width, height);
        }
        else if (roundedCornerRadius > 0)
        {
            using var path = GetRoundedRectPath(new Rectangle(0, 0, width, height), roundedCornerRadius);
            g.SetClip(path);
            g.DrawImage(original, 0, 0, width, height);
        }
        else
        {
            g.DrawImage(original, 0, 0, width, height);
        }

        return bmp;
    }

    public static GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void Clear()
    {
        lock (_lock)
        {
            foreach (var img in _cache.Values)
            {
                img.Dispose();
            }
            _cache.Clear();
        }
    }
}
