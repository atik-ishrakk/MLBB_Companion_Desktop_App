using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using Xunit;

namespace MLBB.Tests;

public class DraftAnalysisSecurityTests
{

    [Fact]
    public async Task DraftScanResult_Simulation_SetsDiagnosticCodeAndPhaseMethod()
    {
        var mockCapture = new TestScreenCaptureProvider();
        var mockEngine = new MLBB.Infrastructure.Vision.OpenCvVisionEngine(NullLogger<MLBB.Infrastructure.Vision.OpenCvVisionEngine>.Instance);
        mockEngine.InitializeTensors();

        var roiRepo = new MLBB.Infrastructure.Repositories.RoiRepository(NullLogger<MLBB.Infrastructure.Repositories.RoiRepository>.Instance);
        var roiService = new RoiConfigurationService(roiRepo);
        var phaseDetection = new TestPhaseDetectionService();
        var heroRepo = new MLBB.Infrastructure.Repositories.HeroRepository(NullLogger<MLBB.Infrastructure.Repositories.HeroRepository>.Instance);
        var heroData = new HeroDataService(heroRepo);

        var service = new DraftAnalysisService(mockCapture, mockEngine, phaseDetection, roiService, heroData);

        var result = await service.ScanLiveDraftAsync(simulate: true);

        Assert.NotNull(result);
        Assert.True(result.IsSimulated);
        Assert.Equal("OK", result.DiagnosticCode);
        Assert.Contains(result.PhaseDetectionMethod, new[] { "anchor", "sim_override", "visual_override" });
        Assert.Equal(5, result.AllySlots.Count);
        Assert.Equal(5, result.EnemySlots.Count);
    }

    [Fact]
    public void HungarianAlgorithm_FindsGlobalMaxWeight_OverGreedyChoice()
    {
        // Scenario where greedy choice fails:
        // Slot 0 candidates: "tigreal" (0.85), "layla" (0.84)
        // Slot 1 candidates: "tigreal" (0.84), "layla" (0.30)
        // Greedy:
        //   Slot 0 takes "tigreal" (0.85)
        //   Slot 1 is forced to take "layla" (0.30)
        //   Total score = 1.15
        // Hungarian (Optimal):
        //   Slot 0 takes "layla" (0.84)
        //   Slot 1 takes "tigreal" (0.84)
        //   Total score = 1.68
        var slots = new List<SlotMatch>
        {
            new() { Slot = 0, Side = "ally" },
            new() { Slot = 1, Side = "ally" }
        };

        var candidateLists = new List<List<(string Hero, double Score)>>
        {
            new() { ("tigreal", 0.85), ("layla", 0.84) },
            new() { ("tigreal", 0.84), ("layla", 0.30) }
        };

        MLBB.Infrastructure.Vision.OpenCvVisionEngine.SolveMaximumWeightBipartiteAssignment(slots, candidateLists, threshold: 0.35);

        Assert.Equal("layla", slots[0].Hero);
        Assert.Equal("tigreal", slots[1].Hero);
        Assert.Equal(0.84, slots[0].Confidence);
        Assert.Equal(0.84, slots[1].Confidence);
    }

    private sealed class TestScreenCaptureProvider : IScreenCaptureProvider
    {
        public CaptureDiagnostics LastDiagnostics { get; } = new() { Code = "OK", Message = "Mock capture active" };

        public Task<CapturedFrame?> CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CapturedFrame?>(null);
        }

        public CapturedFrame? DecodeBase64Frame(string base64Image) => null;
        public bool IsWindowAvailable() => true;
    }

    private sealed class TestPhaseDetectionService : IPhaseDetectionService
    {
        public Task<(string phase, string? subPhase, double confidence, string details)> GetCurrentGamePhaseAsync(CapturedFrame? frame = null)
        {
            return Task.FromResult((GamePhase.DraftPick, (string?)"Pick Phase", 0.95, "Test stub draft confirmed"));
        }
    }
}

