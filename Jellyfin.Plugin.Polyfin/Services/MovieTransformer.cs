using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Dto;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// A movie transformer which transforms movie metadata via the <see cref="Transform"/> method.
/// </summary>
/// <param name="metadataResolver">Instance of the <see cref="MetadataResolver"/> class.</param>
public class MovieTransformer(MetadataResolver metadataResolver)
{
    private readonly MetadataResolver _metadataResolver = metadataResolver;

    /// <summary>
    /// Transforms a movie inplace.
    /// </summary>
    /// <param name="movieItem">The movie item to transform.</param>
    /// <param name="locale">Resolved user locale.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task Transform(BaseItemDto movieItem, ResolvedLocale locale, CancellationToken cancellationToken)
    {
        return;
    }
}
