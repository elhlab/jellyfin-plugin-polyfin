using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Fetches metadata for an item in the specified locale using the library's
/// configured metadata providers.
/// </summary>
public class MetadataFetcher(ILibraryManager libraryManager, IProviderManager providerManager, ILogger<MetadataFetcher> logger)
{
    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly IProviderManager _providerManager = providerManager;
    private readonly ILogger<MetadataFetcher> _logger = logger;

    /// <summary>
    /// Looks up the item by ID and fetches its metadata.
    /// </summary>
    /// <typeparam name="TItem">The item's concrete type, e.g. Movie, Season or Episode.</typeparam>
    /// <typeparam name="TInfo">The lookup info type for TItem, e.g. MovieInfo or EpisodeInfo.</typeparam>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="locale">
    /// The locale to request, or null to use the library's own language.
    /// </param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>
    /// The merged metadata, empty if no provider returned metadata in the requested language,
    /// and whether any provider failed or timed out.
    /// </returns>
    /// <exception cref="FetchItemNotFoundException">
    /// The id does not refer to a <typeparamref name="TItem"/>.
    /// </exception>
    public virtual async Task<(FetchedMetadata Metadata, bool HadFailures)> FetchMetadataAsync<TItem, TInfo>(Guid itemId, Locale? locale, CancellationToken cancellationToken)
        where TItem : BaseItem, IHasLookupInfo<TInfo>
        where TInfo : ItemLookupInfo, new()
    {
        if (_libraryManager.GetItemById(itemId) is TItem item)
        {
            return await FetchMetadataAsync<TItem, TInfo>(item, locale, cancellationToken).ConfigureAwait(false);
        }

        throw new FetchItemNotFoundException($"Item {itemId} not found or not a {typeof(TItem).Name}");
    }

    /// <summary>
    /// Fetches the item's metadata using the configured remote providers.
    /// </summary>
    /// <typeparam name="TItem">The item's concrete type, e.g. Movie, Season or Episode.</typeparam>
    /// <typeparam name="TInfo">The lookup info type for TItem, e.g. MovieInfo or EpisodeInfo.</typeparam>
    /// <param name="item">The library item to fetch metadata for.</param>
    /// <param name="locale">
    /// The locale to request, or null to use the library's own language.
    /// </param>
    /// <param name="cancellationToken">The cancellation token to observe.</param>
    /// <returns>
    /// The merged metadata, empty if no provider returned metadata in the requested language,
    /// and whether any provider failed or timed out.
    /// </returns>
    public async Task<(FetchedMetadata Metadata, bool HadFailures)> FetchMetadataAsync<TItem, TInfo>(TItem item, Locale? locale, CancellationToken cancellationToken)
        where TItem : BaseItem, IHasLookupInfo<TInfo>
        where TInfo : ItemLookupInfo, new()
    {
        TInfo info = item.GetLookupInfo();
        if (locale is not null)
        {
            info.MetadataLanguage = locale.Language;
            info.MetadataCountryCode = locale.Country;
        }

        var libraryOptions = _libraryManager.GetLibraryOptions(item);

        var providerTimeout = TimeSpan.FromSeconds(Plugin.Instance!.Configuration.ProviderTimeoutSeconds);

        var merged = new FetchedMetadata(null, null, null);
        var hadFailures = false;
        foreach (var provider in _providerManager.GetMetadataProviders<TItem>(item, libraryOptions))
        {
            if (provider is not IRemoteMetadataProvider<TItem, TInfo> remote)
            {
                continue;
            }

            using var providerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            providerCts.CancelAfter(providerTimeout);

            try
            {
                var result = await remote.GetMetadata(info, providerCts.Token).ConfigureAwait(false);
                if (!result.HasMetadata)
                {
                    _logger.LogDebug("{Provider} returned no metadata for {Item}", provider.Name, item.Name);
                    continue;
                }

                if (!IsSameLanguage(result.ResultLanguage, info.MetadataLanguage))
                {
                    _logger.LogDebug("{Provider} answered in {ResultLanguage} instead of {Language} for {Item}, skipped", provider.Name, result.ResultLanguage, info.MetadataLanguage, item.Name);
                    continue;
                }

                merged = merged.FillFrom(result.Item);

                if (merged.IsComplete)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                hadFailures = true;
                _logger.LogWarning("{Provider} timed out after {Timeout} for {Item}, trying next provider", provider.Name, providerTimeout, item.Name);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                hadFailures = true;
                _logger.LogWarning(ex, "{Provider} failed for {Item}, trying next provider", provider.Name, item.Name);
            }
        }

        return (merged, hadFailures);
    }

    /// <summary>
    /// Compares language subtags only, so "de" matches "de-AT". An unreported
    /// language counts as a match.
    /// </summary>
    /// <param name="resultLanguage">The language to compare.</param>
    /// <param name="requestedLanguage">The language to compare against.</param>
    /// <returns>Whether the languages match.</returns>
    internal static bool IsSameLanguage(string? resultLanguage, string? requestedLanguage)
    {
        if (string.IsNullOrEmpty(resultLanguage) || string.IsNullOrEmpty(requestedLanguage))
        {
            return true;
        }

        return string.Equals(Subtag(resultLanguage), Subtag(requestedLanguage), StringComparison.OrdinalIgnoreCase);

        static string Subtag(string language)
        {
            var dash = language.IndexOf('-', StringComparison.Ordinal);
            return dash < 0 ? language : language[..dash];
        }
    }
}
