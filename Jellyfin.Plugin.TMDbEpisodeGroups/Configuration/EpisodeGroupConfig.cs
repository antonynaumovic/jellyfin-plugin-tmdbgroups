namespace Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;

/// <summary>
/// Configuration for episode group per series.
/// </summary>
public class EpisodeGroupConfig
{
    /// <summary>
    /// Gets or sets TMDB Series ID.
    /// </summary>
    public string TmdbSeriesId { get; set; }

    /// <summary>
    /// Gets or sets the selected episode group ID.
    /// </summary>
    public string EpisodeGroupId { get; set; }

    /// <summary>
    /// Gets or sets the episode group name.
    /// </summary>
    public string EpisodeGroupName { get; set; }
}
