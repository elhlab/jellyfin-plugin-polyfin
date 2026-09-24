using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Polyfin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Polyfin;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        // Built directly by PluginManager via reflection, before the DI
        // (Dependency Injection) container exists - so DI can't inject Plugin
        // elsewhere. Instance is the escape hatch other code uses to reach it
        // (e.g. Plugin.Instance.DataFolderPath).
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Polyfin";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("b8c970d4-41d3-4dec-9c37-b37536f36f77");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.ConfigPage.Polyfin.html", GetType().Namespace)
            },

            // The page requests these by name. (E.g. 'configurationpage?name=Polyfin.css')
            // The lookup is global across every plugin, so they have to be unique.
            new PluginPageInfo
            {
                Name = $"{Name}.css",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.ConfigPage.Polyfin.css", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = $"{Name}.js",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.ConfigPage.Polyfin.js", GetType().Namespace)
            }
        ];
    }
}
