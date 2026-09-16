using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Polyfin;

/// <inheritdoc />
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Make the filter resolvable from DI (needed since AddService below fetches it
        // per-request via the service provider, not by calling `new`).
        serviceCollection.AddSingleton<DemoInterceptFilter>();

        // MvcOptions.Filters is ASP.NET Core's GLOBAL filter list - every controller
        // action in the whole Jellyfin process gets this filter added to its pipeline,
        // automatically, with no change to Jellyfin's own controller code (which we
        // don't own and can't add attributes to). This one line is what makes
        // DemoInterceptFilter run on every request instead of none.
        serviceCollection.PostConfigure<MvcOptions>(options => options.Filters.AddService<DemoInterceptFilter>());
    }
}
