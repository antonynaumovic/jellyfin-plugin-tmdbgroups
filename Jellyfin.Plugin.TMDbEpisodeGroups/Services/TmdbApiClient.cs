using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Services;

/// <summary>
/// Client for TMDB API to fetch episode group data.
/// </summary>
public class TmdbApiClient : ITmdbApiClient
{
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TmdbApiClient> _logger;
    private readonly Func<string> _apiKeyProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbApiClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TmdbApiClient}"/> interface.</param>
    public TmdbApiClient(IHttpClientFactory httpClientFactory, ILogger<TmdbApiClient> logger)
        : this(httpClientFactory, logger, () => Plugin.Instance?.PluginConfiguration?.TmdbApiKey ?? string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbApiClient"/> class with a custom API key provider (for testing).
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TmdbApiClient}"/> interface.</param>
    /// <param name="apiKeyProvider">Function that provides the TMDB API key.</param>
    public TmdbApiClient(IHttpClientFactory httpClientFactory, ILogger<TmdbApiClient> logger, Func<string> apiKeyProvider)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _apiKeyProvider = apiKeyProvider;
    }

    /// <summary>
    /// Gets episode groups for a TV series.
    /// </summary>
    /// <param name="tmdbSeriesId">The TMDB series ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TmdbEpisodeGroupList"/> containing available episode groups.</returns>
    public async Task<TmdbEpisodeGroupList> GetEpisodeGroupsAsync(string tmdbSeriesId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[TMDbEpisodeGroups] GetEpisodeGroupsAsync called for series ID: {TmdbSeriesId}", tmdbSeriesId);

        var apiKey = _apiKeyProvider();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("[TMDbEpisodeGroups] TMDB API key is not configured");
            throw new InvalidOperationException("TMDB API key is not configured");
        }

        _logger.LogDebug("[TMDbEpisodeGroups] API key is configured (length: {Length})", apiKey.Length);

        var url = $"{BaseUrl}/tv/{tmdbSeriesId}/episode_groups?api_key={apiKey}";
        _logger.LogInformation("[TMDbEpisodeGroups] Calling TMDB API: {Url}", url.Replace(apiKey, "***", StringComparison.Ordinal));

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[TMDbEpisodeGroups] Received response from TMDB (length: {Length})", response.Length);

            var result = JsonSerializer.Deserialize<TmdbEpisodeGroupList>(response, JsonOptions);
            _logger.LogInformation("[TMDbEpisodeGroups] Successfully parsed {Count} episode groups", result?.Results?.Count ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error fetching episode groups for series {TmdbSeriesId}", tmdbSeriesId);
            throw;
        }
    }

    /// <summary>
    /// Gets detailed episode group information including all episodes.
    /// </summary>
    /// <param name="episodeGroupId">The episode group ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TmdbEpisodeGroupDetails"/> containing full episode group information.</returns>
    public async Task<TmdbEpisodeGroupDetails> GetEpisodeGroupDetailsAsync(string episodeGroupId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[TMDbEpisodeGroups] GetEpisodeGroupDetailsAsync called for group ID: {EpisodeGroupId}", episodeGroupId);

        var apiKey = _apiKeyProvider();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("[TMDbEpisodeGroups] TMDB API key is not configured");
            throw new InvalidOperationException("TMDB API key is not configured");
        }

        var url = $"{BaseUrl}/tv/episode_group/{episodeGroupId}?api_key={apiKey}";
        _logger.LogInformation("[TMDbEpisodeGroups] Calling TMDB API: {Url}", url.Replace(apiKey, "***", StringComparison.Ordinal));

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("[TMDbEpisodeGroups] Received response from TMDB (length: {Length})", response.Length);

            var result = JsonSerializer.Deserialize<TmdbEpisodeGroupDetails>(response, JsonOptions);
            _logger.LogInformation("[TMDbEpisodeGroups] Successfully parsed episode group with {Count} groups", result?.Groups?.Count ?? 0);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TMDbEpisodeGroups] Error fetching episode group details for {EpisodeGroupId}", episodeGroupId);
            throw;
        }
    }
}

#pragma warning disable SA1402 // File may only contain a single type (DTOs are grouped together)

/// <summary>
/// TMDB episode group list response.
/// </summary>
public class TmdbEpisodeGroupList
{
    /// <summary>
    /// Gets the list of episode groups.
    /// </summary>
    [JsonPropertyName("results")]
    public IList<TmdbEpisodeGroup> Results { get; init; }
}

/// <summary>
/// TMDB episode group summary.
/// </summary>
public class TmdbEpisodeGroup
{
    /// <summary>
    /// Gets or sets the episode group ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the episode group name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the episode group type.
    /// </summary>
    [JsonPropertyName("type")]
    public int Type { get; set; }

    /// <summary>
    /// Gets or sets the episode group description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the episode count.
    /// </summary>
    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Gets or sets the group count.
    /// </summary>
    [JsonPropertyName("group_count")]
    public int GroupCount { get; set; }
}

/// <summary>
/// TMDB episode group details with all episodes.
/// </summary>
public class TmdbEpisodeGroupDetails
{
    /// <summary>
    /// Gets or sets the episode group ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the episode group name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the episode group type.
    /// </summary>
    [JsonPropertyName("type")]
    public int Type { get; set; }

    /// <summary>
    /// Gets or sets the episode group description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Gets the list of groups (seasons).
    /// </summary>
    [JsonPropertyName("groups")]
    public IList<TmdbEpisodeGroupSeason> Groups { get; init; }
}

/// <summary>
/// Episode group season/group.
/// </summary>
public class TmdbEpisodeGroupSeason
{
    /// <summary>
    /// Gets or sets the group ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the group order.
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// Gets the list of episodes in this group.
    /// </summary>
    [JsonPropertyName("episodes")]
    public IList<TmdbEpisode> Episodes { get; init; }
}

/// <summary>
/// Episode information from TMDB.
/// </summary>
public class TmdbEpisode
{
    /// <summary>
    /// Gets or sets the episode ID.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the episode name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the episode overview.
    /// </summary>
    [JsonPropertyName("overview")]
    public string Overview { get; set; }

    /// <summary>
    /// Gets or sets the episode number.
    /// </summary>
    [JsonPropertyName("episode_number")]
    public int EpisodeNumber { get; set; }

    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Gets or sets the order within the episode group.
    /// </summary>
    [JsonPropertyName("order")]
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the air date.
    /// </summary>
    [JsonPropertyName("air_date")]
    public string AirDate { get; set; }

    /// <summary>
    /// Gets or sets the runtime in minutes.
    /// </summary>
    [JsonPropertyName("runtime")]
    public int? Runtime { get; set; }

    /// <summary>
    /// Gets or sets the still path (episode thumbnail).
    /// </summary>
    [JsonPropertyName("still_path")]
    public string StillPath { get; set; }
}

#pragma warning restore SA1402 // File may only contain a single type
