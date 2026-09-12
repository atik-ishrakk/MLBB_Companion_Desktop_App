using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Services;
using MLBB.Infrastructure.Repositories;
using Xunit;

namespace MLBB.Tests;

public class RoiConfigurationTests
{
    private readonly RoiConfigurationService _roiService;

    public RoiConfigurationTests()
    {
        var logger = NullLogger<RoiRepository>.Instance;
        var repo = new RoiRepository(logger);
        _roiService = new RoiConfigurationService(repo);
    }

    [Fact]
    public void GetCurrentConfiguration_ContainsAll10BansAnd10Picks()
    {
        var config = _roiService.GetCurrentConfiguration();
        Assert.NotNull(config);
        Assert.Equal(20, config.Rois.Count);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(config.Rois.ContainsKey($"ally_ban_{i}"));
            Assert.True(config.Rois.ContainsKey($"enemy_ban_{i}"));
            Assert.True(config.Rois.ContainsKey($"ally_pick_{i}"));
            Assert.True(config.Rois.ContainsKey($"enemy_pick_{i}"));
        }
    }

    [Fact]
    public void GetRoiPixels_ComputesExactCoordinatesFor1080p()
    {
        // Reference: ally_ban_0 is 30px x, 6px y, 80px w, 80px h
        var ban0 = _roiService.GetRoiPixels(1920, 1080, "ally_ban_0");
        Assert.Equal(30, ban0.X);
        Assert.Equal(6, ban0.Y);
        Assert.Equal(80, ban0.Width);
        Assert.Equal(80, ban0.Height);

        // Reference: ally_pick_0 is 0px x, 125px y, 210px w, 132px h
        var pick0 = _roiService.GetRoiPixels(1920, 1080, "ally_pick_0");
        Assert.Equal(0, pick0.X);
        Assert.Equal(125, pick0.Y);
        Assert.Equal(210, pick0.Width);
        Assert.Equal(132, pick0.Height);

        // Reference: enemy_pick_0 is 1710px x, 125px y, 210px w, 132px h
        var enemyPick0 = _roiService.GetRoiPixels(1920, 1080, "enemy_pick_0");
        Assert.Equal(1710, enemyPick0.X);
        Assert.Equal(125, enemyPick0.Y);
        Assert.Equal(210, enemyPick0.Width);
        Assert.Equal(132, enemyPick0.Height);
    }

    [Fact]
    public void ResetToDefault_RestoresOriginalCoordinates()
    {
        var reset = _roiService.ResetToDefault();
        Assert.Equal("2.0", reset.Version);
        Assert.Equal("normalized", reset.CoordinateSystem);
        Assert.Equal(20, reset.Rois.Count);
    }
}
