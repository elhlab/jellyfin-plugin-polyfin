using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.Polyfin.Models;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Polyfin.Database.Metadata;

/// <summary>
/// Stores metadata in an sqlite database.
/// </summary>
public class MetadataStore
{
    // Interpolated straight into SQL. Only column names should go in here.
    private static readonly string[] Columns = ["guid", "locale", "name", "overview", "tagline"];

    private static readonly string ColumnList = string.Join(", ", Columns.Select(column => $"\"{column}\""));

    private static readonly string ParameterList = string.Join(", ", Columns.Select(column => $"${column}"));

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

        // Interpolated lists are derived from Columns, never user input.
#pragma warning disable CA2100
        command.CommandText =
            $"""
            SELECT {ColumnList}
            FROM metadata
            WHERE "guid" = $guid AND "locale" = $locale;
            """;
#pragma warning restore CA2100
        command.Parameters.AddWithValue("$guid", itemId.ToString());
        command.Parameters.AddWithValue("$locale", locale.ToTag());

        using var reader = command.ExecuteReader();
        return reader.Read() ? FromReader(reader) : null;
    }

    /// <summary>
    /// Reads every row stored in a given locale.
    /// </summary>
    /// <param name="locale">The locale to match.</param>
    /// <returns>The stored metadata of each row.</returns>
    public IEnumerable<StoredMetadata> Enumerate(Locale locale)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        // Interpolated lists are derived from Columns, never user input.
#pragma warning disable CA2100
        command.CommandText =
            $"""
            SELECT {ColumnList}
            FROM metadata
            WHERE "locale" = $locale;
            """;
#pragma warning restore CA2100
        command.Parameters.AddWithValue("$locale", locale.ToTag());

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return FromReader(reader);
        }
    }

    /// <summary>
    /// Stores metadata for an item and locale, overwriting any existing row for that pair.
    /// </summary>
    /// <param name="metadata">The metadata to store.</param>
    public void Replace(StoredMetadata metadata)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        // Interpolated lists are derived from Columns, never user input.
#pragma warning disable CA2100
        command.CommandText =
            $"""
            INSERT OR REPLACE INTO metadata ({ColumnList})
            VALUES ({ParameterList})
            """;
#pragma warning restore CA2100
        WriteParameters(command, metadata);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Stores metadata for an item and locale, keeping the stored value of any field that is
    /// <see langword="null"/> in <paramref name="metadata"/>.
    /// </summary>
    /// <param name="metadata">The metadata to store.</param>
    public void Fill(StoredMetadata metadata)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        // Interpolated lists are derived from Columns, never user input.
#pragma warning disable CA2100
        command.CommandText =
            $"""
            INSERT INTO metadata ({ColumnList})
            VALUES ({ParameterList})
            ON CONFLICT ("guid", "locale") DO UPDATE SET
                "name" = COALESCE(excluded."name", "name"),
                "overview" = COALESCE(excluded."overview", "overview"),
                "tagline" = COALESCE(excluded."tagline", "tagline")
            """;
#pragma warning restore CA2100
        WriteParameters(command, metadata);
        command.ExecuteNonQuery();
    }

    private static StoredMetadata FromReader(SqliteDataReader reader) => new StoredMetadata(
        Guid.Parse(reader.GetString("guid")),
        Locale.FromTag(reader.GetString("locale"))!,
        GetNullableString(reader, "name"),
        GetNullableString(reader, "overview"),
        GetNullableString(reader, "tagline"));

    private static void WriteParameters(SqliteCommand command, StoredMetadata metadata)
    {
        command.Parameters.AddWithValue("$guid", metadata.Guid.ToString());
        command.Parameters.AddWithValue("$locale", metadata.Locale.ToTag());
        AddNullableParameter(command, "$name", metadata.Name);
        AddNullableParameter(command, "$overview", metadata.Overview);
        AddNullableParameter(command, "$tagline", metadata.Tagline);
    }

    private static string? GetNullableString(SqliteDataReader reader, string column)
        => reader.IsDBNull(column) ? null : reader.GetString(column);

    private static void AddNullableParameter(SqliteCommand command, string name, object? value)
        => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
}
