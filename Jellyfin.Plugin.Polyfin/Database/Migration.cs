namespace Jellyfin.Plugin.Polyfin.Database;

/// <summary>
/// A single schema migration script.
/// </summary>
/// <param name="Version">The schema version this migration brings the database to. Migrations are applied in ascending version order.</param>
/// <param name="Name">A human-readable name, e.g. the script's file name.</param>
/// <param name="Sql">The SQL to execute.</param>
public sealed record Migration(int Version, string Name, string Sql);
