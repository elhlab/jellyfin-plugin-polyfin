using System;

namespace Jellyfin.Plugin.Polyfin.Database.Metadata;

/// <summary>
/// A row of the <c>metadata</c> table.
/// </summary>
/// <param name="Guid">The library item's id.</param>
/// <param name="Language">The locale tag the fields were resolved in.</param>
/// <param name="Name">The title, or <see langword="null"/> if not resolved.</param>
/// <param name="Overview">The overview, or <see langword="null"/> if not resolved.</param>
public sealed record StoredMetadata(Guid Guid, string Language, string? Name, string? Overview);
