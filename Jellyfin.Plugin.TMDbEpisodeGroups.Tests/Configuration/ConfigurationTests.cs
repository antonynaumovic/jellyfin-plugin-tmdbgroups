using System.Collections.Generic;
using FluentAssertions;
using Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;
using Xunit;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Tests.Configuration;

public class PluginConfigurationTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var config = new PluginConfiguration();

        // Assert
        config.TmdbApiKey.Should().BeEmpty();
        config.EpisodeGroupConfigs.Should().NotBeNull();
        config.EpisodeGroupConfigs.Should().BeEmpty();
        config.CacheExpirationHours.Should().Be(24);
    }

    [Fact]
    public void EpisodeGroupConfigs_CanAddAndRetrieveConfigs()
    {
        // Arrange
        var config = new PluginConfiguration();
        var episodeGroupConfig = new EpisodeGroupConfig
        {
            TmdbSeriesId = "61709",
            EpisodeGroupId = "test-group-id",
            EpisodeGroupName = "Test Group"
        };

        // Act
        config.EpisodeGroupConfigs.Add(episodeGroupConfig);

        // Assert
        config.EpisodeGroupConfigs.Should().HaveCount(1);
        config.EpisodeGroupConfigs[0].TmdbSeriesId.Should().Be("61709");
        config.EpisodeGroupConfigs[0].EpisodeGroupId.Should().Be("test-group-id");
        config.EpisodeGroupConfigs[0].EpisodeGroupName.Should().Be("Test Group");
    }

    [Fact]
    public void TmdbApiKey_CanBeSetAndRetrieved()
    {
        // Arrange
        var config = new PluginConfiguration();
        var apiKey = "test-api-key-12345";

        // Act
        config.TmdbApiKey = apiKey;

        // Assert
        config.TmdbApiKey.Should().Be(apiKey);
    }

    [Fact]
    public void EpisodeGroupConfigs_SupportsMultipleConfigurations()
    {
        // Arrange
        var config = new PluginConfiguration();
        var configs = new List<EpisodeGroupConfig>
        {
            new EpisodeGroupConfig { TmdbSeriesId = "1", EpisodeGroupId = "group1", EpisodeGroupName = "Group 1" },
            new EpisodeGroupConfig { TmdbSeriesId = "2", EpisodeGroupId = "group2", EpisodeGroupName = "Group 2" },
            new EpisodeGroupConfig { TmdbSeriesId = "3", EpisodeGroupId = "group3", EpisodeGroupName = "Group 3" }
        };

        // Act
        foreach (var episodeConfig in configs)
        {
            config.EpisodeGroupConfigs.Add(episodeConfig);
        }

        // Assert
        config.EpisodeGroupConfigs.Should().HaveCount(3);
        config.EpisodeGroupConfigs.Should().ContainSingle(c => c.TmdbSeriesId == "1");
        config.EpisodeGroupConfigs.Should().ContainSingle(c => c.TmdbSeriesId == "2");
        config.EpisodeGroupConfigs.Should().ContainSingle(c => c.TmdbSeriesId == "3");
    }
}

public class EpisodeGroupConfigTests
{
    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var config = new EpisodeGroupConfig
        {
            TmdbSeriesId = "61709",
            EpisodeGroupId = "651d0a14c50ad2010bfffd7f",
            EpisodeGroupName = "Dragon Ball Z Kai - DVD Order"
        };

        // Assert
        config.TmdbSeriesId.Should().Be("61709");
        config.EpisodeGroupId.Should().Be("651d0a14c50ad2010bfffd7f");
        config.EpisodeGroupName.Should().Be("Dragon Ball Z Kai - DVD Order");
    }

    [Fact]
    public void Properties_DefaultToNull()
    {
        // Act
        var config = new EpisodeGroupConfig();

        // Assert
        config.TmdbSeriesId.Should().BeNull();
        config.EpisodeGroupId.Should().BeNull();
        config.EpisodeGroupName.Should().BeNull();
    }
}
