using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Tests;

public class EpisodeGroupMetadataManagerTests
{
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<ITmdbEpisodeGroupCache> _mockEpisodeGroupCache;
    private readonly Mock<IProviderManager> _mockProviderManager;
    private readonly Mock<ILogger<EpisodeGroupMetadataManager>> _mockLogger;

    public EpisodeGroupMetadataManagerTests()
    {
        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockEpisodeGroupCache = new Mock<ITmdbEpisodeGroupCache>();
        _mockProviderManager = new Mock<IProviderManager>();
        _mockLogger = new Mock<ILogger<EpisodeGroupMetadataManager>>();
    }

    [Fact]
    public async Task RefreshSeriesEpisodeMetadata_WithNoConfiguration_DoesNotCallCache()
    {
        // Arrange
        var tmdbSeriesId = "61709";
        var emptyConfigs = new List<EpisodeGroupConfig>();
        var manager = new EpisodeGroupMetadataManager(
            _mockLibraryManager.Object,
            _mockEpisodeGroupCache.Object,
            _mockProviderManager.Object,
            _mockLogger.Object,
            () => emptyConfigs);

        // Act
        await manager.RefreshSeriesEpisodeMetadata(tmdbSeriesId, CancellationToken.None);

        // Assert
        _mockEpisodeGroupCache.Verify(x => x.GetOrFetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAllConfiguredSeries_WithNoConfigurations_CompletesSuccessfully()
    {
        // Arrange
        var emptyConfigs = new List<EpisodeGroupConfig>();
        var manager = new EpisodeGroupMetadataManager(
            _mockLibraryManager.Object,
            _mockEpisodeGroupCache.Object,
            _mockProviderManager.Object,
            _mockLogger.Object,
            () => emptyConfigs);

        // Act
        await manager.RefreshAllConfiguredSeries(CancellationToken.None);

        // Assert - should complete without errors
        _mockEpisodeGroupCache.Verify(x => x.GetOrFetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var manager = new EpisodeGroupMetadataManager(
            _mockLibraryManager.Object,
            _mockEpisodeGroupCache.Object,
            _mockProviderManager.Object,
            _mockLogger.Object);

        // Assert
        manager.Should().NotBeNull();
    }
}
