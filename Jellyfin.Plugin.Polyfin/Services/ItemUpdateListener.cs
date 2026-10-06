using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Listens for library items whose metadata changed, so their translations can be fetched again.
/// </summary>
public class ItemUpdateListener(
    ILibraryManager libraryManager,
    BackgroundMetadataRefresher refresher,
    LanguageResolver languageResolver,
    ILogger<ItemUpdateListener> logger) : IHostedService
{
    private const ItemUpdateType MetadataChanged =
        ItemUpdateType.MetadataImport | ItemUpdateType.MetadataDownload;

    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly BackgroundMetadataRefresher _refresher = refresher;
    private readonly LanguageResolver _languageResolver = languageResolver;
    private readonly ILogger<ItemUpdateListener> _logger = logger;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated += OnItemUpdated;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated -= OnItemUpdated;
        return Task.CompletedTask;
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if ((e.UpdateReason & MetadataChanged) == 0)
        {
            return;
        }

        var itemType = e.Item.GetBaseItemKind();
        if (!BackgroundMetadataRefresher.SupportedItemTypes.Contains(itemType))
        {
            return;
        }

        // No provider ids yet. Jellyfin raises another update if and when it finds them.
        if (e.Item.ProviderIds.Count == 0)
        {
            return;
        }

        _logger.LogDebug("{Item} metadata changed ({Reason}), queued for refreshing", e.Item.Name, e.UpdateReason);

        // Refetch even if stored: the item may have been re-identified with new provider ids.
        foreach (var language in _languageResolver.Languages)
        {
            _refresher.Enqueue(e.Item.Id, itemType, language.MetadataLocale, QueuePriority.Normal, refetch: true);
        }
    }
}
