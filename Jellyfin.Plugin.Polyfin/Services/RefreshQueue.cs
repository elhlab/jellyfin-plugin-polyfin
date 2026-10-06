using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Models;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// A priority queue of items to refresh, holding at most one entry per item and locale.
/// Items with the same priority are dequeued in the order they were queued.
/// Safe to use from multiple threads.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "It is a queue.")]
public sealed class RefreshQueue : IDisposable
{
    // Each queued item exists in a list and in the dictionary.
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _itemsAvailable = new(0);
    private readonly SortedDictionary<QueuePriority, LinkedList<QueuedItem>> _listsByPriority =
        new(
            Enum.GetValues<QueuePriority>()
                .ToDictionary(
                    priority => priority,
                    _ => new LinkedList<QueuedItem>()));

    private readonly Dictionary<(Guid ItemId, Locale Locale), LinkedListNode<QueuedItem>> _nodesByKey = [];

    /// <summary>
    /// Gets the number of queued items.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _nodesByKey.Count;
            }
        }
    }

    /// <summary>
    /// Queues an item. If it is already queued in the same locale, the existing entry keeps the higher
    /// priority and is refetched if either entry asks for it. Raising an entry's priority
    /// places it after existing entries at that priority.
    /// </summary>
    /// <param name="item">The item to queue.</param>
    /// <returns>
    /// True if the item was added; false if it was already queued in this locale,
    /// in which case its existing entry may have been updated.
    /// </returns>
    public bool Enqueue(QueuedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_lock)
        {
            if (_nodesByKey.TryGetValue((item.ItemId, item.Locale), out var node))
            {
                node.Value = node.Value with { Refetch = node.Value.Refetch || item.Refetch };

                if (item.Priority < node.Value.Priority)
                {
                    node.List!.Remove(node);
                    node.Value = node.Value with { Priority = item.Priority };
                    _listsByPriority[item.Priority].AddLast(node);
                }

                return false;
            }

            _nodesByKey[(item.ItemId, item.Locale)] = _listsByPriority[item.Priority].AddLast(item);
            _itemsAvailable.Release();
            return true;
        }
    }

    /// <summary>
    /// Waits for an item, then removes and returns the highest-priority one.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>The dequeued item.</returns>
    public async Task<QueuedItem> DequeueAsync(CancellationToken cancellationToken)
    {
        await _itemsAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

        lock (_lock)
        {
            foreach (var list in _listsByPriority.Values)
            {
                if (list.First is { } node)
                {
                    list.RemoveFirst();
                    _nodesByKey.Remove((node.Value.ItemId, node.Value.Locale));
                    return node.Value;
                }
            }
        }

        throw new InvalidOperationException("No item queued after the queue signalled one.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _itemsAvailable.Dispose();
    }
}
