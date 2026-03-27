using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Services;

/// <summary>
/// In-memory cache for TMDB episode group details with a configurable TTL.
/// </summary>
public class TmdbEpisodeGroupCache : ITmdbEpisodeGroupCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fetchLocks = new(StringComparer.Ordinal);
    private readonly ITmdbApiClient _apiClient;
    private readonly ILogger<TmdbEpisodeGroupCache> _logger;
    private readonly Func<int> _expirationHoursProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbEpisodeGroupCache"/> class.
    /// </summary>
    /// <param name="apiClient">Instance of the <see cref="ITmdbApiClient"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TmdbEpisodeGroupCache}"/> interface.</param>
    public TmdbEpisodeGroupCache(ITmdbApiClient apiClient, ILogger<TmdbEpisodeGroupCache> logger)
        : this(apiClient, logger, () => Plugin.Instance?.PluginConfiguration?.CacheExpirationHours ?? 24)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbEpisodeGroupCache"/> class with a custom expiration provider (for testing).
    /// </summary>
    /// <param name="apiClient">Instance of the <see cref="ITmdbApiClient"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TmdbEpisodeGroupCache}"/> interface.</param>
    /// <param name="expirationHoursProvider">Function that returns the cache TTL in hours.</param>
    public TmdbEpisodeGroupCache(ITmdbApiClient apiClient, ILogger<TmdbEpisodeGroupCache> logger, Func<int> expirationHoursProvider)
    {
        _apiClient = apiClient;
        _logger = logger;
        _expirationHoursProvider = expirationHoursProvider;
    }

    /// <inheritdoc/>
    public async Task<TmdbEpisodeGroupDetails> GetOrFetchAsync(string episodeGroupId, CancellationToken cancellationToken)
    {
        var expirationHours = _expirationHoursProvider();

        if (TryGetValid(episodeGroupId, expirationHours, out var cached))
        {
            return cached;
        }

        var fetchLock = _fetchLocks.GetOrAdd(episodeGroupId, _ => new SemaphoreSlim(1, 1));
        await fetchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock — another thread may have fetched already
            if (TryGetValid(episodeGroupId, expirationHours, out cached))
            {
                return cached;
            }

            _logger.LogInformation(
                "[TMDbEpisodeGroups] Cache miss for episode group {EpisodeGroupId}, fetching from TMDB",
                episodeGroupId);

            return await FetchAndStoreAsync(episodeGroupId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            fetchLock.Release();
        }
    }

    private bool TryGetValid(string episodeGroupId, int expirationHours, out TmdbEpisodeGroupDetails details)
    {
        if (_cache.TryGetValue(episodeGroupId, out var entry))
        {
            var age = DateTime.UtcNow - entry.FetchedAt;
            if (age.TotalHours < expirationHours)
            {
                _logger.LogDebug(
                    "[TMDbEpisodeGroups] Cache hit for episode group {EpisodeGroupId} (age: {Age:F1}h, TTL: {Ttl}h)",
                    episodeGroupId,
                    age.TotalHours,
                    expirationHours);
                details = entry.Details;
                return true;
            }

            _logger.LogInformation(
                "[TMDbEpisodeGroups] Cache expired for episode group {EpisodeGroupId} (age: {Age:F1}h, TTL: {Ttl}h), re-fetching",
                episodeGroupId,
                age.TotalHours,
                expirationHours);
        }

        details = default;
        return false;
    }

    /// <inheritdoc/>
    public async Task WarmCacheAsync(IEnumerable<string> episodeGroupIds, CancellationToken cancellationToken)
    {
        foreach (var episodeGroupId in episodeGroupIds)
        {
            if (string.IsNullOrEmpty(episodeGroupId))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _logger.LogInformation("[TMDbEpisodeGroups] Warming cache for episode group {EpisodeGroupId}", episodeGroupId);
                await FetchAndStoreAsync(episodeGroupId, cancellationToken).ConfigureAwait(false);

                // Fixed 500ms delay to stay within TMDB's rate limit (~40 req/10s)
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TMDbEpisodeGroups] Error warming cache for episode group {EpisodeGroupId}", episodeGroupId);
            }
        }
    }

    /// <inheritdoc/>
    public void Invalidate(string episodeGroupId)
    {
        _cache.TryRemove(episodeGroupId, out _);
        _logger.LogInformation("[TMDbEpisodeGroups] Invalidated cache for episode group {EpisodeGroupId}", episodeGroupId);
    }

    /// <inheritdoc/>
    public void InvalidateAll()
    {
        _cache.Clear();
        _logger.LogInformation("[TMDbEpisodeGroups] Cleared entire TMDB episode group cache");
    }

    private async Task<TmdbEpisodeGroupDetails> FetchAndStoreAsync(string episodeGroupId, CancellationToken cancellationToken)
    {
        var details = await _apiClient.GetEpisodeGroupDetailsAsync(episodeGroupId, cancellationToken).ConfigureAwait(false);
        _cache[episodeGroupId] = new CacheEntry(details, DateTime.UtcNow);
        _logger.LogInformation("[TMDbEpisodeGroups] Cached episode group {EpisodeGroupId}", episodeGroupId);
        return details;
    }

    private sealed record CacheEntry(TmdbEpisodeGroupDetails Details, DateTime FetchedAt);
}
