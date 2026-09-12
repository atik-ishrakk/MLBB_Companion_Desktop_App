using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Xunit.Abstractions;

namespace MLBB.Tests;

public class AssetDimensionTests
{
    private readonly ITestOutputHelper _output;

    public AssetDimensionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ResizeSpellsTo50x50()
    {
        string solutionRoot = FindSolutionRoot();
        string spellsDir = Path.Combine(solutionRoot, "Assets", "spells");
        Assert.True(Directory.Exists(spellsDir), $"Spells directory not found: {spellsDir}");

        var spellFiles = Directory.GetFiles(spellsDir, "*.png");
        _output.WriteLine($"Found {spellFiles.Length} spell files.");

        foreach (var file in spellFiles)
        {
            byte[] rawBytes = File.ReadAllBytes(file);
            using var ms = new MemoryStream(rawBytes);
            using var original = Image.FromStream(ms);

            _output.WriteLine($"Original {Path.GetFileName(file)}: {original.Width}x{original.Height} ({rawBytes.Length / 1024} KB)");

            // Resize to 50x50 with high-quality bicubic interpolation
            using var dest = new Bitmap(50, 50, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dest))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(original, new Rectangle(0, 0, 50, 50));
            }

            dest.Save(file, ImageFormat.Png);
            _output.WriteLine($"Resized {Path.GetFileName(file)} -> 50x50 PNG ({new FileInfo(file).Length} bytes)");
        }

        string lanesDir = Path.Combine(solutionRoot, "Assets", "lanes");
        if (Directory.Exists(lanesDir))
        {
            var laneFiles = Directory.GetFiles(lanesDir, "*.png");
            foreach (var file in laneFiles)
            {
                byte[] rawBytes = File.ReadAllBytes(file);
                using var ms = new MemoryStream(rawBytes);
                using var original = Image.FromStream(ms);
                _output.WriteLine($"Lane {Path.GetFileName(file)}: {original.Width}x{original.Height} ({rawBytes.Length} bytes)");

                // If not 50x50, let's normalize lane icons to 50x50 as well!
                if (original.Width != 50 || original.Height != 50)
                {
                    using var dest = new Bitmap(50, 50, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(dest))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(original, new Rectangle(0, 0, 50, 50));
                    }
                    dest.Save(file, ImageFormat.Png);
                    _output.WriteLine($"Normalized Lane {Path.GetFileName(file)} -> 50x50 PNG");
                }
            }
        }
    }

    private static string FindSolutionRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "MLBBCompanion.slnx")) || Directory.Exists(Path.Combine(dir, "Assets")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return @"c:\Users\mdati\Desktop\Try";
    }
}
