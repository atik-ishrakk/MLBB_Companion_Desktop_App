using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;
using OpenCvSharp;

namespace MLBB.Infrastructure.Repositories;

public class PhaseAnchorStorageService : IPhaseAnchorStorageService
{
    private readonly ILogger<PhaseAnchorStorageService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();

    private static readonly List<PhaseAnchorEntity> DefaultAnchors =
    [
        new() { Id = "matchmaking_top_banner", Name = "Matchmaking Top Banner", Phase = "Matchmaking", Filename = "matchmaking_top_banner.png", X1 = 700, Y1 = 0, X2 = 1220, Y2 = 105, Threshold = 0.60 },
        new() { Id = "scoreboard_equipment_tab", Name = "Scoreboard Equipment Tab", Phase = "In Game ScoreBoard", Filename = "scoreboard_equipment_tab.png", X1 = 30, Y1 = 75, X2 = 480, Y2 = 160, Threshold = 0.70 },
        new() { Id = "scoreboard_vs_header", Name = "Scoreboard VS Header", Phase = "In Game ScoreBoard", Filename = "scoreboard_vs_header.png", X1 = 650, Y1 = 145, X2 = 1270, Y2 = 215, Threshold = 0.70 },
        new() { Id = "matchstart_enter_btn", Name = "Match Start Enter Button", Phase = "Match Start", Filename = "matchstart_enter_btn.png", X1 = 680, Y1 = 825, X2 = 1240, Y2 = 925, Threshold = 0.70 },
        new() { Id = "draft_battlefield_badge", Name = "Draft Battlefield Badge", Phase = GamePhase.DraftPick, Filename = "draft_battlefield_badge.png", X1 = 1500, Y1 = 880, X2 = 1920, Y2 = 1020, Threshold = 0.65 },
        new() { Id = "draft_hero_prep_tabs", Name = "Draft Hero Prep Tabs", Phase = GamePhase.DraftPick, Filename = "draft_hero_prep_tabs.png", X1 = 680, Y1 = 880, X2 = 1300, Y2 = 1000, Threshold = 0.65 },
        new() { Id = "draft_search_icon", Name = "Draft Search Icon", Phase = GamePhase.DraftPick, Filename = "draft_search_icon.png", X1 = 300, Y1 = 110, X2 = 500, Y2 = 220, Threshold = 0.65 },
        new() { Id = "draft_bottom_chat_dock", Name = "Draft Bottom Chat Dock", Phase = GamePhase.DraftPick, Filename = "draft_bottom_chat_dock.png", X1 = 0, Y1 = 880, X2 = 320, Y2 = 1020, Threshold = 0.60 },
        new() { Id = "draft_ally_slot_frame", Name = "Draft Ally Slot Frame", Phase = GamePhase.DraftPick, Filename = "draft_ally_slot_frame.png", X1 = 0, Y1 = 115, X2 = 230, Y2 = 275, Threshold = 0.65 },
        new() { Id = "draft_enemy_slot_frame", Name = "Draft Enemy Slot Frame", Phase = GamePhase.DraftPick, Filename = "draft_enemy_slot_frame.png", X1 = 1690, Y1 = 115, X2 = 1920, Y2 = 275, Threshold = 0.65 },
        new() { Id = "draft_ban_slot_frame", Name = "Draft Ban Slot Frame", Phase = GamePhase.DraftPick, Filename = "draft_ban_slot_frame.png", X1 = 20, Y1 = 0, X2 = 120, Y2 = 95, Threshold = 0.65 },
        new() { Id = "ingame_recall_regen", Name = "InGame Recall Regen Cluster", Phase = GamePhase.InGame, Filename = "ingame_recall_regen.png", X1 = 900, Y1 = 750, X2 = 1360, Y2 = 1060, Threshold = 0.70 },
        new() { Id = "ingame_top_gold_purse", Name = "InGame Top Gold Purse", Phase = GamePhase.InGame, Filename = "ingame_top_gold_purse.png", X1 = 1750, Y1 = 5, X2 = 1920, Y2 = 90, Threshold = 0.70 },
        new() { Id = "hp_bottom_tabs", Name = "Homepage Bottom Nav Tabs", Phase = GamePhase.Homepage, Filename = "hp_bottom_tabs.png", X1 = 280, Y1 = 980, X2 = 720, Y2 = 1070, Threshold = 0.70 },
        new() { Id = "hp_mode_icon", Name = "Homepage Mode Icon", Phase = GamePhase.Homepage, Filename = "hp_mode_icon.png", X1 = 1300, Y1 = 920, X2 = 1430, Y2 = 1060, Threshold = 0.70 },
        new() { Id = "lobby_start_btn", Name = "Lobby Start Game Button", Phase = GamePhase.Lobby, Filename = "lobby_start_btn.png", X1 = 400, Y1 = 845, X2 = 940, Y2 = 950, Threshold = 0.70 },
        new() { Id = "lobby_header_title", Name = "Lobby Header Title", Phase = GamePhase.Lobby, Filename = "lobby_header_title.png", X1 = 25, Y1 = 25, X2 = 360, Y2 = 90, Threshold = 0.70 }
    ];

    public PhaseAnchorStorageService(ILogger<PhaseAnchorStorageService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        EnsureStorageFileExists();
    }

    public List<PhaseAnchorEntity> GetAllAnchors()
    {
        lock (_lock)
        {
            return LoadAnchorsInternal();
        }
    }

    public PhaseAnchorEntity? GetAnchorById(string id)
    {
        lock (_lock)
        {
            var list = LoadAnchorsInternal();
            return list.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }

    public PhaseAnchorEntity SaveOrUpdateAnchor(PhaseAnchorEntity anchor)
    {
        lock (_lock)
        {
            var list = LoadAnchorsInternal();
            int idx = list.FindIndex(a => a.Id.Equals(anchor.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                list[idx] = anchor;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(anchor.Id))
                {
                    anchor.Id = SanitizeSlug(anchor.Name);
                }
                list.Add(anchor);
            }

            SaveAnchorsInternal(list);
            NotifyVisionEngineReload();
            return anchor;
        }
    }

    public bool DeleteAnchor(string id)
    {
        lock (_lock)
        {
            var list = LoadAnchorsInternal();
            var target = list.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (target == null) return false;

            list.Remove(target);
            SaveAnchorsInternal(list);
            NotifyVisionEngineReload();
            return true;
        }
    }

    public (bool success, string message, PhaseAnchorEntity? anchor) CropAndSaveAnchorFromFrame(CropAnchorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return (false, "Image data must be provided in base64 format.", null);
        }

        try
        {
            using var mat = DecodeBase64(request.ImageBase64);
            if (mat == null || mat.Empty())
            {
                return (false, "Failed to decode frame image.", null);
            }

            // Normalize and clamp coordinates
            int x1 = Math.Clamp(Math.Min(request.X1, request.X2), 0, mat.Width - 1);
            int x2 = Math.Clamp(Math.Max(request.X1, request.X2), x1 + 1, mat.Width);
            int y1 = Math.Clamp(Math.Min(request.Y1, request.Y2), 0, mat.Height - 1);
            int y2 = Math.Clamp(Math.Max(request.Y1, request.Y2), y1 + 1, mat.Height);

            int width = x2 - x1;
            int height = y2 - y1;

            if (width < 5 || height < 5)
            {
                return (false, "Selected anchor area is too small (minimum 5x5 pixels required).", null);
            }

            using var crop = new Mat(mat, new Rect(x1, y1, width, height));

            string id = string.IsNullOrWhiteSpace(request.Id) ? SanitizeSlug(request.Name) : SanitizeSlug(request.Id);
            string filename = $"{id}.png";

            // Save cropped image across all asset anchor folders
            var anchorDirs = ResolveAnchorDirectories();
            foreach (var dir in anchorDirs)
            {
                Directory.CreateDirectory(dir);
                string filePath = Path.Combine(dir, filename);
                crop.SaveImage(filePath);
            }

            var entity = new PhaseAnchorEntity
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(request.Name) ? id : request.Name,
                Phase = string.IsNullOrWhiteSpace(request.Phase) ? GamePhase.DraftPick : request.Phase,
                Filename = filename,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Threshold = request.Threshold > 0 ? request.Threshold : 0.65,
                Enabled = true
            };

            SaveOrUpdateAnchor(entity);
            _logger.LogInformation("Anchor cropped and saved: {Id} ({Phase}) at ({X1},{Y1})-({X2},{Y2})", id, entity.Phase, x1, y1, x2, y2);

            return (true, $"Anchor '{entity.Name}' successfully cropped and saved to detection database.", entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cropping anchor: {Message}", ex.Message);
            return (false, $"Error processing anchor crop: {ex.Message}", null);
        }
    }

    public (bool success, string message, string? filePath) CropAndSaveSlotToDatabase(CropSlotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            return (false, "Image data must be provided in base64 format.", null);
        }

        try
        {
            using var mat = DecodeBase64(request.ImageBase64);
            if (mat == null || mat.Empty())
            {
                return (false, "Failed to decode frame image.", null);
            }

            int x1 = Math.Clamp(Math.Min(request.X1, request.X2), 0, mat.Width - 1);
            int x2 = Math.Clamp(Math.Max(request.X1, request.X2), x1 + 1, mat.Width);
            int y1 = Math.Clamp(Math.Min(request.Y1, request.Y2), 0, mat.Height - 1);
            int y2 = Math.Clamp(Math.Max(request.Y1, request.Y2), y1 + 1, mat.Height);

            int width = x2 - x1;
            int height = y2 - y1;

            if (width < 5 || height < 5)
            {
                return (false, "Selected slot area is too small.", null);
            }

            using var crop = new Mat(mat, new Rect(x1, y1, width, height));

            string subDir = string.IsNullOrWhiteSpace(request.SubDir) ? "ally_picks" : request.SubDir.Trim();
            string slug = SanitizeSlug(request.TargetName);
            string filename = $"{slug}.png";

            string assetsRoot = ResolveAssetsRoot();
            var targetDirs = new[]
            {
                Path.Combine(assetsRoot, "training", subDir),
                WorkspacePathResolver.ResolvePath("Assets", "training", subDir),
                WorkspacePathResolver.ResolvePath("Solution", "API", "Assets", "training", subDir),
                Path.Combine(AppContext.BaseDirectory, "Assets", "training", subDir)
            };

            string primaryPath = string.Empty;
            foreach (var dir in targetDirs)
            {
                Directory.CreateDirectory(dir);
                string outPath = Path.Combine(dir, filename);
                crop.SaveImage(outPath);
                if (string.IsNullOrEmpty(primaryPath)) primaryPath = outPath;
            }

            NotifyVisionEngineReload();
            _logger.LogInformation("Slot cropped and saved to training database: {TargetName} in {SubDir}", slug, subDir);

            return (true, $"Slot crop successfully saved as '{filename}' in {subDir}.", primaryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cropping slot: {Message}", ex.Message);
            return (false, $"Error processing slot crop: {ex.Message}", null);
        }
    }

    public List<PhaseAnchorEntity> ResetToDefault()
    {
        lock (_lock)
        {
            var list = new List<PhaseAnchorEntity>(DefaultAnchors);
            SaveAnchorsInternal(list);
            NotifyVisionEngineReload();
            return list;
        }
    }

    private void EnsureStorageFileExists()
    {
        string primaryPath = GetPrimaryStoragePath();
        if (!File.Exists(primaryPath))
        {
            SaveAnchorsInternal(DefaultAnchors);
        }
    }

    private List<PhaseAnchorEntity> LoadAnchorsInternal()
    {
        string path = GetPrimaryStoragePath();
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<PhaseAnchorEntity>>(json);
                if (list != null && list.Count > 0) return list;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse phase_anchors.json, fallback to defaults.");
            }
        }
        return new List<PhaseAnchorEntity>(DefaultAnchors);
    }

    private void SaveAnchorsInternal(List<PhaseAnchorEntity> list)
    {
        string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        var paths = GetAllStoragePaths();
        foreach (var p in paths)
        {
            try
            {
                string? dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(p, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed writing anchors to {Path}", p);
            }
        }
    }

    private static string GetPrimaryStoragePath()
    {
        return GetAllStoragePaths()[0];
    }

    private static string[] GetAllStoragePaths()
    {
        return
        [
            WorkspacePathResolver.ResolvePath("Assets", "templates", "Phase Template", "phase_anchors.json"),
            WorkspacePathResolver.ResolvePath("Solution", "API", "Assets", "templates", "Phase Template", "phase_anchors.json"),
            WorkspacePathResolver.ResolvePath("Solution", "API", "wwwroot", "assets", "templates", "Phase Template", "phase_anchors.json"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "templates", "Phase Template", "phase_anchors.json")
        ];
    }

    private static string[] ResolveAnchorDirectories()
    {
        return
        [
            WorkspacePathResolver.ResolvePath("Assets", "templates", "Phase Template", "anchors"),
            WorkspacePathResolver.ResolvePath("Solution", "API", "Assets", "templates", "Phase Template", "anchors"),
            WorkspacePathResolver.ResolvePath("Solution", "API", "wwwroot", "assets", "templates", "Phase Template", "anchors"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "templates", "Phase Template", "anchors")
        ];
    }

    private static string ResolveAssetsRoot()
    {
        var candidates = new[]
        {
            WorkspacePathResolver.ResolvePath("Assets"),
            WorkspacePathResolver.ResolvePath("Solution", "API", "Assets"),
            Path.Combine(AppContext.BaseDirectory, "Assets")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private void NotifyVisionEngineReload()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var engine = scope.ServiceProvider.GetService(typeof(IVisionEngine)) as IVisionEngine;
            engine?.ReloadTemplates();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not notify IVisionEngine to reload: {Message}", ex.Message);
        }
    }

    private static Mat? DecodeBase64(string b64)
    {
        if (string.IsNullOrWhiteSpace(b64)) return null;
        int comma = b64.IndexOf(',');
        if (comma >= 0) b64 = b64[(comma + 1)..];
        byte[] bytes = Convert.FromBase64String(b64.Trim());
        return Mat.FromImageData(bytes, ImreadModes.Color);
    }

    private static string SanitizeSlug(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "anchor_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return s.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }
}
