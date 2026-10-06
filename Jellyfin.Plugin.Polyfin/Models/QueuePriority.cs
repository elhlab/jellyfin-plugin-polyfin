namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// The priority used when refreshing a queued item. Lower values are refreshed first.
/// </summary>
public enum QueuePriority
{
    /// <summary>
    /// High refresh priority.
    /// </summary>
    High = 0,

    /// <summary>
    /// Normal refresh priority.
    /// </summary>
    Normal = 1,
}
