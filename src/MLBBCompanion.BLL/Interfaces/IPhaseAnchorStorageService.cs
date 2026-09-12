using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Service managing dynamic storage, editing, and screenshot-cropping of game phase anchors and slot templates.
/// </summary>
public interface IPhaseAnchorStorageService
{
    /// <summary>
    /// Returns all registered phase detection anchors.
    /// </summary>
    List<PhaseAnchorEntity> GetAllAnchors();

    /// <summary>
    /// Returns a phase anchor by its unique ID.
    /// </summary>
    PhaseAnchorEntity? GetAnchorById(string id);

    /// <summary>
    /// Saves or updates a phase anchor configuration in the database/JSON storage.
    /// </summary>
    PhaseAnchorEntity SaveOrUpdateAnchor(PhaseAnchorEntity anchor);

    /// <summary>
    /// Deletes a phase anchor and optionally removes its template image.
    /// </summary>
    bool DeleteAnchor(string id);

    /// <summary>
    /// Crops an anchor slot area from an uploaded in-game screenshot, saves it to the anchors folder,
    /// updates the database/JSON storage, and triggers vision engine hot-reload.
    /// </summary>
    (bool success, string message, PhaseAnchorEntity? anchor) CropAndSaveAnchorFromFrame(CropAnchorRequest request);

    /// <summary>
    /// Crops a general slot area (e.g. hero pick skin or ban) from an uploaded in-game screenshot to the database,
    /// and triggers vision engine hot-reload.
    /// </summary>
    (bool success, string message, string? filePath) CropAndSaveSlotToDatabase(CropSlotRequest request);

    /// <summary>
    /// Resets anchors to factory default MLBB specification.
    /// </summary>
    List<PhaseAnchorEntity> ResetToDefault();
}
