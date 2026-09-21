using System;
using System.IO;
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
    /// Looks up the stored metadata for an item in a given language.
    /// </summary>
    /// <param name="itemId">The library item's id.</param>
    /// <param name="language">The locale tag to match, e.g. "de".</param>
    /// <returns>The stored metadata, or <see langword="null"/> if nothing is stored for this item and language.</returns>
    public StoredMetadata? Get(Guid itemId, string language)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT "guid", "language", "name", "overview"
            FROM metadata
            WHERE "guid" = $guid AND "language" = $language;
            """;
        command.Parameters.AddWithValue("$guid", itemId.ToString());
        command.Parameters.AddWithValue("$language", language);

        using var reader = command.ExecuteReader();
        return reader.Read() ? FromReader(reader) : null;
    }

    /// <summary>
    /// Stores metadata for an item and language, overwriting any existing row for that pair.
    /// </summary>
    /// <param name="metadata">The metadata to store.</param>
    public void Set(StoredMetadata metadata)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO metadata ("guid", "language", "name", "overview")
            VALUES ($guid, $language, $name, $overview)
            """;
        WriteParameters(command, metadata);
        command.ExecuteNonQuery();
    }

    private static StoredMetadata FromReader(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(reader.GetOrdinal("guid"))),
        reader.GetString(reader.GetOrdinal("language")),
        reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString(reader.GetOrdinal("name")),
        reader.IsDBNull(reader.GetOrdinal("overview")) ? null : reader.GetString(reader.GetOrdinal("overview")));

    private static void WriteParameters(SqliteCommand command, StoredMetadata metadata)
    {
        command.Parameters.AddWithValue("$guid", metadata.Guid.ToString());
        command.Parameters.AddWithValue("$language", metadata.Language);
        command.Parameters.AddWithValue("$name", (object?)metadata.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("$overview", (object?)metadata.Overview ?? DBNull.Value);
    }
}
