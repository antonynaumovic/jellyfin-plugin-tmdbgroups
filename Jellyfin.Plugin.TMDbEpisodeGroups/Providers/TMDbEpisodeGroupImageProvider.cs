using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Providers;

/// <summary>
/// Image provider for episodes from TMDB episode groups.
/// </summary>
public class TMDbEpisodeGroupImageProvider : IRemoteImageProvider, IHasOrder
{
    private const string TmdbImageBaseUrl = "https://image.tmdb.org/t/p/original";

    private readonly ITmdbEpisodeGroupCache _episodeGroupCache;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<TMDbEpisodeGroupImageProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TMDbEpisodeGroupImageProvider"/> class.
    /// </summary>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TMDbEpisodeGroupImageProvider}"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    public TMDbEpisodeGroupImageProvider(
        ITmdbEpisodeGroupCache episodeGroupCache,
        ILibraryManager libraryManager,
        ILogger<TMDbEpisodeGroupImageProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _episodeGroupCache = episodeGroupCache;
        _libraryManager = libraryManager;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public string Name => "TMDb Episode Groups";

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public bool Supports(BaseItem item) => item is Episode;

    /// <inheritdoc/>
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        => [ImageType.Primary];

    /// <inheritdoc/>
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var episode = (Episode)item;

        if (episode.ParentIndexNumber is null or 0 || episode.IndexNumber is null)
        {
            return [];
        }

        var series = _libraryManager.GetItemById(episode.SeriesId) as Series;
        var seriesTmdbId = series?.GetProviderId(MetadataProvider.Tmdb);
        if (string.IsNullOrEmpty(seriesTmdbId))
        {
            return [];
        }

        var episodeGroupId = series!.GetProviderId(TmdbEpisodeGroupExternalId.EpisodeGroupProviderId);
        if (string.IsNullOrEmpty(episodeGroupId))
        {
            episodeGroupId = Plugin.Instance?.PluginConfiguration?.EpisodeGroupConfigs
                ?.FirstOrDefault(c => c.TmdbSeriesId == seriesTmdbId)?.EpisodeGroupId;
        }

        if (string.IsNullOrEmpty(episodeGroupId))
        {
            return [];
        }

        try
        {
            var episodeGroupDetails = await _episodeGroupCache.GetOrFetchAsync(episodeGroupId, cancellationToken).ConfigureAwait(false);

            var seasonIdx = episode.ParentIndexNumber.Value;
            var episodeIdx = episode.IndexNumber.Value;

            if (seasonIdx < 1 || seasonIdx > episodeGroupDetails.Groups.Count)
            {
                return [];
            }

            var group = episodeGroupDetails.Groups[seasonIdx - 1];
            if (episodeIdx < 1 || episodeIdx > group.Episodes.Count)
            {
                return [];
            }

            var tmdbEpisode = group.Episodes[episodeIdx - 1];
            if (string.IsNullOrEmpty(tmdbEpisode.StillPath))
            {
                return [];
            }

            _logger.LogDebug(
                "[TMDbEpisodeGroups] Found still image for S{Season}E{Episode}",
                seasonIdx,
                episodeIdx);

            return
            [
                new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = TmdbImageBaseUrl + tmdbEpisode.StillPath
                }
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error fetching image for S{Season}E{Episode}", episode.ParentIndexNumber, episode.IndexNumber);
            return [];
        }
    }

    /// <inheritdoc/>
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        return httpClient.GetAsync(new Uri(url), cancellationToken);
    }
}
