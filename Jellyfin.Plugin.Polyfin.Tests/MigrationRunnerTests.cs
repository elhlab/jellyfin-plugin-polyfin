using Jellyfin.Plugin.Polyfin.Database;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class MigrationRunnerTests
{
    [Fact]
    public void Migrate_Throws_OnDuplicateVersion()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Migration[] migrations =
        [
            new Migration(1, "001_First", "CREATE TABLE foo (id INTEGER);"),
            new Migration(1, "001_Second", "CREATE TABLE bar (id INTEGER);"),
        ];

        Assert.Throws<InvalidOperationException>(() => MigrationRunner.Migrate(connection, migrations));
    }

    [Fact]
    public void Migrate_AppliesOnlyNewMigrations_OnSecondCall()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Migration[] first = [new Migration(1, "001_First", "CREATE TABLE foo (id INTEGER);")];
        MigrationRunner.Migrate(connection, first);

        Migration[] second =
        [
            new Migration(1, "001_First", "CREATE TABLE foo (id INTEGER);"),
            new Migration(2, "002_Second", "CREATE TABLE bar (id INTEGER);"),
        ];

        // If version 1 were re-applied here instead of skipped, this would throw
        // (the table already exists), so a passing test also proves the skip works.
        MigrationRunner.Migrate(connection, second);

        using var checkBar = connection.CreateCommand();
        checkBar.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'bar';";
        Assert.Equal(1L, (long)checkBar.ExecuteScalar()!);
    }

    [Fact]
    public void Migrate_SkipsAlreadyAppliedMigration()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Each application appends a row, so a re-applied migration is directly
        // observable as a second row, rather than inferred from the absence of
        // an error.
        Migration[] migrations =
        [
            new Migration(1, "001_First", "CREATE TABLE log (id INTEGER); INSERT INTO log (id) VALUES (1);"),
        ];

        MigrationRunner.Migrate(connection, migrations);
        MigrationRunner.Migrate(connection, migrations);

        using var checkLog = connection.CreateCommand();
        checkLog.CommandText = "SELECT COUNT(*) FROM log;";
        Assert.Equal(1L, (long)checkLog.ExecuteScalar()!);
    }

    [Fact]
    public void Migrate_AppliesInVersionOrder_RegardlessOfInputOrder()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Migration[] migrations =
        [
            new Migration(2, "002_Second", "ALTER TABLE foo ADD COLUMN name TEXT;"),
            new Migration(1, "001_First", "CREATE TABLE foo (id INTEGER);"),
        ];

        // If these ran in the given (wrong) order, the ALTER TABLE would fail
        // with "no such table: foo" instead of just quietly succeeding either way.
        MigrationRunner.Migrate(connection, migrations);

        using var checkColumn = connection.CreateCommand();
        checkColumn.CommandText = "SELECT COUNT(*) FROM pragma_table_info('foo') WHERE name = 'name';";
        Assert.Equal(1L, (long)checkColumn.ExecuteScalar()!);
    }

    [Fact]
    public void Migrate_DoesNotThrow_WithNoMigrations()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var exception = Record.Exception(() => MigrationRunner.Migrate(connection, []));

        Assert.Null(exception);
    }

    [Fact]
    public void Migrate_KeepsEarlierMigration_ButDiscardsFailedOne()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Migration[] migrations =
        [
            new Migration(1, "001_Working", "CREATE TABLE foo (id INTEGER);"),
            new Migration(2, "002_Broken", "CREATE TABLE bar (id INTEGER); CREATE TABLE bar (id INTEGER);"),
        ];

        // Each migration commits its own transaction, so migration 1 succeeding
        // should not depend on migration 2 (applied right after it) also succeeding.
        Assert.Throws<InvalidOperationException>(() => MigrationRunner.Migrate(connection, migrations));

        using var checkFoo = connection.CreateCommand();
        checkFoo.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'foo';";
        Assert.Equal(1L, (long)checkFoo.ExecuteScalar()!);

        using var checkBar = connection.CreateCommand();
        checkBar.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'bar';";
        Assert.Equal(0L, (long)checkBar.ExecuteScalar()!);

        using var checkVersions = connection.CreateCommand();
        checkVersions.CommandText = "SELECT version FROM __schema_migrations;";
        using var reader = checkVersions.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.False(reader.Read());
    }
}
