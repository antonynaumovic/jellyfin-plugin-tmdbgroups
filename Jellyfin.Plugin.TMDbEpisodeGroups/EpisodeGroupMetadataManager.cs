using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups;

/// <summary>
/// Manages episode metadata updates from TMDB episode groups.
/// </summary>
public class EpisodeGroupMetadataManager
{
    private readonly ILibraryManager _libraryManager;
    private readonly ITmdbEpisodeGroupCache _episodeGroupCache;
    private readonly ILogger<EpisodeGroupMetadataManager> _logger;
    private readonly Func<IList<EpisodeGroupConfig>> _configProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeGroupMetadataManager"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{EpisodeGroupMetadataManager}"/> interface.</param>
    public EpisodeGroupMetadataManager(
        ILibraryManager libraryManager,
        ITmdbEpisodeGroupCache episodeGroupCache,
        ILogger<EpisodeGroupMetadataManager> logger)
        : this(libraryManager, episodeGroupCache, logger, () => Plugin.Instance?.PluginConfiguration?.EpisodeGroupConfigs ?? new List<EpisodeGroupConfig>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeGroupMetadataManager"/> class with a custom config provider (for testing).
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{EpisodeGroupMetadataManager}"/> interface.</param>
    /// <param name="configProvider">Function that provides the episode group configurations.</param>
    public EpisodeGroupMetadataManager(
        ILibraryManager libraryManager,
        ITmdbEpisodeGroupCache episodeGroupCache,
        ILogger<EpisodeGroupMetadataManager> logger,
        Func<IList<EpisodeGroupConfig>> configProvider)
    {
        _libraryManager = libraryManager;
        _episodeGroupCache = episodeGroupCache;
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
        _logger.LogInformation("[TMDbEpisodeGroups] RefreshSeriesEpisodeMetadata called for series TMDB ID: {TmdbSeriesId}", tmdbSeriesId);

        // Get the series from Jellyfin library first to check for external ID
        _logger.LogDebug("[TMDbEpisodeGroups] Searching for series in Jellyfin library");
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

        _logger.LogInformation("[TMDbEpisodeGroups] Found series: {SeriesName}", series.Name);

        // Log all provider IDs for debugging
        _logger.LogDebug("[TMDbEpisodeGroups] Series has {Count} provider IDs", series.ProviderIds.Count);
        foreach (var providerId in series.ProviderIds)
        {
            _logger.LogDebug("[TMDbEpisodeGroups] Provider ID: {Key} = {Value}", providerId.Key, providerId.Value);
        }

        // Check for episode group ID in series external IDs first
        var episodeGroupId = series.GetProviderId("TmdbEpisodeGroup");
        _logger.LogDebug("[TMDbEpisodeGroups] Episode group ID from external ID: {EpisodeGroupId}", episodeGroupId ?? "(null)");

        // Fall back to plugin configuration if not set on series
        if (string.IsNullOrEmpty(episodeGroupId))
        {
            var config = _configProvider()
                .FirstOrDefault(c => c.TmdbSeriesId == tmdbSeriesId);
            episodeGroupId = config?.EpisodeGroupId;
            _logger.LogDebug("[TMDbEpisodeGroups] Episode group ID from plugin config: {EpisodeGroupId}", episodeGroupId ?? "(null)");
        }

        if (string.IsNullOrEmpty(episodeGroupId))
        {
            _logger.LogInformation("[TMDbEpisodeGroups] No episode group configured for series {TmdbSeriesId}", tmdbSeriesId);
            return;
        }

        _logger.LogInformation(
            "[TMDbEpisodeGroups] Refreshing episode metadata for series {TmdbSeriesId} using episode group {EpisodeGroupId}",
            tmdbSeriesId,
            episodeGroupId);

        try
        {
            // Fetch episode group details (from cache or TMDB)
            _logger.LogDebug("[TMDbEpisodeGroups] Fetching episode group details (from cache or TMDB)");
            var episodeGroupDetails = await _episodeGroupCache.GetOrFetchAsync(
                episodeGroupId,
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[TMDbEpisodeGroups] Found {GroupCount} groups in episode group", episodeGroupDetails.Groups?.Count ?? 0);

            // Get all episodes for this series
            var episodes = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Episode],
                AncestorIds = [series.Id],
                Recursive = true
            }).Select(e => e as Episode).Where(e => e != null).ToList();

            _logger.LogInformation("[TMDbEpisodeGroups] Found {Count} episodes for series", episodes.Count);

            // Log sample of Jellyfin episodes for debugging
            if (episodes.Count > 0)
            {
                var sampleCount = Math.Min(5, episodes.Count);
                _logger.LogDebug("[TMDbEpisodeGroups] Sample of Jellyfin episodes (showing first {Count}):", sampleCount);
                for (int i = 0; i < sampleCount; i++)
                {
                    var episode = episodes[i];
                    var tmdbId = episode.GetProviderId(MetadataProvider.Tmdb);
                    _logger.LogInformation(
                        "[TMDbEpisodeGroups]   Episode: S{SeasonNumber}E{EpisodeNumber} - {EpisodeName}, TMDB ID: {TmdbId}",
                        episode.ParentIndexNumber,
                        episode.IndexNumber,
                        episode.Name,
                        tmdbId ?? "(none)");
                }
            }

            // Map episode group data to Jellyfin episodes
            int updatedCount = 0;
            int groupIndex = 0;
            foreach (var group in episodeGroupDetails.Groups)
            {
                groupIndex++;
                _logger.LogInformation(
                    "[TMDbEpisodeGroups] Processing group {GroupIndex}/{TotalGroups}: {GroupName} with {EpisodeCount} episodes",
                    groupIndex,
                    episodeGroupDetails.Groups.Count,
                    group.Name,
                    group.Episodes.Count);

                int episodeIndex = 0;
                foreach (var tmdbEpisode in group.Episodes)
                {
                    episodeIndex++;
                    _logger.LogInformation(
                        "[TMDbEpisodeGroups] Looking for S{SeasonNumber}E{EpisodeNumber} - {EpisodeName} (original: S{OriginalSeason}E{OriginalEpisode})",
                        groupIndex,
                        episodeIndex,
                        tmdbEpisode.Name,
                        tmdbEpisode.SeasonNumber,
                        tmdbEpisode.EpisodeNumber);

                    // Find matching episode by season/episode number using group index and position
                    var matchingEpisode = episodes.FirstOrDefault(e =>
                        e.ParentIndexNumber == groupIndex &&
                        e.IndexNumber == episodeIndex);

                    if (matchingEpisode != null)
                    {
                        _logger.LogInformation(
                            "[TMDbEpisodeGroups] Found match: S{SeasonNumber}E{EpisodeNumber} - {EpisodeName}",
                            groupIndex,
                            episodeIndex,
                            matchingEpisode.Name);

                        // Update episode metadata
                        bool updated = false;

                        // Update name
                        if (matchingEpisode.Name != tmdbEpisode.Name && !string.IsNullOrEmpty(tmdbEpisode.Name))
                        {
                            _logger.LogInformation(
                                "[TMDbEpisodeGroups] Updating name: '{OldName}' -> '{NewName}'",
                                matchingEpisode.Name,
                                tmdbEpisode.Name);
                            matchingEpisode.Name = tmdbEpisode.Name;
                            updated = true;
                        }

                        // Update overview
                        if (matchingEpisode.Overview != tmdbEpisode.Overview && !string.IsNullOrEmpty(tmdbEpisode.Overview))
                        {
                            _logger.LogInformation("[TMDbEpisodeGroups] Updating overview");
                            matchingEpisode.Overview = tmdbEpisode.Overview;
                            updated = true;
                        }

                        // Update TMDB episode ID
                        var currentTmdbId = matchingEpisode.GetProviderId(MetadataProvider.Tmdb);
                        var newTmdbId = tmdbEpisode.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        if (currentTmdbId != newTmdbId)
                        {
                            _logger.LogInformation(
                                "[TMDbEpisodeGroups] Updating TMDB ID: '{OldId}' -> '{NewId}'",
                                currentTmdbId ?? "(none)",
                                newTmdbId);
                            matchingEpisode.SetProviderId(MetadataProvider.Tmdb, newTmdbId);
                            updated = true;
                        }

                        // Update air date
                        if (!string.IsNullOrEmpty(tmdbEpisode.AirDate) && DateTime.TryParse(tmdbEpisode.AirDate, out var airDate))
                        {
                            if (matchingEpisode.PremiereDate != airDate)
                            {
                                _logger.LogInformation(
                                    "[TMDbEpisodeGroups] Updating air date: '{OldDate}' -> '{NewDate}'",
                                    matchingEpisode.PremiereDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "(none)",
                                    airDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                                matchingEpisode.PremiereDate = airDate;
                                updated = true;
                            }
                        }

                        // Update runtime
                        if (tmdbEpisode.Runtime.HasValue && tmdbEpisode.Runtime.Value > 0)
                        {
                            var runtimeTicks = TimeSpan.FromMinutes(tmdbEpisode.Runtime.Value).Ticks;
                            if (matchingEpisode.RunTimeTicks != runtimeTicks)
                            {
                                _logger.LogInformation(
                                    "[TMDbEpisodeGroups] Updating runtime: {OldRuntime} -> {NewRuntime} minutes",
                                    matchingEpisode.RunTimeTicks.HasValue ? TimeSpan.FromTicks(matchingEpisode.RunTimeTicks.Value).TotalMinutes : 0,
                                    tmdbEpisode.Runtime.Value);
                                matchingEpisode.RunTimeTicks = runtimeTicks;
                                updated = true;
                            }
                        }

                        // Update still path (primary image)
                        if (!string.IsNullOrEmpty(tmdbEpisode.StillPath))
                        {
                            var imageUrl = $"https://image.tmdb.org/t/p/original{tmdbEpisode.StillPath}";
                            _logger.LogInformation(
                                "[TMDbEpisodeGroups] Episode has still path, will be fetched by image provider: {StillPath}",
                                tmdbEpisode.StillPath);
                            // Note: Image downloading is handled by Jellyfin's image providers
                            // We just ensure the TMDB ID is set so the image provider can fetch it
                        }

                        if (updated)
                        {
                            await _libraryManager.UpdateItemAsync(
                                matchingEpisode,
                                matchingEpisode.GetParent(),
                                ItemUpdateType.MetadataEdit,
                                cancellationToken).ConfigureAwait(false);

                            updatedCount++;
                            _logger.LogInformation(
                                "[TMDbEpisodeGroups] Updated episode S{SeasonNumber}E{EpisodeNumber}: {EpisodeName}",
                                groupIndex,
                                episodeIndex,
                                tmdbEpisode.Name);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "[TMDbEpisodeGroups] No update needed for S{Season}E{Episode} - metadata already matches",
                                groupIndex,
                                episodeIndex);
                        }
                    }
                    else
                    {
                        _logger.LogInformation(
                            "[TMDbEpisodeGroups] No matching episode found for S{SeasonNumber}E{EpisodeNumber} - {EpisodeName}",
                            groupIndex,
                            episodeIndex,
                            tmdbEpisode.Name);
                    }
                }
            }

            _logger.LogInformation(
                "[TMDbEpisodeGroups] Updated metadata for {UpdatedCount} episodes in series {TmdbSeriesId}",
                updatedCount,
                tmdbSeriesId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TMDbEpisodeGroups] Error refreshing episode metadata for series {TmdbSeriesId}",
                tmdbSeriesId);
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

        if (string.IsNullOrEmpty(episodeGroupId))
        {
            series.ProviderIds.Remove("TmdbEpisodeGroup");
            _logger.LogInformation(
                "[TMDbEpisodeGroups] Cleared TmdbEpisodeGroup provider ID from series {SeriesName}",
                series.Name);
        }
        else
        {
            series.SetProviderId("TmdbEpisodeGroup", episodeGroupId);
            _logger.LogInformation(
                "[TMDbEpisodeGroups] Set TmdbEpisodeGroup provider ID on series {SeriesName} to {EpisodeGroupId}",
                series.Name,
                episodeGroupId);
        }

        await _libraryManager.UpdateItemAsync(
            series,
            series.GetParent(),
            ItemUpdateType.MetadataEdit,
            cancellationToken).ConfigureAwait(false);
    }
}
