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
    public void MergedWith_FillsMissingFields()
    {
        var fetched = Samples.EmptyFetched.MergedWith(ToMovie(Samples.FullFetched));

        Assert.Equal(Samples.FullFetched, fetched);
    }

    [Fact]
    public void MergedWith_KeepsEarlierValues()
    {
        var fetched = Samples.FullFetched.MergedWith(
            ToMovie(new FetchedMetadata("Another title", "Another overview", "Another tagline"))
        );

        Assert.Equal(Samples.FullFetched, fetched);
    }

    [Fact]
    public void MergedWith_IgnoresBlankValues()
    {
        var fetched = Samples.EmptyFetched.MergedWith(
            ToMovie(new FetchedMetadata("  ", string.Empty, "  "))
        );

        Assert.Equal(Samples.EmptyFetched, fetched);
    }

    public static TheoryData<FetchedMetadata, bool> IsFullCases => new TheoryData<FetchedMetadata, bool>
    {
        { Samples.FullFetched, true },
        { Samples.FullFetched with { Name = null }, false },
        { Samples.FullFetched with { Overview = null }, false },
        { Samples.FullFetched with { Tagline = null }, false },
    };

    [Theory]
    [MemberData(nameof(IsFullCases))]
    public void IsFull_WhenAllFieldsSet(FetchedMetadata fetched, bool expected)
    {
        Assert.Equal(expected, fetched.IsFull);
    }

    public static TheoryData<FetchedMetadata, bool> IsEmptyCases => new TheoryData<FetchedMetadata, bool>
    {
        { Samples.EmptyFetched, true },
        { Samples.EmptyFetched with { Name = Samples.Name }, false },
        { Samples.EmptyFetched with { Overview = Samples.Overview }, false },
        { Samples.EmptyFetched with { Tagline = Samples.Tagline }, false },
    };

    [Theory]
    [MemberData(nameof(IsEmptyCases))]
    public void IsEmpty_WhenNoFieldsSet(FetchedMetadata fetched, bool expected)
    {
        Assert.Equal(expected, fetched.IsEmpty);
    }
}
