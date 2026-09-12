using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using MLBB.Infrastructure.Vision;
using Xunit;

namespace MLBB.Tests;

public class VisionEngineTests
{
    private readonly OpenCvVisionEngine _visionEngine;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public VisionEngineTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        var logger = NullLogger<OpenCvVisionEngine>.Instance;
        _visionEngine = new OpenCvVisionEngine(logger);
        _visionEngine.InitializeTensors();
    }

    [Fact]
    public void InitializeTensors_LoadsExpectedTemplateCounts()
    {
        var counts = _visionEngine.GetTemplateCounts();
        Assert.NotNull(counts);
        Assert.True(_visionEngine.IsReady, "Vision engine should be ready after InitializeTensors");
        Assert.True(counts.ContainsKey("picks"));
        Assert.True(counts.ContainsKey("bans"));
        Assert.True(counts.ContainsKey("lanes"));
        Assert.True(counts.ContainsKey("unique_heroes"));
        Assert.True(counts.ContainsKey("total_hero_templates"));
        Assert.True(counts["unique_heroes"] >= 100, $"Expected >= 100 unique heroes, got {counts["unique_heroes"]}");
        Assert.True(counts["total_hero_templates"] >= counts["unique_heroes"], "Total hero templates must be >= unique heroes");
        Assert.True(counts["bans"] >= 100, $"Expected >= 100 bans, got {counts["bans"]}");
        Assert.True(counts["lanes"] == 5, $"Expected exactly 5 lanes, got {counts["lanes"]}");
    }

    [Fact]
    public void InspectFrameIntegrity_WithValid1080pFrame_ReturnsValid()
    {
        using var testMat = new OpenCvSharp.Mat(1080, 1920, OpenCvSharp.MatType.CV_8UC3, new OpenCvSharp.Scalar(100, 120, 140));
        OpenCvSharp.Cv2.Rectangle(testMat, new OpenCvSharp.Rect(100, 100, 500, 500), new OpenCvSharp.Scalar(200, 50, 80), -1);
        OpenCvSharp.Cv2.ImEncode(".png", testMat, out byte[] pngBytes);

        var frame = new CapturedFrame
        {
            Buffer = pngBytes,
            Width = 1920,
            Height = 1080,
            Source = "Test"
        };
        var (isValid, isTransient, reason, _) = _visionEngine.InspectFrameIntegrity(frame);
        Assert.True(isValid, $"Integrity check failed: {reason}");
        Assert.False(isTransient);
    }

    [Fact]
    public void MatchBans_OnRealDraftFixture_DetectsKnownBannedHeroes()
    {
        string fixturePath = WorkspacePathResolver.ResolvePath("Solution", "MLBB.Tests", "fixtures", "real_user_draft_1080.png");
        if (!File.Exists(fixturePath))
        {
            fixturePath = WorkspacePathResolver.ResolvePath("Assets", "real_user_draft_1080.png");
        }
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "real_user_draft_1080.png");
        }

        Assert.True(File.Exists(fixturePath), $"Fixture file must exist: {fixturePath}");
        byte[] bytes = File.ReadAllBytes(fixturePath);

        var frame = new CapturedFrame
        {
            Buffer = bytes,
            Width = 1920,
            Height = 1080,
            Source = "Fixture"
        };

        var roiRepo = new MLBB.Infrastructure.Repositories.RoiRepository(NullLogger<MLBB.Infrastructure.Repositories.RoiRepository>.Instance);
        var config = roiRepo.GetConfiguration();

        // 1. Bans
        var (timedBans, extMs, infMs) = _visionEngine.MatchBansWithTimings(frame, config, 0.40);
        Assert.Equal(10, timedBans.Count);
        Assert.True(extMs >= 0.0, "ExtractionMs should be >= 0");
        Assert.True(infMs >= 0.0, "BanInferenceMs should be >= 0");

        var bans = _visionEngine.MatchBans(frame, config, 0.40);
        Assert.Equal(10, bans.Count);
        _output.WriteLine($"Bans: {string.Join(", ", bans.Select(b => b.MatchedHero))}");

        // 2. Picks
        var bannedNames = bans.Where(b => b.MatchedHero != "Empty").Select(b => b.MatchedHero!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var (allyPicks, enemyPicks, _) = _visionEngine.MatchPicksJoint(frame, config, bannedNames, 0.35);
        Assert.Equal(5, allyPicks.Count);
        Assert.Equal(5, enemyPicks.Count);
        _output.WriteLine($"Ally Picks: {string.Join(", ", allyPicks.Select(p => p.MatchedHero))}");
        _output.WriteLine($"Enemy Picks: {string.Join(", ", enemyPicks.Select(p => p.MatchedHero))}");

        // 3. Lanes
        var lanes = _visionEngine.DetectLanes(frame, config);
        Assert.Equal(5, lanes.Count);
        _output.WriteLine($"Lanes: {string.Join(", ", lanes)}");

        // 4. Spells
        var (allySpells, enemySpells) = _visionEngine.DetectSpells(frame, config);
        Assert.Equal(5, allySpells.Count);
        Assert.Equal(5, enemySpells.Count);
        _output.WriteLine($"Ally Spells: {string.Join(", ", allySpells)}");
        _output.WriteLine($"Enemy Spells: {string.Join(", ", enemySpells)}");

        // 5. Phase
        var (phase, subPhase, conf, _) = _visionEngine.DetectPhase(frame);
        _output.WriteLine($"Phase: {phase}, SubPhase: {subPhase}, Conf: {conf}");
        Assert.Equal("Draft Pick", phase);
    }

    [Fact]
    public void DetectPhase_OnLobbyScreen_DetectsLobbyPhase()
    {
        string lobbyPath = WorkspacePathResolver.ResolvePath("current_screencap.png");
        if (File.Exists(lobbyPath))
        {
            byte[] bytes = File.ReadAllBytes(lobbyPath);
            var frame = new CapturedFrame { Buffer = bytes, Width = 1920, Height = 1080 };
            var (phase, _, conf, _) = _visionEngine.DetectPhase(frame);
            Assert.Equal("Lobby", phase);
        }
    }

    [Fact]
    public void VisionEngine_ReadinessAndAnchorSource_AreValid()
    {
        Assert.True(_visionEngine.IsReady);
        Assert.False(_visionEngine.IsReloading);
        Assert.Empty(_visionEngine.MissingCategories);
        Assert.Contains(_visionEngine.AnchorSource, new[] { "json_file", "embedded_defaults" });
    }

    [Fact]
    public void ReloadTemplates_AtomicallyRefreshes_WithoutBreakingReadiness()
    {
        Assert.True(_visionEngine.IsReady);
        _visionEngine.ReloadTemplates();
        Assert.True(_visionEngine.IsReady);
        Assert.False(_visionEngine.IsReloading);

        var counts = _visionEngine.GetTemplateCounts();
        Assert.True(counts["unique_heroes"] >= 100);
        Assert.True(counts.ContainsKey("estimated_mat_memory_mb"));
        Assert.True(counts["estimated_mat_memory_mb"] > 0);
    }

    [Fact]
    public void TransformAnchorCoordinates_HandlesExactAndLetterboxedRatios()
    {
        // 1. Exact 1920x1080 -> 1:1 mapping
        var (x1, y1, x2, y2) = OpenCvVisionEngine.TransformAnchorCoordinates(1920, 1080, 100, 200, 300, 400);
        Assert.Equal(100, x1);
        Assert.Equal(200, y1);
        Assert.Equal(300, x2);
        Assert.Equal(400, y2);

        // 2. Uniform 1280x720 (16:9 scaled down) -> 2/3 scale
        var (sx1, sy1, sx2, sy2) = OpenCvVisionEngine.TransformAnchorCoordinates(1280, 720, 300, 150, 600, 300);
        Assert.Equal(200, sx1);
        Assert.Equal(100, sy1);
        Assert.Equal(400, sx2);
        Assert.Equal(200, sy2);

        // 3. 1920x1200 (16:10 with letterbox bars) -> 60px vertical centering offset
        var (lx1, ly1, lx2, ly2) = OpenCvVisionEngine.TransformAnchorCoordinates(1920, 1200, 100, 100, 200, 200);
        Assert.Equal(100, lx1);
        Assert.Equal(160, ly1); // 100 + 60 offset
        Assert.Equal(200, lx2);
        Assert.Equal(260, ly2); // 200 + 60 offset
    }

    [Fact]
    public void NativeMemoryBytes_IsTracked_AndWithinSafetyLimit()
    {
        long bytes = _visionEngine.EstimatedNativeMemoryBytes;
        Assert.True(bytes > 0, "Native OpenCV Mat bytes must be greater than 0.");
        Assert.True(bytes < 500L * 1024 * 1024, "Native memory must be well within 500MB safety ceiling.");
    }

    [Fact]
    public void ProcessCycle_FiresOnDraftDetectionUpdated_WithCompleteDraftResults()
    {
        string fixturePath = WorkspacePathResolver.ResolvePath("Assets", "real_user_draft_1080.png");
        if (!File.Exists(fixturePath))
        {
            fixturePath = WorkspacePathResolver.ResolvePath("Solution", "MLBB.Tests", "fixtures", "real_user_draft_1080.png");
        }
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist: {fixturePath}");
        byte[] bytes = File.ReadAllBytes(fixturePath);

        var frame = new CapturedFrame
        {
            Buffer = bytes,
            Width = 1920,
            Height = 1080,
            Source = "Fixture"
        };

        var roiRepo = new MLBB.Infrastructure.Repositories.RoiRepository(NullLogger<MLBB.Infrastructure.Repositories.RoiRepository>.Instance);
        var config = roiRepo.GetConfiguration();

        DraftScanResult? eventResult = null;
        _visionEngine.OnDraftDetectionUpdated += res => eventResult = res;

        var cycleResult = _visionEngine.ProcessCycle(frame, config);

        Assert.NotNull(eventResult);
        Assert.Same(cycleResult, eventResult);

        // 1. Bans (10 total, none empty)
        Assert.Equal(10, cycleResult.Bans.Count);
        Assert.All(cycleResult.Bans, b => Assert.False(string.IsNullOrEmpty(b.HeroId) || b.HeroId == "Empty"));
        Assert.Equal("saber", cycleResult.Bans[0].HeroId);
        Assert.Equal("paquito", cycleResult.Bans[3].HeroId);
        Assert.Equal("atlas", cycleResult.Bans[5].HeroId);

        // 2. Ally Picks (5 total, none empty)
        Assert.Equal(5, cycleResult.AllyPicks.Count);
        Assert.All(cycleResult.AllyPicks, p => Assert.False(string.IsNullOrEmpty(p.HeroId) || p.HeroId == "Empty"));
        Assert.Equal("layla", cycleResult.AllyPicks[0].HeroId);
        Assert.Equal("ling", cycleResult.AllyPicks[4].HeroId);

        // 3. Enemy Picks (5 total, none empty)
        Assert.Equal(5, cycleResult.EnemyPicks.Count);
        Assert.All(cycleResult.EnemyPicks, p => Assert.False(string.IsNullOrEmpty(p.HeroId) || p.HeroId == "Empty"));
        Assert.Equal("vexana", cycleResult.EnemyPicks[0].HeroId);
        Assert.Equal("carmilla", cycleResult.EnemyPicks[1].HeroId);
        Assert.Equal("zetian", cycleResult.EnemyPicks[2].HeroId);

        // 4. Lanes
        Assert.Equal(5, cycleResult.Lanes.Count);
        Assert.All(cycleResult.Lanes, l => Assert.NotNull(l));

        // 5. Phase
        Assert.Equal("Draft Pick", cycleResult.DetectedPhase);
    }

    [Fact]
    public void MatchBans_PopulatesTop3PriorityCandidatesWithDescendingScores()
    {
        string fixturePath = WorkspacePathResolver.ResolvePath("Assets", "real_user_draft_1080.png");
        Assert.True(File.Exists(fixturePath), $"Fixture file must exist: {fixturePath}");
        byte[] bytes = File.ReadAllBytes(fixturePath);

        var frame = new CapturedFrame
        {
            Buffer = bytes,
            Width = 1920,
            Height = 1080,
            Source = "Fixture"
        };

        var roiRepo = new MLBB.Infrastructure.Repositories.RoiRepository(NullLogger<MLBB.Infrastructure.Repositories.RoiRepository>.Instance);
        var config = roiRepo.GetConfiguration();

        var bans = _visionEngine.MatchBans(frame, config, threshold: 0.45, topK: 5);
        Assert.Equal(10, bans.Count);

        foreach (var b in bans)
        {
            Assert.True(b.TopKCandidates.Count >= 3, $"Ban slot {b.Slot} should have at least 3 candidates");
            Assert.False(string.IsNullOrWhiteSpace(b.TopKCandidates[0].Hero), "Priority 1 hero must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(b.TopKCandidates[1].Hero), "Priority 2 hero must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(b.TopKCandidates[2].Hero), "Priority 3 hero must not be empty");

            // Verify descending scores
            Assert.True(b.TopKCandidates[0].Score >= b.TopKCandidates[1].Score,
                $"Priority 1 ({b.TopKCandidates[0].Score}) must be >= Priority 2 ({b.TopKCandidates[1].Score})");
            Assert.True(b.TopKCandidates[1].Score >= b.TopKCandidates[2].Score,
                $"Priority 2 ({b.TopKCandidates[1].Score}) must be >= Priority 3 ({b.TopKCandidates[2].Score})");

            // Matched hero should be set when confidence >= threshold
            if (b.Confidence >= 0.45)
            {
                Assert.False(string.IsNullOrEmpty(b.Hero), $"Slot {b.Slot} hero should be detected");
                Assert.NotEqual("Empty", b.Hero);
            }
        }
    }
}
