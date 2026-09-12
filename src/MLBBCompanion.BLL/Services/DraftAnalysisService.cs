using System.Diagnostics;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Orchestrator service running live draft CV pipeline, frame analysis, concurrency throttling, and timings telemetry.
/// </summary>
public class DraftAnalysisService : IDraftAnalysisService
{
    private readonly IScreenCaptureProvider _screenCapture;
    private readonly IVisionEngine _visionEngine;
    private readonly IPhaseDetectionService _phaseDetection;
    private readonly IRoiConfigurationService _roiService;
    private readonly IHeroDataService _heroDataService;
    private readonly ISimulationStateService? _simState;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private int _visualOverrideFrameCount = 0;

    public DraftAnalysisService(
        IScreenCaptureProvider screenCapture,
        IVisionEngine visionEngine,
        IPhaseDetectionService phaseDetection,
        IRoiConfigurationService roiService,
        IHeroDataService heroDataService,
        ISimulationStateService? simState = null)
    {
        _screenCapture = screenCapture;
        _visionEngine = visionEngine;
        _phaseDetection = phaseDetection;
        _roiService = roiService;
        _heroDataService = heroDataService;
        _simState = simState;
    }

    public Dictionary<string, int> InitializeAndWarmup()
    {
        _visionEngine.InitializeTensors();
        return _visionEngine.GetTemplateCounts();
    }

    public async Task<DraftScanResult> ScanLiveDraftAsync(bool simulate = false, CancellationToken cancellationToken = default)
    {
        // Item 17: Concurrency throttle - drop concurrent scans to avoid CPU spikes and OpenCV contention
        if (!await _scanGate.WaitAsync(0, cancellationToken))
        {
            return new DraftScanResult
            {
                Status = "busy",
                Phase = GamePhase.Transient,
                Message = "A draft scan is currently in progress. Dropping concurrent request to prevent CPU contention.",
                DiagnosticCode = "CONCURRENT_SCAN_THROTTLED",
                PhaseDetectionMethod = "throttled",
                IsTransient = true,
                CaptureDetails = _screenCapture.LastDiagnostics,
                AllySlots = Enumerable.Repeat("Empty", 5).ToList(),
                EnemySlots = Enumerable.Repeat("Empty", 5).ToList(),
                AllyBans = Enumerable.Repeat("Empty", 5).ToList(),
                EnemyBans = Enumerable.Repeat("Empty", 5).ToList()
            };
        }

        try
        {
            var sw = Stopwatch.StartNew();
            CapturedFrame? frame = null;

            bool isSim = simulate || (_simState?.IsSimulating ?? false);
            if (isSim)
            {
                byte[]? simBytes = LoadSimulationFixtureBytes();
                if (simBytes != null)
                {
                    frame = new CapturedFrame
                    {
                        Buffer = simBytes,
                        Width = 1920,
                        Height = 1080,
                        Source = "SimulationFixture"
                    };
                }
            }
            else
            {
                frame = await _screenCapture.CaptureActiveWindowAsync(cancellationToken);
            }

            if (frame == null || !frame.IsValid)
            {
                frame?.Dispose();
                var diag = _screenCapture.LastDiagnostics;
                return new DraftScanResult
                {
                    Status = "standby",
                    Phase = GamePhase.Standby,
                    Message = diag.Message ?? "No active BlueStacks window or ADB stream found.",
                    DiagnosticCode = diag.Code,
                    PhaseDetectionMethod = "none",
                    CaptureDetails = diag,
                    IsSimulated = false,
                    AllySlots = Enumerable.Repeat("Empty", 5).ToList(),
                    EnemySlots = Enumerable.Repeat("Empty", 5).ToList(),
                    AllyBans = Enumerable.Repeat("Empty", 5).ToList(),
                    EnemyBans = Enumerable.Repeat("Empty", 5).ToList()
                };
            }

            using (frame)
            {
                // Frame integrity check
                var (isValid, isTransient, reason, metrics) = _visionEngine.InspectFrameIntegrity(frame);
                if (!isValid || isTransient)
                {
                    return new DraftScanResult
                    {
                        Status = "transient",
                        Phase = GamePhase.Transient,
                        SubPhase = null,
                        Confidence = 0.0,
                        Message = $"Frame integrity check skipped processing: {reason}",
                        DiagnosticCode = "FRAME_INTEGRITY_REJECTED",
                        PhaseDetectionMethod = "frame_integrity",
                        CaptureDetails = _screenCapture.LastDiagnostics,
                        IsTransient = true,
                        IsSimulated = isSim,
                        Metrics = metrics,
                        AllySlots = Enumerable.Repeat("Empty", 5).ToList(),
                        EnemySlots = Enumerable.Repeat("Empty", 5).ToList(),
                        AllyBans = Enumerable.Repeat("Empty", 5).ToList(),
                        EnemyBans = Enumerable.Repeat("Empty", 5).ToList()
                    };
                }

                // Phase check
                var (phase, subPhase, phaseConf, _) = await _phaseDetection.GetCurrentGamePhaseAsync(frame);
                string detectionMethod = "anchor";

                // Natural fallback if simulation fixture could not match phase anchors
                if (isSim && !GamePhase.IsInDraft(phase))
                {
                    phase = GamePhase.DraftPick;
                    subPhase ??= "Pick Phase";
                    phaseConf = Math.Max(phaseConf, 0.95);
                    detectionMethod = "sim_override";
                }

                var config = _roiService.GetCurrentConfiguration();
                double banThresh = isSim ? 0.40 : _roiService.GetThreshold("ban_threshold", 0.45);
                double pickThresh = isSim ? 0.35 : _roiService.GetThreshold("pick_threshold", 0.35);

                var pipeline = ExecuteVisionPipeline(frame, config, banThresh, pickThresh, sw, includeThumbnails: false);

                var allyBans = pipeline.DebugBans.Take(5).Select(b => b.MatchedHero ?? "Empty").ToList();
                var enemyBans = pipeline.DebugBans.Skip(5).Take(5).Select(b => b.MatchedHero ?? "Empty").ToList();

                var allySlotNames = pipeline.AllyPicks.Select(p => p.MatchedHero ?? "Empty").ToList();
                var enemySlotNames = pipeline.EnemyPicks.Select(p => p.MatchedHero ?? "Empty").ToList();

                // Item 4 & 15: Check visual draft evidence with consecutive-frame threshold to prevent flicker
                int detectedSlotsCount = 0;
                double highestConfidence = 0.0;

                foreach (var p in pipeline.AllyPicks.Concat(pipeline.EnemyPicks))
                {
                    if (!string.IsNullOrWhiteSpace(p.MatchedHero) && !p.MatchedHero.Equals("Empty", StringComparison.OrdinalIgnoreCase))
                    {
                        detectedSlotsCount++;
                        if (p.Confidence > highestConfidence) highestConfidence = p.Confidence;
                    }
                }
                foreach (var b in pipeline.DebugBans)
                {
                    if (!string.IsNullOrWhiteSpace(b.MatchedHero) && !b.MatchedHero.Equals("Empty", StringComparison.OrdinalIgnoreCase))
                    {
                        detectedSlotsCount++;
                        if (b.Confidence > highestConfidence) highestConfidence = b.Confidence;
                    }
                }

                bool hasSufficientDraftEvidence = detectedSlotsCount >= 2 || highestConfidence >= 0.60;

                if (hasSufficientDraftEvidence && !GamePhase.IsInDraft(phase))
                {
                    int overrideFrames = Interlocked.Increment(ref _visualOverrideFrameCount);
                    if (overrideFrames >= 2 || detectedSlotsCount >= 3)
                    {
                        phase = GamePhase.DraftPick;
                        subPhase ??= "Ban / Pick Phase";
                        phaseConf = Math.Max(phaseConf, Math.Round(highestConfidence, 2));
                        detectionMethod = "visual_override";
                    }
                }
                else
                {
                    _visualOverrideFrameCount = 0;
                }

                // If not in draft and no sufficient evidence to override, report standby
                if (!isSim && !GamePhase.IsInDraft(phase))
                {
                    return new DraftScanResult
                    {
                        Status = "standby",
                        Phase = phase,
                        SubPhase = subPhase,
                        Confidence = phaseConf,
                        DiagnosticCode = "PHASE_NON_DRAFT",
                        PhaseDetectionMethod = detectionMethod,
                        CaptureDetails = _screenCapture.LastDiagnostics,
                        IsSimulated = false,
                        Message = $"Game is currently in {phase} phase (Draft Pick inactive).",
                        AllySlots = Enumerable.Repeat("Empty", 5).ToList(),
                        EnemySlots = Enumerable.Repeat("Empty", 5).ToList(),
                        AllyBans = Enumerable.Repeat("Empty", 5).ToList(),
                        EnemyBans = Enumerable.Repeat("Empty", 5).ToList()
                    };
                }

                // Confirmed draft activity
                return new DraftScanResult
                {
                    Status = "active",
                    Phase = phase,
                    SubPhase = subPhase,
                    Confidence = phaseConf,
                    DiagnosticCode = "OK",
                    PhaseDetectionMethod = detectionMethod,
                    CaptureDetails = _screenCapture.LastDiagnostics,
                    IsSimulated = isSim,
                    AllySlots = allySlotNames,
                    EnemySlots = enemySlotNames,
                    AllyLanes = pipeline.AllyLanes,
                    AllySpells = pipeline.AllySpells,
                    EnemySpells = pipeline.EnemySpells,
                    AllyBans = allyBans,
                    EnemyBans = enemyBans,
                    Timings = pipeline.Timings,
                    DebugBansArray = pipeline.DebugBans,
                    DebugAllyPicksArray = pipeline.AllyPicks,
                    DebugEnemyPicksArray = pipeline.EnemyPicks,
                    DebugPicksArray = pipeline.AllyPicks.Concat(pipeline.EnemyPicks).ToList()
                };
            }
        }
        finally
        {
            _scanGate.Release();
        }
    }

    public async Task<FrameAnalysisResult> AnalyzeFrameAsync(string? base64Image = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        CapturedFrame? frame = null;

        if (!string.IsNullOrWhiteSpace(base64Image))
        {
            frame = _screenCapture.DecodeBase64Frame(base64Image);
        }
        else
        {
            frame = await _screenCapture.CaptureActiveWindowAsync(cancellationToken);
        }

        if (frame == null || !frame.IsValid)
        {
            frame?.Dispose();
            return new FrameAnalysisResult
            {
                Success = false,
                Error = "No image provided and no active window to capture."
            };
        }

        using (frame)
        {
            // Frame integrity
            var (isValid, isTransient, reason, metrics) = _visionEngine.InspectFrameIntegrity(frame);
            if (!isValid || isTransient)
            {
                return new FrameAnalysisResult
                {
                    Success = false,
                    IsTransient = true,
                    Error = $"Frame rejected by integrity guard: {reason}",
                    Phase = GamePhase.Transient,
                    Metrics = metrics
                };
            }

            var config = _roiService.GetCurrentConfiguration();
            var (phase, subPhase, conf, details) = _visionEngine.DetectPhase(frame);

            double banThresh = _roiService.GetThreshold("ban_threshold", 0.45);
            double pickThresh = _roiService.GetThreshold("pick_threshold", 0.35);

            var pipeline = ExecuteVisionPipeline(frame, config, banThresh, pickThresh, sw, includeThumbnails: true);
            var allPicks = pipeline.AllyPicks.Concat(pipeline.EnemyPicks).ToList();

            return new FrameAnalysisResult
            {
                Success = true,
                Phase = phase,
                SubPhase = subPhase,
                Confidence = Math.Round(conf, 2),
                Details = details,
                Bans = pipeline.DebugBans,
                Picks = allPicks,
                AllySpells = pipeline.AllySpells,
                EnemySpells = pipeline.EnemySpells,
                DebugBansArray = pipeline.DebugBans,
                DebugAllyPicksArray = pipeline.AllyPicks,
                DebugEnemyPicksArray = pipeline.EnemyPicks,
                DebugPicksArray = allPicks,
                PipelineTimingsMs = pipeline.Timings
            };
        }
    }

    private sealed record DraftVisionPipelineResult(
        List<SlotMatch> DebugBans,
        List<SlotMatch> AllyPicks,
        List<SlotMatch> EnemyPicks,
        List<string?> AllyLanes,
        List<string?> AllySpells,
        List<string?> EnemySpells,
        PipelineTimings Timings
    );

    private DraftVisionPipelineResult ExecuteVisionPipeline(
        CapturedFrame frame,
        RoiConfiguration config,
        double banThresh,
        double pickThresh,
        Stopwatch totalSw,
        bool includeThumbnails = false)
    {
        var (debugBans, banExtMs, banInfMs) = _visionEngine.MatchBansWithTimings(frame, config, banThresh, topK: 5, includeThumbnails: includeThumbnails);

        var bannedHeroes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in debugBans)
        {
            if (!string.IsNullOrWhiteSpace(b.MatchedHero) && !b.MatchedHero.Equals("Empty", StringComparison.OrdinalIgnoreCase))
            {
                bannedHeroes.Add(b.MatchedHero);
            }
        }

        var (allyPicks, enemyPicks, pickTimings) = _visionEngine.MatchPicksJoint(
            frame,
            config,
            bannedHeroes,
            pickThresh,
            topK: 5,
            includeThumbnails: includeThumbnails
        );

        var allyLanes = _visionEngine.DetectLanes(frame, config);
        var (allySpells, enemySpells) = _visionEngine.DetectSpells(frame, config);

        for (int i = 0; i < Math.Min(allyPicks.Count, allyLanes.Count); i++)
        {
            // Item 9: Only assign lane and spell badges if the corresponding hero slot is populated
            bool isSlotPopulated = !string.IsNullOrWhiteSpace(allyPicks[i].MatchedHero) &&
                                   !string.Equals(allyPicks[i].MatchedHero, "Empty", StringComparison.OrdinalIgnoreCase);

            string? lane = isSlotPopulated ? allyLanes[i] : null;
            string? spell = isSlotPopulated ? allySpells[i] : null;

            allyPicks[i].Lane = lane;
            allyPicks[i].DetectedLane = lane;
            allyPicks[i].Spell = spell;
            allyPicks[i].DetectedSpell = spell;
        }

        for (int i = 0; i < Math.Min(enemyPicks.Count, enemySpells.Count); i++)
        {
            bool isSlotPopulated = !string.IsNullOrWhiteSpace(enemyPicks[i].MatchedHero) &&
                                   !string.Equals(enemyPicks[i].MatchedHero, "Empty", StringComparison.OrdinalIgnoreCase);

            string? spell = isSlotPopulated ? enemySpells[i] : null;
            enemyPicks[i].Spell = spell;
            enemyPicks[i].DetectedSpell = spell;
        }

        totalSw.Stop();
        pickTimings.ExtractionMs = banExtMs;
        pickTimings.BanInferenceMs = banInfMs;
        pickTimings.TotalMs = Math.Round(totalSw.Elapsed.TotalMilliseconds, 2);

        return new DraftVisionPipelineResult(
            debugBans,
            allyPicks,
            enemyPicks,
            allyLanes,
            allySpells,
            enemySpells,
            pickTimings
        );
    }

    public void ReloadDatabase()
    {
        _heroDataService.ReloadDatabase();
        _visionEngine.ReloadTemplates();
    }

    private static byte[]? LoadSimulationFixtureBytes()
    {
        string[] candidates =
        [
            WorkspacePathResolver.ResolvePath("Assets", "real_user_draft_1080.png"),
            WorkspacePathResolver.ResolvePath("Solution", "MLBB.Tests", "fixtures", "real_user_draft_1080.png"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "real_user_draft_1080.png"),
            Path.Combine(AppContext.BaseDirectory, "fixtures", "real_user_draft_1080.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "real_user_draft_1080.png")
        ];
        string? file = candidates.FirstOrDefault(File.Exists);
        return file != null ? File.ReadAllBytes(file) : null;
    }
}

