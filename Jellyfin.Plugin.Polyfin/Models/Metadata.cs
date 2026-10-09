using System;
using Jellyfin.Plugin.Polyfin.Database.Metadata;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// Translated metadata for an item.
/// </summary>
/// <param name="ItemId">The library item's id.</param>
/// <param name="Name">The title, if any.</param>
/// <param name="Overview">The overview, if any.</param>
/// <param name="Tagline">The tagline, if any.</param>
public sealed record Metadata(Guid ItemId, string? Name, string? Overview, string? Tagline)
{
    /// <summary>
    /// Creates metadata from stored metadata.
    /// </summary>
    /// <param name="stored">The stored metadata.</param>
    /// <returns>The metadata.</returns>
    public static Metadata FromStore(StoredMetadata stored) => new(stored.Guid, stored.Name, stored.Overview, stored.Tagline);

    /// <summary>
    /// Creates metadata from fetched metadata.
    /// </summary>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="fetched">The fetched metadata.</param>
    /// <returns>The metadata.</returns>
    public static Metadata FromFetched(Guid itemId, FetchedMetadata fetched) => new(itemId, fetched.Name, fetched.Overview, fetched.Tagline);

    /// <summary>
    /// Converts to stored metadata.
    /// </summary>
    /// <param name="locale">The locale of the metadata.</param>
    /// <returns>The stored metadata.</returns>
    public StoredMetadata ToStored(Locale locale) => new(ItemId, locale, Name, Overview, Tagline);
}
