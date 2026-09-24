using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.Polyfin.Transformers;

/// <summary>
/// A movie transformer which transforms movie metadata via the <see cref="Transform"/> method.
/// </summary>
/// <param name="metadataResolver">Instance of the <see cref="MetadataFetcher"/> class.</param>
public class MovieTransformer(MetadataFetcher metadataResolver)
{
    private readonly MetadataFetcher _metadataResolver = metadataResolver;

    /// <summary>
    /// Transforms a movie inplace.
    /// </summary>
    /// <param name="movieItem">The movie item to transform.</param>
    /// <param name="locale">Resolved user locale.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task Transform(BaseItemDto movieItem, Locale locale, CancellationToken cancellationToken)
    {
        // TODO: this should use the metadata store that will transparently resolve and cache our metadata.
        var resolved = await _metadataResolver.ResolveMetadataAsync<Movie, MovieInfo>(movieItem.Id, locale, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return;
        }

        movieItem.Name = resolved.Name ?? movieItem.Name;
        movieItem.Overview = resolved.Overview ?? movieItem.Overview;
    }
}
