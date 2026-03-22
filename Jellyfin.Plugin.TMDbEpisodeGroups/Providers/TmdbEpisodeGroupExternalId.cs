using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TMDbEpisodeGroups.Providers;

/// <summary>
/// External ID for TMDb Episode Group.
/// </summary>
public class TmdbEpisodeGroupExternalId : IExternalId
{
    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string ProviderName => "TMDb Episode Group";

    /// <summary>
    /// Gets the provider key.
    /// </summary>
    public string Key => "TmdbEpisodeGroup";

    /// <summary>
    /// Gets the supported media type.
    /// </summary>
    public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

    /// <summary>
    /// Gets the URL format string.
    /// </summary>
    public string UrlFormatString => "https://www.themoviedb.org/tv/{0}/episode_group/{1}";

    /// <summary>
    /// Checks if the item is supported.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True if the item is a Series.</returns>
    public bool Supports(IHasProviderIds item) => item is Series;
}
