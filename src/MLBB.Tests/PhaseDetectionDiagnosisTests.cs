using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using MLBB.Infrastructure.Vision;
using Xunit;
using Xunit.Abstractions;

namespace MLBB.Tests;

public class PhaseDetectionDiagnosisTests
{
    private readonly ITestOutputHelper _output;
    private readonly OpenCvVisionEngine _visionEngine;

    public PhaseDetectionDiagnosisTests(ITestOutputHelper output)
    {
        _output = output;
        _visionEngine = new OpenCvVisionEngine(NullLogger<OpenCvVisionEngine>.Instance);
        _visionEngine.InitializeTensors();
    }

    [Fact]
    public void DiagnoseAllBluestacksScreenshots()
    {
        string baseDir = @"C:\Users\mdati\Desktop\Bluestacks";
        if (!Directory.Exists(baseDir))
        {
            _output.WriteLine($"Directory does not exist: {baseDir}");
            return;
        }

        var files = Directory.GetFiles(baseDir, "*.png", SearchOption.AllDirectories);
        _output.WriteLine($"Found {files.Length} screenshots to test.\n");

        foreach (var file in files)
        {
            string relName = Path.GetRelativePath(baseDir, file);
            byte[] bytes = File.ReadAllBytes(file);
            var frame = new CapturedFrame
            {
                Buffer = bytes,
                Width = 1920,
                Height = 1080,
                Source = relName
            };

            var (phase, subPhase, conf, details) = _visionEngine.DetectPhase(frame);
            _output.WriteLine($"=== [{relName}] -> Detected: {phase} (sub: {subPhase ?? "none"}, conf: {conf:F2}) ===");
            _output.WriteLine($"    Details: {details}");

            // Let's inspect raw anchor scores for this frame
            DiagnoseRawAnchorScores(frame);
            _output.WriteLine("");
        }
    }

    private void DiagnoseRawAnchorScores(CapturedFrame frame)
    {
        // Load phase_anchors.json
        string root = WorkspacePathResolver.GetWorkspaceRoot();
        string jsonPath = Path.Combine(root, "Assets", "templates", "Phase Template", "phase_anchors.json");
        string anchorDir = Path.Combine(root, "Assets", "templates", "Phase Template", "anchors");
        if (!File.Exists(jsonPath) || !Directory.Exists(anchorDir)) return;

        var anchors = System.Text.Json.JsonSerializer.Deserialize<List<PhaseAnchorEntity>>(File.ReadAllText(jsonPath)) ?? [];
        using var mat = OpenCvSharp.Mat.FromImageData(frame.Buffer!, OpenCvSharp.ImreadModes.Color);
        using var fullGray = new OpenCvSharp.Mat();
        OpenCvSharp.Cv2.CvtColor(mat, fullGray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
        using var clahe = OpenCvSharp.Cv2.CreateCLAHE(2.0, new OpenCvSharp.Size(8, 8));

        foreach (var a in anchors)
        {
            string tplPath = Path.Combine(anchorDir, a.Filename);
            if (!File.Exists(tplPath)) continue;

            using var rawTpl = new OpenCvSharp.Mat(tplPath, OpenCvSharp.ImreadModes.Grayscale);
            if (rawTpl.Empty()) continue;
            using var tplClahe = new OpenCvSharp.Mat();
            clahe.Apply(rawTpl, tplClahe);

            // Bounding box
            int rx1 = Math.Clamp(a.X1, 0, mat.Width - 1);
            int ry1 = Math.Clamp(a.Y1, 0, mat.Height - 1);
            int rx2 = Math.Clamp(a.X2, rx1 + 1, mat.Width);
            int ry2 = Math.Clamp(a.Y2, ry1 + 1, mat.Height);

            using var roiGray = new OpenCvSharp.Mat(fullGray, new OpenCvSharp.Rect(rx1, ry1, rx2 - rx1, ry2 - ry1));
            using var roiClahe = new OpenCvSharp.Mat();
            clahe.Apply(roiGray, roiClahe);

            using var evalCrop = (roiClahe.Width < tplClahe.Width || roiClahe.Height < tplClahe.Height)
                ? roiClahe.Resize(new OpenCvSharp.Size(Math.Max(roiClahe.Width, tplClahe.Width), Math.Max(roiClahe.Height, tplClahe.Height)))
                : roiClahe.Clone();

            using var res = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.MatchTemplate(evalCrop, tplClahe, res, OpenCvSharp.TemplateMatchModes.CCoeffNormed);
            OpenCvSharp.Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);

            bool pass = maxVal >= a.Threshold;
            if (maxVal >= 0.35)
            {
                _output.WriteLine($"    Anchor [{a.Id}] ({a.Phase}) -> NCC: {maxVal:F3} (Req: {a.Threshold:F2}) -> {(pass ? "PASS" : "FAIL")}");
            }
        }
    }

    [Fact]
    public void FullFrameSearchToDiagnose()
    {
        string baseDir = @"C:\Users\mdati\Desktop\Bluestacks";
        string root = WorkspacePathResolver.GetWorkspaceRoot();
        string anchorDir = Path.Combine(root, "Assets", "templates", "Phase Template", "anchors");

        var problemFiles = new[]
        {
            "Homepage.png",
            "Match Countdown.png",
            "Matchmaking in homepage.png",
            "Matchmaking in lobby.png",
            "Draft Pick 2.png",
            "Lobby.png"
        };

        var allAnchorFiles = Directory.GetFiles(anchorDir, "*.png");

        foreach (var pFile in problemFiles)
        {
            string fullPath = Path.Combine(baseDir, pFile);
            if (!File.Exists(fullPath)) continue;

            _output.WriteLine($"==================================================");
            _output.WriteLine($"FILE: {pFile}");
            _output.WriteLine($"==================================================");

            using var frameMat = OpenCvSharp.Mat.FromImageData(File.ReadAllBytes(fullPath), OpenCvSharp.ImreadModes.Color);
            using var frameGray = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.CvtColor(frameMat, frameGray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
            using var clahe = OpenCvSharp.Cv2.CreateCLAHE(2.0, new OpenCvSharp.Size(8, 8));
            using var frameClahe = new OpenCvSharp.Mat();
            clahe.Apply(frameGray, frameClahe);

            var matches = new List<(string Name, double Score, OpenCvSharp.Point Loc, int W, int H)>();

            foreach (var aPath in allAnchorFiles)
            {
                string aName = Path.GetFileName(aPath);
                using var tplRaw = new OpenCvSharp.Mat(aPath, OpenCvSharp.ImreadModes.Grayscale);
                if (tplRaw.Empty() || tplRaw.Width > frameClahe.Width || tplRaw.Height > frameClahe.Height) continue;

                using var tplClahe = new OpenCvSharp.Mat();
                clahe.Apply(tplRaw, tplClahe);

                using var res = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.MatchTemplate(frameClahe, tplClahe, res, OpenCvSharp.TemplateMatchModes.CCoeffNormed);
                OpenCvSharp.Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

                if (maxVal >= 0.40)
                {
                    matches.Add((aName, maxVal, maxLoc, tplRaw.Width, tplRaw.Height));
                }
            }

            foreach (var m in matches.OrderByDescending(x => x.Score))
            {
                _output.WriteLine(string.Format("  Anchor: {0,-30} | Score: {1:F3} | Loc: X={2,4}, Y={3,4} (Box: [{2}, {3}, {4}, {5}])",
                    m.Name, m.Score, m.Loc.X, m.Loc.Y, m.Loc.X + m.W, m.Loc.Y + m.H));
            }
        }
    }
}
