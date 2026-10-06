using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Tasks;

/// <summary>
/// Fetches and stores metadata for every movie in each configured locale.
/// </summary>
/// <remarks>
/// Jellyfin finds and constructs scheduled tasks itself; no registration in
/// <see cref="PluginServiceRegistrator"/> is needed.
/// </remarks>
public class RefreshMetadataTask(
    BackgroundMetadataRefresher refresher,
    MetadataResolver metadataResolver,
    LanguageResolver languageResolver,
    ILibraryManager libraryManager,
    ILogger<RefreshMetadataTask> logger) : IScheduledTask
{
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromSeconds(5);

    private readonly BackgroundMetadataRefresher _refresher = refresher;
    private readonly MetadataResolver _metadataResolver = metadataResolver;
    private readonly LanguageResolver _languageResolver = languageResolver;
    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly ILogger<RefreshMetadataTask> _logger = logger;

    /// <inheritdoc />
    public string Name => "Refresh Metadata";

    /// <inheritdoc />
    public string Key => "PolyfinRefreshMetadata";

    /// <inheritdoc />
    public string Description => "Refreshes metadata for your configured locales.";

    /// <inheritdoc />
    public string Category => "Polyfin";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var locales = _languageResolver.Languages
            .Select(language => language.MetadataLocale)
            .ToArray();

        var found = 0;
        foreach (var itemType in BackgroundMetadataRefresher.SupportedItemTypes)
        {
            found += QueueItemsToRefresh(itemType, locales);
        }

        _logger.LogInformation("Found {Count} items to refresh", found);
        if (found == 0)
        {
            return;
        }

        // Approximate: the queue also holds items queued outside this task, e.g. by item updates.
        while (_refresher.Count > 0)
        {
            progress.Report(Math.Clamp(100.0 * (found - _refresher.Count) / found, 0, 100));
            await Task.Delay(ProgressReportInterval, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Refresh finished");
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
            },
        ];
    }

    private int QueueItemsToRefresh(BaseItemKind itemType, Locale[] locales)
    {
        var itemIds = _libraryManager.GetItemIds(new InternalItemsQuery
        {
            IncludeItemTypes = [itemType],
            IsVirtualItem = false,
        });

        var found = 0;
        foreach (var locale in locales)
        {
            var itemsToRefresh = _metadataResolver.FilterItemsToRefresh(itemIds, locale);
            foreach (var itemId in itemsToRefresh)
            {
                _refresher.Enqueue(itemId, itemType, locale, QueuePriority.Normal, refetch: false);
            }

            found += itemsToRefresh.Count;
        }

        return found;
    }
}
