namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// How complete an item's metadata is.
/// </summary>
public enum Completeness
{
    /// <summary>
    /// A required field is missing.
    /// </summary>
    Incomplete,

    /// <summary>
    /// All required fields are set.
    /// </summary>
    Complete,

    /// <summary>
    /// All possible fields are set.
    /// </summary>
    Full,
}
