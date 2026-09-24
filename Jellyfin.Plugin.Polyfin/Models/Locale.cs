using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// A language with an optional country. <see cref="Country"/> is set only when the
/// source tag specified a region (e.g. "de-DE"); a language-only tag (e.g. "fi") leaves it null.
/// </summary>
/// <param name="Language">ISO 639-1 language code, e.g. "de".</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code, e.g. "DE", if resolved.</param>
public sealed record Locale(string Language, string? Country)
{
    /// <summary>
    /// Parses a BCP-47 tag (e.g. "de-DE", "fi") into a <see cref="Locale"/>.
    /// </summary>
    /// <param name="tag">The locale tag to parse.</param>
    /// <returns>The locale, or <see langword="null"/> if the tag isn't recognized.</returns>
    public static Locale? FromTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(tag);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }

        // RegionInfo infers a region even for neutral cultures when the language
        // happens to match a valid country code (e.g. "ar" can be interpreted as
        // Argentina). Only use the region when the tag explicitly specified one.
        var country = culture.IsNeutralCulture
            ? null
            : new RegionInfo(culture.Name).TwoLetterISORegionName;

        return new Locale(culture.TwoLetterISOLanguageName, country);
    }

    /// <summary>
    /// Parses a raw Accept-Language header value (e.g. "fi,en-US;q=0.9,en;q=0.8")
    /// into all recognized locale candidates, ordered by descending quality.
    /// Unrecognized candidates are omitted.
    /// </summary>
    /// <param name="acceptLanguageHeader">The raw Accept-Language header value.</param>
    /// <returns>The recognized locales, best first; empty if none are recognized.</returns>
    public static IReadOnlyList<Locale> AllFromAcceptLanguage(string acceptLanguageHeader)
    {
        return StringWithQualityHeaderValue.ParseList([acceptLanguageHeader])
            .OrderByDescending(candidate => candidate.Quality ?? 1)
            .Select(candidate => FromTag(candidate.Value.ToString()))
            .OfType<Locale>()
            .ToList();
    }
}
