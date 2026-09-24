using System.IO;
using Jellyfin.Plugin.Polyfin.Configuration;
using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Filters;
using Jellyfin.Plugin.Polyfin.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin;

/// <inheritdoc />
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Called once by PluginManager at startup, before the container is built -
        // this only hands it recipes. Nothing here is actually constructed yet.
        serviceCollection.AddSingleton<DemoMetadataAnnotator>();
        serviceCollection.AddSingleton<DemoInterceptFilter>();

        // Plugin.Instance is still null while services are
        // being registered, and exists by the time anything resolves this.
        serviceCollection.AddSingleton(serviceProvider => new LanguageResolver(
            Plugin.Instance!.Configuration,
            callback => Plugin.Instance!.ConfigurationChanged += (_, configuration) => callback((PluginConfiguration)configuration),
            serviceProvider.GetRequiredService<ILogger<LanguageResolver>>()));

        // Lives next to the plugin's config XML rather than in DataFolderPath, which
        // has the assembly version in its name and would orphan the db on every update.
        // See notes/plugin-data-location.md.
        serviceCollection.AddSingleton(serviceProvider =>
        {
            var dataFolderPath = Path.Join(serviceProvider.GetRequiredService<IApplicationPaths>().PluginConfigurationsPath, "Polyfin");
            Directory.CreateDirectory(dataFolderPath);
            return new MetadataStore(dataFolderPath);
        });

        // MvcOptions.Filters is ASP.NET Core's GLOBAL filter list - every controller
        // action in the whole Jellyfin process gets this filter added to its pipeline,
        // automatically, with no change to Jellyfin's own controller code (which we
        // don't own and can't add attributes to). This one line is what makes
        // DemoInterceptFilter run on every request instead of none.
        serviceCollection.PostConfigure<MvcOptions>(options => options.Filters.AddService<DemoInterceptFilter>());
    }
}
