using System.Threading;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// A movie transformer which transforms movie metadata via the <see cref="Transform"/> method.
/// </summary>
public class MovieTransformer
{
    private readonly MetadataResolver _metadataResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="MovieTransformer"/> class.
    /// </summary>
    /// <param name="metadataResolver">Instance of the <see cref="MetadataResolver"/> class.</param>
    public MovieTransformer(MetadataResolver metadataResolver)
    {
        this._metadataResolver = metadataResolver;
    }

    /// <summary>
    /// Transforms a movie.
    /// </summary>
    /// <param name="movieItem">The movie item to transform.</param>
    /// <param name="locale">Resolved user locale.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    public void Transform(BaseItemDto movieItem, ResolvedLocale locale, CancellationToken cancellationToken)
    {
        return;
    }
}
