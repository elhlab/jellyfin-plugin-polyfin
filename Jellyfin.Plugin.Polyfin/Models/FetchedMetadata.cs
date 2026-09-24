using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// Metadata fetched from providers.
/// </summary>
/// <param name="Name">The title, or <see langword="null"/> if not resolved.</param>
/// <param name="Overview">The overview, or <see langword="null"/> if not resolved.</param>
public sealed record FetchedMetadata(string? Name, string? Overview)
{
    /// <summary>
    /// Gets a value indicating whether both Name and Overview have been resolved, so no further providers need to be asked.
    /// </summary>
    public bool IsComplete => Name is not null && Overview is not null;

    /// <summary>
    /// Updates the fields from <paramref name="item"/> while keeping existing fields intact.
    /// </summary>
    /// <param name="item">The provider result to take values from.</param>
    /// <returns>The copy.</returns>
    public FetchedMetadata FillFrom(BaseItem item) => new(
        Name ?? NonEmpty(item.Name),
        Overview ?? NonEmpty(item.Overview));

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
