using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Providers;

/// <summary>
/// Metadata provider for episodes from TMDB episode groups.
/// </summary>
public class TMDbEpisodeGroupProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    private readonly ITmdbEpisodeGroupCache _episodeGroupCache;
    private readonly ILogger<TMDbEpisodeGroupProvider> _logger;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TMDbEpisodeGroupProvider"/> class.
    /// </summary>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TMDbEpisodeGroupProvider}"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public TMDbEpisodeGroupProvider(
        ITmdbEpisodeGroupCache episodeGroupCache,
        ILogger<TMDbEpisodeGroupProvider> logger,
        ILibraryManager libraryManager)
    {
        _episodeGroupCache = episodeGroupCache;
        _logger = logger;
        _libraryManager = libraryManager;
    }

    /// <inheritdoc/>
    public string Name => "TMDb Episode Groups";

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

            // Check for episode group ID in series external IDs first
            var episodeGroupId = info.SeriesProviderIds?.GetValueOrDefault("TmdbEpisodeGroup");

            // Fall back to plugin configuration if not set on series
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

            // Get TMDB API key
            var apiKey = Plugin.Instance?.PluginConfiguration?.TmdbApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("[TMDbEpisodeGroups] TMDB API key not configured");
                return result;
            }

            // Fetch episode group details (from cache or TMDB)
            var episodeGroupDetails = await _episodeGroupCache.GetOrFetchAsync(
                episodeGroupId,
                cancellationToken).ConfigureAwait(false);

            // Find the matching episode in the episode group
            foreach (var group in episodeGroupDetails.Groups)
            {
                var tmdbEpisode = group.Episodes.FirstOrDefault(e =>
                    e.SeasonNumber == info.ParentIndexNumber &&
                    e.EpisodeNumber == info.IndexNumber);

                if (tmdbEpisode != null)
                {
                    result.Item = new Episode
                    {
                        Name = tmdbEpisode.Name,
                        Overview = tmdbEpisode.Overview,
                        IndexNumber = info.IndexNumber,
                        ParentIndexNumber = info.ParentIndexNumber
                    };

                    // Set TMDB episode ID if available
                    if (tmdbEpisode.Id > 0)
                    {
                        result.Item.SetProviderId(MetadataProvider.Tmdb, tmdbEpisode.Id.ToString(CultureInfo.InvariantCulture));
                    }

                    result.HasMetadata = true;

                    _logger.LogInformation(
                        "Found episode metadata from episode group: S{Season}E{Episode} - {Name}",
                        info.ParentIndexNumber,
                        info.IndexNumber,
                        tmdbEpisode.Name);

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching episode metadata from TMDB episode group");
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
