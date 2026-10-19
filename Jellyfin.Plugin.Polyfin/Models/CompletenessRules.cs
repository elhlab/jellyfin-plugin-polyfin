using Jellyfin.Plugin.Polyfin.Database.Metadata;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// Rules for which fields each kind of metadata requires.
/// </summary>
public static class CompletenessRules
{
    /// <summary>
    /// Returns how complete the metadata is for its kind.
    /// </summary>
    /// <param name="metadata">The stored metadata to check.</param>
    /// <returns>The metadata's completeness.</returns>
    /// <remarks>Only movies are supported, so the movie rule always applies.</remarks>
    public static Completeness GetCompleteness(this StoredMetadata metadata) => GetMovieCompleteness(metadata);

    /// <summary>
    /// Returns how complete the metadata is for a movie.
    /// </summary>
    /// <param name="metadata">The stored metadata to check.</param>
    /// <returns>
    /// <see cref="Completeness.Incomplete"/> without a name or overview, <see cref="Completeness.Complete"/>
    /// without a tagline, otherwise <see cref="Completeness.Full"/>.
    /// </returns>
    /// <remarks>
    /// The tagline isn't required, as some movies do not have one.
    /// The metadata's kind isn't checked.
    /// </remarks>
    public static Completeness GetMovieCompleteness(StoredMetadata metadata)
    {
        if (metadata.Name is null || metadata.Overview is null)
        {
            return Completeness.Incomplete;
        }

        return metadata.Tagline is null
            ? Completeness.Complete
            : Completeness.Full;
    }
}
