using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;

namespace Jellyfin.Plugin.Polyfin.Tests.Models;

public class RefreshBackoffTests
{
    [Fact]
    public void FullItemDoesNotHaveRefreshTime()
    {
        var refreshTime = RefreshBackoff.GetRefreshTime(Samples.FullStored, Samples.RefreshedAt);
        Assert.Null(refreshTime);
    }

    public static TheoryData<StoredMetadata, TimeSpan> NonFullItemCases => new TheoryData<StoredMetadata, TimeSpan>
    {
        { Samples.EmptyStored, TimeSpan.Zero },
        { Samples.EmptyStored, TimeSpan.FromDays(30) },
        { Samples.CompleteStored, TimeSpan.Zero },
        { Samples.CompleteStored, TimeSpan.FromDays(30) },
    };

    [Theory]
    [MemberData(nameof(NonFullItemCases))]
    public void NonFullItemAlwaysHasRefreshTime(StoredMetadata sample, TimeSpan delta)
    {
        var refreshTime = RefreshBackoff.GetRefreshTime(sample, Samples.RefreshedAt + delta);
        Assert.NotNull(refreshTime);
    }

    public static TheoryData<TimeSpan> EarlyCases => new TheoryData<TimeSpan>
    {
        { TimeSpan.Zero },
        { TimeSpan.FromMinutes(30) },
        { TimeSpan.FromHours(1) },
        { TimeSpan.FromHours(12) },
    };

    [Theory]
    [MemberData(nameof(EarlyCases))]
    public void ItemIsNotRefreshedSubHourly(TimeSpan delta)
    {
        var refreshTime = RefreshBackoff.GetRefreshTime(Samples.EmptyStored, Samples.RefreshedAt + delta);

        Assert.True(refreshTime >= Samples.RefreshedAt + TimeSpan.FromHours(1));
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(5, 10)]
    [InlineData(10, 20)]
    [InlineData(20, 60)]
    [InlineData(60, 100)]
    public void OlderItemWaitsLonger(int youngerDays, int olderDays)
    {
        var now = Samples.RefreshedAt;
        var younger = RefreshBackoff.GetRefreshTime(Samples.EmptyStored with { AddedAt = now - TimeSpan.FromDays(youngerDays) }, now);
        var older = RefreshBackoff.GetRefreshTime(Samples.EmptyStored with { AddedAt = now - TimeSpan.FromDays(olderDays) }, now);

        Assert.True(older > younger);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void CompleteItemWaitsLongerThanIncompleteItem(int ageDays)
    {
        var now = Samples.RefreshedAt;
        var addedAt = now - TimeSpan.FromDays(ageDays);
        var incomplete = RefreshBackoff.GetRefreshTime(Samples.EmptyStored with { AddedAt = addedAt }, now);
        var complete = RefreshBackoff.GetRefreshTime(Samples.CompleteStored with { AddedAt = addedAt }, now);

        Assert.True(complete > incomplete);
    }

    [Fact]
    public void ItemRefreshedLongAgoIsDue()
    {
        var now = Samples.RefreshedAt;
        var longAgo = now - TimeSpan.FromDays(1000);
        var refreshTime = RefreshBackoff.GetRefreshTime(Samples.EmptyStored with { AddedAt = longAgo, RefreshedAt = longAgo }, now);

        Assert.True(refreshTime <= now);
    }

    [Fact]
    public void VeryOldItemWaitsAtMost90Days()
    {
        var now = Samples.RefreshedAt;
        var refreshTime = RefreshBackoff.GetRefreshTime(Samples.EmptyStored with { AddedAt = now - TimeSpan.FromDays(1000) }, now);

        Assert.True(refreshTime <= Samples.RefreshedAt + TimeSpan.FromDays(90));
    }
}
