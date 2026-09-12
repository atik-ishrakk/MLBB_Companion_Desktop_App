using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace MLBB.Infrastructure.Vision;

/// <summary>
/// Encapsulates an immutable snapshot of pre-warmed OpenCV tensors for ban, pick, lane, spell, and phase matching.
/// Enables race-free template reloading and zero-lock reader access.
/// </summary>
internal sealed class VisionTensorsSnapshot : IDisposable
{
    // Legacy Mat dictionaries preserved for backward-compatibility with tests and counters
    public Dictionary<string, List<Mat>> BanTemplates { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<Mat>> PickTemplates { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Mat> LaneTemplates { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Mat> SpellTemplates { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<(PhaseAnchorEntity Config, Mat Template)> LoadedPhaseAnchors { get; } = [];
    public Mat? CircularBanMask { get; set; }
    public string AnchorSource { get; set; } = "none";
    public string? AnchorConfigPath { get; set; }
    public string AssetsRoot { get; set; } = string.Empty;
    public List<string> MissingCategories { get; } = [];
    public List<string> MissingAnchors { get; } = [];
    public long EstimatedNativeMemoryBytes { get; set; }
    public bool IsInitialized { get; set; }

    // High-Precision Pre-Extracted Feature Vectors (ZNCC + 2D HSV)
    public List<string> BanHeroNames { get; } = [];
    public Dictionary<int, List<HeroFeatureVector>> HeroToBanVectors { get; } = [];
    public Mat? EmptyBanAlly { get; set; }
    public Mat? EmptyBanEnemy { get; set; }

    public List<string> PickHeroNames { get; } = [];
    public Dictionary<int, List<HeroFeatureVector>> HeroToAllyPickVectors { get; } = [];
    public Dictionary<int, List<HeroFeatureVector>> HeroToEnemyPickVectors { get; } = [];
    public Mat? EmptyPickAlly { get; set; }
    public Mat? EmptyPickAllyDim { get; set; }
    public Mat? EmptyPickEnemy { get; set; }

    public Dictionary<(string LaneKey, double Scale), Mat> ScaledLaneTemplates { get; } = [];

    public bool IsComplete =>
        IsInitialized &&
        BanHeroNames.Count > 0 &&
        PickHeroNames.Count > 0 &&
        LaneTemplates.Count > 0 &&
        SpellTemplates.Count > 0 &&
        LoadedPhaseAnchors.Count > 0 &&
        MissingCategories.Count == 0 &&
        MissingAnchors.Count == 0;

    public void Dispose()
    {
        CircularBanMask?.Dispose();
        CircularBanMask = null;

        EmptyBanAlly?.Dispose();
        EmptyBanAlly = null;
        EmptyBanEnemy?.Dispose();
        EmptyBanEnemy = null;

        EmptyPickAlly?.Dispose();
        EmptyPickAlly = null;
        EmptyPickAllyDim?.Dispose();
        EmptyPickAllyDim = null;
        EmptyPickEnemy?.Dispose();
        EmptyPickEnemy = null;

        foreach (var list in BanTemplates.Values)
        {
            foreach (var m in list) m.Dispose();
        }
        foreach (var list in PickTemplates.Values)
        {
            foreach (var m in list) m.Dispose();
        }
        foreach (var m in LaneTemplates.Values) m.Dispose();
        foreach (var m in SpellTemplates.Values) m.Dispose();
        foreach (var (_, m) in LoadedPhaseAnchors) m.Dispose();
        foreach (var m in ScaledLaneTemplates.Values) m.Dispose();

        BanTemplates.Clear();
        PickTemplates.Clear();
        LaneTemplates.Clear();
        SpellTemplates.Clear();
        LoadedPhaseAnchors.Clear();
        ScaledLaneTemplates.Clear();
        MissingCategories.Clear();
        MissingAnchors.Clear();

        BanHeroNames.Clear();
        HeroToBanVectors.Clear();
        PickHeroNames.Clear();
        HeroToAllyPickVectors.Clear();
        HeroToEnemyPickVectors.Clear();
    }
}

/// <summary>
/// High-performance OpenCV vision engine implementing unified draft classification matching MLBB_Companion_FastAPI.
/// Employs 9-point spatial jitter search, combined masked CLAHE ZNCC + 2D HSV color histograms,
/// Kuhn-Munkres joint maximum-weight bipartite matching, and multi-scale lane evidence assignment.
/// </summary>
public class OpenCvVisionEngine : IVisionEngine
{
    private readonly ILogger<OpenCvVisionEngine> _logger;
    private readonly IHeroRepository? _heroRepository;
    private readonly object _tensorLock = new();
    private readonly object _reloadLock = new();
    private readonly HashSet<string> _knownHeroIds = new(StringComparer.OrdinalIgnoreCase);

    private VisionTensorsSnapshot _snapshot = new();
    private volatile bool _isReloading;
    private int _activeReaders;

    public event Action<DraftScanResult>? OnDraftDetectionUpdated;

    public bool IsReady => _snapshot.IsComplete && !_isReloading;
    public bool IsReloading => _isReloading;
    public string AnchorSource => _snapshot.AnchorSource;
    public string AssetsRoot => _snapshot.AssetsRoot;
    public string? AnchorConfigPath => _snapshot.AnchorConfigPath;
    public IReadOnlyList<string> MissingCategories => _snapshot.MissingCategories;
    public IReadOnlyList<string> MissingAnchors => _snapshot.MissingAnchors;
    public long EstimatedNativeMemoryBytes => _snapshot.EstimatedNativeMemoryBytes;

    public void ClearSlotCache()
    {
        // Stateless cycle execution; no slot caching to avoid stale picks
    }

    public OpenCvVisionEngine(ILogger<OpenCvVisionEngine> logger, IHeroRepository? heroRepository = null)
    {
        _logger = logger;
        _heroRepository = heroRepository;
    }

    private void EnsureKnownHeroIds()
    {
        if (_knownHeroIds.Count > 0) return;

        if (_heroRepository != null)
        {
            try
            {
                var allHeroes = _heroRepository.GetAllHeroes();
                foreach (var h in allHeroes)
                {
                    if (!string.IsNullOrWhiteSpace(h.Id))
                        _knownHeroIds.Add(h.Id.ToLowerInvariant());
                    if (!string.IsNullOrWhiteSpace(h.Name))
                        _knownHeroIds.Add(h.Name.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", ""));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load heroes from IHeroRepository during EnsureKnownHeroIds.");
            }
        }

        string[] hardcodedIds =
        [
            "miya", "balmond", "saber", "alice", "nana", "tigreal", "alucard", "karina", "akai", "franco",
            "bane", "bruno", "clint", "rafaela", "eudora", "zilong", "fanny", "layla", "minotaur", "lolita",
            "hayabusa", "freya", "gord", "natalia", "kagura", "chou", "sun", "alpha", "ruby", "cyclops",
            "moskov", "johnson", "hilda", "aurora", "lapu_lapu", "vexana", "roger", "karrie", "gatotkaca",
            "harley", "irithiel", "grock", "argus", "odette", "lancelot", "diggie", "helcurt", "lesley",
            "jawhead", "angela", "gusion", "valir", "martis", "uranus", "hanabi", "change", "kaja",
            "selena", "aldous", "claude", "vale", "leomord", "lunox", "hanzo", "belerick", "kimmy",
            "thamuz", "harith", "minsitthar", "kadita", "faramis", "badang", "khufra", "granger", "guinevere",
            "esmeralda", "terizla", "x_borg", "ling", "dyrroth", "lylia", "baxia", "masha", "wanwan",
            "silvanna", "cecilion", "carmilla", "atlas", "popol_and_kupa", "yu_zhong", "luo_yi", "benedetta",
            "khaleed", "barats", "brody", "yve", "mathilda", "paquito", "gloo", "beatrix", "phoveus",
            "natan", "aulus", "aamon", "floryn", "valentina", "edith", "yin", "melissa", "xavier",
            "julian", "fredrinn", "joy", "novaria", "arlott", "ixia", "nolan", "cici", "chip", "zhuxin",
            "suyou", "lucas", "marcel", "zetian", "estes"
        ];

        foreach (var id in hardcodedIds)
        {
            _knownHeroIds.Add(id);
        }
    }

    private string ResolveHeroKey(string name)
    {
        string raw = name.Trim().ToLowerInvariant();

        if (raw.StartsWith("ally_")) raw = raw[5..];
        if (raw.StartsWith("enemy_")) raw = raw[6..];

        raw = raw.Replace(" ", "_").Replace("-", "_");

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "chang'e", "change" },
            { "change", "change" },
            { "lapulapu", "lapu_lapu" },
            { "yuzhong", "yu_zhong" },
            { "xborg", "x_borg" },
            { "popol", "popol_and_kupa" },
            { "popolandkupa", "popol_and_kupa" },
            { "irithel", "irithiel" },
            { "irithiel", "irithel" }
        };

        if (aliases.TryGetValue(raw, out string? mapped))
            return mapped;

        string normalized = raw.Replace("_", "");
        if (_knownHeroIds.Contains(raw)) return raw;
        if (_knownHeroIds.Contains(normalized)) return normalized;

        return raw;
    }

    private string ResolveAssetsDirectory()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Assets"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets"),
            WorkspacePathResolver.ResolvePath("Assets")
        ];

        foreach (var p in candidates)
        {
            if (Directory.Exists(p) && Directory.Exists(Path.Combine(p, "heroes")))
            {
                return Path.GetFullPath(p);
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "Assets");
    }

    private VisionTensorsSnapshot BuildSnapshot()
    {
        var sw = Stopwatch.StartNew();
        var snapshot = new VisionTensorsSnapshot();

        EnsureKnownHeroIds();

        string assetsRoot = ResolveAssetsDirectory();
        snapshot.AssetsRoot = assetsRoot;

        snapshot.CircularBanMask = CreateCircularMask(VisionFeatureExtractor.BanSize, VisionFeatureExtractor.BanSize);

        using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));

        // 1. Load Ban Templates (Assets/heroes + Assets/training/bans)
        var banHeroTemplates = new Dictionary<string, List<HeroFeatureVector>>(StringComparer.OrdinalIgnoreCase);

        void ExtractAndAddBan(string file, string heroKey)
        {
            using var raw = new Mat(file, ImreadModes.Color);
            if (raw.Empty()) return;

            using var resized = new Mat();
            Cv2.Resize(raw, resized, new Size(VisionFeatureExtractor.BanSize, VisionFeatureExtractor.BanSize));

            using var gray = new Mat();
            Cv2.CvtColor(resized, gray, ColorConversionCodes.BGR2GRAY);
            using var grayClahe = new Mat();
            clahe.Apply(gray, grayClahe);

            float[] zncc = VisionFeatureExtractor.ExtractBanZnccVector(grayClahe, VisionFeatureExtractor.BanMaskIndices);
            float[] hsv = VisionFeatureExtractor.ExtractBanHsvHistogram(resized, VisionFeatureExtractor.BanMaskIndices);

            if (!banHeroTemplates.TryGetValue(heroKey, out var vecList))
            {
                vecList = [];
                banHeroTemplates[heroKey] = vecList;
            }
            vecList.Add(new HeroFeatureVector { HeroName = heroKey, Zncc = zncc, Hsv = hsv });

            // Legacy Mat template storage (max 5 per hero)
            if (!snapshot.BanTemplates.TryGetValue(heroKey, out var matList))
            {
                matList = [];
                snapshot.BanTemplates[heroKey] = matList;
            }
            if (matList.Count < 5)
            {
                var enhancedMat = new Mat();
                grayClahe.CopyTo(enhancedMat);
                matList.Add(enhancedMat);
            }
        }

        string heroesDir = Path.Combine(assetsRoot, "heroes");
        if (Directory.Exists(heroesDir))
        {
            foreach (var file in Directory.GetFiles(heroesDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (name.StartsWith("question", StringComparison.OrdinalIgnoreCase)) continue;
                string heroKey = ResolveHeroKey(name);
                ExtractAndAddBan(file, heroKey);
            }
        }

        string trainingBansDir = Path.Combine(assetsRoot, "training", "bans");
        if (Directory.Exists(trainingBansDir))
        {
            foreach (var file in Directory.GetFiles(trainingBansDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                string heroKey = ResolveHeroKey(name);
                ExtractAndAddBan(file, heroKey);
            }
        }

        snapshot.BanHeroNames.AddRange(banHeroTemplates.Keys.OrderBy(k => k));
        for (int i = 0; i < snapshot.BanHeroNames.Count; i++)
        {
            snapshot.HeroToBanVectors[i] = banHeroTemplates[snapshot.BanHeroNames[i]];
        }

        // Empty question mark ban templates
        string tplDir = Path.Combine(assetsRoot, "templates");
        string pAllyEmptyBan = Path.Combine(tplDir, "empty_ban_ally.png");
        string pEnemyEmptyBan = Path.Combine(tplDir, "empty_ban_enemy.png");
        if (File.Exists(pAllyEmptyBan))
        {
            using var raw = new Mat(pAllyEmptyBan, ImreadModes.Color);
            if (!raw.Empty())
            {
                snapshot.EmptyBanAlly = new Mat();
                Cv2.Resize(raw, snapshot.EmptyBanAlly, new Size(80, 80));
            }
        }
        if (File.Exists(pEnemyEmptyBan))
        {
            using var raw = new Mat(pEnemyEmptyBan, ImreadModes.Color);
            if (!raw.Empty())
            {
                snapshot.EmptyBanEnemy = new Mat();
                Cv2.Resize(raw, snapshot.EmptyBanEnemy, new Size(80, 80));
            }
        }

        // 2. Load Rect Pick Templates (Assets/training/ally_picks, Assets/training/enemy_picks, Assets/rect)
        var allyPickTemplates = new Dictionary<string, List<HeroFeatureVector>>(StringComparer.OrdinalIgnoreCase);
        var enemyPickTemplates = new Dictionary<string, List<HeroFeatureVector>>(StringComparer.OrdinalIgnoreCase);

        HeroFeatureVector ExtractWindowFeatures(Mat winImg, string heroKey)
        {
            using var winResized = new Mat();
            if (winImg.Rows != VisionFeatureExtractor.PickWindowHeight || winImg.Cols != VisionFeatureExtractor.PickWindowWidth)
            {
                Cv2.Resize(winImg, winResized, new Size(VisionFeatureExtractor.PickWindowWidth, VisionFeatureExtractor.PickWindowHeight));
            }
            else
            {
                winImg.CopyTo(winResized);
            }

            using var gray = new Mat();
            Cv2.CvtColor(winResized, gray, ColorConversionCodes.BGR2GRAY);
            using var grayClahe = new Mat();
            clahe.Apply(gray, grayClahe);

            float[] zncc = VisionFeatureExtractor.ExtractPickZnccVector(grayClahe);
            float[] hsv = VisionFeatureExtractor.ExtractPickHsvHistogram(winResized);

            return new HeroFeatureVector { HeroName = heroKey, Zncc = zncc, Hsv = hsv };
        }

        // Ally Training Picks
        string allyPicksDir = Path.Combine(assetsRoot, "training", "ally_picks");
        if (Directory.Exists(allyPicksDir))
        {
            foreach (var file in Directory.GetFiles(allyPicksDir, "*.png"))
            {
                string heroKey = ResolveHeroKey(Path.GetFileNameWithoutExtension(file));
                using var raw = new Mat(file, ImreadModes.Color);
                if (raw.Empty()) continue;

                using var resized = new Mat();
                Cv2.Resize(raw, resized, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));
                using var win = new Mat(resized, new Rect(55, 0, 139, 110));
                var vec = ExtractWindowFeatures(win, heroKey);

                if (!allyPickTemplates.TryGetValue(heroKey, out var list))
                {
                    list = [];
                    allyPickTemplates[heroKey] = list;
                }
                list.Add(vec);
            }
        }

        // Enemy Training Picks
        string enemyPicksDir = Path.Combine(assetsRoot, "training", "enemy_picks");
        if (Directory.Exists(enemyPicksDir))
        {
            foreach (var file in Directory.GetFiles(enemyPicksDir, "*.png"))
            {
                string heroKey = ResolveHeroKey(Path.GetFileNameWithoutExtension(file));
                using var raw = new Mat(file, ImreadModes.Color);
                if (raw.Empty()) continue;

                using var resized = new Mat();
                Cv2.Resize(raw, resized, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));
                using var win = new Mat(resized, new Rect(16, 0, 139, 110));
                var vec = ExtractWindowFeatures(win, heroKey);

                if (!enemyPickTemplates.TryGetValue(heroKey, out var list))
                {
                    list = [];
                    enemyPickTemplates[heroKey] = list;
                }
                list.Add(vec);
            }
        }

        // Master Rect Images (Both Ally and Horizontally Mirrored Enemy)
        string rectDir = Path.Combine(assetsRoot, "rect");
        if (Directory.Exists(rectDir))
        {
            foreach (var file in Directory.GetFiles(rectDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (name.StartsWith("question", StringComparison.OrdinalIgnoreCase)) continue;
                string heroKey = ResolveHeroKey(name);

                using var raw = new Mat(file, ImreadModes.Color);
                if (raw.Empty()) continue;

                using var resized = new Mat();
                Cv2.Resize(raw, resized, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));

                // Ally Window: [x: 55..194, y: 0..110]
                using var allyWin = new Mat(resized, new Rect(55, 0, 139, 110));
                var allyVec = ExtractWindowFeatures(allyWin, heroKey);
                if (!allyPickTemplates.TryGetValue(heroKey, out var aList))
                {
                    aList = [];
                    allyPickTemplates[heroKey] = aList;
                }
                aList.Add(allyVec);

                // Enemy Mirrored Window: flip card horizontally, [x: 16..155, y: 0..110]
                using var flipped = new Mat();
                Cv2.Flip(resized, flipped, FlipMode.Y);
                using var enemyWin = new Mat(flipped, new Rect(16, 0, 139, 110));
                var enemyVec = ExtractWindowFeatures(enemyWin, heroKey);
                if (!enemyPickTemplates.TryGetValue(heroKey, out var eList))
                {
                    eList = [];
                    enemyPickTemplates[heroKey] = eList;
                }
                eList.Add(enemyVec);

                // Legacy PickTemplates Mat storage (max 5 per hero)
                if (!snapshot.PickTemplates.TryGetValue(heroKey, out var matList))
                {
                    matList = [];
                    snapshot.PickTemplates[heroKey] = matList;
                }
                if (matList.Count < 5)
                {
                    using var gWin = new Mat();
                    Cv2.CvtColor(allyWin, gWin, ColorConversionCodes.BGR2GRAY);
                    var enh = new Mat();
                    clahe.Apply(gWin, enh);
                    matList.Add(enh);
                }
            }
        }

        var allPickHeroes = allyPickTemplates.Keys.Union(enemyPickTemplates.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
        snapshot.PickHeroNames.AddRange(allPickHeroes);

        for (int i = 0; i < snapshot.PickHeroNames.Count; i++)
        {
            string hero = snapshot.PickHeroNames[i];
            snapshot.HeroToAllyPickVectors[i] = allyPickTemplates.TryGetValue(hero, out var aVecs) ? aVecs : [];
            snapshot.HeroToEnemyPickVectors[i] = enemyPickTemplates.TryGetValue(hero, out var eVecs) ? eVecs : [];
        }

        // Empty Pick Slot Templates
        string pAllyEmptyPick = Path.Combine(tplDir, "empty_pick_slot_ally.png");
        string pAllyEmptyPickDim = Path.Combine(tplDir, "empty_pick_slot_ally_dim.png");
        string pEnemyEmptyPick = Path.Combine(tplDir, "empty_pick_slot_enemy.png");
        if (File.Exists(pAllyEmptyPick))
        {
            using var raw = new Mat(pAllyEmptyPick, ImreadModes.Color);
            if (!raw.Empty())
            {
                snapshot.EmptyPickAlly = new Mat();
                Cv2.Resize(raw, snapshot.EmptyPickAlly, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));
            }
        }
        if (File.Exists(pAllyEmptyPickDim))
        {
            using var raw = new Mat(pAllyEmptyPickDim, ImreadModes.Color);
            if (!raw.Empty())
            {
                snapshot.EmptyPickAllyDim = new Mat();
                Cv2.Resize(raw, snapshot.EmptyPickAllyDim, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));
            }
        }
        if (File.Exists(pEnemyEmptyPick))
        {
            using var raw = new Mat(pEnemyEmptyPick, ImreadModes.Color);
            if (!raw.Empty())
            {
                snapshot.EmptyPickEnemy = new Mat();
                Cv2.Resize(raw, snapshot.EmptyPickEnemy, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));
            }
        }

        // 3. Load Lane Badges & Multi-Scale Templates (Assets/lanes)
        string lanesDir = Path.Combine(assetsRoot, "lanes");
        string[] laneKeys = ["exp", "gold", "jungle", "mid", "roam"];
        double[] laneScales = [0.75, 0.85, 1.0, 1.15];

        if (Directory.Exists(lanesDir))
        {
            foreach (var laneKey in laneKeys)
            {
                string p = Path.Combine(lanesDir, $"{laneKey}.png");
                if (File.Exists(p))
                {
                    using var raw = new Mat(p, ImreadModes.Color);
                    if (!raw.Empty())
                    {
                        var standard50 = new Mat();
                        Cv2.Resize(raw, standard50, new Size(50, 50));
                        snapshot.LaneTemplates[laneKey.ToUpperInvariant()] = standard50;

                        foreach (var s in laneScales)
                        {
                            int tw = (int)Math.Round(raw.Width * s);
                            int th = (int)Math.Round(raw.Height * s);
                            var scaled = new Mat();
                            Cv2.Resize(raw, scaled, new Size(tw, th));
                            snapshot.ScaledLaneTemplates[(laneKey, s)] = scaled;
                        }
                    }
                }
            }
        }

        // 4. Load Battle Spell Badges (Assets/spells)
        string spellsDir = Path.Combine(assetsRoot, "spells");
        if (Directory.Exists(spellsDir))
        {
            foreach (var file in Directory.GetFiles(spellsDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                using var raw = new Mat(file, ImreadModes.Color);
                if (!raw.Empty())
                {
                    var resized = new Mat();
                    Cv2.Resize(raw, resized, new Size(50, 50));
                    snapshot.SpellTemplates[name] = resized;
                }
            }
        }

        // 5. Load Phase Anchors
        string jsonPath = Path.Combine(assetsRoot, "templates", "Phase Template", "phase_anchors.json");
        snapshot.AnchorConfigPath = jsonPath;
        List<PhaseAnchorEntity> anchorsToLoad = [];
        if (File.Exists(jsonPath))
        {
            try
            {
                string json = File.ReadAllText(jsonPath);
                var list = JsonSerializer.Deserialize<List<PhaseAnchorEntity>>(json);
                if (list != null && list.Count > 0)
                {
                    anchorsToLoad = list.Where(a => a.Enabled).ToList();
                    snapshot.AnchorSource = "json_file";
                }
                else
                {
                    snapshot.MissingCategories.Add("anchors_config_empty");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed loading phase_anchors.json in VisionEngine");
                snapshot.MissingCategories.Add("anchors_config_corrupt");
            }
        }

        if (anchorsToLoad.Count == 0)
        {
            anchorsToLoad = GetEmbeddedDefaultAnchors();
            snapshot.AnchorSource = "embedded_defaults";
        }

        string anchorDir = Path.Combine(assetsRoot, "templates", "Phase Template", "anchors");
        if (!Directory.Exists(anchorDir)) anchorDir = Path.Combine(assetsRoot, "templates");

        if (Directory.Exists(anchorDir))
        {
            foreach (var cfg in anchorsToLoad)
            {
                string filePath = Path.Combine(anchorDir, cfg.Filename);
                if (!File.Exists(filePath))
                {
                    var found = Directory.GetFiles(anchorDir, cfg.Filename, SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null) filePath = found;
                }

                if (File.Exists(filePath))
                {
                    using var raw = new Mat(filePath, ImreadModes.Color);
                    if (!raw.Empty())
                    {
                        using var gray = new Mat();
                        Cv2.CvtColor(raw, gray, ColorConversionCodes.BGR2GRAY);
                        var grayClahe = new Mat();
                        clahe.Apply(gray, grayClahe);
                        snapshot.LoadedPhaseAnchors.Add((cfg, grayClahe));
                    }
                }
                else
                {
                    snapshot.MissingAnchors.Add(cfg.Filename);
                }
            }
        }

        // Compute Memory Usage
        long totalMatBytes = 0;
        if (snapshot.CircularBanMask != null) totalMatBytes += ComputeMatBytes(snapshot.CircularBanMask);
        if (snapshot.EmptyBanAlly != null) totalMatBytes += ComputeMatBytes(snapshot.EmptyBanAlly);
        if (snapshot.EmptyBanEnemy != null) totalMatBytes += ComputeMatBytes(snapshot.EmptyBanEnemy);
        if (snapshot.EmptyPickAlly != null) totalMatBytes += ComputeMatBytes(snapshot.EmptyPickAlly);
        if (snapshot.EmptyPickAllyDim != null) totalMatBytes += ComputeMatBytes(snapshot.EmptyPickAllyDim);
        if (snapshot.EmptyPickEnemy != null) totalMatBytes += ComputeMatBytes(snapshot.EmptyPickEnemy);

        foreach (var l in snapshot.BanTemplates.Values)
        {
            foreach (var m in l) totalMatBytes += ComputeMatBytes(m);
        }
        foreach (var l in snapshot.PickTemplates.Values)
        {
            foreach (var m in l) totalMatBytes += ComputeMatBytes(m);
        }
        foreach (var m in snapshot.LaneTemplates.Values) totalMatBytes += ComputeMatBytes(m);
        foreach (var m in snapshot.ScaledLaneTemplates.Values) totalMatBytes += ComputeMatBytes(m);
        foreach (var m in snapshot.SpellTemplates.Values) totalMatBytes += ComputeMatBytes(m);
        foreach (var (_, m) in snapshot.LoadedPhaseAnchors) totalMatBytes += ComputeMatBytes(m);
        snapshot.EstimatedNativeMemoryBytes = totalMatBytes;

        if (snapshot.BanHeroNames.Count == 0) snapshot.MissingCategories.Add("bans");
        if (snapshot.PickHeroNames.Count == 0) snapshot.MissingCategories.Add("picks");
        if (snapshot.LaneTemplates.Count == 0) snapshot.MissingCategories.Add("lanes");
        if (snapshot.SpellTemplates.Count == 0) snapshot.MissingCategories.Add("spells");
        if (snapshot.LoadedPhaseAnchors.Count == 0) snapshot.MissingCategories.Add("anchors");

        snapshot.IsInitialized = true;
        sw.Stop();

        _logger.LogInformation("Vision tensors loaded in {ElapsedMs}ms ({MemoryMb:F1}MB RAM). Unique Ban Heroes: {BanHeroes}, Unique Pick Heroes: {PickHeroes}, Lanes: {LaneCount}, Spells: {SpellCount}",
            sw.ElapsedMilliseconds, (double)totalMatBytes / (1024 * 1024), snapshot.BanHeroNames.Count, snapshot.PickHeroNames.Count, snapshot.LaneTemplates.Count, snapshot.SpellTemplates.Count);

        return snapshot;
    }

    public void InitializeTensors()
    {
        if (_snapshot.IsInitialized) return;

        lock (_tensorLock)
        {
            if (_snapshot.IsInitialized) return;
            _snapshot = BuildSnapshot();
        }
    }

    public Dictionary<string, int> GetTemplateCounts()
    {
        InitializeTensors();
        var snapshot = _snapshot;
        return new Dictionary<string, int>
        {
            ["picks"] = snapshot.PickTemplates.Values.Sum(l => l.Count),
            ["bans"] = snapshot.BanTemplates.Values.Sum(l => l.Count),
            ["lanes"] = snapshot.LaneTemplates.Count,
            ["spells"] = snapshot.SpellTemplates.Count,
            ["anchors"] = snapshot.LoadedPhaseAnchors.Count,
            ["unique_heroes"] = snapshot.PickHeroNames.Count,
            ["total_hero_templates"] = snapshot.PickTemplates.Values.Sum(l => l.Count) + snapshot.BanTemplates.Values.Sum(l => l.Count),
            ["estimated_mat_memory_mb"] = (int)(snapshot.EstimatedNativeMemoryBytes / (1024 * 1024))
        };
    }

    public void ReloadTemplates()
    {
        lock (_reloadLock)
        {
            _isReloading = true;
            try
            {
                var newSnapshot = BuildSnapshot();
                VisionTensorsSnapshot oldSnapshot;
                lock (_tensorLock)
                {
                    oldSnapshot = _snapshot;
                    _snapshot = newSnapshot;
                }

                var sw = Stopwatch.StartNew();
                while (Volatile.Read(ref _activeReaders) > 0 && sw.ElapsedMilliseconds < 5000)
                {
                    Thread.Sleep(10);
                }

                oldSnapshot.Dispose();
            }
            finally
            {
                _isReloading = false;
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_tensorLock)
            {
                _snapshot.Dispose();
            }
        }
    }

    public (bool isValid, bool isTransient, string reason, Dictionary<string, object> metrics) InspectFrameIntegrity(CapturedFrame frame)
    {
        var metrics = new Dictionary<string, object>();
        if (frame == null || (frame.Buffer == null && frame.NativeHandle == null))
        {
            return (false, false, "Frame buffer is null or empty.", metrics);
        }

        using var mat = ToMat(frame);
        if (mat == null || mat.Empty())
        {
            return (false, false, "Unable to decode frame buffer into OpenCV Mat.", metrics);
        }

        Cv2.MeanStdDev(mat, out var meanScalar, out var stdScalar);
        double meanIntensity = (meanScalar.Val0 + meanScalar.Val1 + meanScalar.Val2) / 3.0;
        double stdDev = (stdScalar.Val0 + stdScalar.Val1 + stdScalar.Val2) / 3.0;

        metrics["mean"] = Math.Round(meanIntensity, 2);
        metrics["std"] = Math.Round(stdDev, 2);

        if (meanIntensity < 10.0 && stdDev < 5.0)
        {
            return (false, true, "Black frame detected (loading screen or fade transition).", metrics);
        }

        if (meanIntensity > 245.0 && stdDev < 5.0)
        {
            return (false, true, "White frame detected (flash or transition).", metrics);
        }

        return (true, false, "Frame passed integrity validation.", metrics);
    }

    public (string phase, string? subPhase, double confidence, string details) DetectPhase(CapturedFrame frame)
    {
        InitializeTensors();
        Interlocked.Increment(ref _activeReaders);
        var snapshot = _snapshot;
        try
        {
            if (snapshot.LoadedPhaseAnchors.Count == 0)
            {
                return (GamePhase.Standby, null, 0.0, "No phase anchors loaded.");
            }

            using var mat = ToMat(frame);
            if (mat == null || mat.Empty())
            {
                return (GamePhase.Standby, null, 0.0, "Frame could not be converted to image buffer.");
            }

            using var fullGray = new Mat();
            Cv2.CvtColor(mat, fullGray, ColorConversionCodes.BGR2GRAY);

            using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));
            var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var (cfg, tpl) in snapshot.LoadedPhaseAnchors)
            {
                var (rx1, ry1, rx2, ry2) = TransformAnchorCoordinates(mat.Width, mat.Height, cfg.X1, cfg.Y1, cfg.X2, cfg.Y2);

                using var roiGray = new Mat(fullGray, new Rect(rx1, ry1, rx2 - rx1, ry2 - ry1));
                if (roiGray.Empty()) continue;

                using var roiClahe = new Mat();
                clahe.Apply(roiGray, roiClahe);

                using var evalCrop = (roiClahe.Width < tpl.Width || roiClahe.Height < tpl.Height)
                    ? roiClahe.Resize(new Size(Math.Max(roiClahe.Width, tpl.Width), Math.Max(roiClahe.Height, tpl.Height)))
                    : roiClahe.Clone();

                using var res = new Mat();
                Cv2.MatchTemplate(evalCrop, tpl, res, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);

                if (maxVal >= cfg.Threshold)
                {
                    if (!scores.TryGetValue(cfg.Phase, out double existing) || maxVal > existing)
                    {
                        scores[cfg.Phase] = maxVal;
                    }
                }
            }

            string detectedPhase = GamePhase.Standby;
            double bestScore = 0.0;

            if (scores.Count > 0)
            {
                double maxScore = scores.Values.Max();
                string[] priority = [
                    GamePhase.DraftPick,
                    "In Game ScoreBoard",
                    "Match Start",
                    "Matchmaking",
                    GamePhase.InGame,
                    GamePhase.Lobby,
                    GamePhase.Homepage
                ];

                const double epsilon = 0.05;
                var topCandidates = scores
                    .Where(kvp => kvp.Value >= maxScore - epsilon)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

                foreach (var p in priority)
                {
                    if (topCandidates.TryGetValue(p, out double s))
                    {
                        detectedPhase = p;
                        bestScore = s;
                        break;
                    }
                }

                if (detectedPhase == GamePhase.Standby)
                {
                    var highest = scores.OrderByDescending(kvp => kvp.Value).First();
                    detectedPhase = highest.Key;
                    bestScore = highest.Value;
                }
            }

            if (detectedPhase == GamePhase.DraftPick)
            {
                var (subPhase, redRatio, reason) = DetectDraftSubPhase(mat);
                return (GamePhase.DraftPick, subPhase, Math.Round(bestScore, 2), $"Draft Pick confirmed (NCC={bestScore:F3}, {reason})");
            }

            if (bestScore > 0.0)
            {
                return (detectedPhase, null, Math.Round(bestScore, 2), $"{detectedPhase} confirmed (NCC={bestScore:F3})");
            }

            return (GamePhase.Standby, null, 0.0, "No phase anchor matched.");
        }
        finally
        {
            Interlocked.Decrement(ref _activeReaders);
        }
    }

    private static (string subPhase, double redRatio, string reason) DetectDraftSubPhase(Mat mat)
    {
        int y1 = 0;
        int y2 = (int)Math.Round(mat.Height * 0.09);
        int x1 = (int)Math.Round(mat.Width * 0.35);
        int x2 = (int)Math.Round(mat.Width * 0.65);

        using var headerCrop = new Mat(mat, new Rect(x1, y1, x2 - x1, y2 - y1));
        if (headerCrop.Empty()) return ("Pick Phase", 0.0, "Empty header crop");

        using var hsv = new Mat();
        Cv2.CvtColor(headerCrop, hsv, ColorConversionCodes.BGR2HSV);

        using var mask1 = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 70, 70), new Scalar(15, 255, 255), mask1);

        using var mask2 = new Mat();
        Cv2.InRange(hsv, new Scalar(165, 70, 70), new Scalar(180, 255, 255), mask2);

        using var combined = new Mat();
        Cv2.BitwiseOr(mask1, mask2, combined);

        int redPixels = Cv2.CountNonZero(combined);
        double redRatio = (double)redPixels / (headerCrop.Width * headerCrop.Height);

        string subPhase = redRatio >= 0.035 ? "Ban Phase" : "Pick Phase";
        string reason = $"header_red_ratio={redRatio:P2}";
        return (subPhase, redRatio, reason);
    }

    public (List<SlotMatch> matches, double extractionMs, double inferenceMs) MatchBansWithTimings(
        CapturedFrame frame,
        RoiConfiguration config,
        double threshold = 0.45,
        int topK = 5,
        bool includeThumbnails = false)
    {
        InitializeTensors();
        Interlocked.Increment(ref _activeReaders);
        var snapshot = _snapshot;
        try
        {
            var swTotal = Stopwatch.StartNew();
            using var mat = ToMat(frame);
            if (mat == null || mat.Empty())
            {
                var emptyList = Enumerable.Range(0, 10).Select(i =>
                    CreateEmptySlot(i, i < 5 ? "ally" : "enemy", "round", "FRAME_DECODE_FAILED")).ToList();
                return (emptyList, 0.0, 0.0);
            }

            var matches = new List<SlotMatch>();
            var takenBansSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));

            float[] zCandBuf = new float[64];
            float[] hCandBuf = new float[64];

            for (int i = 0; i < 10; i++)
            {
                string side = i < 5 ? "ally" : "enemy";
                string roiKey = (side == "ally")
                    ? $"ally_ban_{i}"
                    : (config.Rois.ContainsKey($"enemy_ban_{i - 5}") ? $"enemy_ban_{i - 5}" : $"enemy_ban_{i}");
                var roiRect = ExtractRoiRect(mat.Width, mat.Height, config, roiKey);
                using var crop = CropSafe(mat, roiRect);

                if (crop == null || crop.Empty())
                {
                    matches.Add(CreateEmptySlot(i, side, "round", "DARK_OR_EMPTY_SLOT"));
                    continue;
                }

                Cv2.MeanStdDev(crop, out var meanScalar, out var stdScalar);
                double mean = (meanScalar.Val0 + meanScalar.Val1 + meanScalar.Val2) / 3.0;
                double std = (stdScalar.Val0 + stdScalar.Val1 + stdScalar.Val2) / 3.0;

                if (mean < 12.0 || std < 8.0)
                {
                    matches.Add(CreateEmptySlot(i, side, "round", "DARK_OR_EMPTY_SLOT"));
                    continue;
                }

                using var crop80 = new Mat();
                Cv2.Resize(crop, crop80, new Size(VisionFeatureExtractor.BanSize, VisionFeatureExtractor.BanSize));

                // 1. Check Empty Question Mark Ban Template
                double eSim = 0.0;
                Mat? emptyTpl = (side == "ally") ? snapshot.EmptyBanAlly : snapshot.EmptyBanEnemy;
                if (emptyTpl != null && !emptyTpl.Empty())
                {
                    using var eRes = new Mat();
                    Cv2.MatchTemplate(crop80, emptyTpl, eRes, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(eRes, out _, out double maxVal, out _, out _);
                    eSim = maxVal;
                }

                if (eSim >= 0.62)
                {
                    var emptyMatch = CreateEmptySlot(i, side, "round", "EMPTY_QUESTION_SLOT");
                    emptyMatch.EmptySimilarity = Math.Round(eSim, 4);
                    matches.Add(emptyMatch);
                    continue;
                }

                if (snapshot.BanHeroNames.Count == 0)
                {
                    matches.Add(CreateEmptySlot(i, side, "round", "NO_TEMPLATES"));
                    continue;
                }

                // 2. 9-Point Spatial Jitter Search
                double? bestQuality = null;
                float[]? bestFused = null;
                float[]? bestZncc = null;
                float[]? bestHsv = null;

                int nHeroes = snapshot.BanHeroNames.Count;
                float[] fScores = new float[nHeroes];
                float[] zCons = new float[nHeroes];
                float[] hCons = new float[nHeroes];

                foreach (var (dx, dy) in VisionFeatureExtractor.JitterOffsets)
                {
                    using var shifted = new Mat();
                    if (dx != 0 || dy != 0)
                    {
                        using var m = new Mat(2, 3, MatType.CV_32FC1);
                        m.Set(0, 0, 1.0f); m.Set(0, 1, 0.0f); m.Set(0, 2, (float)dx);
                        m.Set(1, 0, 0.0f); m.Set(1, 1, 1.0f); m.Set(1, 2, (float)dy);
                        Cv2.WarpAffine(crop80, shifted, m, new Size(80, 80), InterpolationFlags.Linear, BorderTypes.Reflect);
                    }
                    else
                    {
                        crop80.CopyTo(shifted);
                    }

                    using var gray = new Mat();
                    Cv2.CvtColor(shifted, gray, ColorConversionCodes.BGR2GRAY);
                    using var grayClahe = new Mat();
                    clahe.Apply(gray, grayClahe);

                    float[] qZncc = VisionFeatureExtractor.ExtractBanZnccVector(grayClahe, VisionFeatureExtractor.BanMaskIndices);
                    float[] qHsv = VisionFeatureExtractor.ExtractBanHsvHistogram(shifted, VisionFeatureExtractor.BanMaskIndices);

                    for (int hIdx = 0; hIdx < nHeroes; hIdx++)
                    {
                        var tplList = snapshot.HeroToBanVectors[hIdx];
                        int tCount = tplList.Count;

                        if (tCount > zCandBuf.Length)
                        {
                            Array.Resize(ref zCandBuf, tCount * 2);
                            Array.Resize(ref hCandBuf, tCount * 2);
                        }

                        var zCand = zCandBuf.AsSpan(0, tCount);
                        var hCand = hCandBuf.AsSpan(0, tCount);

                        for (int t = 0; t < tCount; t++)
                        {
                            zCand[t] = VisionFeatureExtractor.Dot(tplList[t].Zncc, qZncc) / VisionFeatureExtractor.BanMaskPixelCount;
                            hCand[t] = VisionFeatureExtractor.Dot(tplList[t].Hsv, qHsv);
                        }

                        var (fused, zBest, hBest) = VisionFeatureExtractor.ConsolidateHeroScore(zCand, hCand, (float)threshold);
                        fScores[hIdx] = fused;
                        zCons[hIdx] = zBest;
                        hCons[hIdx] = hBest;
                    }

                    // Candidate quality evaluates top-1 score and margin
                    var sortedCopy = fScores.ToArray();
                    Array.Sort(sortedCopy);
                    Array.Reverse(sortedCopy);

                    double top1 = sortedCopy[0];
                    double top2 = sortedCopy.Length > 1 ? sortedCopy[1] : 0.0;
                    double margin = Math.Max(0.0, top1 - top2);
                    double centerBias = (dx == 0 && dy == 0) ? 0.005 : 0.0;
                    double quality = top1 + 0.35 * margin + centerBias;

                    if (bestQuality == null || quality > bestQuality.Value)
                    {
                        bestQuality = quality;
                        bestFused = fScores.ToArray();
                        bestZncc = zCons.ToArray();
                        bestHsv = hCons.ToArray();
                    }
                }

                if (bestFused == null)
                {
                    matches.Add(CreateEmptySlot(i, side, "round", "NO_SIGNAL"));
                    continue;
                }

                // Rank Candidates
                var indices = Enumerable.Range(0, nHeroes).ToArray();
                Array.Sort(indices, (a, b) => bestFused[b].CompareTo(bestFused[a]));

                var cands = new List<CandidateMatch>();
                for (int k = 0; k < Math.Min(topK, indices.Length); k++)
                {
                    int idx = indices[k];
                    cands.Add(new CandidateMatch
                    {
                        Hero = snapshot.BanHeroNames[idx],
                        Score = Math.Round(bestFused[idx], 4),
                        Shape = "round"
                    });
                }

                double topScore = cands.Count > 0 ? cands[0].Score : 0.0;
                double topMargin = cands.Count > 1 ? (cands[0].Score - cands[1].Score) : 1.0;
                double minMargin = GetThresholdDouble(config.Thresholds, "min_top_margin", 0.025);

                string? matchedHero = null;
                string? rej = null;

                if (cands.Count == 0 || topScore < threshold)
                {
                    rej = "LOW_CONFIDENCE";
                }
                else if (cands.Count > 1 && topMargin < minMargin && topScore < 0.55)
                {
                    rej = "AMBIGUOUS_MARGIN";
                }
                else if (takenBansSet.Contains(cands[0].Hero))
                {
                    bool foundAlt = false;
                    for (int altIdx = 1; altIdx < cands.Count; altIdx++)
                    {
                        double altMargin = altIdx < cands.Count - 1 ? (cands[altIdx].Score - cands[altIdx + 1].Score) : 1.0;
                        if (!takenBansSet.Contains(cands[altIdx].Hero) && cands[altIdx].Score >= threshold)
                        {
                            if (altIdx < cands.Count - 1 && altMargin < minMargin && cands[altIdx].Score < 0.55) continue;
                            matchedHero = cands[altIdx].Hero;
                            topScore = cands[altIdx].Score;
                            takenBansSet.Add(matchedHero);
                            foundAlt = true;
                            break;
                        }
                    }
                    if (!foundAlt) rej = "DUPLICATE_BAN_PREVENTED";
                }
                else
                {
                    matchedHero = cands[0].Hero;
                    takenBansSet.Add(matchedHero);
                }

                var slotMatch = new SlotMatch
                {
                    Slot = i,
                    Side = side,
                    Hero = matchedHero ?? "Empty",
                    MatchedHero = matchedHero ?? "Empty",
                    Confidence = matchedHero != null ? Math.Round(topScore, 3) : (cands.Count > 0 ? Math.Round(cands[0].Score, 3) : 0.0),
                    Shape = "round",
                    RejectionReason = rej,
                    EmptySimilarity = Math.Round(eSim, 4),
                    TopKCandidates = cands,
                    CropThumb = includeThumbnails ? MatToBase64(crop) : null
                };

                matches.Add(slotMatch);
            }

            swTotal.Stop();
            double totalMs = Math.Round(swTotal.Elapsed.TotalMilliseconds, 2);
            return (matches, totalMs * 0.15, totalMs * 0.85);
        }
        finally
        {
            Interlocked.Decrement(ref _activeReaders);
        }
    }

    public List<SlotMatch> MatchBans(
        CapturedFrame frame,
        RoiConfiguration config,
        double threshold = 0.45,
        int topK = 5,
        bool includeThumbnails = false)
    {
        return MatchBansWithTimings(frame, config, threshold, topK, includeThumbnails).matches;
    }

    public (List<SlotMatch> allyPicks, List<SlotMatch> enemyPicks, PipelineTimings timings) MatchPicksJoint(
        CapturedFrame frame,
        RoiConfiguration config,
        HashSet<string>? takenBans = null,
        double threshold = 0.50,
        int topK = 5,
        bool includeThumbnails = false)
    {
        InitializeTensors();
        Interlocked.Increment(ref _activeReaders);
        var snapshot = _snapshot;
        try
        {
            var sw = Stopwatch.StartNew();
            var timings = new PipelineTimings();

            using var mat = ToMat(frame);
            if (mat == null || mat.Empty())
            {
                var emptyAlly = Enumerable.Range(0, 5).Select(i => CreateEmptySlot(i, "ally", "rectangle", "FRAME_DECODE_FAILED")).ToList();
                var emptyEnemy = Enumerable.Range(0, 5).Select(i => CreateEmptySlot(i, "enemy", "rectangle", "FRAME_DECODE_FAILED")).ToList();
                return (emptyAlly, emptyEnemy, timings);
            }

            var excluded = takenBans != null
                ? new HashSet<string>(takenBans.Select(b => b.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase)
                : [];

            using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));

            // Extract crops
            var allyCrops = new List<Mat?>();
            var enemyCrops = new List<Mat?>();

            for (int i = 0; i < 5; i++)
            {
                var rAlly = ExtractRoiRect(mat.Width, mat.Height, config, $"ally_pick_{i}");
                allyCrops.Add(CropSafe(mat, rAlly));

                var rEnemy = ExtractRoiRect(mat.Width, mat.Height, config, $"enemy_pick_{i}");
                enemyCrops.Add(CropSafe(mat, rEnemy));
            }

            // Compute optimal lane assignments for ally picks
            var optimalLanes = ComputeOptimalLanes(allyCrops, snapshot);

            var allySlots = new List<SlotMatch>();
            var enemySlots = new List<SlotMatch>();
            var slotCandidateLists = new List<List<(string Hero, double Score)>>();

            int nHeroes = snapshot.PickHeroNames.Count;
            float[] zCandBuf = new float[64];
            float[] hCandBuf = new float[64];

            // Score Ally Pick Slots
            var allySw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                var crop = allyCrops[i];
                string assignedLane = (i < optimalLanes.Count && optimalLanes[i] != null) ? optimalLanes[i]! : "Roam";

                if (crop == null || crop.Empty())
                {
                    var s = CreateEmptySlot(i, "ally", "rectangle", "DARK_OR_EMPTY_SLOT");
                    s.DetectedLane = assignedLane;
                    allySlots.Add(s);
                    slotCandidateLists.Add([]);
                    continue;
                }

                Cv2.MeanStdDev(crop, out var meanScalar, out var stdScalar);
                double mean = (meanScalar.Val0 + meanScalar.Val1 + meanScalar.Val2) / 3.0;
                double std = (stdScalar.Val0 + stdScalar.Val1 + stdScalar.Val2) / 3.0;

                if (mean < 12.0 || std < 6.0)
                {
                    var s = CreateEmptySlot(i, "ally", "rectangle", "DARK_OR_EMPTY_SLOT");
                    s.DetectedLane = assignedLane;
                    allySlots.Add(s);
                    slotCandidateLists.Add([]);
                    continue;
                }

                using var card210 = new Mat();
                Cv2.Resize(crop, card210, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));

                // Empty slot template matching
                double emptySim = 0.0;
                if (snapshot.EmptyPickAlly != null)
                {
                    using var res = new Mat();
                    Cv2.MatchTemplate(card210, snapshot.EmptyPickAlly, res, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);
                    emptySim = Math.Max(emptySim, maxVal);
                }
                if (snapshot.EmptyPickAllyDim != null)
                {
                    using var res = new Mat();
                    Cv2.MatchTemplate(card210, snapshot.EmptyPickAllyDim, res, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);
                    emptySim = Math.Max(emptySim, maxVal);
                }

                if (emptySim >= 0.60)
                {
                    var s = CreateEmptySlot(i, "ally", "rectangle", "EMPTY_SLOT");
                    s.EmptySimilarity = Math.Round(emptySim, 4);
                    s.DetectedLane = assignedLane;
                    allySlots.Add(s);
                    slotCandidateLists.Add([]);
                    continue;
                }

                // 9-Point Spatial Jitter Search: Ally focal window [x: 55..194, y: 0..110]
                double? bestQuality = null;
                float[]? bestFused = null;
                float[] fScores = new float[nHeroes];

                foreach (var (dx, dy) in VisionFeatureExtractor.JitterOffsets)
                {
                    int x1 = 55 + dx;
                    int y1 = 0 + dy;
                    int x2 = 194 + dx;
                    int y2 = 110 + dy;

                    if (x1 < 0 || x2 > card210.Cols || y1 < 0 || y2 > card210.Rows) continue;

                    using var win = new Mat(card210, new Rect(x1, y1, 139, 110));
                    Cv2.MeanStdDev(win, out _, out var winStd);
                    if ((winStd.Val0 + winStd.Val1 + winStd.Val2) / 3.0 < 3.0) continue;

                    using var gray = new Mat();
                    Cv2.CvtColor(win, gray, ColorConversionCodes.BGR2GRAY);
                    using var grayClahe = new Mat();
                    clahe.Apply(gray, grayClahe);

                    float[] qZncc = VisionFeatureExtractor.ExtractPickZnccVector(grayClahe);
                    float[] qHsv = VisionFeatureExtractor.ExtractPickHsvHistogram(win);

                    for (int hIdx = 0; hIdx < nHeroes; hIdx++)
                    {
                        var tplList = snapshot.HeroToAllyPickVectors[hIdx];
                        int tCount = tplList.Count;
                        if (tCount == 0)
                        {
                            fScores[hIdx] = 0f;
                            continue;
                        }

                        if (tCount > zCandBuf.Length)
                        {
                            Array.Resize(ref zCandBuf, tCount * 2);
                            Array.Resize(ref hCandBuf, tCount * 2);
                        }

                        var zCand = zCandBuf.AsSpan(0, tCount);
                        var hCand = hCandBuf.AsSpan(0, tCount);

                        for (int t = 0; t < tCount; t++)
                        {
                            zCand[t] = VisionFeatureExtractor.Dot(tplList[t].Zncc, qZncc) / VisionFeatureExtractor.PickWindowPixelCount;
                            hCand[t] = VisionFeatureExtractor.Dot(tplList[t].Hsv, qHsv);
                        }

                        var (fused, _, _) = VisionFeatureExtractor.ConsolidateHeroScore(zCand, hCand, (float)threshold);
                        fScores[hIdx] = fused;
                    }

                    var sorted = fScores.ToArray();
                    Array.Sort(sorted);
                    Array.Reverse(sorted);

                    double top1 = sorted[0];
                    double top2 = sorted.Length > 1 ? sorted[1] : 0.0;
                    double margin = Math.Max(0.0, top1 - top2);
                    double quality = top1 + 0.35 * margin + (dx == 0 && dy == 0 ? 0.005 : 0.0);

                    if (bestQuality == null || quality > bestQuality.Value)
                    {
                        bestQuality = quality;
                        bestFused = fScores.ToArray();
                    }
                }

                var candList = new List<(string Hero, double Score)>();
                var topKMatches = new List<CandidateMatch>();
                if (bestFused != null)
                {
                    var indices = Enumerable.Range(0, nHeroes).ToArray();
                    Array.Sort(indices, (a, b) => bestFused[b].CompareTo(bestFused[a]));

                    foreach (int idx in indices)
                    {
                        string hero = snapshot.PickHeroNames[idx];
                        if (excluded.Contains(hero)) continue;

                        double score = Math.Round(bestFused[idx], 4);
                        candList.Add((hero, score));

                        if (topKMatches.Count < topK)
                        {
                            topKMatches.Add(new CandidateMatch { Hero = hero, Score = score, Lane = assignedLane, Shape = "rectangle" });
                        }
                    }
                }

                slotCandidateLists.Add(candList);
                var slot = new SlotMatch
                {
                    Slot = i,
                    Side = "ally",
                    Hero = "Empty",
                    MatchedHero = "Empty",
                    Shape = "rectangle",
                    DetectedLane = assignedLane,
                    EmptySimilarity = Math.Round(emptySim, 4),
                    TopKCandidates = topKMatches,
                    CropThumb = includeThumbnails ? MatToBase64(crop) : null
                };
                allySlots.Add(slot);
            }
            allySw.Stop();
            timings.AllyPickMs = Math.Round(allySw.Elapsed.TotalMilliseconds, 2);

            // Score Enemy Pick Slots
            var enemySw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                var crop = enemyCrops[i];
                if (crop == null || crop.Empty())
                {
                    enemySlots.Add(CreateEmptySlot(i, "enemy", "rectangle", "DARK_OR_EMPTY_SLOT"));
                    slotCandidateLists.Add([]);
                    continue;
                }

                Cv2.MeanStdDev(crop, out var meanScalar, out var stdScalar);
                double mean = (meanScalar.Val0 + meanScalar.Val1 + meanScalar.Val2) / 3.0;
                double std = (stdScalar.Val0 + stdScalar.Val1 + stdScalar.Val2) / 3.0;

                if (mean < 12.0 || std < 6.0)
                {
                    enemySlots.Add(CreateEmptySlot(i, "enemy", "rectangle", "DARK_OR_EMPTY_SLOT"));
                    slotCandidateLists.Add([]);
                    continue;
                }

                using var card210 = new Mat();
                Cv2.Resize(crop, card210, new Size(VisionFeatureExtractor.PickCardWidth, VisionFeatureExtractor.PickCardHeight));

                double emptySim = 0.0;
                if (snapshot.EmptyPickEnemy != null)
                {
                    using var res = new Mat();
                    Cv2.MatchTemplate(card210, snapshot.EmptyPickEnemy, res, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);
                    emptySim = Math.Max(emptySim, maxVal);
                }

                if (emptySim >= 0.60)
                {
                    var s = CreateEmptySlot(i, "enemy", "rectangle", "EMPTY_SLOT");
                    s.EmptySimilarity = Math.Round(emptySim, 4);
                    enemySlots.Add(s);
                    slotCandidateLists.Add([]);
                    continue;
                }

                // 9-Point Spatial Jitter Search: Enemy focal window [x: 16..155, y: 0..110]
                double? bestQuality = null;
                float[]? bestFused = null;
                float[] fScores = new float[nHeroes];

                foreach (var (dx, dy) in VisionFeatureExtractor.JitterOffsets)
                {
                    int x1 = 16 + dx;
                    int y1 = 0 + dy;
                    int x2 = 155 + dx;
                    int y2 = 110 + dy;

                    if (x1 < 0 || x2 > card210.Cols || y1 < 0 || y2 > card210.Rows) continue;

                    using var win = new Mat(card210, new Rect(x1, y1, 139, 110));
                    Cv2.MeanStdDev(win, out _, out var winStd);
                    if ((winStd.Val0 + winStd.Val1 + winStd.Val2) / 3.0 < 3.0) continue;

                    using var gray = new Mat();
                    Cv2.CvtColor(win, gray, ColorConversionCodes.BGR2GRAY);
                    using var grayClahe = new Mat();
                    clahe.Apply(gray, grayClahe);

                    float[] qZncc = VisionFeatureExtractor.ExtractPickZnccVector(grayClahe);
                    float[] qHsv = VisionFeatureExtractor.ExtractPickHsvHistogram(win);

                    for (int hIdx = 0; hIdx < nHeroes; hIdx++)
                    {
                        var tplList = snapshot.HeroToEnemyPickVectors[hIdx];
                        int tCount = tplList.Count;
                        if (tCount == 0)
                        {
                            fScores[hIdx] = 0f;
                            continue;
                        }

                        if (tCount > zCandBuf.Length)
                        {
                            Array.Resize(ref zCandBuf, tCount * 2);
                            Array.Resize(ref hCandBuf, tCount * 2);
                        }

                        var zCand = zCandBuf.AsSpan(0, tCount);
                        var hCand = hCandBuf.AsSpan(0, tCount);

                        for (int t = 0; t < tCount; t++)
                        {
                            zCand[t] = VisionFeatureExtractor.Dot(tplList[t].Zncc, qZncc) / VisionFeatureExtractor.PickWindowPixelCount;
                            hCand[t] = VisionFeatureExtractor.Dot(tplList[t].Hsv, qHsv);
                        }

                        var (fused, _, _) = VisionFeatureExtractor.ConsolidateHeroScore(zCand, hCand, (float)threshold);
                        fScores[hIdx] = fused;
                    }

                    var sorted = fScores.ToArray();
                    Array.Sort(sorted);
                    Array.Reverse(sorted);

                    double top1 = sorted[0];
                    double top2 = sorted.Length > 1 ? sorted[1] : 0.0;
                    double margin = Math.Max(0.0, top1 - top2);
                    double quality = top1 + 0.35 * margin + (dx == 0 && dy == 0 ? 0.005 : 0.0);

                    if (bestQuality == null || quality > bestQuality.Value)
                    {
                        bestQuality = quality;
                        bestFused = fScores.ToArray();
                    }
                }

                var candList = new List<(string Hero, double Score)>();
                var topKMatches = new List<CandidateMatch>();
                if (bestFused != null)
                {
                    var indices = Enumerable.Range(0, nHeroes).ToArray();
                    Array.Sort(indices, (a, b) => bestFused[b].CompareTo(bestFused[a]));

                    foreach (int idx in indices)
                    {
                        string hero = snapshot.PickHeroNames[idx];
                        if (excluded.Contains(hero)) continue;

                        double score = Math.Round(bestFused[idx], 4);
                        candList.Add((hero, score));

                        if (topKMatches.Count < topK)
                        {
                            topKMatches.Add(new CandidateMatch { Hero = hero, Score = score, Shape = "rectangle" });
                        }
                    }
                }

                slotCandidateLists.Add(candList);
                var slot = new SlotMatch
                {
                    Slot = i,
                    Side = "enemy",
                    Hero = "Empty",
                    MatchedHero = "Empty",
                    Shape = "rectangle",
                    EmptySimilarity = Math.Round(emptySim, 4),
                    TopKCandidates = topKMatches,
                    CropThumb = includeThumbnails ? MatToBase64(crop) : null
                };
                enemySlots.Add(slot);
            }
            enemySw.Stop();
            timings.EnemyPickMs = Math.Round(enemySw.Elapsed.TotalMilliseconds, 2);

            // Clean up crop Mats
            foreach (var c in allyCrops) c?.Dispose();
            foreach (var c in enemyCrops) c?.Dispose();

            // Kuhn-Munkres Joint Global 1-to-1 Bipartite Optimal Assignment
            var assignSw = Stopwatch.StartNew();
            var allSlots = allySlots.Concat(enemySlots).ToList();
            SolveMaximumWeightBipartiteAssignment(allSlots, slotCandidateLists, threshold);
            assignSw.Stop();

            timings.PickAssignmentMs = Math.Round(assignSw.Elapsed.TotalMilliseconds, 2);
            sw.Stop();
            timings.PickInferenceMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);

            return (allySlots, enemySlots, timings);
        }
        finally
        {
            Interlocked.Decrement(ref _activeReaders);
        }
    }

    private static List<string?> ComputeOptimalLanes(List<Mat?> allyCrops, VisionTensorsSnapshot snapshot)
    {
        string[] laneKeys = ["exp", "gold", "jungle", "mid", "roam"];
        double[] laneScales = [0.75, 0.85, 1.0, 1.15];
        var displayMap = new Dictionary<string, string>
        {
            ["exp"] = "EXP Lane",
            ["gold"] = "Gold Lane",
            ["jungle"] = "Jungle",
            ["mid"] = "Mid Lane",
            ["roam"] = "Roam"
        };

        int nSlots = Math.Min(5, allyCrops.Count);
        double[,] scoreMatrix = new double[nSlots, 5];

        for (int i = 0; i < nSlots; i++)
        {
            var crop = allyCrops[i];
            if (crop == null || crop.Empty()) continue;

            using var card = new Mat();
            if (crop.Rows != 132 || crop.Cols != 210)
            {
                Cv2.Resize(crop, card, new Size(210, 132));
            }
            else
            {
                crop.CopyTo(card);
            }

            // Lane badge region: y=45..105, x=0..56
            using var laneArea = new Mat(card, new Rect(0, 45, 56, 60));

            for (int j = 0; j < laneKeys.Length; j++)
            {
                string lk = laneKeys[j];
                double bestForLane = 0.0;

                foreach (var s in laneScales)
                {
                    if (snapshot.ScaledLaneTemplates.TryGetValue((lk, s), out var tpl))
                    {
                        if (tpl.Width < laneArea.Width && tpl.Height < laneArea.Height)
                        {
                            using var res = new Mat();
                            Cv2.MatchTemplate(laneArea, tpl, res, TemplateMatchModes.CCoeffNormed);
                            Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);
                            if (maxVal > bestForLane) bestForLane = maxVal;
                        }
                    }
                }

                scoreMatrix[i, j] = bestForLane;
            }
        }

        // Add distinctiveness evidence bonus: margin above next best lane in this slot
        double[,] evidenceMatrix = new double[nSlots, 5];
        for (int i = 0; i < nSlots; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                double cur = scoreMatrix[i, j];
                double nextBest = 0.0;
                for (int other = 0; other < 5; other++)
                {
                    if (other != j && scoreMatrix[i, other] > nextBest)
                        nextBest = scoreMatrix[i, other];
                }
                double margin = Math.Max(0.0, cur - nextBest);
                evidenceMatrix[i, j] = cur + 0.25 * margin;
            }
        }

        // Linear sum assignment via Hungarian minimization (cost = 10.0 - evidence)
        double[,] costMatrix = new double[nSlots, 5];
        for (int i = 0; i < nSlots; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                costMatrix[i, j] = 10.0 - evidenceMatrix[i, j];
            }
        }

        int[] assignment = HungarianMinCost(costMatrix);
        var lanes = new List<string?>();

        for (int i = 0; i < nSlots; i++)
        {
            int col = assignment[i];
            if (col >= 0 && col < laneKeys.Length)
            {
                lanes.Add(displayMap[laneKeys[col]]);
            }
            else
            {
                lanes.Add(null);
            }
        }

        return lanes;
    }

    public List<string?> DetectLanes(CapturedFrame frame, RoiConfiguration config)
    {
        InitializeTensors();
        Interlocked.Increment(ref _activeReaders);
        var snapshot = _snapshot;
        try
        {
            using var mat = ToMat(frame);
            if (mat == null || mat.Empty())
            {
                return Enumerable.Repeat<string?>(null, 5).ToList();
            }

            var allyCrops = new List<Mat?>();
            for (int i = 0; i < 5; i++)
            {
                var rAlly = ExtractRoiRect(mat.Width, mat.Height, config, $"ally_pick_{i}");
                allyCrops.Add(CropSafe(mat, rAlly));
            }

            var lanes = ComputeOptimalLanes(allyCrops, snapshot);
            foreach (var c in allyCrops) c?.Dispose();
            return lanes;
        }
        finally
        {
            Interlocked.Decrement(ref _activeReaders);
        }
    }

    public (List<string?> allySpells, List<string?> enemySpells) DetectSpells(CapturedFrame frame, RoiConfiguration config)
    {
        InitializeTensors();
        Interlocked.Increment(ref _activeReaders);
        var snapshot = _snapshot;
        try
        {
            var allySpells = new List<string?>();
            var enemySpells = new List<string?>();
            using var mat = ToMat(frame);
            if (mat == null || mat.Empty() || snapshot.SpellTemplates.Count == 0)
            {
                return (Enumerable.Repeat<string?>(null, 5).ToList(), Enumerable.Repeat<string?>(null, 5).ToList());
            }

            for (int i = 0; i < 5; i++)
            {
                string roiKey = $"ally_pick_{i}";
                var pickRect = ExtractRoiRect(mat.Width, mat.Height, config, roiKey);

                int spellX = pickRect.X + (int)Math.Round(194.0 / 210.0 * pickRect.Width);
                int spellY = pickRect.Y + (int)Math.Round(2.0 / 132.0 * pickRect.Height);
                int spellW = Math.Max(1, (int)Math.Round(54.0 / 210.0 * pickRect.Width));
                int spellH = Math.Max(1, (int)Math.Round(55.0 / 132.0 * pickRect.Height));

                using var crop = CropSafe(mat, new RoiRect(spellX, spellY, spellW, spellH));
                if (crop == null || crop.Empty())
                {
                    allySpells.Add(null);
                    continue;
                }

                using var resized = new Mat();
                Cv2.Resize(crop, resized, new Size(50, 50));

                Cv2.MeanStdDev(resized, out var meanScalar, out var stdScalar);
                double mean = (meanScalar.Val0 + meanScalar.Val1 + meanScalar.Val2) / 3.0;
                double std = (stdScalar.Val0 + stdScalar.Val1 + stdScalar.Val2) / 3.0;
                if (mean < 12.0 || std < 6.0)
                {
                    allySpells.Add(null);
                    continue;
                }

                string? bestSpell = null;
                double bestScore = 0.0;

                foreach (var (spellName, tpl) in snapshot.SpellTemplates)
                {
                    double score = ComputeSimilarity(resized, tpl);
                    if (score > bestScore && score >= 0.40)
                    {
                        bestScore = score;
                        bestSpell = spellName;
                    }
                }

                allySpells.Add(bestSpell);
            }

            for (int i = 0; i < 5; i++)
            {
                string roiKey = $"enemy_pick_{i}";
                var pickRect = ExtractRoiRect(mat.Width, mat.Height, config, roiKey);

                int spellX = pickRect.X - (int)Math.Round(38.0 / 210.0 * pickRect.Width);
                int spellY = pickRect.Y + (int)Math.Round(2.0 / 132.0 * pickRect.Height);
                int spellW = Math.Max(1, (int)Math.Round(54.0 / 210.0 * pickRect.Width));
                int spellH = Math.Max(1, (int)Math.Round(55.0 / 132.0 * pickRect.Height));

                using var crop = CropSafe(mat, new RoiRect(spellX, spellY, spellW, spellH));
                if (crop == null || crop.Empty())
                {
                    enemySpells.Add(null);
                    continue;
                }

                using var resized = new Mat();
                Cv2.Resize(crop, resized, new Size(50, 50));

                Cv2.MeanStdDev(resized, out var meanScalar, out var stdScalar);
                double mean = (meanScalar.Val0 + meanScalar.Val1 + meanScalar.Val2) / 3.0;
                double std = (stdScalar.Val0 + stdScalar.Val1 + stdScalar.Val2) / 3.0;
                if (mean < 12.0 || std < 6.0)
                {
                    enemySpells.Add(null);
                    continue;
                }

                string? bestSpell = null;
                double bestScore = 0.0;

                foreach (var (spellName, tpl) in snapshot.SpellTemplates)
                {
                    double score = ComputeSimilarity(resized, tpl);
                    if (score > bestScore && score >= 0.40)
                    {
                        bestScore = score;
                        bestSpell = spellName;
                    }
                }

                enemySpells.Add(bestSpell);
            }

            return (allySpells, enemySpells);
        }
        finally
        {
            Interlocked.Decrement(ref _activeReaders);
        }
    }

    public DraftScanResult ProcessCycle(CapturedFrame frame, RoiConfiguration config, HashSet<string>? takenBans = null)
    {
        var (phase, subPhase, conf, details) = DetectPhase(frame);

        double banThresh = GetThresholdDouble(config.Thresholds, "ban_threshold", 0.45);
        var bans = MatchBans(frame, config, threshold: banThresh, topK: 5);
        var combinedBans = takenBans != null ? new HashSet<string>(takenBans, StringComparer.OrdinalIgnoreCase) : [];
        foreach (var b in bans)
        {
            if (!string.IsNullOrEmpty(b.HeroId) && !b.HeroId.Equals("Empty", StringComparison.OrdinalIgnoreCase))
            {
                combinedBans.Add(b.HeroId);
            }
        }

        var (allyPicks, enemyPicks, _) = MatchPicksJoint(frame, config, combinedBans, threshold: 0.50, topK: 5);
        var lanes = DetectLanes(frame, config);
        var (allySpells, enemySpells) = DetectSpells(frame, config);

        for (int i = 0; i < allyPicks.Count; i++)
        {
            if (i < lanes.Count && lanes[i] != null)
            {
                allyPicks[i].DetectedLane = lanes[i];
            }
            if (i < allySpells.Count && allySpells[i] != null)
            {
                allyPicks[i].SpellName = allySpells[i];
            }
        }

        for (int i = 0; i < enemyPicks.Count; i++)
        {
            if (i < enemySpells.Count && enemySpells[i] != null)
            {
                enemyPicks[i].SpellName = enemySpells[i];
            }
        }

        var result = new DraftScanResult
        {
            Bans = bans,
            AllyPicks = allyPicks,
            EnemyPicks = enemyPicks,
            Lanes = lanes,
            AllySpells = allySpells,
            EnemySpells = enemySpells,
            DetectedPhase = phase,
            DetectedSubPhase = subPhase,
            Confidence = conf,
            Details = details,
            Status = (phase == GamePhase.DraftPick) ? "active" : "standby"
        };

        OnDraftDetectionUpdated?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Kuhn-Munkres (Hungarian) algorithm for optimal maximum-weight bipartite matching.
    /// Employs private slack dummy columns per slot matching MLBB_Companion_FastAPI
    /// so weak or unassigned slots never steal candidate heroes from valid slots.
    /// </summary>
    internal static void SolveMaximumWeightBipartiteAssignment(
        List<SlotMatch> slots,
        List<List<(string Hero, double Score)>> candidateLists,
        double threshold)
    {
        int numSlots = slots.Count;
        if (numSlots == 0) return;

        var candidateHeroes = new List<string>();
        var heroIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < numSlots && i < candidateLists.Count; i++)
        {
            foreach (var (hero, score) in candidateLists[i])
            {
                if (score >= threshold && !heroIndexMap.ContainsKey(hero))
                {
                    heroIndexMap[hero] = candidateHeroes.Count;
                    candidateHeroes.Add(hero);
                }
            }
        }

        int numHeroes = candidateHeroes.Count;
        if (numHeroes == 0)
        {
            for (int i = 0; i < numSlots; i++)
            {
                slots[i].Hero = "Empty";
                slots[i].MatchedHero = "Empty";
                slots[i].Confidence = 0.0;
            }
            return;
        }

        // Columns = numHeroes + numSlots (private dummy column per slot)
        int totalCols = numHeroes + numSlots;
        double[,] weights = new double[numSlots, totalCols];
        const double RejectScore = -1000000.0;

        for (int i = 0; i < numSlots; i++)
        {
            for (int j = 0; j < numHeroes; j++)
            {
                weights[i, j] = RejectScore;
            }
            for (int d = 0; d < numSlots; d++)
            {
                weights[i, numHeroes + d] = (d == i) ? 0.0 : RejectScore;
            }

            if (i < candidateLists.Count)
            {
                foreach (var (hero, score) in candidateLists[i])
                {
                    if (heroIndexMap.TryGetValue(hero, out int hIdx) && score >= threshold)
                    {
                        weights[i, hIdx] = score;
                    }
                }
            }
        }

        // Convert weights to non-negative costs for Hungarian minimization: cost = maxWeight - weight
        double maxWeight = 1.0;
        double[,] costMatrix = new double[numSlots, totalCols];
        for (int i = 0; i < numSlots; i++)
        {
            for (int j = 0; j < totalCols; j++)
            {
                costMatrix[i, j] = maxWeight - weights[i, j];
            }
        }

        int[] assignment = HungarianMinCost(costMatrix);

        for (int i = 0; i < numSlots; i++)
        {
            int assignedCol = assignment[i];
            if (assignedCol >= 0 && assignedCol < numHeroes)
            {
                double score = weights[i, assignedCol];
                if (score >= threshold)
                {
                    string hero = candidateHeroes[assignedCol];
                    slots[i].Hero = hero;
                    slots[i].MatchedHero = hero;
                    slots[i].Confidence = Math.Round(score, 3);
                    slots[i].RejectionReason = null;
                    continue;
                }
            }

            slots[i].Hero = "Empty";
            slots[i].MatchedHero = "Empty";
            slots[i].Confidence = 0.0;
            if (string.IsNullOrEmpty(slots[i].RejectionReason))
            {
                slots[i].RejectionReason = "LOW_CONFIDENCE";
            }
        }
    }

    private static int[] HungarianMinCost(double[,] costMatrix)
    {
        int n = costMatrix.GetLength(0);
        int m = costMatrix.GetLength(1);

        double[] u = new double[n + 1];
        double[] v = new double[m + 1];
        int[] p = new int[m + 1];
        int[] way = new int[m + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            double[] minv = new double[m + 1];
            Array.Fill(minv, double.MaxValue);
            bool[] used = new bool[m + 1];

            do
            {
                used[j0] = true;
                int i0 = p[j0];
                double delta = double.MaxValue;
                int j1 = 0;

                for (int j = 1; j <= m; j++)
                {
                    if (!used[j])
                    {
                        double cur = costMatrix[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j])
                        {
                            minv[j] = cur;
                            way[j] = j0;
                        }
                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }
                }

                for (int j = 0; j <= m; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        int[] assignment = new int[n];
        Array.Fill(assignment, -1);
        for (int j = 1; j <= m; j++)
        {
            if (p[j] > 0 && p[j] <= n)
            {
                assignment[p[j] - 1] = j - 1;
            }
        }
        return assignment;
    }

    private static long ComputeMatBytes(Mat? mat) => mat != null && !mat.IsDisposed ? (long)(mat.Total() * mat.ElemSize()) : 0L;

    internal static (int x1, int y1, int x2, int y2) TransformAnchorCoordinates(int frameW, int frameH, int x1, int y1, int x2, int y2)
    {
        double scale = Math.Min((double)frameW / 1920.0, (double)frameH / 1080.0);
        double vpW = 1920.0 * scale;
        double vpH = 1080.0 * scale;
        double offsetX = (frameW - vpW) / 2.0;
        double offsetY = (frameH - vpH) / 2.0;

        int rx1 = (int)Math.Round(offsetX + x1 * scale);
        int rx2 = (int)Math.Round(offsetX + x2 * scale);
        int ry1 = (int)Math.Round(offsetY + y1 * scale);
        int ry2 = (int)Math.Round(offsetY + y2 * scale);

        rx1 = Math.Clamp(rx1, 0, frameW - 1);
        rx2 = Math.Clamp(rx2, rx1 + 1, frameW);
        ry1 = Math.Clamp(ry1, 0, frameH - 1);
        ry2 = Math.Clamp(ry2, ry1 + 1, frameH);

        return (rx1, ry1, rx2, ry2);
    }

    private static double ComputeSimilarity(Mat crop, Mat tpl)
    {
        using var res = new Mat();
        Cv2.MatchTemplate(crop, tpl, res, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(res, out _, out double maxVal, out _, out _);
        return maxVal;
    }

    private static Mat ApplyClahe(Mat bgr)
    {
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        using var clahe = Cv2.CreateCLAHE(2.0, new Size(8, 8));
        var enhanced = new Mat();
        clahe.Apply(gray, enhanced);
        return enhanced;
    }

    private static Mat CreateCircularMask(int width, int height)
    {
        var mask = new Mat(new Size(width, height), MatType.CV_8UC1, Scalar.All(0));
        var center = new Point(width / 2, height / 2);
        int radius = VisionFeatureExtractor.BanRadius;
        Cv2.Circle(mask, center, radius, Scalar.All(255), -1);
        return mask;
    }

    private static RoiRect ExtractRoiRect(int frameW, int frameH, RoiConfiguration config, string roiKey)
    {
        if (config.Rois.TryGetValue(roiKey, out var coords) && coords.Count == 4)
        {
            int x = (int)Math.Round(coords[0] * frameW);
            int y = (int)Math.Round(coords[1] * frameH);
            int w = (int)Math.Round(coords[2] * frameW);
            int h = (int)Math.Round(coords[3] * frameH);

            x = Math.Clamp(x, 0, Math.Max(0, frameW - 1));
            y = Math.Clamp(y, 0, Math.Max(0, frameH - 1));
            w = Math.Clamp(w, 1, Math.Max(1, frameW - x));
            h = Math.Clamp(h, 1, Math.Max(1, frameH - y));

            return new RoiRect(x, y, w, h);
        }
        return RoiRect.Empty;
    }

    private static Mat? CropSafe(Mat src, RoiRect rect)
    {
        if (rect == RoiRect.Empty) return null;

        int x = Math.Clamp(rect.X, 0, src.Cols - 1);
        int y = Math.Clamp(rect.Y, 0, src.Rows - 1);
        int w = Math.Clamp(rect.Width, 1, src.Cols - x);
        int h = Math.Clamp(rect.Height, 1, src.Rows - y);

        if (w <= 0 || h <= 0) return null;

        return new Mat(src, new Rect(x, y, w, h)).Clone();
    }

    private static SlotMatch CreateEmptySlot(int slot, string side, string shape, string reason)
    {
        return new SlotMatch
        {
            Slot = slot,
            Side = side,
            Hero = "Empty",
            MatchedHero = "Empty",
            Shape = shape,
            Confidence = 0.0,
            RejectionReason = reason,
            EmptySimilarity = 1.0
        };
    }

    private static Mat? ToMat(CapturedFrame frame)
    {
        if (frame.NativeHandle is Mat m && !m.Empty())
            return m.Clone();

        if (frame.Buffer != null && frame.Buffer.Length > 0)
            return Mat.FromImageData(frame.Buffer, ImreadModes.Color);

        return null;
    }

    private static string? MatToBase64(Mat? mat)
    {
        if (mat == null || mat.Empty()) return null;
        try
        {
            Cv2.ImEncode(".png", mat, out byte[] bytes);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static List<PhaseAnchorEntity> GetEmbeddedDefaultAnchors() =>
    [
        new() { Id = "matchmaking_top_banner", Phase = "Matchmaking", Filename = "matchmaking_top_banner.png", X1 = 700, Y1 = 0, X2 = 1220, Y2 = 105, Threshold = 0.60 },
        new() { Id = "scoreboard_equipment_tab", Phase = "In Game ScoreBoard", Filename = "scoreboard_equipment_tab.png", X1 = 30, Y1 = 75, X2 = 480, Y2 = 160, Threshold = 0.70 },
        new() { Id = "scoreboard_vs_header", Phase = "In Game ScoreBoard", Filename = "scoreboard_vs_header.png", X1 = 650, Y1 = 145, X2 = 1270, Y2 = 215, Threshold = 0.70 },
        new() { Id = "matchstart_enter_btn", Phase = "Match Start", Filename = "matchstart_enter_btn.png", X1 = 680, Y1 = 825, X2 = 1240, Y2 = 925, Threshold = 0.70 },
        new() { Id = "draft_battlefield_badge", Phase = GamePhase.DraftPick, Filename = "draft_battlefield_badge.png", X1 = 1500, Y1 = 880, X2 = 1920, Y2 = 1020, Threshold = 0.65 },
        new() { Id = "draft_hero_prep_tabs", Phase = GamePhase.DraftPick, Filename = "draft_hero_prep_tabs.png", X1 = 680, Y1 = 880, X2 = 1300, Y2 = 1000, Threshold = 0.65 },
        new() { Id = "draft_search_icon", Phase = GamePhase.DraftPick, Filename = "draft_search_icon.png", X1 = 300, Y1 = 110, X2 = 500, Y2 = 220, Threshold = 0.65 },
        new() { Id = "draft_bottom_chat_dock", Phase = GamePhase.DraftPick, Filename = "draft_bottom_chat_dock.png", X1 = 0, Y1 = 880, X2 = 320, Y2 = 1020, Threshold = 0.60 },
        new() { Id = "draft_ally_slot_frame", Phase = GamePhase.DraftPick, Filename = "draft_ally_slot_frame.png", X1 = 0, Y1 = 115, X2 = 230, Y2 = 275, Threshold = 0.65 },
        new() { Id = "draft_enemy_slot_frame", Phase = GamePhase.DraftPick, Filename = "draft_enemy_slot_frame.png", X1 = 1690, Y1 = 115, X2 = 1920, Y2 = 275, Threshold = 0.65 },
        new() { Id = "draft_ban_slot_frame", Phase = GamePhase.DraftPick, Filename = "draft_ban_slot_frame.png", X1 = 20, Y1 = 0, X2 = 120, Y2 = 95, Threshold = 0.65 },
        new() { Id = "ingame_recall_regen", Phase = GamePhase.InGame, Filename = "ingame_recall_regen.png", X1 = 900, Y1 = 750, X2 = 1360, Y2 = 1060, Threshold = 0.70 },
        new() { Id = "ingame_top_gold_purse", Phase = GamePhase.InGame, Filename = "ingame_top_gold_purse.png", X1 = 1750, Y1 = 5, X2 = 1920, Y2 = 90, Threshold = 0.70 },
        new() { Id = "hp_bottom_tabs", Phase = GamePhase.Homepage, Filename = "hp_bottom_tabs.png", X1 = 280, Y1 = 980, X2 = 720, Y2 = 1070, Threshold = 0.70 },
        new() { Id = "hp_mode_icon", Phase = GamePhase.Homepage, Filename = "hp_mode_icon.png", X1 = 1300, Y1 = 920, X2 = 1430, Y2 = 1060, Threshold = 0.70 },
        new() { Id = "lobby_start_btn", Phase = GamePhase.Lobby, Filename = "lobby_start_btn.png", X1 = 400, Y1 = 845, X2 = 940, Y2 = 950, Threshold = 0.70 },
        new() { Id = "lobby_header_title", Phase = GamePhase.Lobby, Filename = "lobby_header_title.png", X1 = 25, Y1 = 25, X2 = 360, Y2 = 90, Threshold = 0.70 }
    ];

    private static double GetThresholdDouble(Dictionary<string, object>? dict, string key, double fallback)
    {
        if (dict == null || !dict.TryGetValue(key, out var val) || val == null) return fallback;
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        if (val is decimal dec) return (double)dec;
        if (val is JsonElement elem)
        {
            if (elem.TryGetDouble(out var dVal)) return dVal;
            if (double.TryParse(elem.ToString(), out var parsed)) return parsed;
        }
        try { return Convert.ToDouble(val); } catch { return fallback; }
    }
}