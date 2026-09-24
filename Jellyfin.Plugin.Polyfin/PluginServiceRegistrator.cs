using System.IO;
using Jellyfin.Plugin.Polyfin.Configuration;
using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Filters;
using Jellyfin.Plugin.Polyfin.Services;
using Jellyfin.Plugin.Polyfin.Transformers;
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
        serviceCollection.AddSingleton<MetadataFetcher>();
        serviceCollection.AddSingleton<MetadataResolver>();
        serviceCollection.AddSingleton<MovieTransformer>();
        serviceCollection.AddSingleton<InterceptionFilter>();

        // Plugin.Instance is still null while services are
        // being registered, and exists by the time anything resolves this.
        serviceCollection.AddSingleton(serviceProvider => new LanguageResolver(
            Plugin.Instance!.Configuration,
            callback => Plugin.Instance!.ConfigurationChanged += (_, configuration) => callback((PluginConfiguration)configuration),
            serviceProvider.GetRequiredService<ILogger<LanguageResolver>>()));

        // Lives next to the plugin's config XML rather than in DataFolderPath, which
        // has the assembly version in its name and would orphan the db on every update.
        serviceCollection.AddSingleton(serviceProvider =>
        {
            var dataFolderPath = Path.Join(serviceProvider.GetRequiredService<IApplicationPaths>().PluginConfigurationsPath, "Polyfin");
            Directory.CreateDirectory(dataFolderPath);
            return new MetadataStore(dataFolderPath);
        });

        // Global filter: runs on the result of every controller action in Jellyfin.
        serviceCollection.PostConfigure<MvcOptions>(options => options.Filters.AddService<InterceptionFilter>());
    }
}
