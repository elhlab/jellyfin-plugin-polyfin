using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class CompletenessRulesTests
{
    public static TheoryData<StoredMetadata, Completeness> MovieCases => new TheoryData<StoredMetadata, Completeness>
    {
        { Samples.FullStored, Completeness.Full },
        { Samples.FullStored with { Tagline = null }, Completeness.Complete },
        { Samples.FullStored with { Name = null }, Completeness.Incomplete },
        { Samples.FullStored with { Overview = null }, Completeness.Incomplete },
        { Samples.EmptyStored, Completeness.Incomplete },
    };

    [Theory]
    [MemberData(nameof(MovieCases))]
    public void GetMovieCompleteness_RequiresNameAndOverview(StoredMetadata metadata, Completeness expected)
    {
        Assert.Equal(expected, CompletenessRules.GetMovieCompleteness(metadata));
        Assert.Equal(expected, metadata.GetCompleteness());
    }
}
