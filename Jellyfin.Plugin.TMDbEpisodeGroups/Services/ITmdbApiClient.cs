using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Services;

/// <summary>
/// Interface for TMDB API client.
/// </summary>
public interface ITmdbApiClient
{
    /// <summary>
    /// Gets episode groups for a TV series.
    /// </summary>
    /// <param name="tmdbSeriesId">The TMDB series ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TmdbEpisodeGroupList"/> containing available episode groups.</returns>
    Task<TmdbEpisodeGroupList> GetEpisodeGroupsAsync(string tmdbSeriesId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets detailed episode group information including all episodes.
    /// </summary>
    /// <param name="episodeGroupId">The episode group ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TmdbEpisodeGroupDetails"/> containing full episode group information.</returns>
    Task<TmdbEpisodeGroupDetails> GetEpisodeGroupDetailsAsync(string episodeGroupId, CancellationToken cancellationToken);
}
