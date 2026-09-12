using MLBBCompanion.BLL.Models;

namespace MLBBCompanion.BLL.Interfaces;

public interface IEnvironmentDetector
{
    string? DetectBrowser();
    string? DetectBlueStacksHdPlayer();
    string? DetectBlueStacksLauncher();
    string? DetectAdbPath();
    EnvironmentSummary GetSummary();
}
