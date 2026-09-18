using System.Globalization;
using System.Linq;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Polyfin.Services;

/// <summary>
/// A locale resolved for a single request. <see cref="Country"/> is set only when
/// the tag specified a region (e.g. "de-DE"); a language-only tag (e.g. "fi") leaves it null.
/// </summary>
/// <param name="Language">ISO 639-1 language code, e.g. "de".</param>
/// <param name="Country">ISO 3166-1 alpha-2 country code, e.g. "DE", if resolved.</param>
public sealed record ResolvedLocale(string Language, string? Country)
{
    /// <summary>
    /// Parses a BCP-47 tag (e.g. "de-DE", "fi") into a <see cref="ResolvedLocale"/>.
    /// </summary>
    /// <param name="tag">The locale tag to parse.</param>
    /// <returns>The resolved locale, or <see langword="null"/> if the tag isn't recognized.</returns>
    public static ResolvedLocale? FromTag(string tag)
    {
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

        return new ResolvedLocale(culture.TwoLetterISOLanguageName, country);
    }

    /// <summary>
    /// Parses a raw Accept-Language header value (e.g. "fi,en-US;q=0.9,en;q=0.8")
    /// and resolves the caller's highest-quality candidate that's actually
    /// recognized, trying the next one down if a candidate doesn't resolve.
    /// </summary>
    /// <param name="acceptLanguageHeader">The raw Accept-Language header value.</param>
    /// <returns>The resolved locale, or <see langword="null"/> if nothing resolves.</returns>
    public static ResolvedLocale? ResolveFromAcceptLanguage(string acceptLanguageHeader)
    {
        return StringWithQualityHeaderValue.ParseList([acceptLanguageHeader])
            .OrderByDescending(candidate => candidate.Quality ?? 1)
            .Select(candidate => FromTag(candidate.Value.ToString()))
            .FirstOrDefault(resolved => resolved is not null);
    }
}
