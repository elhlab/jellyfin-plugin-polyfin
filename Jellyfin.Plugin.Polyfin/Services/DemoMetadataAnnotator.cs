using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Manual verification service: marks an intercepted movie's title and swaps
/// its overview for a German one pulled live from the library's own
/// metadata providers. The actual business logic - deciding what a
/// <see cref="Filters.DemoInterceptFilter"/> found is worth touching, and
/// what to do about it. Not real feature logic - delete once the
/// result-filter + provider-query mechanism has been checked against a live
/// server. See notes/jellyfin-plugin-migration.md.
/// </summary>
public class DemoMetadataAnnotator
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly ILogger<DemoMetadataAnnotator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoMetadataAnnotator"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{DemoMetadataAnnotator}"/> interface.</param>
    public DemoMetadataAnnotator(ILibraryManager libraryManager, IProviderManager providerManager, ILogger<DemoMetadataAnnotator> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _logger = logger;
    }

    /// <summary>
    /// Mutates a single item's DTO in place, if it's a movie.
    /// </summary>
    /// <param name="dto">The item to annotate.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AnnotateAsync(BaseItemDto dto)
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
