using System.Numerics;
using OpenCvSharp;

namespace MLBB.Infrastructure.Vision;

/// <summary>
/// Pre-extracted ZNCC and 2D HSV color features for a hero template.
/// </summary>
internal sealed class HeroFeatureVector
{
    public string HeroName { get; init; } = string.Empty;
    public float[] Zncc { get; init; } = [];
    public float[] Hsv { get; init; } = [];
}

/// <summary>
/// High-performance SIMD/OpenCV feature extraction matching the MLBB Companion FastAPI algorithms.
/// Combines circular/windowed CLAHE ZNCC with L2-normalized 2D HSV color histograms.
/// </summary>
internal static class VisionFeatureExtractor
{
    public const int BanSize = 80;
    public const int BanRadius = 30;
    public const int BanCenter = 40;

    public const int PickCardWidth = 210;
    public const int PickCardHeight = 132;
    public const int PickWindowWidth = 139;
    public const int PickWindowHeight = 110;
    public const int PickWindowPixelCount = PickWindowWidth * PickWindowHeight; // 15290

    public static readonly int[] BanMaskIndices = CreateBanMaskIndices();
    public static readonly int BanMaskPixelCount = BanMaskIndices.Length; // 2821

    public static readonly (int dx, int dy)[] JitterOffsets =
    [
        (0, 0), (-3, 0), (3, 0), (0, -3), (0, 3), (-6, 0), (6, 0), (-3, -3), (3, -3)
    ];

    private static int[] CreateBanMaskIndices()
    {
        var list = new List<int>();
        for (int y = 0; y < BanSize; y++)
        {
            for (int x = 0; x < BanSize; x++)
            {
                int dx = x - BanCenter;
                int dy = y - BanCenter;
                if (dx * dx + dy * dy <= BanRadius * BanRadius)
                {
                    list.Add(y * BanSize + x);
                }
            }
        }
        return list.ToArray();
    }

    /// <summary>
    /// Extracts normalized zero-mean unit-variance ZNCC feature vector on masked pixels of 80x80 CLAHE grayscale image.
    /// </summary>
    public static float[] ExtractBanZnccVector(Mat grayClahe, int[] maskIndices)
    {
        float[] vec = new float[maskIndices.Length];
        int count = maskIndices.Length;
        int cols = grayClahe.Cols;

        double sum = 0.0;
        double sumSq = 0.0;

        for (int i = 0; i < count; i++)
        {
            int idx = maskIndices[i];
            byte val = grayClahe.At<byte>(idx / cols, idx % cols);
            sum += val;
            sumSq += (double)val * val;
        }

        double mean = sum / count;
        double variance = (sumSq / count) - (mean * mean);
        double std = variance > 0.0 ? Math.Sqrt(variance) : 0.0;

        if (std < 1e-5)
        {
            return vec;
        }

        float invStd = (float)(1.0 / std);
        float meanF = (float)mean;
        for (int i = 0; i < count; i++)
        {
            int idx = maskIndices[i];
            byte val = grayClahe.At<byte>(idx / cols, idx % cols);
            vec[i] = (val - meanF) * invStd;
        }
        return vec;
    }

    /// <summary>
    /// Extracts 128-bin (16 Hue x 8 Saturation) L2-normalized 2D HSV color histogram on circular masked pixels.
    /// </summary>
    public static float[] ExtractBanHsvHistogram(Mat bgr, int[] maskIndices)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        float[] hist = new float[128];
        int cols = hsv.Cols;

        for (int i = 0; i < maskIndices.Length; i++)
        {
            int idx = maskIndices[i];
            var vec3 = hsv.At<Vec3b>(idx / cols, idx % cols);
            byte h = vec3.Item0; // 0..180
            byte s = vec3.Item1; // 0..255

            int hBin = Math.Clamp((h * 16) / 180, 0, 15);
            int sBin = Math.Clamp((s * 8) / 256, 0, 7);
            hist[hBin * 8 + sBin]++;
        }

        double sumSq = 0.0;
        for (int i = 0; i < 128; i++) sumSq += hist[i] * hist[i];
        float norm = (float)Math.Sqrt(sumSq) + 1e-6f;
        for (int i = 0; i < 128; i++) hist[i] /= norm;
        return hist;
    }

    /// <summary>
    /// Extracts normalized zero-mean unit-variance ZNCC feature vector on 139x110 focal window CLAHE grayscale image.
    /// </summary>
    public static float[] ExtractPickZnccVector(Mat grayClahe)
    {
        int rows = grayClahe.Rows;
        int cols = grayClahe.Cols;
        int total = rows * cols;
        float[] vec = new float[total];

        Cv2.MeanStdDev(grayClahe, out var meanScalar, out var stdScalar);
        double mean = meanScalar.Val0;
        double std = stdScalar.Val0;

        if (std < 1e-5)
        {
            return vec;
        }

        float invStd = (float)(1.0 / (std + 1e-6));
        float meanF = (float)mean;

        byte[] rawBytes = new byte[total];
        System.Runtime.InteropServices.Marshal.Copy(grayClahe.Data, rawBytes, 0, total);
        for (int i = 0; i < total; i++)
        {
            vec[i] = (rawBytes[i] - meanF) * invStd;
        }
        return vec;
    }

    /// <summary>
    /// Extracts 128-bin (16 Hue x 8 Saturation) L2-normalized 2D HSV color histogram on 139x110 focal window.
    /// </summary>
    public static float[] ExtractPickHsvHistogram(Mat bgr)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        int rows = hsv.Rows;
        int cols = hsv.Cols;
        var indexer = hsv.GetUnsafeGenericIndexer<Vec3b>();

        float[] hist = new float[128];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var vec3 = indexer[r, c];
                byte h = vec3.Item0;
                byte s = vec3.Item1;

                int hBin = Math.Clamp((h * 16) / 180, 0, 15);
                int sBin = Math.Clamp((s * 8) / 256, 0, 7);
                hist[hBin * 8 + sBin]++;
            }
        }

        double sumSq = 0.0;
        for (int i = 0; i < 128; i++) sumSq += hist[i] * hist[i];
        float norm = (float)Math.Sqrt(sumSq) + 1e-6f;
        for (int i = 0; i < 128; i++) hist[i] /= norm;
        return hist;
    }

    /// <summary>
    /// Hardware-accelerated SIMD dot product using System.Numerics.Vector.
    /// </summary>
    public static float Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        int i = 0;
        int vLen = Vector<float>.Count;
        var acc = Vector<float>.Zero;

        while (i <= a.Length - vLen)
        {
            var va = new Vector<float>(a.Slice(i, vLen));
            var vb = new Vector<float>(b.Slice(i, vLen));
            acc += va * vb;
            i += vLen;
        }

        float sum = Vector.Dot(acc, Vector<float>.One);
        while (i < a.Length)
        {
            sum += a[i] * b[i];
            i++;
        }
        return sum;
    }

    /// <summary>
    /// Fuses structural ZNCC (weight 0.85) with HSV color histogram verification (weight 0.15).
    /// Implements structural gating and color contradiction penalty matching the FastAPI reference.
    /// </summary>
    public static (float fused, float zCons, float hCons) ConsolidateHeroScore(
        ReadOnlySpan<float> zScores,
        ReadOnlySpan<float> hScores,
        float activeThreshold)
    {
        int bestIdx = 0;
        float bestZ = zScores[0];
        for (int i = 1; i < zScores.Length; i++)
        {
            if (zScores[i] > bestZ)
            {
                bestZ = zScores[i];
                bestIdx = i;
            }
        }
        float consZ = bestZ;
        float consH = hScores[bestIdx];

        // Primary signal: structural ZNCC (0.85). Secondary verification: HSV (0.15)
        float fused = 0.85f * consZ + 0.15f * Math.Max(0f, consH);

        // Structural gating: low structural match cannot be artificially inflated by HSV
        if (consZ < activeThreshold * 0.75f)
        {
            fused = Math.Min(fused, consZ);
        }
        // Color contradiction penalty: good structural match but contradictory colors
        else if (consZ >= 0.50f && consH < 0.10f)
        {
            fused = Math.Max(0f, fused - 0.10f);
        }

        return (fused, consZ, consH);
    }
}
