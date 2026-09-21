using Jellyfin.Plugin.Polyfin.Database;
using Jellyfin.Plugin.Polyfin.Tests.Fixtures;
using Jellyfin.Plugin.Polyfin.Tests.Fixtures.BadName;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class EmbeddedMigrationsTests
{
    [Fact]
    public void For_LoadsMigrations_InVersionOrder()
    {
        var migrations = EmbeddedMigrations.For<MigrationFixtures>();

        Assert.Collection(
            migrations,
            first =>
            {
                Assert.Equal(1, first.Version);
                Assert.Equal("001_First.sql", first.Name);
                Assert.Contains("CREATE TABLE first", first.Sql, StringComparison.Ordinal);
            },
            second =>
            {
                Assert.Equal(2, second.Version);
                Assert.Equal("002_Second.sql", second.Name);
                Assert.Contains("CREATE TABLE second", second.Sql, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void For_Throws_WhenNoMigrationsEmbedded()
    {
        // This test class has no Migrations folder next to it.
        var exception = Assert.Throws<InvalidOperationException>(() => EmbeddedMigrations.For<EmbeddedMigrationsTests>());

        Assert.Contains("Jellyfin.Plugin.Polyfin.Tests.Migrations.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void For_Throws_WhenFileNameHasNoVersion()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EmbeddedMigrations.For<BadNameFixtures>());

        Assert.Contains("NoVersion.sql", exception.Message, StringComparison.Ordinal);
    }
}
