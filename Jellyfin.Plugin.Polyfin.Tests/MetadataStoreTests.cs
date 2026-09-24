using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Polyfin.Tests;

public sealed class MetadataStoreTests : IDisposable
{
    private static readonly Locale German = new("de", "DE");

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory();
    private readonly MetadataStore _store;

    public MetadataStoreTests()
    {
        _store = new MetadataStore(_folder.FullName);
    }

    [Fact]
    public void Set_ThenGet_RoundTrips()
    {
        var itemId = Guid.NewGuid();
        var metadata = new StoredMetadata(itemId, German, "Der Titel", "Die Beschreibung");

        _store.Set(metadata);

        Assert.Equal(metadata, _store.Get(itemId, German));
        Assert.Null(_store.Get(itemId, new Locale("de", null)));
    }

    [Fact]
    public void Set_OverwritesExistingRow()
    {
        var itemId = Guid.NewGuid();
        var updated = new StoredMetadata(itemId, German, "Neuer Titel", null);

        _store.Set(new StoredMetadata(itemId, German, "Alter Titel", "Alte Beschreibung"));
        _store.Set(updated);

        Assert.Equal(updated, _store.Get(itemId, German));
    }

    public void Dispose()
    {
        // Pooling keeps the db file open; release it so the folder can be deleted.
        SqliteConnection.ClearAllPools();
        _folder.Delete(recursive: true);
    }
}
