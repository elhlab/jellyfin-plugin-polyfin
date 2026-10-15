using Jellyfin.Plugin.Polyfin.Models;
using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class FetchedMetadataTests
{
    private static Movie ToMovie(FetchedMetadata values) => new Movie
    {
        Name = values.Name,
        Overview = values.Overview,
        Tagline = values.Tagline,
    };

    [Fact]
    public void FillFrom_FillsMissingFields()
    {
        var fetched = Samples.EmptyFetched.FillFrom(ToMovie(Samples.FullFetched));

        Assert.Equal(Samples.FullFetched, fetched);
    }

    [Fact]
    public void FillFrom_KeepsEarlierValues()
    {
        var fetched = Samples.FullFetched.FillFrom(
            ToMovie(new FetchedMetadata("Another title", "Another overview", "Another tagline"))
        );

        Assert.Equal(Samples.FullFetched, fetched);
    }

    [Fact]
    public void FillFrom_IgnoresBlankValues()
    {
        var fetched = Samples.EmptyFetched.FillFrom(
            ToMovie(new FetchedMetadata("  ", string.Empty, "  "))
        );

        Assert.Equal(Samples.EmptyFetched, fetched);
    }

    [Theory]
    [InlineData(Samples.Name, Samples.Overview, Samples.Tagline, true)]
    [InlineData(Samples.Name, Samples.Overview, null, false)]
    [InlineData(Samples.Name, null, Samples.Tagline, false)]
    [InlineData(null, Samples.Overview, null, false)]
    [InlineData(null, null, Samples.Tagline, false)]
    public void IsComplete_WhenAllFieldsSet(string? name, string? overview, string? tagline, bool expected)
    {
        Assert.Equal(expected, new FetchedMetadata(name, overview, tagline).IsComplete);
    }

    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData(Samples.Name, null, null, false)]
    [InlineData(null, Samples.Overview, null, false)]
    [InlineData(null, null, Samples.Tagline, false)]
    public void IsEmpty_WhenNoFieldsSet(string? name, string? overview, string? tagline, bool expected)
    {
        Assert.Equal(expected, new FetchedMetadata(name, overview, tagline).IsEmpty);
    }
}
