using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
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
    public Task Transform(BaseItemDto movieItem, Locale locale, CancellationToken cancellationToken)
    {
        var metadata = _metadataResolver.Find(movieItem.Id, locale);
        if (metadata is null)
        {
            return Task.CompletedTask;
        }

        movieItem.Name = metadata.Name ?? movieItem.Name;
        movieItem.Overview = metadata.Overview ?? movieItem.Overview;

        if (movieItem.Taglines is not null && metadata.Tagline is not null)
        {
            // TODO: maybe advanced config to make it drop taglines if they are not translated.
            movieItem.Taglines = [metadata.Tagline];
        }

        return Task.CompletedTask;
    }
}
