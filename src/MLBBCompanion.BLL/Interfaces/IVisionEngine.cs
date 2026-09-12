using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// High-performance Computer Vision Engine abstraction.
/// Decouples CV algorithms (ZNCC, CLAHE, Hungarian Assignment, Template Matching) from application services.
/// </summary>
public interface IVisionEngine : IDisposable
{
    bool IsReady { get; }
    bool IsReloading { get; }
    string AnchorSource { get; }
    string AssetsRoot { get; }
    string? AnchorConfigPath { get; }
    IReadOnlyList<string> MissingCategories { get; }
    IReadOnlyList<string> MissingAnchors { get; }
    long EstimatedNativeMemoryBytes { get; }
    void InitializeTensors();
    Dictionary<string, int> GetTemplateCounts();
    void ReloadTemplates();
    void ClearSlotCache();

    (bool isValid, bool isTransient, string reason, Dictionary<string, object> metrics) InspectFrameIntegrity(CapturedFrame frame);

    (string phase, string? subPhase, double confidence, string details) DetectPhase(CapturedFrame frame);

    List<SlotMatch> MatchBans(CapturedFrame frame, RoiConfiguration config, double threshold, int topK = 5, bool includeThumbnails = false);

    (List<SlotMatch> matches, double extractionMs, double inferenceMs) MatchBansWithTimings(
        CapturedFrame frame,
        RoiConfiguration config,
        double threshold,
        int topK = 5,
        bool includeThumbnails = false);

    (List<SlotMatch> allyPicks, List<SlotMatch> enemyPicks, PipelineTimings timings) MatchPicksJoint(
        CapturedFrame frame,
        RoiConfiguration config,
        HashSet<string> bannedHeroes,
        double threshold,
        int topK = 5,
        bool includeThumbnails = false);

    List<string?> DetectLanes(CapturedFrame frame, RoiConfiguration config);

    (List<string?> allySpells, List<string?> enemySpells) DetectSpells(CapturedFrame frame, RoiConfiguration config);

    event Action<DraftScanResult>? OnDraftDetectionUpdated;

    DraftScanResult ProcessCycle(CapturedFrame frame, RoiConfiguration config, HashSet<string>? takenBans = null);
}

