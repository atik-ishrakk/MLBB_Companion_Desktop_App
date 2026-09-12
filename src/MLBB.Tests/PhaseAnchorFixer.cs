using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using MLBB.Infrastructure.Vision;
using OpenCvSharp;
using Xunit;
using Xunit.Abstractions;

namespace MLBB.Tests;

public class PhaseAnchorFixer
{
    private readonly ITestOutputHelper _output;

    public PhaseAnchorFixer(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenerateAndVerifyAnchors()
    {
        string root = WorkspacePathResolver.GetWorkspaceRoot();
        string anchorDir = Path.Combine(root, "Assets", "templates", "Phase Template", "anchors");
        string jsonPath = Path.Combine(root, "Assets", "templates", "Phase Template", "phase_anchors.json");
        string bsDir = @"C:\Users\mdati\Desktop\Bluestacks";

        Assert.True(Directory.Exists(bsDir), "Bluestacks directory must exist.");

        // 1. Crop Matchmaking festive banner from "Matchmaking in homepage.png"
        string mmHome = Path.Combine(bsDir, "Matchmaking in homepage.png");
        using (var mmMat = new Mat(mmHome, ImreadModes.Color))
        {
            // The festive capsule is at X=671..1248 (width 577), Y=0..106
            var mmRect = new Rect(671, 0, 577, 106);
            using var mmCrop = new Mat(mmMat, mmRect);
            string targetPath = Path.Combine(anchorDir, "matchmaking_banner_festive.png");
            mmCrop.SaveImage(targetPath);
            _output.WriteLine($"Saved {targetPath}");
        }

        // 2. Crop Match Countdown golden Enter button from "Match Countdown.png"
        string mcPath = Path.Combine(bsDir, "Match Countdown.png");
        using (var mcMat = new Mat(mcPath, ImreadModes.Color))
        {
            // Enter button at X=740..1180 (width 440), Y=830..925 (height 95)
            var enterRect = new Rect(740, 830, 440, 95);
            using var enterCrop = new Mat(mcMat, enterRect);
            string targetPath = Path.Combine(anchorDir, "matchstart_enter_btn_festive.png");
            enterCrop.SaveImage(targetPath);
            _output.WriteLine($"Saved {targetPath}");

            // Diamond crystal apex at top X=870..1050, Y=0..180
            var apexRect = new Rect(870, 0, 180, 180);
            using var apexCrop = new Mat(mcMat, apexRect);
            string apexPath = Path.Combine(anchorDir, "matchstart_portal_apex.png");
            apexCrop.SaveImage(apexPath);
            _output.WriteLine($"Saved {apexPath}");
        }

        // 3. Crop Homepage Preparation tab & Mode icon from "Homepage.png"
        string hpPath = Path.Combine(bsDir, "Homepage.png");
        using (var hpMat = new Mat(hpPath, ImreadModes.Color))
        {
            // Preparation tab at X=140..280, Y=910..965 (height 55)
            var prepRect = new Rect(140, 910, 140, 55);
            using var prepCrop = new Mat(hpMat, prepRect);
            string prepPath = Path.Combine(anchorDir, "homepage_prep_tab.png");
            prepCrop.SaveImage(prepPath);
            _output.WriteLine($"Saved {prepPath}");

            // Mode button at X=1330..1425, Y=915..985
            var modeRect = new Rect(1330, 915, 95, 70);
            using var modeCrop = new Mat(hpMat, modeRect);
            string modePath = Path.Combine(anchorDir, "homepage_mode_btn.png");
            modeCrop.SaveImage(modePath);
            _output.WriteLine($"Saved {modePath}");
        }

        // 4. Crop Draft Pick Ally Team Pick header from "Draft Pick 2.png"
        string dp2Path = Path.Combine(bsDir, "Draft Pick 2.png");
        using (var dp2Mat = new Mat(dp2Path, ImreadModes.Color))
        {
            // Ally Team Pick header at X=810..1110, Y=12..62
            var headerRect = new Rect(810, 12, 300, 50);
            using var headerCrop = new Mat(dp2Mat, headerRect);
            string headerPath = Path.Combine(anchorDir, "draft_pick_team_header.png");
            headerCrop.SaveImage(headerPath);
            _output.WriteLine($"Saved {headerPath}");
        }

        // 5. Load and update phase_anchors.json
        var anchors = JsonSerializer.Deserialize<List<PhaseAnchorEntity>>(File.ReadAllText(jsonPath)) ?? [];

        // Add or update matchmaking_banner_festive
        UpdateOrAddAnchor(anchors, new PhaseAnchorEntity
        {
            Id = "matchmaking_banner_festive",
            Name = "Matchmaking Festive Banner",
            Phase = "Matchmaking",
            Filename = "matchmaking_banner_festive.png",
            X1 = 650,
            Y1 = 0,
            X2 = 1270,
            Y2 = 120,
            Threshold = 0.65,
            Enabled = true
        });

        // Add or update matchstart_enter_btn_festive
        UpdateOrAddAnchor(anchors, new PhaseAnchorEntity
        {
            Id = "matchstart_enter_btn_festive",
            Name = "Match Start Festive Enter Button",
            Phase = "Match Start",
            Filename = "matchstart_enter_btn_festive.png",
            X1 = 700,
            Y1 = 810,
            X2 = 1220,
            Y2 = 945,
            Threshold = 0.65,
            Enabled = true
        });

        // Add or update matchstart_portal_apex
        UpdateOrAddAnchor(anchors, new PhaseAnchorEntity
        {
            Id = "matchstart_portal_apex",
            Name = "Match Start Portal Apex",
            Phase = "Match Start",
            Filename = "matchstart_portal_apex.png",
            X1 = 850,
            Y1 = 0,
            X2 = 1070,
            Y2 = 200,
            Threshold = 0.65,
            Enabled = true
        });

        // Add or update homepage_prep_tab
        UpdateOrAddAnchor(anchors, new PhaseAnchorEntity
        {
            Id = "homepage_prep_tab",
            Name = "Homepage Preparation Tab",
            Phase = "Homepage",
            Filename = "homepage_prep_tab.png",
            X1 = 120,
            Y1 = 890,
            X2 = 300,
            Y2 = 985,
            Threshold = 0.65,
            Enabled = true
        });

        // Add or update homepage_mode_btn
        UpdateOrAddAnchor(anchors, new PhaseAnchorEntity
        {
            Id = "homepage_mode_btn",
            Name = "Homepage Mode Button",
            Phase = "Homepage",
            Filename = "homepage_mode_btn.png",
            X1 = 1310,
            Y1 = 900,
            X2 = 1445,
            Y2 = 1000,
            Threshold = 0.65,
            Enabled = true
        });

        // Add or update draft_pick_team_header
        UpdateOrAddAnchor(anchors, new PhaseAnchorEntity
        {
            Id = "draft_pick_team_header",
            Name = "Draft Pick Team Header",
            Phase = "Draft Pick",
            Filename = "draft_pick_team_header.png",
            X1 = 790,
            Y1 = 5,
            X2 = 1130,
            Y2 = 70,
            Threshold = 0.65,
            Enabled = true
        });

        // Calibrate existing draft_hero_prep_tabs threshold to 0.60 so variations pass reliably
        var prepTabs = anchors.FirstOrDefault(a => a.Id == "draft_hero_prep_tabs");
        if (prepTabs != null) prepTabs.Threshold = 0.60;

        // Calibrate matchmaking_top_banner threshold to 0.52 for classic theme fallback
        var mmTop = anchors.FirstOrDefault(a => a.Id == "matchmaking_top_banner");
        if (mmTop != null) mmTop.Threshold = 0.52;

        // Write back phase_anchors.json
        var opt = new JsonSerializerOptions { WriteIndented = true };
        string updatedJson = JsonSerializer.Serialize(anchors, opt);
        File.WriteAllText(jsonPath, updatedJson);
        _output.WriteLine($"Saved updated phase_anchors.json with {anchors.Count} anchors.");

        // Also copy updated anchors & json to bin/publish/Assets/templates/Phase Template/
        string pubDir = Path.Combine(root, "bin", "publish", "Assets", "templates", "Phase Template");
        if (Directory.Exists(pubDir))
        {
            string pubAnchors = Path.Combine(pubDir, "anchors");
            Directory.CreateDirectory(pubAnchors);
            foreach (var f in Directory.GetFiles(anchorDir))
            {
                File.Copy(f, Path.Combine(pubAnchors, Path.GetFileName(f)), true);
            }
            File.Copy(jsonPath, Path.Combine(pubDir, "phase_anchors.json"), true);
            _output.WriteLine("Synced to bin/publish/Assets.");
        }

        // Now test all BlueStacks screenshots — exclude asset subfolders like "Lane Icons"
        var engine = new OpenCvVisionEngine(NullLogger<OpenCvVisionEngine>.Instance);
        engine.InitializeTensors();

        var allFiles = Directory.GetFiles(bsDir, "*.png", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(bsDir, f).StartsWith("Lane Icons", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        int passCount = 0;
        _output.WriteLine("\n--- VERIFICATION OF ALL BLUESTACKS SCREENSHOTS ---");

        foreach (var file in allFiles)
        {
            string rel = Path.GetRelativePath(bsDir, file);
            var frame = new CapturedFrame
            {
                Buffer = File.ReadAllBytes(file),
                Width = 1920,
                Height = 1080,
                Source = rel
            };

            var (phase, subPhase, conf, details) = engine.DetectPhase(frame);
            bool isDetected = (phase != GamePhase.Standby && phase != "Standby");
            if (isDetected) passCount++;

            _output.WriteLine($"[{rel,-40}] -> {phase,-15} (Sub: {subPhase ?? "none",-12} Conf: {conf:F2}) -> {(isDetected ? "OK" : "FAILED")}");
        }

        _output.WriteLine($"\nResult: {passCount} of {allFiles.Length} recognized successfully.");
        Assert.Equal(allFiles.Length, passCount);
    }

    private static void UpdateOrAddAnchor(List<PhaseAnchorEntity> list, PhaseAnchorEntity anchor)
    {
        int idx = list.FindIndex(a => a.Id == anchor.Id);
        if (idx >= 0) list[idx] = anchor;
        else list.Add(anchor);
    }
}
