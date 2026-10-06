using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Data.Events;
using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Fetches and stores metadata for queued items in the background.
/// </summary>
public class BackgroundMetadataRefresher(
    MetadataResolver metadataResolver,
    RefreshQueue queue,
    ILibraryManager libraryManager,
    IProviderManager providerManager,
    ILogger<BackgroundMetadataRefresher> logger) : BackgroundService
{
    // TODO: these probably should be advanced plugin settings just to let users fuck up their installation.
    // Read once at startup, changing them would require a restart.
    private const int WorkerCount = 2;
    private static readonly TimeSpan WaitCheckInterval = TimeSpan.FromSeconds(30);

    private readonly MetadataResolver _metadataResolver = metadataResolver;

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed by the DI container.")]
    private readonly RefreshQueue _queue = queue;

    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly IProviderManager _providerManager = providerManager;
    private readonly ILogger<BackgroundMetadataRefresher> _logger = logger;

    // Tracks folders currently undergoing a recursive refresh.
    private readonly ConcurrentDictionary<Guid, byte> _activeFolderRefreshes = new();

    private bool _waiting;

    /// <summary>
    /// Gets the supported item types.
    /// </summary>
    public static IReadOnlySet<BaseItemKind> SupportedItemTypes { get; } = new HashSet<BaseItemKind> { BaseItemKind.Movie };

    /// <summary>
    /// Gets the number of items currently queued for refreshing.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Queues an item to be refreshed in a given locale.
    /// </summary>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="itemType">The library item's type, e.g. Movie.</param>
    /// <param name="locale">The locale to refresh it in.</param>
    /// <param name="priority">How soon to refresh it.</param>
    /// <param name="refetch">Whether to fetch again even if metadata is already stored.</param>
    /// <exception cref="ArgumentException">The item type is not in <see cref="SupportedItemTypes"/>.</exception>
    public void Enqueue(Guid itemId, BaseItemKind itemType, Locale locale, QueuePriority priority, bool refetch)
    {
        ArgumentNullException.ThrowIfNull(locale);
        if (!SupportedItemTypes.Contains(itemType))
        {
            throw new ArgumentException($"Unsupported item type {itemType}.", nameof(itemType));
        }

        if (_queue.Enqueue(new QueuedItem(itemId, itemType, locale, priority, refetch)))
        {
            _logger.LogDebug(
                "Queued {ItemId} in {Locale} for refreshing ({Priority})",
                itemId,
                locale.ToTag(),
                priority);
        }
    }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _providerManager.RefreshStarted += OnRefreshStarted;
        _providerManager.RefreshCompleted += OnRefreshCompleted;
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        _providerManager.RefreshStarted -= OnRefreshStarted;
        _providerManager.RefreshCompleted -= OnRefreshCompleted;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(
            Enumerable.Range(0, WorkerCount)
            .Select(_ => RunWorkerAsync(stoppingToken)));
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            var item = await _queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
            await WaitForJellyfinAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await RefreshItemAsync(item, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Refreshing {ItemId} in {Locale} failed", item.ItemId, item.Locale.ToTag());
            }
        }
    }

    private async Task WaitForJellyfinAsync(CancellationToken cancellationToken)
    {
        if (!ShouldWait())
        {
            return;
        }

        if (!Interlocked.Exchange(ref _waiting, true))
        {
            _logger.LogDebug("Jellyfin is refreshing metadata, waiting until it finishes");
        }

        while (ShouldWait())
        {
            await Task.Delay(WaitCheckInterval, cancellationToken).ConfigureAwait(false);
        }

        if (Interlocked.Exchange(ref _waiting, false))
        {
            _logger.LogDebug("Jellyfin finished refreshing metadata, resuming");
        }
    }

    /// <summary>
    /// Determines whether Jellyfin is currently processing metadata.
    /// </summary>
    /// <remarks>
    /// Scans and recursive folder refreshes can generate enough provider requests to contend
    /// with this refresher, so the refresher waits for them to finish. Individual item refreshes are not tracked
    /// as they generate relatively little provider traffic.
    /// </remarks>
    /// <returns>True if the refresher should wait, false otherwise.</returns>
    private bool ShouldWait()
    {
        return _libraryManager.IsScanRunning
            || !_activeFolderRefreshes.IsEmpty;
    }

    private void OnRefreshStarted(object? sender, GenericEventArgs<BaseItem> e)
    {
        _activeFolderRefreshes.TryAdd(e.Argument.Id, 0);
    }

    private void OnRefreshCompleted(object? sender, GenericEventArgs<BaseItem> e)
    {
        _activeFolderRefreshes.TryRemove(e.Argument.Id, out _);
    }

    private Task RefreshItemAsync(QueuedItem item, CancellationToken cancellationToken)
    {
        return item.ItemType switch
        {
            BaseItemKind.Movie =>
                _metadataResolver.RefreshAsync<Movie, MovieInfo>(item.ItemId, item.Locale, item.Refetch, cancellationToken),

            _ => throw new UnreachableException($"{item.ItemType} is in SupportedItemTypes but has no refresh case."),
        };
    }
}
