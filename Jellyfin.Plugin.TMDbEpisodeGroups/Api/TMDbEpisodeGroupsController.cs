using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Api;

/// <summary>
/// The TMDb Episode Groups API controller.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class TMDbEpisodeGroupsController : ControllerBase
{
    private readonly ITmdbApiClient _tmdbClient;
    private readonly ITmdbEpisodeGroupCache _episodeGroupCache;
    private readonly EpisodeGroupMetadataManager _episodeGroupManager;
    private readonly ILogger<TMDbEpisodeGroupsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TMDbEpisodeGroupsController"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{TMDbEpisodeGroupsController}"/> interface.</param>
    /// <param name="tmdbClient">Instance of the <see cref="ITmdbApiClient"/> interface.</param>
    /// <param name="episodeGroupCache">Instance of the <see cref="ITmdbEpisodeGroupCache"/> interface.</param>
    /// <param name="episodeGroupManager">Instance of the <see cref="EpisodeGroupMetadataManager"/> class.</param>
    public TMDbEpisodeGroupsController(
        ILogger<TMDbEpisodeGroupsController> logger,
        ITmdbApiClient tmdbClient,
        ITmdbEpisodeGroupCache episodeGroupCache,
        EpisodeGroupMetadataManager episodeGroupManager)
    {
        _tmdbClient = tmdbClient;
        _episodeGroupCache = episodeGroupCache;
        _episodeGroupManager = episodeGroupManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets available episode groups for a TV series.
    /// </summary>
    /// <param name="tmdbSeriesId">TMDB series ID.</param>
    /// <response code="200">Episode groups retrieved successfully.</response>
    /// <returns>List of episode groups.</returns>
    [HttpGet("EpisodeGroups/{tmdbSeriesId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TmdbEpisodeGroup>>> GetEpisodeGroups(string tmdbSeriesId)
    {
        _logger.LogInformation("[TMDbEpisodeGroups] API call: GetEpisodeGroups for series TMDB ID: {TmdbSeriesId}", tmdbSeriesId);

        try
        {
            _logger.LogDebug("[TMDbEpisodeGroups] Calling TMDB API client to fetch episode groups");
            var episodeGroups = await _tmdbClient.GetEpisodeGroupsAsync(tmdbSeriesId, HttpContext.RequestAborted)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "[TMDbEpisodeGroups] Successfully fetched {Count} episode groups for series {TmdbSeriesId}",
                episodeGroups.Results?.Count ?? 0,
                tmdbSeriesId);

            // Log first group for debugging
            if (episodeGroups.Results != null && episodeGroups.Results.Count > 0)
            {
                var firstGroup = episodeGroups.Results[0];
                _logger.LogDebug(
                    "[TMDbEpisodeGroups] First group: Id={Id}, Name={Name}, EpisodeCount={EpisodeCount}",
                    firstGroup.Id,
                    firstGroup.Name,
                    firstGroup.EpisodeCount);
            }

            return Ok(episodeGroups.Results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error fetching episode groups for series {TmdbSeriesId}", tmdbSeriesId);
            return StatusCode(500, new { error = "Failed to fetch episode groups", message = ex.Message });
        }
    }

    /// <summary>
    /// Refreshes episode metadata for a series using configured episode group.
    /// </summary>
    /// <param name="tmdbSeriesId">TMDB series ID. </param>
    /// <response code="204">Episode metadata refresh started successfully.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("RefreshEpisodeMetadata/{tmdbSeriesId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RefreshEpisodeMetadata(string tmdbSeriesId)
    {
        _logger.LogInformation("[TMDbEpisodeGroups] API call: RefreshEpisodeMetadata for series TMDB ID: {TmdbSeriesId}", tmdbSeriesId);

        try
        {
            _logger.LogDebug("[TMDbEpisodeGroups] Calling metadata manager to refresh episodes");
            await _episodeGroupManager.RefreshSeriesEpisodeMetadata(tmdbSeriesId, HttpContext.RequestAborted)
                .ConfigureAwait(false);
            _logger.LogInformation("[TMDbEpisodeGroups] Completed episode metadata refresh for series {TmdbSeriesId}", tmdbSeriesId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error refreshing episode metadata for series {TmdbSeriesId}", tmdbSeriesId);
            return StatusCode(500, new { error = "Failed to refresh episode metadata", message = ex.Message });
        }
    }

    /// <summary>
    /// Refreshes episode metadata for all configured series.
    /// </summary>
    /// <response code="204">All episode metadata refresh started successfully.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("RefreshAllEpisodeMetadata")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RefreshAllEpisodeMetadata()
    {
        _logger.LogInformation("Starting episode metadata refresh for all configured series");

        try
        {
            await _episodeGroupManager.RefreshAllConfiguredSeries(HttpContext.RequestAborted)
                .ConfigureAwait(false);
            _logger.LogInformation("Completed episode metadata refresh for all series");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing all episode metadata");
            return StatusCode(500, "Failed to refresh episode metadata");
        }
    }

    /// <summary>
    /// Writes an episode group ID to a series item's metadata so Jellyfin stores it as the TMDb Episode Group provider ID.
    /// Pass an empty string for <paramref name="episodeGroupId"/> to clear the value.
    /// </summary>
    /// <param name="jellyfinSeriesId">The Jellyfin item ID of the series.</param>
    /// <param name="episodeGroupId">The TMDB episode group ID to set, or empty to clear.</param>
    /// <response code="204">Provider ID updated successfully.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("SetSeriesEpisodeGroup/{jellyfinSeriesId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SetSeriesEpisodeGroup(string jellyfinSeriesId, [FromQuery] string episodeGroupId)
    {
        _logger.LogInformation(
            "[TMDbEpisodeGroups] Setting TmdbEpisodeGroup provider ID on series {SeriesId} to {EpisodeGroupId}",
            jellyfinSeriesId,
            episodeGroupId);

        try
        {
            await _episodeGroupManager.SetSeriesEpisodeGroupIdAsync(jellyfinSeriesId, episodeGroupId, HttpContext.RequestAborted)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error setting TmdbEpisodeGroup provider ID on series {SeriesId}", jellyfinSeriesId);
            return StatusCode(500, new { error = "Failed to set episode group on series", message = ex.Message });
        }
    }

    /// <summary>
    /// Forces a fresh fetch from TMDB for all configured episode groups, replacing any cached data.
    /// </summary>
    /// <response code="204">Cache warm completed successfully.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("WarmCache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> WarmCache()
    {
        _logger.LogInformation("[TMDbEpisodeGroups] Warming cache for all configured episode groups");

        try
        {
            var episodeGroupIds = Plugin.Instance?.PluginConfiguration?.EpisodeGroupConfigs
                ?.Select(c => c.EpisodeGroupId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                ?? Enumerable.Empty<string>();

            await _episodeGroupCache.WarmCacheAsync(episodeGroupIds, HttpContext.RequestAborted)
                .ConfigureAwait(false);

            _logger.LogInformation("[TMDbEpisodeGroups] Cache warm completed");
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error warming cache");
            return StatusCode(500, new { error = "Failed to warm cache", message = ex.Message });
        }
    }

    /// <summary>
    /// Forces a fresh fetch from TMDB for a specific episode group, replacing any cached data.
    /// </summary>
    /// <param name="episodeGroupId">The episode group ID to refresh in the cache.</param>
    /// <response code="204">Cache entry refreshed successfully.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpPost("WarmCache/{episodeGroupId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> WarmCacheForGroup(string episodeGroupId)
    {
        _logger.LogInformation("[TMDbEpisodeGroups] Warming cache for episode group {EpisodeGroupId}", episodeGroupId);

        try
        {
            await _episodeGroupCache.WarmCacheAsync([episodeGroupId], HttpContext.RequestAborted)
                .ConfigureAwait(false);

            _logger.LogInformation("[TMDbEpisodeGroups] Cache warm completed for episode group {EpisodeGroupId}", episodeGroupId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error warming cache for episode group {EpisodeGroupId}", episodeGroupId);
            return StatusCode(500, new { error = "Failed to warm cache", message = ex.Message });
        }
    }
}
