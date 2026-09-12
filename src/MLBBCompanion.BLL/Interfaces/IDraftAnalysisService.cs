using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Orchestrator service connecting Computer Vision matchers, screen capture, and draft state.
/// </summary>
public interface IDraftAnalysisService
{
    Dictionary<string, int> InitializeAndWarmup();
    Task<DraftScanResult> ScanLiveDraftAsync(bool simulate = false, CancellationToken cancellationToken = default);
    Task<FrameAnalysisResult> AnalyzeFrameAsync(string? base64Image = null, CancellationToken cancellationToken = default);
    void ReloadDatabase();
}

