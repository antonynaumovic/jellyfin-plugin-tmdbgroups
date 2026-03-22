using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;

/// <summary>
/// Class holding the plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration" /> class.
    /// </summary>
    public PluginConfiguration()
    {
        TmdbApiKey = string.Empty;
        EpisodeGroupConfigs = new List<EpisodeGroupConfig>();
        CacheExpirationHours = 24;
    }

    /// <summary>
    /// Gets or sets TMDB API key for episode group access.
    /// </summary>
    public string TmdbApiKey { get; set; }

    /// <summary>
    /// Gets or sets how many hours fetched TMDB episode group data is cached before being re-fetched.
    /// </summary>
    public int CacheExpirationHours { get; set; }

    /// <summary>
    /// Gets or sets episode group configurations per series.
    /// </summary>
#pragma warning disable CA1002 // Do not expose generic lists - Required for XML serialization
#pragma warning disable CA2227 // Collection properties should be read only - Required for XML serialization
    public List<EpisodeGroupConfig> EpisodeGroupConfigs { get; set; }
#pragma warning restore CA2227
#pragma warning restore CA1002
}
