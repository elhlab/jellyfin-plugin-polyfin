using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin;

/// <summary>
/// Manual verification filter: marks every intercepted movie title and swaps its
/// overview for a German one pulled live from the library's own metadata providers.
/// Not real feature logic - delete once the result-filter + provider-query mechanism
/// has been checked against a live server. See notes/jellyfin-plugin-migration.md.
/// </summary>
public class DemoInterceptFilter : IAsyncResultFilter
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly ILogger<DemoInterceptFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoInterceptFilter"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{DemoInterceptFilter}"/> interface.</param>
    public DemoInterceptFilter(ILibraryManager libraryManager, IProviderManager providerManager, ILogger<DemoInterceptFilter> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Called by ASP.NET Core itself, once per request, after Jellyfin's
        // controller builds context.Result but before it's serialized to JSON.
        // Registered globally (PluginServiceRegistrator), so this runs for every
        // response in the server - the type checks below narrow it to movie items.
        // context.Result.Value is the live object, so mutating it in place (before
        // calling next()) is enough - there's no "send it back" step.
        if (context.Result is ObjectResult { Value: BaseItemDto dto })
        {
            await AnnotateAsync(dto).ConfigureAwait(false);
        }
        else if (context.Result is ObjectResult { Value: QueryResult<BaseItemDto> query })
        {
            foreach (var item in query.Items)
            {
                await AnnotateAsync(item).ConfigureAwait(false);
            }
        }
        else if (context.Result is ObjectResult { Value: IEnumerable<BaseItemDto> list })
        {
            // Items/Latest ("Recently Added") returns a raw list instead of
            // QueryResult<BaseItemDto> - found by testing, not documented anywhere.
            foreach (var item in list)
            {
                await AnnotateAsync(item).ConfigureAwait(false);
            }
        }

        await next().ConfigureAwait(false);
    }

    private async Task AnnotateAsync(BaseItemDto dto)
    {
        if (dto.Type != BaseItemKind.Movie)
        {
            return;
        }

        dto.Name = $"[Polyfin] {dto.Name}";

        if (_libraryManager.GetItemById(dto.Id) is not Movie movie)
        {
            return;
        }

        var libraryOptions = _libraryManager.GetLibraryOptions(movie);

        // Providers already configured for this library (TMDb, TVDb...) - no
        // separate API key needed.
        foreach (var provider in _providerManager.GetMetadataProviders<Movie>(movie, libraryOptions))
        {
            if (provider is not IRemoteMetadataProvider<Movie, MovieInfo> remote)
            {
                continue;
            }

            // Language/country here (not the library's global setting) is what makes
            // this per-request instead of per-library.
            var info = new MovieInfo
            {
                Name = movie.Name,
                MetadataLanguage = "de",
                MetadataCountryCode = "DE",
                ProviderIds = movie.ProviderIds
            };

            // Direct provider call, not persisted back to the library item.
            var result = await remote.GetMetadata(info, default).ConfigureAwait(false);

            if (result.HasMetadata)
            {
                dto.Overview = result.Item.Overview;
                _logger.LogInformation("Polyfin demo: fetched DE overview for {Name} from {Provider}", movie.Name, provider.Name);
            }

            break;
        }
    }
}
