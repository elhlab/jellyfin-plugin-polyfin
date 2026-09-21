using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Polyfin.Database;

/// <summary>
/// Handles schema migrations for an sqlite database.
/// </summary>
public static class MigrationRunner
{
    /// <summary>
    /// Makes sure the migrations are up to date.
    /// </summary>
    /// <param name="connection">The sqlite database connection to migrate.</param>
    /// <param name="migrations">The migrations to apply. Unordered; they are applied in <see cref="Migration.Version"/> order.</param>
    public static void Migrate(SqliteConnection connection, IReadOnlyCollection<Migration> migrations)
    {
        var versions = new HashSet<int>();
        foreach (var migration in migrations)
        {
            if (!versions.Add(migration.Version))
            {
                throw new InvalidOperationException($"Duplicate migration version: {migration.Version}.");
            }
        }

        InitializeDatabase(connection);

        foreach (var migration in migrations.OrderBy(m => m.Version))
        {
            if (IsMigrationApplied(connection, migration))
            {
                continue;
            }

            ApplyMigration(connection, migration);
        }
    }

    private static void InitializeDatabase(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS __schema_migrations (
                version INTEGER PRIMARY KEY
            );
            """;
        command.ExecuteNonQuery();
    }

    private static bool IsMigrationApplied(SqliteConnection connection, Migration migration)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 1 FROM __schema_migrations WHERE version = $version;
            """;
        command.Parameters.AddWithValue("$version", migration.Version);

        return command.ExecuteScalar() is not null;
    }

    private static void ApplyMigration(SqliteConnection connection, Migration migration)
    {
        using var transaction = connection.BeginTransaction();

        try
        {
            using var script = connection.CreateCommand();
#pragma warning disable CA2100 // callers supply trusted SQL (embedded resources); by design, not user input
            script.CommandText = migration.Sql;
#pragma warning restore CA2100
            script.Transaction = transaction;
            script.ExecuteNonQuery();

            using var record = connection.CreateCommand();
            record.CommandText =
                """
                INSERT INTO __schema_migrations (version)
                VALUES ($version);
                """;
            record.Parameters.AddWithValue("$version", migration.Version);
            record.Transaction = transaction;
            record.ExecuteNonQuery();

            transaction.Commit();
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"Migration {migration.Version} ({migration.Name}) failed.", ex);
        }
    }
}
