using System;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// An item waiting to be refreshed in one locale.
/// </summary>
/// <param name="ItemId">The library item's id.</param>
/// <param name="ItemType">The library item's type, e.g. Movie.</param>
/// <param name="Locale">The locale to refresh it in.</param>
/// <param name="Priority">The item's refresh priority.</param>
/// <param name="Refetch">Whether to fetch again even if metadata is already stored.</param>
public sealed record QueuedItem(
    Guid ItemId,
    BaseItemKind ItemType,
    Locale Locale,
    QueuePriority Priority,
    bool Refetch);
