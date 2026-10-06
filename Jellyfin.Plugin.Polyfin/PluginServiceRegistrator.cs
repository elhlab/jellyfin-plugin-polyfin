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

        serviceCollection.AddSingleton<RefreshQueue>();

        serviceCollection.AddSingleton<BackgroundMetadataRefresher>();

        serviceCollection.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<BackgroundMetadataRefresher>());

        // Plugin.Instance is still null while services are
        // being registered, and exists by the time anything resolves this.
        serviceCollection.AddSingleton(serviceProvider => new LanguageResolver(
            Plugin.Instance!.Configuration,
            callback => Plugin.Instance!.ConfigurationChanged += (_, configuration) => callback((PluginConfiguration)configuration),
            serviceProvider.GetRequiredService<ILogger<LanguageResolver>>()));

        // In Jellyfin's data folder, as the official plugins do. The plugins folder doesn't seem
        // the most stable place at the moment.
        // https://github.com/jellyfin/jellyfin/issues/17699
        serviceCollection.AddSingleton(serviceProvider =>
        {
            var dataFolderPath = Path.Join(serviceProvider.GetRequiredService<IApplicationPaths>().DataPath, "polyfin");
            Directory.CreateDirectory(dataFolderPath);
            return new MetadataStore(dataFolderPath);
        });

        // Global filter: runs on the result of every controller action in Jellyfin.
        serviceCollection.PostConfigure<MvcOptions>(options => options.Filters.AddService<InterceptionFilter>());
    }
}
