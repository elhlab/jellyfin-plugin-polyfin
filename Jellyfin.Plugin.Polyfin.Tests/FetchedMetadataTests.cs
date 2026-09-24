using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class FetchedMetadataTests
{
    [Fact]
    public void FillFrom_KeepsEarlierValues()
    {
        var fetched = new FetchedMetadata("Der Titel", null)
            .FillFrom(new Movie { Name = "Another title", Overview = "Die Beschreibung" });

        Assert.Equal(new FetchedMetadata("Der Titel", "Die Beschreibung"), fetched);
    }

    [Fact]
    public void FillFrom_IgnoresBlankValues()
    {
        var fetched = new FetchedMetadata(null, null)
            .FillFrom(new Movie { Name = "  ", Overview = string.Empty });

        Assert.Equal(new FetchedMetadata(null, null), fetched);
    }

    [Theory]
    [InlineData("Der Titel", "Die Beschreibung", true)]
    [InlineData("Der Titel", null, false)]
    [InlineData(null, "Die Beschreibung", false)]
    public void IsComplete_RequiresBothFields(string? name, string? overview, bool expected)
    {
        Assert.Equal(expected, new FetchedMetadata(name, overview).IsComplete);
    }
}
