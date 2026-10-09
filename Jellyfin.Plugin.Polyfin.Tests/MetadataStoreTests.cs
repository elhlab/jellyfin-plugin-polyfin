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
        var metadata = new StoredMetadata(itemId, German, "Der Titel", "Die Beschreibung", "Der Slogan");

        _store.Set(metadata);

        Assert.Equal(metadata, _store.Get(itemId, German));
        Assert.Null(_store.Get(itemId, new Locale("de", null)));
    }

    [Fact]
    public void Set_OverwritesExistingRow()
    {
        var itemId = Guid.NewGuid();
        var updated = new StoredMetadata(itemId, German, "Neuer Titel", null, null);

        _store.Set(new StoredMetadata(itemId, German, "Alter Titel", "Alte Beschreibung", "Alter Slogan"));
        _store.Set(updated);

        Assert.Equal(updated, _store.Get(itemId, German));
    }

    [Fact]
    public void Enumerate_ReturnsRequestedLocale()
    {
        var first = new StoredMetadata(Guid.NewGuid(), German, "Titel", "Handlung", "Slogan");
        var second = new StoredMetadata(Guid.NewGuid(), German, null, null, null);

        _store.Set(first);
        _store.Set(second);
        _store.Set(new StoredMetadata(Guid.NewGuid(), new Locale("fr", "FR"), "Titre", null, null));

        Assert.Equivalent(new[] { first, second }, _store.Enumerate(German), strict: true);
    }

    public void Dispose()
    {
        // Pooling keeps the db file open; release it so the folder can be deleted.
        SqliteConnection.ClearAllPools();
        _folder.Delete(recursive: true);
    }
}
