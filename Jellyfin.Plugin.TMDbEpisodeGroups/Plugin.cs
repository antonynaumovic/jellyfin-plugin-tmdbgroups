using System;
using System.Collections.Generic;
using Jellyfin.Plugin.TMDbEpisodeGroups.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TMDbEpisodeGroups;

/// <summary>
/// Plugin class for TMDb episode group metadata management.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="appPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths appPaths, IXmlSerializer xmlSerializer)
        : base(appPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc/>
    public override string Name => "TMDb Episode Groups";

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static Plugin Instance { get; private set; }

    /// <inheritdoc/>
    public override string Description => "Update episode metadata (titles and descriptions) from TMDb episode groups for TV series with alternate orderings";

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public PluginConfiguration PluginConfiguration => Configuration;

    /// <inheritdoc/>
    public override Guid Id => new("7e0a7d42-3f8c-4b9e-a1f2-5d8c9e6f4a3b");

    /// <inheritdoc/>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "TMDb Episode Groups",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.html"
            }
        ];
    }
}
