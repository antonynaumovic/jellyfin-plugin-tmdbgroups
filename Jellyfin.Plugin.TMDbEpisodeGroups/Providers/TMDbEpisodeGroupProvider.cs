using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Providers;

/// <summary>
/// Metadata provider for episodes from TMDB episode groups.
/// </summary>
public class TMDbEpisodeGroupProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
{
    private readonly ITmdbEpisodeGroupCache _episodeGroupCache;
    private readonly ILogger<TMDbEpisodeGroupProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TMDbEpisodeGroupProvider"/> class.
    /// </summary>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TMDbEpisodeGroupProvider}"/> interface.</param>
    public TMDbEpisodeGroupProvider(
        ITmdbEpisodeGroupCache episodeGroupCache,
        ILogger<TMDbEpisodeGroupProvider> logger)
    {
        _episodeGroupCache = episodeGroupCache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "TMDb Episode Groups";

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
    {
        // This provider doesn't support search
        return Array.Empty<RemoteSearchResult>();
    }

    /// <inheritdoc/>
    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Episode>();

        _logger.LogInformation(
            "[TMDbEpisodeGroups] GetMetadata called for episode S{Season}E{Episode}",
            info.ParentIndexNumber,
            info.IndexNumber);

        if (info.ParentIndexNumber is null or 0)
        {
            _logger.LogDebug("[TMDbEpisodeGroups] Skipping specials (season 0)");
            return result;
        }

        try
        {
            // Get the series TMDB ID
            var seriesTmdbId = info.SeriesProviderIds?.GetValueOrDefault(MetadataProvider.Tmdb.ToString());
            if (string.IsNullOrEmpty(seriesTmdbId))
            {
                _logger.LogDebug("[TMDbEpisodeGroups] No TMDB ID found for series");
                return result;
            }

            _logger.LogDebug("[TMDbEpisodeGroups] Series TMDB ID: {TmdbId}", seriesTmdbId);

            // Check for episode group ID in series external IDs first, fall back to plugin config
            var episodeGroupId = info.SeriesProviderIds?.GetValueOrDefault(TmdbEpisodeGroupExternalId.EpisodeGroupProviderId);
            if (string.IsNullOrEmpty(episodeGroupId))
            {
                var config = Plugin.Instance?.PluginConfiguration?.EpisodeGroupConfigs
                    ?.FirstOrDefault(c => c.TmdbSeriesId == seriesTmdbId);
                episodeGroupId = config?.EpisodeGroupId;
            }

            if (string.IsNullOrEmpty(episodeGroupId))
            {
                _logger.LogDebug("[TMDbEpisodeGroups] No episode group configured for series TMDB ID {TmdbId}", seriesTmdbId);
                return result;
            }

            _logger.LogInformation("[TMDbEpisodeGroups] Using episode group: {GroupId}", episodeGroupId);

            // Fetch episode group details (from cache or TMDB)
            var episodeGroupDetails = await _episodeGroupCache.GetOrFetchAsync(
                episodeGroupId,
                cancellationToken).ConfigureAwait(false);

            // Map season/episode to group position (1-indexed), same logic as EpisodeGroupMetadataManager
            var seasonIdx = info.ParentIndexNumber!.Value;
            var episodeIdx = info.IndexNumber.GetValueOrDefault();

            if (episodeIdx < 1 || seasonIdx > episodeGroupDetails.Groups.Count)
            {
                _logger.LogDebug(
                    "[TMDbEpisodeGroups] S{Season}E{Episode} is out of range for episode group",
                    seasonIdx,
                    episodeIdx);
                return result;
            }

            var group = episodeGroupDetails.Groups[seasonIdx - 1];
            if (episodeIdx > group.Episodes.Count)
            {
                _logger.LogDebug(
                    "[TMDbEpisodeGroups] Episode {Episode} is out of range for group {Season}",
                    episodeIdx,
                    seasonIdx);
                return result;
            }

            var tmdbEpisode = group.Episodes[episodeIdx - 1];

            result.Item = new Episode
            {
                Name = tmdbEpisode.Name,
                Overview = tmdbEpisode.Overview,
                IndexNumber = info.IndexNumber,
                ParentIndexNumber = info.ParentIndexNumber
            };

            if (tmdbEpisode.Id > 0)
            {
                result.Item.SetProviderId(MetadataProvider.Tmdb, tmdbEpisode.Id.ToString(CultureInfo.InvariantCulture));
            }

            result.HasMetadata = true;

            _logger.LogInformation(
                "[TMDbEpisodeGroups] Found metadata for S{Season}E{Episode} - {Name}",
                info.ParentIndexNumber,
                info.IndexNumber,
                tmdbEpisode.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error fetching episode metadata from TMDB episode group");
        }

        return result;
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        // This provider doesn't provide images
        throw new NotImplementedException();
    }
}
