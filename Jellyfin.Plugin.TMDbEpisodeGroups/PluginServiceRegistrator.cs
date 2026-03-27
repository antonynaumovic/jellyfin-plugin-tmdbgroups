using Jellyfin.Plugin.TMDbEpisodeGroups.Providers;
using Jellyfin.Plugin.TMDbEpisodeGroups.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TMDbEpisodeGroups;

/// <summary>
/// Register plugin service.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient();
        serviceCollection.AddSingleton<ITmdbApiClient, TmdbApiClient>();
        serviceCollection.AddSingleton<ITmdbEpisodeGroupCache, TmdbEpisodeGroupCache>();
        serviceCollection.AddSingleton<EpisodeGroupMetadataManager>();
        serviceCollection.AddSingleton<IRemoteMetadataProvider<Episode, EpisodeInfo>, TMDbEpisodeGroupProvider>();
        serviceCollection.AddSingleton<IRemoteImageProvider, TMDbEpisodeGroupImageProvider>();
        serviceCollection.AddSingleton<IExternalId, TmdbEpisodeGroupExternalId>();
    }
}
