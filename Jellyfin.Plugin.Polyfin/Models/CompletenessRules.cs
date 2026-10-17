using Jellyfin.Plugin.Polyfin.Database.Metadata;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// Rules for when metadata counts as complete.
/// </summary>
public static class CompletenessRules
{
    /// <summary>
    /// Returns whether the metadata has every field its kind needs.
    /// </summary>
    /// <param name="metadata">The stored metadata to check.</param>
    /// <returns>True if no required field is missing.</returns>
    /// <remarks>Only movies are supported, so the movie rule always applies.</remarks>
    public static bool IsComplete(this StoredMetadata metadata) => IsMovieComplete(metadata);

    /// <summary>
    /// Returns whether the metadata has every field a movie needs.
    /// </summary>
    /// <param name="metadata">The stored metadata to check.</param>
    /// <returns>True if the name and overview are set.</returns>
    /// <remarks>
    /// The metadata's kind isn't checked.
    /// The tagline isn't required, as some movies do not have one.
    /// </remarks>
    public static bool IsMovieComplete(StoredMetadata metadata) =>
        metadata.Name is not null
        && metadata.Overview is not null;
}
