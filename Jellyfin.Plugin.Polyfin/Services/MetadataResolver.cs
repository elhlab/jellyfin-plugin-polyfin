using System;
using System.Collections.Generic;
using System.Diagnostics;
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
public class MetadataResolver(
    MetadataStore metadataStore,
    MetadataFetcher metadataFetcher,
    TimeProvider timeProvider,
    ILogger<MetadataResolver> logger)
{
    /// <summary>
    /// How long complete metadata is kept before it is refreshed.
    /// </summary>
    internal static readonly TimeSpan CompleteRefreshAge = TimeSpan.FromDays(14);

    /// <summary>
    /// How long incomplete metadata is kept before it is fetched again.
    /// </summary>
    internal static readonly TimeSpan IncompleteRefreshAge = TimeSpan.FromDays(3);

    private readonly MetadataStore _metadataStore = metadataStore;
    private readonly MetadataFetcher _metadataFetcher = metadataFetcher;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<MetadataResolver> _logger = logger;

    /// <summary>
    /// Refreshes metadata for the given item and locale as needed.
    /// </summary>
    /// <typeparam name="TItem">The item's concrete type.</typeparam>
    /// <typeparam name="TInfo">The lookup info type for <typeparamref name="TItem"/>.</typeparam>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="locale">The locale to fetch metadata for.</param>
    /// <param name="refetch">Whether to fetch even if the stored metadata doesn't need refreshing.</param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If no provider returns anything, empty metadata is stored, and <see cref="NeedsRefresh"/> decides when it is retried.
    /// If nothing was found and a provider failed, the store is left unchanged and a warning is logged, so the
    /// item is retried later. If a provider failed, partial results only update the stored metadata, so
    /// fields that are missing keep their values. If the item no longer exists, nothing is stored.
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

        // Do not update RefreshedAt so the next scan will try again.
        if (fetched.Metadata.IsEmpty && fetched.HadFailures)
        {
            _logger.LogWarning("Nothing found for {ItemId} in {Locale} and a provider failed, retrying later", itemId, locale.ToTag());
            return;
        }

        var metadata = Metadata.FromFetched(itemId, fetched.Metadata).ToStored(locale, _timeProvider.GetUtcNow());
        if (fetched.HadFailures)
        {
            // A failed provider might have had the missing fields, so keep the ones already stored.
            _metadataStore.Fill(metadata);
        }
        else
        {
            _metadataStore.Replace(metadata);
        }
    }

    /// <summary>
    /// Returns the items whose metadata for the locale needs refreshing.
    /// </summary>
    /// <param name="itemIds">The library item ids to check.</param>
    /// <param name="locale">The locale to check.</param>
    /// <returns>The ids of the items whose metadata needs refreshing.</returns>
    /// <remarks>See <see cref="NeedsRefresh"/> for when an item needs refreshing.</remarks>
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

    /// <summary>
    /// Returns whether stored metadata needs refreshing.
    /// </summary>
    /// <param name="stored">The stored metadata, or <see langword="null"/> if none is stored.</param>
    /// <returns>True if nothing is stored, or the stored metadata is due for a refresh.</returns>
    /// <remarks>
    /// Full metadata is never refreshed, complete metadata is refreshed after <see cref="CompleteRefreshAge"/>,
    /// and incomplete metadata after <see cref="IncompleteRefreshAge"/>.
    /// </remarks>
    public bool NeedsRefresh(StoredMetadata? stored)
    {
        if (stored is null)
        {
            return true;
        }

        var completeness = stored.GetCompleteness();

        // TODO: Back off refreshes over time.
        // TODO: Make the refresh ages configurable in advanced settings.
        var now = _timeProvider.GetUtcNow();
        return completeness switch
        {
            // Rescanning could catch metadata changes over time, but isn't worth
            // supporting right now. Users can force an update per item if needed.
            Completeness.Full => false,

            Completeness.Complete => now - stored.RefreshedAt >= CompleteRefreshAge,

            Completeness.Incomplete => now - stored.RefreshedAt >= IncompleteRefreshAge,

            _ => throw new UnreachableException(
                $"Completeness {completeness} has no refresh rule."),
        };
    }
}
