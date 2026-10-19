using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Jellyfin.Plugin.Polyfin.Tests;

public sealed class MetadataResolverTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory();
    private readonly MetadataStore _store;
    private readonly FakeFetcher _fetcher = new();
    private readonly FakeTimeProvider _timeProvider = new FakeTimeProvider(Samples.RefreshedAt);
    private readonly FakeLogger<MetadataResolver> _logger = new();
    private readonly MetadataResolver _resolver;

    public MetadataResolverTests()
    {
        _store = new MetadataStore(_folder.FullName);
        _resolver = new MetadataResolver(_store, _fetcher, _timeProvider, _logger);
    }

    private Task RefreshMovieAsync(Guid itemId, bool refetch = false)
        => _resolver.RefreshAsync<Movie, MovieInfo>(itemId, Samples.German, refetch, CancellationToken.None);

    [Fact]
    public async Task FetchedMetadata_IsStored()
    {
        _fetcher.Result = Samples.FullFetched;

        await RefreshMovieAsync(Samples.ItemId);

        Assert.Equal(Samples.FullMetadata, _resolver.Find(Samples.ItemId, Samples.German));
    }

    [Fact]
    public async Task FetchedMetadata_IsStoredWithCurrentTime()
    {
        var now = Samples.RefreshedAt.AddDays(1);
        _timeProvider.SetUtcNow(now);
        _fetcher.Result = Samples.FullFetched;

        await RefreshMovieAsync(Samples.ItemId);

        Assert.Equal(now, _store.Get(Samples.ItemId, Samples.German)?.RefreshedAt);
    }

    [Fact]
    public async Task MissingTranslation_StoresEmptyMetadata()
    {
        _fetcher.Result = Samples.EmptyFetched;

        await RefreshMovieAsync(Samples.ItemId);

        Assert.Equal(Samples.EmptyMetadata, _resolver.Find(Samples.ItemId, Samples.German));
    }

    [Fact]
    public async Task FullItem_IsNotFetched()
    {
        _store.Replace(Samples.FullStored);

        await RefreshMovieAsync(Samples.ItemId);

        Assert.Equal(0, _fetcher.Calls);
        Assert.Equal(Samples.FullMetadata, _resolver.Find(Samples.ItemId, Samples.German));
    }

    [Fact]
    public async Task CompleteItem_IsFetchedOnceDue()
    {
        _store.Replace(Samples.FullStored with { Tagline = null });

        await RefreshMovieAsync(Samples.ItemId);
        Assert.Equal(0, _fetcher.Calls);

        _timeProvider.Advance(MetadataResolver.CompleteRefreshAge);
        await RefreshMovieAsync(Samples.ItemId);
        Assert.Equal(1, _fetcher.Calls);
    }

    [Fact]
    public async Task Refetch_ReplacesStoredMetadata()
    {
        _store.Replace(Samples.FullStored);
        _fetcher.Result = Samples.EmptyFetched with { Name = "Neuer Titel" };

        await RefreshMovieAsync(Samples.ItemId, refetch: true);

        Assert.Equal(Samples.EmptyMetadata with { Name = "Neuer Titel" }, _resolver.Find(Samples.ItemId, Samples.German));
    }

    [Fact]
    public async Task MissingItem_StoresNothing()
    {
        var itemId = Guid.NewGuid();
        _fetcher.ItemMissing = true;

        await RefreshMovieAsync(itemId);

        Assert.Null(_resolver.Find(itemId, Samples.German));
        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    [Fact]
    public async Task EmptyFetchWithFailures_StoresNothing()
    {
        var itemId = Guid.NewGuid();
        _fetcher.HadFailures = true;

        await RefreshMovieAsync(itemId);

        Assert.Null(_resolver.Find(itemId, Samples.German));
        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
    }

    [Fact]
    public async Task EmptyRefetchWithFailures_KeepsStoredMetadata()
    {
        _store.Replace(Samples.FullStored);
        _timeProvider.SetUtcNow(Samples.RefreshedAt.AddDays(1));
        _fetcher.HadFailures = true;

        await RefreshMovieAsync(Samples.ItemId, refetch: true);

        Assert.Equal(Samples.FullMetadata, _resolver.Find(Samples.ItemId, Samples.German));
        Assert.Equal(Samples.RefreshedAt, _store.Get(Samples.ItemId, Samples.German)?.RefreshedAt);
    }

    [Fact]
    public async Task PartialFetchWithFailures_IsStored()
    {
        _fetcher.Result = Samples.EmptyFetched with { Name = Samples.Name };
        _fetcher.HadFailures = true;

        await RefreshMovieAsync(Samples.ItemId);

        Assert.Equal(Samples.EmptyMetadata with { Name = Samples.Name }, _resolver.Find(Samples.ItemId, Samples.German));
    }

    [Fact]
    public async Task PartialRefetchWithFailures_KeepsStoredFields()
    {
        _store.Replace(Samples.FullStored);
        _fetcher.Result = Samples.EmptyFetched with { Name = "Neuer Titel" };
        _fetcher.HadFailures = true;

        await RefreshMovieAsync(Samples.ItemId, refetch: true);

        Assert.Equal(Samples.FullMetadata with { Name = "Neuer Titel" }, _resolver.Find(Samples.ItemId, Samples.German));
    }

    [Fact]
    public async Task MissingItemOnRefetch_KeepsStoredMetadata()
    {
        _store.Replace(Samples.FullStored);
        _fetcher.ItemMissing = true;

        await RefreshMovieAsync(Samples.ItemId, refetch: true);

        Assert.Equal(Samples.FullMetadata, _resolver.Find(Samples.ItemId, Samples.German));
    }

    [Fact]
    public void FilterItemsToRefresh_ReturnsItemsThatNeedRefreshing()
    {
        var now = _timeProvider.GetUtcNow();
        var complete = Samples.FullStored with { Tagline = null };

        var full = Samples.FullStored with { Guid = Guid.NewGuid() };
        var recent = complete with { Guid = Guid.NewGuid(), RefreshedAt = now - (MetadataResolver.CompleteRefreshAge - TimeSpan.FromDays(1)) };
        var old = complete with { Guid = Guid.NewGuid(), RefreshedAt = now - (MetadataResolver.CompleteRefreshAge + TimeSpan.FromDays(1)) };
        Guid newId = Guid.NewGuid();
        Guid[] itemIds = [full.Guid, recent.Guid, old.Guid, newId];

        _store.Replace(full);
        _store.Replace(recent);
        _store.Replace(old);

        Assert.Equivalent(new[] { old.Guid, newId }, _resolver.FilterItemsToRefresh(itemIds, Samples.German), strict: true);
        Assert.Equivalent(itemIds, _resolver.FilterItemsToRefresh(itemIds, Samples.French), strict: true);
    }

    [Fact]
    public void NullRow_NeedsRefresh()
    {
        Assert.True(_resolver.NeedsRefresh(null));
    }

    [Fact]
    public void FullRow_NeverNeedsRefresh()
    {
        var now = _timeProvider.GetUtcNow();

        Assert.False(_resolver.NeedsRefresh(Samples.FullStored with { RefreshedAt = now - TimeSpan.FromDays(10000) }));
    }

    [Fact]
    public void CompleteRow_NeedsRefreshAfterRefreshAge()
    {
        var now = _timeProvider.GetUtcNow();
        var recentTime = now - (MetadataResolver.CompleteRefreshAge - TimeSpan.FromMinutes(1));
        var oldTime = now - (MetadataResolver.CompleteRefreshAge + TimeSpan.FromMinutes(1));

        var complete = Samples.FullStored with { Tagline = null };
        Assert.False(_resolver.NeedsRefresh(complete with { RefreshedAt = recentTime }));
        Assert.True(_resolver.NeedsRefresh(complete with { RefreshedAt = oldTime }));
    }

    [Fact]
    public void IncompleteRow_NeedsRefreshAfterRefreshAge()
    {
        var now = _timeProvider.GetUtcNow();
        var recentTime = now - (MetadataResolver.IncompleteRefreshAge - TimeSpan.FromMinutes(1));
        var oldTime = now - (MetadataResolver.IncompleteRefreshAge + TimeSpan.FromMinutes(1));

        Assert.False(_resolver.NeedsRefresh(Samples.EmptyStored with { RefreshedAt = recentTime }));
        Assert.True(_resolver.NeedsRefresh(Samples.EmptyStored with { RefreshedAt = oldTime }));
    }

    [Fact]
    public void Find_ReturnsStoredMetadata()
    {
        _store.Replace(Samples.FullStored);

        // Ensure Find does not fetch missing metadata.
        _fetcher.Result = Samples.EmptyFetched with { Name = "Fetched" };

        Assert.Equal(Samples.FullMetadata, _resolver.Find(Samples.ItemId, Samples.German));
        Assert.Null(_resolver.Find(Samples.ItemId, Samples.French));
    }

    public void Dispose()
    {
        // Pooling keeps the db file open; release it so the folder can be deleted.
        SqliteConnection.ClearAllPools();
        _folder.Delete(recursive: true);
    }

    // Returns the configured result, or throws as if the item is missing, instead of calling real providers.
    private sealed class FakeFetcher() : MetadataFetcher(null!, null!, null!)
    {
        public FetchedMetadata Result { get; set; } = Samples.EmptyFetched;

        public bool HadFailures { get; set; }

        public bool ItemMissing { get; set; }

        public int Calls { get; private set; }

        public override Task<(FetchedMetadata Metadata, bool HadFailures)> FetchMetadataAsync<TItem, TInfo>(
            Guid itemId, Locale? locale, CancellationToken cancellationToken)
        {
            Calls++;

            if (ItemMissing)
            {
                throw new FetchItemNotFoundException("Item not found");
            }

            return Task.FromResult((Result, HadFailures));
        }
    }
}
