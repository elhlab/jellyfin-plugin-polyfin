using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Fetches metadata and stores it for later lookup.
/// </summary>
public class MetadataResolver(MetadataStore metadataStore, MetadataFetcher metadataFetcher, ILogger<MetadataResolver> logger)
{
    private readonly MetadataStore _metadataStore = metadataStore;
    private readonly MetadataFetcher _metadataFetcher = metadataFetcher;
    private readonly ILogger<MetadataResolver> _logger = logger;

    /// <summary>
    /// Refreshes metadata for the given item and locale as needed.
    /// </summary>
    /// <typeparam name="TItem">The item's concrete type.</typeparam>
    /// <typeparam name="TInfo">The lookup info type for <typeparamref name="TItem"/>.</typeparam>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="locale">The locale to fetch metadata for.</param>
    /// <param name="refetch">Whether to fetch even if metadata is already stored.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If no provider has a translation, empty metadata is stored so the item isn't fetched again.
    /// If nothing was found and a provider failed, the store is left unchanged and a warning is logged, so the
    /// item is retried later. Partial results are stored even if a provider failed. If the item no longer
    /// exists, nothing is stored.
    /// </remarks>
    /// <example>
    /// Refresh metadata for a movie:
    /// <code>
    /// await RefreshAsync&lt;Movie, MovieInfo&gt;(itemId, locale, false, cancellationToken);
    /// </code>
    /// </example>
    public async Task RefreshAsync<TItem, TInfo>(Guid itemId, Locale locale, bool refetch, CancellationToken cancellationToken)
        where TItem : BaseItem, IHasLookupInfo<TInfo>
        where TInfo : ItemLookupInfo, new()
    {
        if (!refetch && !NeedsRefresh(_metadataStore.Get(itemId, locale)))
        {
            return;
        }

        (FetchedMetadata Metadata, bool HadFailures) fetched;
        try
        {
            fetched = await _metadataFetcher.FetchMetadataAsync<TItem, TInfo>(
                itemId, locale, cancellationToken).ConfigureAwait(false);
        }
        catch (FetchItemNotFoundException)
        {
            _logger.LogWarning("Item {ItemId} not found, nothing stored for {Locale}", itemId, locale.ToTag());
            return;
        }

        // An empty result only means "no translation" if no provider failed. Otherwise don't store
        // it, so the item remains eligible for a future retry.
        // TODO: Consider storing these once fetched_at is tracked, to back off retries.
        if (fetched.Metadata.IsEmpty && fetched.HadFailures)
        {
            _logger.LogWarning("Nothing found for {ItemId} in {Locale} and a provider failed, retrying later", itemId, locale.ToTag());
            return;
        }

        SaveMetadata(Metadata.FromFetched(itemId, fetched.Metadata), locale);
    }

    /// <summary>
    /// Returns the items whose metadata for the locale needs refreshing.
    /// </summary>
    /// <param name="itemIds">The library item ids to check.</param>
    /// <param name="locale">The locale to check.</param>
    /// <returns>The ids of the items whose metadata needs refreshing.</returns>
    /// <remarks>An item needs refreshing when nothing is stored for it in the locale.</remarks>
    public IReadOnlySet<Guid> FilterItemsToRefresh(IReadOnlyCollection<Guid> itemIds, Locale locale)
    {
        var toRefresh = new HashSet<Guid>(itemIds);

        var freshItemIds = _metadataStore.Enumerate(locale)
            .Where(metadata => !NeedsRefresh(metadata))
            .Select(metadata => metadata.Guid);

        toRefresh.ExceptWith(freshItemIds);

        return toRefresh;
    }

    /// <summary>
    /// Finds stored metadata for an item and locale.
    /// </summary>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="locale">The locale to look up.</param>
    /// <returns>The stored metadata, or <see langword="null"/> if none is stored.</returns>
    public Metadata? Find(Guid itemId, Locale locale)
    {
        var storedMetadata = _metadataStore.Get(itemId, locale);
        return storedMetadata is not null
            ? Metadata.FromStore(storedMetadata)
            : null;
    }

    // TODO: Refetch partial or stale metadata once completeness and fetched_at are tracked.
    private static bool NeedsRefresh(StoredMetadata? stored) => stored is null;

    private void SaveMetadata(Metadata metadata, Locale locale)
    {
        _metadataStore.Set(metadata.ToStored(locale));
    }
}
