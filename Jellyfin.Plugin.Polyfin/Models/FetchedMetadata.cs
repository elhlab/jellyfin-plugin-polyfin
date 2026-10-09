using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// Metadata fetched from providers.
/// </summary>
/// <param name="Name">The title, or <see langword="null"/> if not resolved.</param>
/// <param name="Overview">The overview, or <see langword="null"/> if not resolved.</param>
/// <param name="Tagline">The tagline, or <see langword="null"/> if not resolved.</param>
public sealed record FetchedMetadata(string? Name, string? Overview, string? Tagline)
{
    /// <summary>
    /// Gets a value indicating whether every field is filled.
    /// </summary>
    public bool IsComplete => Name is not null && Overview is not null && Tagline is not null;

    /// <summary>
    /// Gets a value indicating whether no field is filled.
    /// </summary>
    public bool IsEmpty => Name is null && Overview is null && Tagline is null;

    /// <summary>
    /// Updates the fields from <paramref name="item"/> while keeping existing fields intact.
    /// </summary>
    /// <param name="item">The provider result to take values from.</param>
    /// <returns>The copy.</returns>
    public FetchedMetadata FillFrom(BaseItem item) => new(
        Name ?? NonEmpty(item.Name),
        Overview ?? NonEmpty(item.Overview),
        Tagline ?? NonEmpty(item.Tagline)
    );

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
