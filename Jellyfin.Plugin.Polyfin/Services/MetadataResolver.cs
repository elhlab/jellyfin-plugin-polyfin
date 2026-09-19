using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Resolves metadata for an item in the specified locale, or the default locale,
/// using the library's own metadata providers (TMDb, TVDb, etc.).
/// </summary>
public class MetadataResolver(ILibraryManager libraryManager, IProviderManager providerManager, ILogger<MetadataResolver> logger)
{
    private readonly ILibraryManager _libraryManager = libraryManager;
    private readonly IProviderManager _providerManager = providerManager;
    private readonly ILogger<MetadataResolver> _logger = logger;

    /// <summary>
    /// Gets or sets how long a single provider may take before it is skipped and the next one is tried.
    /// </summary>
    // TODO: once the settings page exists, this should be read from there directly.
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Looks up the item by id, then resolves it using the same metadata resolution
    /// as the item overload. Returns null if the id does not refer to a <typeparamref name="TItem"/>.
    /// </summary>
    /// <typeparam name="TItem">The item's concrete type, e.g. Movie, Season or Episode.</typeparam>
    /// <typeparam name="TInfo">The lookup info type for TItem, e.g. MovieInfo or EpisodeInfo.</typeparam>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="locale">
    /// The locale to request, or null to use the library's own language.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token to observe. Each provider is also given <see cref="ProviderTimeout"/>;
    /// one that overruns is skipped, not fatal.
    /// </param>
    /// <returns>
    /// The resolved metadata, or null if the id does not refer to a <typeparamref name="TItem"/>.
    /// </returns>
    public async Task<ResolvedMetadata?> ResolveMetadataAsync<TItem, TInfo>(Guid itemId, Locale? locale, CancellationToken cancellationToken)
        where TItem : BaseItem, IHasLookupInfo<TInfo>
        where TInfo : ItemLookupInfo, new()
    {
        if (_libraryManager.GetItemById(itemId) is TItem item)
        {
            return await ResolveMetadataAsync<TItem, TInfo>(item, locale, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Resolves <paramref name="item"/> for the specified <paramref name="locale"/>
    /// using the configured remote metadata providers in order. For each field,
    /// the first provider that supplies a valid value is used. Fallbacks are not
    /// handled: if a provider returns metadata in a different language than requested,
    /// its result is ignored.
    /// </summary>
    /// <typeparam name="TItem">The item's concrete type, e.g. Movie, Season or Episode.</typeparam>
    /// <typeparam name="TInfo">The lookup info type for TItem, e.g. MovieInfo or EpisodeInfo.</typeparam>
    /// <param name="item">The library item to resolve.</param>
    /// <param name="locale">
    /// The locale to request, or null to use the library's own language.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token to observe. Each provider is also given <see cref="ProviderTimeout"/>;
    /// one that overruns is skipped, not fatal.
    /// </param>
    /// <returns>
    /// The merged metadata, or null if no provider returned metadata in the requested language.
    /// </returns>
    public async Task<ResolvedMetadata?> ResolveMetadataAsync<TItem, TInfo>(TItem item, Locale? locale, CancellationToken cancellationToken)
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

        ResolvedMetadata? merged = null;
        foreach (var provider in _providerManager.GetMetadataProviders<TItem>(item, libraryOptions))
        {
            if (provider is not IRemoteMetadataProvider<TItem, TInfo> remote)
            {
                continue;
            }

            using var providerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            providerCts.CancelAfter(ProviderTimeout);

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

                merged = (merged ?? new ResolvedMetadata(null, null)).FillFrom(result.Item);

                if (merged.IsComplete)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("{Provider} timed out after {Timeout} for {Item}, trying next provider", provider.Name, ProviderTimeout, item.Name);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "{Provider} failed for {Item}, trying next provider", provider.Name, item.Name);
            }
        }

        return merged;
    }

    /// <summary>
    /// Compares language subtags only, so "de" matches "de-AT". An unreported
    /// language counts as a match.
    /// </summary>
    private static bool IsSameLanguage(string? resultLanguage, string? requestedLanguage)
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
