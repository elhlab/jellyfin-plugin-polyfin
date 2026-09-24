using System;
using System.IO;
using Jellyfin.Plugin.Polyfin.Models;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Polyfin.Database.Metadata;

/// <summary>
/// Stores metadata in an sqlite database.
/// </summary>
public class MetadataStore
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataStore"/> class, migrating the
    /// database to the latest schema if needed.
    /// </summary>
    /// <param name="dataFolderPath">The folder the sqlite database lives in. Must already exist.</param>
    public MetadataStore(string dataFolderPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Join(dataFolderPath, "metadata.db"),
            Pooling = true,
        }.ConnectionString;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        MigrationRunner.Migrate(connection, EmbeddedMigrations.For<MetadataStore>());
    }

    /// <summary>
    /// Looks up the stored metadata for an item in a given locale.
    /// </summary>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="locale">The locale to match.</param>
    /// <returns>The stored metadata, or <see langword="null"/> if nothing is stored for this item and locale.</returns>
    public StoredMetadata? Get(Guid itemId, Locale locale)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT "guid", "locale", "name", "overview"
            FROM metadata
            WHERE "guid" = $guid AND "locale" = $locale;
            """;
        command.Parameters.AddWithValue("$guid", itemId.ToString());
        command.Parameters.AddWithValue("$locale", locale.ToTag());

        using var reader = command.ExecuteReader();
        return reader.Read() ? FromReader(reader) : null;
    }

    /// <summary>
    /// Stores metadata for an item and locale, overwriting any existing row for that pair.
    /// </summary>
    /// <param name="metadata">The metadata to store.</param>
    public void Set(StoredMetadata metadata)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO metadata ("guid", "locale", "name", "overview")
            VALUES ($guid, $locale, $name, $overview)
            """;
        WriteParameters(command, metadata);
        command.ExecuteNonQuery();
    }

    private static StoredMetadata FromReader(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(reader.GetOrdinal("guid"))),
        Locale.FromTag(reader.GetString(reader.GetOrdinal("locale")))!,
        reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
        reader.IsDBNull(reader.GetOrdinal("overview")) ? null : reader.GetString(reader.GetOrdinal("overview")));

    private static void WriteParameters(SqliteCommand command, StoredMetadata metadata)
    {
        command.Parameters.AddWithValue("$guid", metadata.Guid.ToString());
        command.Parameters.AddWithValue("$locale", metadata.Locale.ToTag());
        command.Parameters.AddWithValue("$name", (object?)metadata.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("$overview", (object?)metadata.Overview ?? DBNull.Value);
    }
}
