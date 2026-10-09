using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class FetchedMetadataTests
{
    [Fact]
    public void FillFrom_FillsMissingFields()
    {
        var fetched = new FetchedMetadata(null, null, null)
            .FillFrom(new Movie { Name = "Der Titel", Overview = "Die Beschreibung", Tagline = "Der Slogan" });

        Assert.Equal(new FetchedMetadata("Der Titel", "Die Beschreibung", "Der Slogan"), fetched);
    }

    [Fact]
    public void FillFrom_KeepsEarlierValues()
    {
        var fetched = new FetchedMetadata("Der Titel", "Die Beschreibung", "Der Slogan")
            .FillFrom(new Movie { Name = "Another title", Overview = "Another overview", Tagline = "Another tagline" });

        Assert.Equal(new FetchedMetadata("Der Titel", "Die Beschreibung", "Der Slogan"), fetched);
    }

    [Fact]
    public void FillFrom_IgnoresBlankValues()
    {
        var fetched = new FetchedMetadata(null, null, null)
            .FillFrom(new Movie { Name = "  ", Overview = string.Empty, Tagline = "  " });

        Assert.Equal(new FetchedMetadata(null, null, null), fetched);
    }

    [Theory]
    [InlineData("Der Titel", "Die Beschreibung", "Der Slogan", true)]
    [InlineData("Der Titel", "Die Beschreibung", null, false)]
    [InlineData("Der Titel", null, "Der Slogan", false)]
    [InlineData(null, "Die Beschreibung", null, false)]
    [InlineData(null, null, "Der Slogan", false)]
    public void IsFullyFilled_RequiresAllFields(string? name, string? overview, string? tagline, bool expected)
    {
        Assert.Equal(expected, new FetchedMetadata(name, overview, tagline).IsFullyFilled);
    }
}
