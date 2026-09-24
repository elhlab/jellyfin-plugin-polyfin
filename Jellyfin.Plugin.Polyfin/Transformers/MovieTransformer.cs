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
/// <param name="metadataResolver">Instance of the <see cref="MetadataResolver"/> class.</param>
public class MovieTransformer(MetadataResolver metadataResolver)
{
    private readonly MetadataResolver _metadataResolver = metadataResolver;

    /// <summary>
    /// Transforms a movie in place.
    /// </summary>
    /// <param name="movieItem">The movie item to transform.</param>
    /// <param name="locale">Resolved user locale.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task Transform(BaseItemDto movieItem, Locale locale, CancellationToken cancellationToken)
    {
        var resolved = await _metadataResolver.ResolveAsync<Movie, MovieInfo>(movieItem.Id, locale, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return;
        }

        movieItem.Name = resolved.Name ?? movieItem.Name;
        movieItem.Overview = resolved.Overview ?? movieItem.Overview;
    }
}
