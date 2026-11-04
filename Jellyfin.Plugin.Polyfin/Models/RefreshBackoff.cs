using System;
using Jellyfin.Plugin.Polyfin.Database.Metadata;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// Backoff rules for refreshing stored metadata.
/// </summary>
public static class RefreshBackoff
{
    /// <summary>
    /// How many times longer a complete item waits.
    /// </summary>
    private const int CompleteWaitMultiplier = 2;

    /// <summary>
    /// How long to wait since the last refresh, by item age. Ordered from youngest to oldest.
    /// An item exactly at an age limit falls in the next step, and the last step covers every older item.
    /// </summary>
    private static readonly (TimeSpan AgeBelow, TimeSpan Wait)[] _steps =
    {
        (TimeSpan.FromDays(3), TimeSpan.FromHours(12)),
        (TimeSpan.FromDays(7), TimeSpan.FromDays(1)),
        (TimeSpan.FromDays(14), TimeSpan.FromDays(2)),
        (TimeSpan.FromDays(30), TimeSpan.FromDays(7)),
        (TimeSpan.FromDays(90), TimeSpan.FromDays(30)),
        (TimeSpan.MaxValue, TimeSpan.FromDays(90)),
    };

    /// <summary>
    /// Gets a refresh time for a given stored metadata.
    /// An item that should be refreshed always gets a time.
    /// </summary>
    /// <param name="metadata">The stored metadata to check.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The time to refresh the item, or <see langword="null"/> if it should not be refreshed.</returns>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Newer items wait less than older items.</item>
    /// <item>Items are not refreshed within 1 hour of their last refresh.</item>
    /// <item><see cref="Completeness.Complete"/> items wait longer than <see cref="Completeness.Incomplete"/> items of the same age.</item>
    /// </list>
    /// </remarks>
    public static DateTimeOffset? GetRefreshTime(StoredMetadata metadata, DateTimeOffset now)
    {
        var completeness = metadata.GetCompleteness();

        // Rescanning could catch metadata changes over time, but isn't worth
        // supporting right now. Users can force an update per item if needed.
        if (completeness == Completeness.Full)
        {
            return null;
        }

        var wait = GetWait(now - metadata.CreatedAt);
        if (completeness == Completeness.Complete)
        {
            wait *= CompleteWaitMultiplier;
        }

        return metadata.RefreshedAt + wait;
    }

    private static TimeSpan GetWait(TimeSpan age)
    {
        foreach (var step in _steps)
        {
            if (age < step.AgeBelow)
            {
                return step.Wait;
            }
        }

        return _steps[^1].Wait;
    }
}
