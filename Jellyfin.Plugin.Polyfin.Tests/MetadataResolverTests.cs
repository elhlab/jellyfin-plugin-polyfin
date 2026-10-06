using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Jellyfin.Plugin.Polyfin.Tests;

public sealed class MetadataResolverTests : IDisposable
{
    private static readonly Locale German = new("de", "DE");
    private static readonly Locale French = new("fr", "FR");

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory();
    private readonly MetadataStore _store;
    private readonly FakeFetcher _fetcher = new();
    private readonly FakeLogger<MetadataResolver> _logger = new();
    private readonly MetadataResolver _resolver;

    public MetadataResolverTests()
    {
        _store = new MetadataStore(_folder.FullName);
        _resolver = new MetadataResolver(_store, _fetcher, _logger);
    }

    private Task RefreshMovieAsync(Guid itemId, bool refetch = false)
        => _resolver.RefreshAsync<Movie, MovieInfo>(itemId, German, refetch, CancellationToken.None);

    [Fact]
    public async Task FetchedMetadata_IsStored()
    {
        var itemId = Guid.NewGuid();
        _fetcher.Result = new FetchedMetadata("Titel", "Handlung");

        await RefreshMovieAsync(itemId);

        Assert.Equal(new Metadata(itemId, "Titel", "Handlung"), _resolver.Find(itemId, German));
    }

    [Fact]
    public async Task MissingTranslation_StoresEmptyMetadata()
    {
        var itemId = Guid.NewGuid();
        _fetcher.Result = null;

        await RefreshMovieAsync(itemId);

        Assert.Equal(new Metadata(itemId, null, null), _resolver.Find(itemId, German));
    }

    [Fact]
    public async Task StoredItem_IsNotFetched()
    {
        var stored = new StoredMetadata(Guid.NewGuid(), German, "Titel", "Handlung");
        _store.Set(stored);

        await RefreshMovieAsync(stored.Guid);

        Assert.Equal(0, _fetcher.Calls);
        Assert.Equal(new Metadata(stored.Guid, "Titel", "Handlung"), _resolver.Find(stored.Guid, German));
    }

    [Fact]
    public async Task Refetch_ReplacesStoredMetadata()
    {
        var itemId = Guid.NewGuid();
        _store.Set(new StoredMetadata(itemId, German, "Alter Titel", null));
        _fetcher.Result = new FetchedMetadata("Neuer Titel", "Handlung");

        await RefreshMovieAsync(itemId, refetch: true);

        Assert.Equal(new Metadata(itemId, "Neuer Titel", "Handlung"), _resolver.Find(itemId, German));
    }

    [Fact]
    public async Task FailedFetch_StoresNothing()
    {
        var itemId = Guid.NewGuid();
        _fetcher.Fails = true;

        await RefreshMovieAsync(itemId);

        Assert.Null(_resolver.Find(itemId, German));
        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    [Fact]
    public async Task FailedRefetch_KeepsStoredMetadata()
    {
        var stored = new StoredMetadata(Guid.NewGuid(), German, "Titel", "Handlung");
        _store.Set(stored);
        _fetcher.Fails = true;

        await RefreshMovieAsync(stored.Guid, refetch: true);

        Assert.Equal(new Metadata(stored.Guid, "Titel", "Handlung"), _resolver.Find(stored.Guid, German));
    }

    [Fact]
    public void FilterItemsToRefresh_SkipsStoredItems()
    {
        Guid storedId = Guid.NewGuid();
        Guid missingId = Guid.NewGuid();

        _store.Set(new StoredMetadata(storedId, German, "Titel", null));

        Assert.Equivalent(new[] { missingId }, _resolver.FilterItemsToRefresh([storedId, missingId], German), strict: true);
        Assert.Equivalent(new[] { storedId, missingId }, _resolver.FilterItemsToRefresh([storedId, missingId], French), strict: true);
    }

    [Fact]
    public void Find_ReturnsStoredMetadata()
    {
        var stored = new StoredMetadata(Guid.NewGuid(), German, "Titel", "Handlung");
        _store.Set(stored);

        // Ensure Find does not fetch missing metadata.
        _fetcher.Result = new FetchedMetadata("Fetched", "Fetched");

        Assert.Equal(new Metadata(stored.Guid, "Titel", "Handlung"), _resolver.Find(stored.Guid, German));
        Assert.Null(_resolver.Find(stored.Guid, French));
    }

    public void Dispose()
    {
        // Pooling keeps the db file open; release it so the folder can be deleted.
        SqliteConnection.ClearAllPools();
        _folder.Delete(recursive: true);
    }

    // Returns the configured result, or fails, instead of calling real providers.
    private sealed class FakeFetcher() : MetadataFetcher(null!, null!, null!)
    {
        public FetchedMetadata? Result { get; set; }

        public bool Fails { get; set; }

        public int Calls { get; private set; }

        public override Task<FetchedMetadata?> FetchMetadataAsync<TItem, TInfo>(
            Guid itemId, Locale? locale, CancellationToken cancellationToken)
        {
            Calls++;

            if (Fails)
            {
                throw new MetadataFetchException("Fetch failed");
            }

            return Task.FromResult(Result);
        }
    }
}
