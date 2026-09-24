using Jellyfin.Plugin.Polyfin.Services;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class MetadataFetcherTests
{
    [Theory]
    [InlineData("de", "de", true)]
    [InlineData("de-AT", "de", true)]
    [InlineData("de", "de-DE", true)]
    [InlineData("DE", "de", true)]
    [InlineData("fr", "de", false)]
    [InlineData("fr-DE", "de", false)]
    [InlineData(null, "de", true)]
    [InlineData("", "de", true)]
    public void IsSameLanguage_ComparesLanguageOnly(string? resultLanguage, string requestedLanguage, bool expected)
    {
        Assert.Equal(expected, MetadataFetcher.IsSameLanguage(resultLanguage, requestedLanguage));
    }
}
