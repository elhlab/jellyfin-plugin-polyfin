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

    public static TheoryData<bool, bool, bool> AllFieldCombinations()
    {
        TheoryData<bool, bool, bool> data = new TheoryData<bool, bool, bool>();
        bool[] values = [true, false];
        foreach (bool name in values)
        {
            foreach (bool overview in values)
            {
                foreach (bool tagline in values)
                {
                    data.Add(name, overview, tagline);
                }
            }
        }

        return data;
    }

    private static FetchedMetadata FromFlags(bool name, bool overview, bool tagline) => new FetchedMetadata(
        name ? Samples.Name : null,
        overview ? Samples.Overview : null,
        tagline ? Samples.Tagline : null);

    [Theory]
    [MemberData(nameof(AllFieldCombinations))]
    public void IsComplete_WhenAllFieldsSet(bool name, bool overview, bool tagline)
    {
        Assert.Equal(name && overview && tagline, FromFlags(name, overview, tagline).IsComplete);
    }

    [Theory]
    [MemberData(nameof(AllFieldCombinations))]
    public void IsEmpty_WhenNoFieldsSet(bool name, bool overview, bool tagline)
    {
        Assert.Equal(!name && !overview && !tagline, FromFlags(name, overview, tagline).IsEmpty);
    }
}
