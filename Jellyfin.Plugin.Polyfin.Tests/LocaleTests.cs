using Jellyfin.Plugin.Polyfin.Models;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class LocaleTests
{
    [Theory]
    [InlineData("de-DE", "de", "DE")]
    [InlineData("en-US", "en", "US")]
    [InlineData("de-at", "de", "AT")]
    [InlineData("DE-AT", "de", "AT")]
    public void FromTag_ParsesLanguageAndCountry(string tag, string expectedLanguage, string expectedCountry)
    {
        var result = Locale.FromTag(tag);

        Assert.NotNull(result);
        Assert.Equal(expectedLanguage, result.Language);
        Assert.Equal(expectedCountry, result.Country);
    }

    [Theory]
    [InlineData("fi", "fi")]
    [InlineData("en", "en")]
    [InlineData("ar", "ar")]
    public void FromTag_DoesNotInferCountryForLanguageOnlyTag(
        string tag,
        string expectedLanguage)
    {
        var result = Locale.FromTag(tag);

        Assert.NotNull(result);
        Assert.Equal(tag, expectedLanguage);
        Assert.Null(result.Country);
    }

    [Theory]
    [InlineData("de", null, "de")]
    [InlineData("de", "DE", "de-DE")]
    public void ToTag_FormatsTag(string language, string? country, string expected)
    {
        Assert.Equal(expected, new Locale(language, country).ToTag());
    }

    // CultureInfo accepts almost any letters-and-hyphens string as a custom culture,
    // so only malformed tags are rejected, not unknown languages.
    [Theory]
    [InlineData("")]
    [InlineData("de DE")]
    [InlineData("de-")]
    [InlineData("de-AT,de-CH")]
    public void FromTag_ReturnsNullForMalformedTag(string tag)
    {
        Assert.Null(Locale.FromTag(tag));
    }

    [Theory]
    [InlineData("fi,en-US;q=0.9", new[] { "fi", "en-US" })]
    [InlineData("en-US;q=0.9,fi", new[] { "fi", "en-US" })]
    [InlineData("de;q=0.5,en-GB;q=0.8", new[] { "en-GB", "de" })]
    [InlineData("de,fi", new[] { "de", "fi" })]
    [InlineData("*,de-DE;q=0.8", new[] { "de-DE" })]
    [InlineData("*", new string[] { })]
    [InlineData("", new string[] { })]
    public void AllFromAcceptLanguage_OrdersRecognizedByQuality(string header, string[] expectedTags)
    {
        var expected = expectedTags.Select(tag => tag.Split('-')).Select(parts => new Locale(parts[0], parts.Length > 1 ? parts[1] : null));

        Assert.Equal(expected, Locale.AllFromAcceptLanguage(header));
    }
}
