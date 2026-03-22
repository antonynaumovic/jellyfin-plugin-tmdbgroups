using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Services;

/// <summary>
/// Cache for TMDB episode group details to avoid repeated API calls.
/// </summary>
public interface ITmdbEpisodeGroupCache
{
    /// <summary>
    /// Gets episode group details, returning a cached copy if one exists and is still fresh,
    /// or fetching from TMDB if the cache is empty or expired.
    /// </summary>
    /// <param name="episodeGroupId">The episode group ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The episode group details.</returns>
    Task<TmdbEpisodeGroupDetails> GetOrFetchAsync(string episodeGroupId, CancellationToken cancellationToken);

    /// <summary>
    /// Forces a fresh fetch from TMDB for each of the given episode group IDs,
    /// replacing any existing cached entries.
    /// </summary>
    /// <param name="episodeGroupIds">The episode group IDs to fetch.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task WarmCacheAsync(IEnumerable<string> episodeGroupIds, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a single entry from the cache, forcing the next call to re-fetch from TMDB.
    /// </summary>
    /// <param name="episodeGroupId">The episode group ID to invalidate.</param>
    void Invalidate(string episodeGroupId);

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    void InvalidateAll();
}
