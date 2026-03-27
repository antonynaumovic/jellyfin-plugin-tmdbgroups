using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;
using Jellyfin.Plugin.TMDbEpisodeGroups.Providers;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups;

/// <summary>
/// Manages episode metadata updates from TMDB episode groups.
/// </summary>
public class EpisodeGroupMetadataManager
{
    private const string TmdbImageBaseUrl = "https://image.tmdb.org/t/p/original";

    private readonly ILibraryManager _libraryManager;
    private readonly ITmdbEpisodeGroupCache _episodeGroupCache;
    private readonly IProviderManager _providerManager;
    private readonly ILogger<EpisodeGroupMetadataManager> _logger;
    private readonly Func<IList<EpisodeGroupConfig>> _configProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeGroupMetadataManager"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{EpisodeGroupMetadataManager}"/> interface.</param>
    public EpisodeGroupMetadataManager(
        ILibraryManager libraryManager,
        ITmdbEpisodeGroupCache episodeGroupCache,
        IProviderManager providerManager,
        ILogger<EpisodeGroupMetadataManager> logger)
        : this(libraryManager, episodeGroupCache, providerManager, logger, () => Plugin.Instance?.PluginConfiguration?.EpisodeGroupConfigs ?? new List<EpisodeGroupConfig>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeGroupMetadataManager"/> class with a custom config provider (for testing).
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{EpisodeGroupMetadataManager}"/> interface.</param>
    /// <param name="configProvider">Function that provides the episode group configurations.</param>
    public EpisodeGroupMetadataManager(
        ILibraryManager libraryManager,
        ITmdbEpisodeGroupCache episodeGroupCache,
        IProviderManager providerManager,
        ILogger<EpisodeGroupMetadataManager> logger,
        Func<IList<EpisodeGroupConfig>> configProvider)
    {
        _libraryManager = libraryManager;
        _episodeGroupCache = episodeGroupCache;
        _providerManager = providerManager;
        _logger = logger;
        _configProvider = configProvider;
    }

    /// <summary>
    /// Refreshes episode metadata for a series based on configured episode group.
    /// </summary>
    /// <param name="tmdbSeriesId">The TMDB series ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RefreshSeriesEpisodeMetadata(string tmdbSeriesId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[TMDbEpisodeGroups] Refreshing metadata for series {TmdbSeriesId}", tmdbSeriesId);

        var seriesList = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Series],
            HasTmdbId = true,
            Recursive = false
        });

        var series = seriesList?.FirstOrDefault(s => s.GetProviderId(MetadataProvider.Tmdb) == tmdbSeriesId);
        if (series == null)
        {
            _logger.LogWarning("[TMDbEpisodeGroups] Series with TMDB ID {TmdbSeriesId} not found in library", tmdbSeriesId);
            return;
        }

        // Check for episode group ID in series external IDs first, fall back to plugin config
        var episodeGroupId = series.GetProviderId(TmdbEpisodeGroupExternalId.EpisodeGroupProviderId);
        if (string.IsNullOrEmpty(episodeGroupId))
        {
            episodeGroupId = _configProvider()
                .FirstOrDefault(c => c.TmdbSeriesId == tmdbSeriesId)?.EpisodeGroupId;
        }

        if (string.IsNullOrEmpty(episodeGroupId))
        {
            _logger.LogInformation("[TMDbEpisodeGroups] No episode group configured for series {TmdbSeriesId}", tmdbSeriesId);
            return;
        }

        _logger.LogInformation(
            "[TMDbEpisodeGroups] Using episode group {EpisodeGroupId} for {SeriesName}",
            episodeGroupId,
            series.Name);

        try
        {
            var episodeGroupDetails = await _episodeGroupCache.GetOrFetchAsync(episodeGroupId, cancellationToken).ConfigureAwait(false);

            var episodes = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Episode],
                AncestorIds = [series.Id],
                Recursive = true
            }).Cast<Episode>().ToList();

            _logger.LogInformation("[TMDbEpisodeGroups] Matching {EpisodeCount} episodes against episode group", episodes.Count);

            // Pre-build a lookup by (season, episode) position for O(1) matching
            var episodeByPosition = episodes
                .Where(e => e.ParentIndexNumber.HasValue && e.ParentIndexNumber.Value > 0 && e.IndexNumber.HasValue)
                .ToDictionary(e => (e.ParentIndexNumber!.Value, e.IndexNumber!.Value));

            int updatedCount = 0;
            int groupIndex = 0;
            foreach (var group in episodeGroupDetails.Groups)
            {
                groupIndex++;
                int episodeIndex = 0;
                foreach (var tmdbEpisode in group.Episodes)
                {
                    episodeIndex++;
                    if (!episodeByPosition.TryGetValue((groupIndex, episodeIndex), out var matchingEpisode))
                    {
                        _logger.LogDebug(
                            "[TMDbEpisodeGroups] No match for S{Season}E{Episode} ({Name})",
                            groupIndex,
                            episodeIndex,
                            tmdbEpisode.Name);
                        continue;
                    }

                    bool updated = false;

                    if (!string.IsNullOrEmpty(tmdbEpisode.Name) && matchingEpisode.Name != tmdbEpisode.Name)
                    {
                        _logger.LogDebug(
                            "[TMDbEpisodeGroups] S{Season}E{Episode}: name '{Old}' -> '{New}'",
                            groupIndex,
                            episodeIndex,
                            matchingEpisode.Name,
                            tmdbEpisode.Name);
                        matchingEpisode.Name = tmdbEpisode.Name;
                        updated = true;
                    }

                    if (!string.IsNullOrEmpty(tmdbEpisode.Overview) && matchingEpisode.Overview != tmdbEpisode.Overview)
                    {
                        _logger.LogDebug("[TMDbEpisodeGroups] S{Season}E{Episode}: updating overview", groupIndex, episodeIndex);
                        matchingEpisode.Overview = tmdbEpisode.Overview;
                        updated = true;
                    }

                    var newTmdbId = tmdbEpisode.Id.ToString(CultureInfo.InvariantCulture);
                    if (matchingEpisode.GetProviderId(MetadataProvider.Tmdb) != newTmdbId)
                    {
                        matchingEpisode.SetProviderId(MetadataProvider.Tmdb, newTmdbId);
                        updated = true;
                    }

                    if (!string.IsNullOrEmpty(tmdbEpisode.AirDate) &&
                        DateTime.TryParse(tmdbEpisode.AirDate, out var airDate) &&
                        matchingEpisode.PremiereDate != airDate)
                    {
                        matchingEpisode.PremiereDate = airDate;
                        updated = true;
                    }

                    if (updated)
                    {
                        await _libraryManager.UpdateItemAsync(
                            matchingEpisode,
                            matchingEpisode.GetParent(),
                            ItemUpdateType.MetadataEdit,
                            cancellationToken).ConfigureAwait(false);
                        updatedCount++;
                    }

                    if (!string.IsNullOrEmpty(tmdbEpisode.StillPath))
                    {
                        try
                        {
                            await _providerManager.SaveImage(
                                matchingEpisode,
                                TmdbImageBaseUrl + tmdbEpisode.StillPath,
                                ImageType.Primary,
                                null,
                                cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "[TMDbEpisodeGroups] Failed to save image for S{Season}E{Episode}",
                                groupIndex,
                                episodeIndex);
                        }
                    }
                }
            }

            _logger.LogInformation(
                "[TMDbEpisodeGroups] Updated {UpdatedCount} episodes for series {TmdbSeriesId}",
                updatedCount,
                tmdbSeriesId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error refreshing metadata for series {TmdbSeriesId}", tmdbSeriesId);
            throw;
        }
    }

    /// <summary>
    /// Refreshes metadata for all series with configured episode groups.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RefreshAllConfiguredSeries(CancellationToken cancellationToken)
    {
        var configs = _configProvider();

        _logger.LogInformation("Refreshing episode metadata for {Count} configured series", configs.Count);

        foreach (var config in configs)
        {
            try
            {
                await RefreshSeriesEpisodeMetadata(config.TmdbSeriesId, cancellationToken).ConfigureAwait(false);

                // Add a small delay to avoid overwhelming the TMDB API
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error refreshing episode metadata for series {TmdbSeriesId}",
                    config.TmdbSeriesId);
                // Continue with other series even if one fails
            }
        }

        _logger.LogInformation("Completed refreshing all configured series");
    }

    /// <summary>
    /// Writes an episode group ID to a series item's provider IDs so it is stored in Jellyfin's metadata.
    /// Pass an empty or null <paramref name="episodeGroupId"/> to clear the value.
    /// </summary>
    /// <param name="jellyfinSeriesId">The Jellyfin item GUID of the series.</param>
    /// <param name="episodeGroupId">The TMDB episode group ID to set, or null/empty to clear.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetSeriesEpisodeGroupIdAsync(string jellyfinSeriesId, string episodeGroupId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(jellyfinSeriesId, out var seriesGuid))
        {
            _logger.LogError("[TMDbEpisodeGroups] Invalid series GUID: {SeriesId}", jellyfinSeriesId);
            return;
        }

        var series = _libraryManager.GetItemById(seriesGuid) as Series;
        if (series == null)
        {
            _logger.LogWarning("[TMDbEpisodeGroups] Series {SeriesId} not found in library", jellyfinSeriesId);
            return;
        }

        // SetProviderId with an empty string removes the key; with a value it sets it
        series.SetProviderId(TmdbEpisodeGroupExternalId.EpisodeGroupProviderId, episodeGroupId ?? string.Empty);

        _logger.LogInformation(
            "[TMDbEpisodeGroups] {Action} TmdbEpisodeGroup on {SeriesName}: {Value}",
            string.IsNullOrEmpty(episodeGroupId) ? "Cleared" : "Set",
            series.Name,
            episodeGroupId ?? "(cleared)");

        await _libraryManager.UpdateItemAsync(
            series,
            series.GetParent(),
            ItemUpdateType.MetadataEdit,
            cancellationToken).ConfigureAwait(false);
    }
}
