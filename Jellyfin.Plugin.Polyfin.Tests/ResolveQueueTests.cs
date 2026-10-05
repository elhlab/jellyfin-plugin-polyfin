using System.Collections.Concurrent;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;

namespace Jellyfin.Plugin.Polyfin.Tests;

public sealed class ResolveQueueTests : IDisposable
{
    private static readonly Locale EnglishLocale = new("en", null);
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    private readonly ResolveQueue _queue = new();

    private static QueuedItem Item(Guid itemId, Locale locale,
        ResolvePriority priority = ResolvePriority.Normal, bool refetch = false)
        => new(itemId, BaseItemKind.Movie, locale, priority, refetch);

    private static Guid[] NewItemIds(int count)
        => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

    private Task<QueuedItem> DequeueAsync()
        => _queue.DequeueAsync(CancellationToken.None).WaitAsync(_timeout);

    /// <summary>
    /// Asserts that the queue is empty: Count is 0, and a dequeue on it ends only
    /// because it was cancelled, not by returning or failing on its own.
    /// </summary>
    private async Task AssertQueueIsEmptyAsync()
    {
        Assert.Equal(0, _queue.Count);

        using var cancellation = new CancellationTokenSource();
        var dequeue = _queue.DequeueAsync(cancellation.Token);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dequeue.WaitAsync(_timeout));
    }

    // Order

    [Fact]
    public async Task SamePriority_IsDequeuedInOrder()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Guid third = Guid.NewGuid();

        _queue.Enqueue(Item(first, EnglishLocale));
        _queue.Enqueue(Item(second, EnglishLocale));
        _queue.Enqueue(Item(third, EnglishLocale));

        Assert.Equal(first, (await DequeueAsync()).ItemId);
        Assert.Equal(second, (await DequeueAsync()).ItemId);
        Assert.Equal(third, (await DequeueAsync()).ItemId);
    }

    [Fact]
    public async Task HigherPriority_IsDequeuedFirst()
    {
        Guid normal = Guid.NewGuid();
        Guid high = Guid.NewGuid();

        _queue.Enqueue(Item(normal, EnglishLocale, ResolvePriority.Normal));
        _queue.Enqueue(Item(high, EnglishLocale, ResolvePriority.High));

        Assert.Equal(high, (await DequeueAsync()).ItemId);
        Assert.Equal(normal, (await DequeueAsync()).ItemId);
    }

    // Count

    [Fact]
    public async Task Count_TracksQueuedItems()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        _queue.Enqueue(Item(first, EnglishLocale));
        _queue.Enqueue(Item(second, EnglishLocale));
        _queue.Enqueue(Item(first, EnglishLocale));
        Assert.Equal(2, _queue.Count);

        await DequeueAsync();
        Assert.Equal(1, _queue.Count);
    }

    // Repeated items

    [Fact]
    public async Task RepeatedItem_IsDequeuedOnce()
    {
        var itemId = Guid.NewGuid();

        Assert.True(_queue.Enqueue(Item(itemId, EnglishLocale)));
        Assert.False(_queue.Enqueue(Item(itemId, EnglishLocale)));

        Assert.Equal(itemId, (await DequeueAsync()).ItemId);
        await AssertQueueIsEmptyAsync();
    }

    [Fact]
    public async Task RepeatedItem_IsNotMoved()
    {
        Guid repeated = Guid.NewGuid();
        Guid other = Guid.NewGuid();

        _queue.Enqueue(Item(repeated, EnglishLocale));
        _queue.Enqueue(Item(other, EnglishLocale));
        _queue.Enqueue(Item(repeated, EnglishLocale));

        Assert.Equal(repeated, (await DequeueAsync()).ItemId);
        Assert.Equal(other, (await DequeueAsync()).ItemId);
    }

    [Theory]
    [InlineData(ResolvePriority.High, ResolvePriority.Normal)]
    [InlineData(ResolvePriority.Normal, ResolvePriority.High)]
    public async Task RepeatedItem_UsesHighestPriority(params ResolvePriority[] priorities)
    {
        Guid other = Guid.NewGuid();
        Guid repeated = Guid.NewGuid();

        _queue.Enqueue(Item(other, EnglishLocale, ResolvePriority.Normal));
        foreach (var priority in priorities)
        {
            _queue.Enqueue(Item(repeated, EnglishLocale, priority));
        }

        var dequeued = await DequeueAsync();
        Assert.Equal(repeated, dequeued.ItemId);
        Assert.Equal(ResolvePriority.High, dequeued.Priority);
        Assert.Equal(other, (await DequeueAsync()).ItemId);
        await AssertQueueIsEmptyAsync();
    }

    [Fact]
    public async Task RaisedItem_ComesAfterExistingItemsAtNewPriority()
    {
        Guid high = Guid.NewGuid();
        Guid raised = Guid.NewGuid();

        _queue.Enqueue(Item(raised, EnglishLocale, ResolvePriority.Normal));
        _queue.Enqueue(Item(high, EnglishLocale, ResolvePriority.High));
        _queue.Enqueue(Item(raised, EnglishLocale, ResolvePriority.High));

        Assert.Equal(high, (await DequeueAsync()).ItemId);
        Assert.Equal(raised, (await DequeueAsync()).ItemId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RepeatedItem_KeepsRefetch(params bool[] refetches)
    {
        var itemId = Guid.NewGuid();

        foreach (var refetch in refetches)
        {
            _queue.Enqueue(Item(itemId, EnglishLocale, refetch: refetch));
        }

        Assert.True((await DequeueAsync()).Refetch);
    }

    [Fact]
    public async Task ItemInOtherLocale_IsQueuedSeparately()
    {
        var itemId = Guid.NewGuid();
        var germanLocale = new Locale("de", null);

        Assert.True(_queue.Enqueue(Item(itemId, EnglishLocale)));
        Assert.True(_queue.Enqueue(Item(itemId, germanLocale)));

        Assert.Equal(EnglishLocale, (await DequeueAsync()).Locale);
        Assert.Equal(germanLocale, (await DequeueAsync()).Locale);
        await AssertQueueIsEmptyAsync();
    }

    [Fact]
    public async Task DequeuedItem_CanBeQueuedAgain()
    {
        var itemId = Guid.NewGuid();

        _queue.Enqueue(Item(itemId, EnglishLocale));
        await DequeueAsync();

        Assert.True(_queue.Enqueue(Item(itemId, EnglishLocale)));
        Assert.Equal(itemId, (await DequeueAsync()).ItemId);
    }

    // Cancellation

    [Fact]
    public async Task CancelledConsumer_DoesNotStealItem()
    {
        using var cancellation = new CancellationTokenSource();
        var itemId = Guid.NewGuid();

        var cancelledDequeue = _queue.DequeueAsync(cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledDequeue.WaitAsync(_timeout));

        _queue.Enqueue(Item(itemId, EnglishLocale));

        Assert.Equal(itemId, (await DequeueAsync()).ItemId);
    }

    // Concurrency

    [Fact]
    public async Task ConcurrentEnqueue_QueuesEachItemOnce()
    {
        var itemIds = NewItemIds(1000);

        Parallel.ForEach(itemIds.Concat(itemIds), itemId => _queue.Enqueue(Item(itemId, EnglishLocale)));

        var dequeued = new List<Guid>();
        foreach (var _ in itemIds)
        {
            dequeued.Add((await DequeueAsync()).ItemId);
        }

        Assert.Equal(itemIds.Order(), dequeued.Order());
        await AssertQueueIsEmptyAsync();
    }

    [Fact]
    public async Task ConcurrentConsumers_ReceiveEachItemOnce()
    {
        var itemIds = NewItemIds(1000);
        var dequeued = new ConcurrentBag<Guid>();
        var remaining = itemIds.Length;

        var consumers = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            while (Interlocked.Decrement(ref remaining) >= 0)
            {
                dequeued.Add((await DequeueAsync()).ItemId);
            }
        })).ToArray();

        Parallel.ForEach(itemIds, itemId => _queue.Enqueue(Item(itemId, EnglishLocale)));
        await Task.WhenAll(consumers).WaitAsync(_timeout);

        Assert.Equal(itemIds.Order(), dequeued.Order());
        await AssertQueueIsEmptyAsync();
    }

    public void Dispose()
    {
        _queue.Dispose();
    }
}
