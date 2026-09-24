using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Jellyfin.Plugin.Polyfin.Configuration;
using Jellyfin.Plugin.Polyfin.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// Resolves the language to use for a client from the configured languages.
/// </summary>
public class LanguageResolver
{
    private LanguageLookups _languages;

    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageResolver"/> class.
    /// </summary>
    /// <param name="configuration">The current plugin configuration.</param>
    /// <param name="onConfigurationChanged">Registers a callback to run with the new configuration whenever it's saved.</param>
    /// <param name="logger">The logger used to report invalid configuration entries.</param>
    public LanguageResolver(PluginConfiguration configuration, Action<Action<PluginConfiguration>> onConfigurationChanged, ILogger<LanguageResolver> logger)
    {
        _languages = BuildLookups(configuration, logger);
        onConfigurationChanged(changed => _languages = BuildLookups(changed, logger));
    }

    /// <summary>
    /// Matches a configured language against the client's accepted languages.
    /// Each accepted locale is tried in quality order, first exactly and then
    /// by language only.
    /// </summary>
    /// <param name="acceptLanguageHeader">The user's raw <c>Accept-Language</c> header.</param>
    /// <returns>The matched language, or <see langword="null"/> if no configured language matches.</returns>
    private Language? MatchLanguageByHeader(string acceptLanguageHeader)
    {
        var locales = Locale.AllFromAcceptLanguage(acceptLanguageHeader);

        foreach (var locale in locales)
        {
            var language = _languages.ByTag.GetValueOrDefault(locale)
                ?? _languages.ByTag.GetValueOrDefault(locale with { Country = null });

            if (language is not null)
            {
                return language;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the language explicitly configured for a user.
    /// </summary>
    /// <param name="userId">The Jellyfin user ID.</param>
    /// <returns>The user's configured language, or <see langword="null"/> if none is configured.</returns>
    private Language? MatchLanguageByUserId(Guid userId)
    {
        return _languages.ByUser.GetValueOrDefault(userId);
    }

    /// <summary>
    /// Resolves the language for a client, preferring a user-specific setting
    /// over the languages provided by the client.
    /// </summary>
    /// <param name="userId">The Jellyfin user ID, or <see cref="Guid.Empty"/> if there is no user.</param>
    /// <param name="acceptLanguageHeader">The raw <c>Accept-Language</c> header, possibly empty.</param>
    /// <returns>The resolved language, or <see langword="null"/> if no configured language matches.</returns>
    public Language? Resolve(Guid userId, string acceptLanguageHeader)
    {
        return MatchLanguageByUserId(userId)
            ?? MatchLanguageByHeader(acceptLanguageHeader);
    }

    /// <summary>
    /// Builds lookup tables from the configured languages and user assignments.
    /// Invalid entries are logged and ignored. Languages with duplicate ids,
    /// match locales assigned to multiple languages, and users assigned to
    /// multiple languages are dropped entirely rather than picking one.
    /// </summary>
    /// <param name="configuration">The plugin configuration.</param>
    /// <param name="logger">The logger used to report invalid configuration entries.</param>
    /// <returns>The lookup tables used to resolve languages.</returns>
    private static LanguageLookups BuildLookups(PluginConfiguration configuration, ILogger logger)
    {
        var languages = new List<(Language Language, string[] MatchLocales)>();
        foreach (var configured in configuration.Languages)
        {
            var metadataLocale = Locale.FromTag(configured.MetadataLocale);
            if (metadataLocale is null)
            {
                logger.LogWarning("Skipping language {Language}: metadata locale '{MetadataLocale}' is not a recognized tag", configured.Name, configured.MetadataLocale);
                continue;
            }

            languages.Add((new Language(configured.Id, configured.Name, metadataLocale), configured.MatchLocales));
        }

        var byId = new Dictionary<string, Language>(StringComparer.Ordinal);
        var sharedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (language, _) in languages)
        {
            if (!byId.TryAdd(language.Id, language) && sharedIds.Add(language.Id))
            {
                logger.LogWarning("Skipping every language with id '{Id}': more than one language uses it", language.Id);
            }
        }

        foreach (var id in sharedIds)
        {
            byId.Remove(id);
        }

        var byTag = new Dictionary<Locale, Language>();
        var sharedLocales = new HashSet<Locale>();
        foreach (var (language, matchLocales) in languages)
        {
            if (sharedIds.Contains(language.Id))
            {
                continue;
            }

            foreach (var tag in matchLocales)
            {
                var locale = Locale.FromTag(tag);
                if (locale is null)
                {
                    logger.LogWarning("Ignoring match locale '{Tag}' of {Language}: not a recognized tag", tag, language.Name);
                }
                else if (!byTag.TryAdd(locale, language) && byTag[locale] != language && sharedLocales.Add(locale))
                {
                    logger.LogWarning("Ignoring match locale '{Tag}': both {Language} and {Other} list it", tag, language.Name, byTag[locale].Name);
                }
            }
        }

        foreach (var locale in sharedLocales)
        {
            byTag.Remove(locale);
        }

        var byUser = new Dictionary<Guid, Language>();
        var sharedUsers = new HashSet<Guid>();
        foreach (var user in configuration.Users)
        {
            if (!Guid.TryParse(user.UserId, out var userId))
            {
                logger.LogWarning("Ignoring user assignment: '{UserId}' is not a user id", user.UserId);
            }
            else if (!byId.TryGetValue(user.LanguageId, out var language))
            {
                logger.LogWarning("Ignoring user assignment for {UserId}: no language has id '{LanguageId}'", userId, user.LanguageId);
            }
            else if (!byUser.TryAdd(userId, language) && byUser[userId] != language && sharedUsers.Add(userId))
            {
                logger.LogWarning("Ignoring every assignment for {UserId}: the user is assigned more than one language", userId);
            }
        }

        foreach (var userId in sharedUsers)
        {
            byUser.Remove(userId);
        }

        return new LanguageLookups(byTag.ToFrozenDictionary(), byId.ToFrozenDictionary(StringComparer.Ordinal), byUser.ToFrozenDictionary());
    }

    private sealed record LanguageLookups(
        IReadOnlyDictionary<Locale, Language> ByTag,
        IReadOnlyDictionary<string, Language> ById,
        IReadOnlyDictionary<Guid, Language> ByUser);
}
