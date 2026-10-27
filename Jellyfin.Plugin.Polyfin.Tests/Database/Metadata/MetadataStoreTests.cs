using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;
using Microsoft.Data.Sqlite;

namespace Jellyfin.Plugin.Polyfin.Tests.Database.Metadata;

public sealed class MetadataStoreTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory();
    private readonly MetadataStore _store;

    public MetadataStoreTests()
    {
        _store = new MetadataStore(_folder.FullName);
    }

    [Fact]
    public void Get_ReturnsReplacedMetadata()
    {
        var metadata = Samples.FullStored;

        _store.Replace(metadata);

        Assert.Equal(metadata, _store.Get(metadata.Guid, Samples.German));
        Assert.Null(_store.Get(metadata.Guid, Samples.German with { Country = null }));
    }

    [Fact]
    public void Replace_OverwritesEveryField()
    {
        var updated = new StoredMetadata(Samples.ItemId, Samples.German, "Neuer Titel", "Neue Beschreibung", null, Samples.RefreshedAt.AddMinutes(5));

        _store.Replace(Samples.FullStored);
        _store.Replace(updated);

        Assert.Equal(updated, _store.Get(updated.Guid, Samples.German));
    }

    [Fact]
    public void Fill_UpdatesWithoutClearing()
    {
        var refreshedAt = Samples.RefreshedAt.AddMinutes(5);

        _store.Replace(Samples.FullStored);
        _store.Fill(Samples.EmptyStored with { Name = "Neuer Titel", RefreshedAt = refreshedAt });

        Assert.Equal(
            Samples.FullStored with { Name = "Neuer Titel", RefreshedAt = refreshedAt },
            _store.Get(Samples.ItemId, Samples.German));
    }

    [Fact]
    public void Fill_UpdatesEveryProvidedField()
    {
        var updated = new StoredMetadata(
            Samples.ItemId, Samples.German, "Neuer Titel",
            "Neue Beschreibung", "Neuer Slogan", Samples.RefreshedAt.AddMinutes(5)
        );

        _store.Replace(Samples.FullStored);
        _store.Fill(updated);

        Assert.Equal(updated, _store.Get(Samples.ItemId, Samples.German));
    }

    [Fact]
    public void Fill_InsertsMissingRow()
    {
        var metadata = Samples.FullStored;

        _store.Fill(metadata);

        Assert.Equal(metadata, _store.Get(metadata.Guid, Samples.German));
    }

    [Fact]
    public void Enumerate_ReturnsRequestedLocale()
    {
        var first = Samples.FullStored;
        var second = Samples.EmptyStored with { Guid = Guid.NewGuid() };

        _store.Replace(first);
        _store.Replace(second);
        _store.Replace(Samples.FullStored with { Guid = Guid.NewGuid(), Locale = Samples.French });

        Assert.Equivalent(new[] { first, second }, _store.Enumerate(Samples.German), strict: true);
    }

    public void Dispose()
    {
        // Pooling keeps the db file open; release it so the folder can be deleted.
        SqliteConnection.ClearAllPools();
        _folder.Delete(recursive: true);
    }
}
