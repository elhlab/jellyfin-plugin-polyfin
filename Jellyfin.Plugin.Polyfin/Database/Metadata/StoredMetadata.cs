using System;
using Jellyfin.Plugin.Polyfin.Models;

namespace Jellyfin.Plugin.Polyfin.Database.Metadata;

/// <summary>
/// A row of the <c>metadata</c> table.
/// </summary>
/// <param name="Guid">The library item's id.</param>
/// <param name="Locale">The locale the fields are in.</param>
/// <param name="Name">The title, or <see langword="null"/> if not resolved.</param>
/// <param name="Overview">The overview, or <see langword="null"/> if not resolved.</param>
/// <param name="Tagline">The tagline, or <see langword="null"/> if not resolved.</param>
public sealed record StoredMetadata(Guid Guid, Locale Locale, string? Name, string? Overview, string? Tagline);
