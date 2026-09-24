using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Resolves metadata from a local metadata cache, or by fetching it.
/// </summary>
public class MetadataResolver(MetadataStore metadataStore, MetadataFetcher metadataFetcher)
{
    private readonly MetadataStore _metadataStore = metadataStore;
    private readonly MetadataFetcher _metadataFetcher = metadataFetcher;

    /// <summary>
    /// Resolves metadata from the local metadata cache, or by fetching and caching it.
    /// </summary>
    /// <typeparam name="TItem">The item's concrete type, e.g. Movie, Season or Episode.</typeparam>
    /// <typeparam name="TInfo">The lookup info type for TItem, e.g. MovieInfo or EpisodeInfo.</typeparam>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="locale">The locale to request.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>
    /// The resolved metadata, or null if the id does not refer to a <typeparamref name="TItem"/>
    /// or no metadata is available in the locale.
    /// </returns>
    public async Task<Metadata?> ResolveAsync<TItem, TInfo>(Guid itemId, Locale locale, CancellationToken cancellationToken)
        where TItem : BaseItem, IHasLookupInfo<TInfo>
        where TInfo : ItemLookupInfo, new()
    {
        var storedMetadata = _metadataStore.Get(itemId, locale);
        if (storedMetadata is not null)
        {
            return Metadata.FromStore(storedMetadata);
        }

        var fetchedMetadata = await _metadataFetcher.FetchMetadataAsync<TItem, TInfo>(itemId, locale, cancellationToken).ConfigureAwait(false);
        if (fetchedMetadata is not null)
        {
            var metadata = Metadata.FromFetched(itemId, fetchedMetadata);
            SaveMetadata(metadata, locale);
            return metadata;
        }

        return null;
    }

    private void SaveMetadata(Metadata metadata, Locale locale)
    {
        _metadataStore.Set(metadata.ToStored(locale));
    }
}
