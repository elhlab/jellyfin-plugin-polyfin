using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class CompletenessRulesTests
{
    public static TheoryData<StoredMetadata, bool> MovieCases => new TheoryData<StoredMetadata, bool>
    {
        { Samples.FullStored, true },
        { Samples.FullStored with { Name = null }, false },
        { Samples.FullStored with { Overview = null }, false },
        { Samples.FullStored with { Tagline = null }, true },
    };

    [Theory]
    [MemberData(nameof(MovieCases))]
    public void IsMovieComplete_RequiresNameAndOverview(StoredMetadata metadata, bool expected)
    {
        Assert.Equal(expected, CompletenessRules.IsMovieComplete(metadata));
        Assert.Equal(expected, metadata.IsComplete());
    }
}
