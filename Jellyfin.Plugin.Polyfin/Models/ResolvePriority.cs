namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// The priority used when resolving a queued item. Lower values are resolved first.
/// </summary>
public enum ResolvePriority
{
    /// <summary>
    /// High resolution priority.
    /// </summary>
    High = 0,

    /// <summary>
    /// Normal resolution priority.
    /// </summary>
    Normal = 1,
}
