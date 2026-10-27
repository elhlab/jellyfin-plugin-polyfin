using Jellyfin.Plugin.Polyfin.Database.Metadata;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
using Jellyfin.Plugin.Polyfin.Transformers;
using MediaBrowser.Model.Dto;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.Polyfin.Tests.Transformers;

public sealed class MovieTransformerTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory();
    private readonly MetadataStore _store;
    private readonly MovieTransformer _transformer;

    public MovieTransformerTests()
    {
        _store = new MetadataStore(_folder.FullName);

        // Find only reads the store, so the fetcher is never accessed.
        var resolver = new MetadataResolver(_store, null!, TimeProvider.System, NullLogger<MetadataResolver>.Instance);
        _transformer = new MovieTransformer(resolver);
    }

    // A response as Jellyfin sends it, before translation.
    private static BaseItemDto UntranslatedResponse() => new BaseItemDto
    {
        Id = Samples.ItemId,
        Name = "Original title",
        Overview = "Original overview",
        Taglines = ["Original tagline"],
    };

    private static Metadata FromResponse(BaseItemDto response)
        => new Metadata(response.Id, response.Name, response.Overview, response.Taglines?.SingleOrDefault());

    private Task TransformAsync(BaseItemDto response, Locale locale)
        => _transformer.Transform(response, locale, CancellationToken.None);

    [Fact]
    public async Task Transform_AppliesStoredMetadata()
    {
        _store.Replace(Samples.FullStored);
        var response = UntranslatedResponse();

        await TransformAsync(response, Samples.German);

        Assert.Equal(Samples.FullMetadata, FromResponse(response));
    }

    [Fact]
    public async Task Transform_KeepsResponseWhenNothingStored()
    {
        _store.Replace(Samples.FullStored);
        var response = UntranslatedResponse();

        await TransformAsync(response, Samples.French);

        Assert.Equal(FromResponse(UntranslatedResponse()), FromResponse(response));
    }

    [Fact]
    public async Task Transform_KeepsResponseWhenStoredMetadataIsEmpty()
    {
        _store.Replace(Samples.EmptyStored);
        var response = UntranslatedResponse();

        await TransformAsync(response, Samples.German);

        Assert.Equal(FromResponse(UntranslatedResponse()), FromResponse(response));
    }

    [Fact]
    public async Task Transform_KeepsTaglinesNullWhenNotRequested()
    {
        _store.Replace(Samples.FullStored);
        var response = UntranslatedResponse();
        response.Taglines = null;

        await TransformAsync(response, Samples.German);

        Assert.Null(response.Taglines);
    }

    [Fact]
    public async Task Transform_AddsTaglineToEmptyTaglines()
    {
        _store.Replace(Samples.FullStored);
        var response = UntranslatedResponse();
        response.Taglines = [];

        await TransformAsync(response, Samples.German);

        Assert.Equal([Samples.Tagline], response.Taglines);
    }

    public void Dispose()
    {
        // Pooling keeps the db file open; release it so the folder can be deleted.
        SqliteConnection.ClearAllPools();
        _folder.Delete(recursive: true);
    }
}
