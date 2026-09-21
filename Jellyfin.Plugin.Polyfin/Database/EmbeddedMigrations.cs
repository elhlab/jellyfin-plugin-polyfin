using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.Polyfin.Database;

/// <summary>
/// Loads the migration scripts embedded in the plugin assembly.
/// </summary>
public static class EmbeddedMigrations
{
    /// <summary>
    /// Loads the <c>Migrations/*.sql</c> files embedded next to <typeparamref name="TSchema"/>.
    /// </summary>
    /// <typeparam name="TSchema">Any type in the namespace whose migrations to load - typically the store the migrations belong to.</typeparam>
    /// <returns>The migrations, ordered by <see cref="Migration.Version"/>.</returns>
    public static IReadOnlyList<Migration> For<TSchema>()
    {
        var assembly = typeof(TSchema).Assembly;

        // MSBuild names an embedded resource after its folder path, so the folder
        // next to TSchema has the same dotted prefix as its namespace.
        var prefix = typeof(TSchema).Namespace + ".Migrations.";

        var migrations = assembly.GetManifestResourceNames()
            .Where(resourceName => resourceName.StartsWith(prefix, StringComparison.Ordinal)
                && resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(resourceName =>
            {
                var fileName = resourceName[prefix.Length..];

                using var stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Embedded migration '{resourceName}' could not be opened.");
                using var reader = new StreamReader(stream);

                return new Migration(ParseVersion(fileName), fileName, reader.ReadToEnd());
            })
            .OrderBy(migration => migration.Version)
            .ToList();

        // An empty list would let the store come up with no tables at all, so
        // treat it as a wiring mistake (folder moved, namespace renamed, glob
        // in the csproj no longer matching) rather than a schema with nothing in it.
        if (migrations.Count == 0)
        {
            throw new InvalidOperationException($"No embedded migrations found with prefix '{prefix}'. Check the Migrations folder sits next to {typeof(TSchema).Name} and the .sql files are embedded in the csproj.");
        }

        return migrations;
    }

    /// <summary>
    /// Parses the leading version number from a <c>&lt;version&gt;_&lt;description&gt;.sql</c> file name, e.g. "001_Initial.sql".
    /// </summary>
    private static int ParseVersion(string fileName)
    {
        var separator = fileName.IndexOf('_', StringComparison.Ordinal);
        if (separator > 0 && int.TryParse(fileName.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            return version;
        }

        throw new InvalidOperationException($"Migration '{fileName}' must be named '<version>_<description>.sql', e.g. '001_Initial.sql'.");
    }
}
