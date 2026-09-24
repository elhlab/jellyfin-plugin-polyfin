using Jellyfin.Plugin.Polyfin.Configuration;
using Jellyfin.Plugin.Polyfin.Models;
using Jellyfin.Plugin.Polyfin.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Jellyfin.Plugin.Polyfin.Tests;

public class LanguageResolverTests
{
    private static readonly Guid UserId = Guid.Parse("6a1f0c3e-2b7d-4c59-9e84-0f3d2a1b5c76");

    private readonly FakeLogger<LanguageResolver> _logger = new();

    private static ConfiguredLanguage ConfiguredLanguage(string id, string metadataLocale, string[] matchLocales)
    {
        return new ConfiguredLanguage { Id = id, Name = id, MetadataLocale = metadataLocale, MatchLocales = matchLocales };
    }

    private static ConfiguredUser User(string userId, string languageId)
    {
        return new ConfiguredUser { UserId = userId, LanguageId = languageId };
    }

    private LanguageResolver Create(ConfiguredLanguage[] languages, ConfiguredUser[]? users = null)
    {
        var configuration = new PluginConfiguration { Languages = languages, Users = users ?? [] };
        return new LanguageResolver(configuration, _ => { }, _logger);
    }

    private int WarningCount()
    {
        return _logger.Collector.GetSnapshot().Count(record => record.Level == LogLevel.Warning);
    }

    [Fact]
    public void Resolve_UsesUpdatedConfiguration()
    {
        Action<PluginConfiguration>? onChange = null;
        var resolver = new LanguageResolver(
            new PluginConfiguration { Languages = [ConfiguredLanguage("german", "de-DE", ["de"])] },
            callback => onChange = callback,
            _logger);

        Assert.Equal("german", resolver.Resolve(Guid.Empty, "de")?.Id);

        onChange!(new PluginConfiguration { Languages = [ConfiguredLanguage("austrian", "de-AT", ["de"])] });

        Assert.Equal("austrian", resolver.Resolve(Guid.Empty, "de")?.Id);
    }

    [Theory]
    [InlineData("de-CH", "german")]
    [InlineData("de-AT", "austrian")]
    [InlineData("de-CH,en-US;q=0.9", "german")]
    [InlineData("fi,en-US;q=0.9,en;q=0.8", "finnish")]
    [InlineData("en-US;q=0.5,fi", "finnish")]
    [InlineData("sv,en-US;q=0.9", "english")]
    [InlineData("en-GB", null)]
    [InlineData("en", null)]
    [InlineData("", null)]
    [InlineData("ja-JP", null)]
    public void Resolve_MatchesAcceptedLanguages(string header, string? expected)
    {
        var resolver = Create(
        [
            ConfiguredLanguage("german", "de-DE", ["de"]),
            ConfiguredLanguage("austrian", "de-AT", ["de-AT"]),
            ConfiguredLanguage("english", "en-US", ["en-US"]),
            ConfiguredLanguage("finnish", "fi-FI", ["fi"])
        ]);

        Assert.Equal(expected, resolver.Resolve(Guid.Empty, header)?.Id);
    }

    [Fact]
    public void Resolve_PreservesMetadataLocale()
    {
        var resolver = Create(
        [
            ConfiguredLanguage("austrian", "de-DE", ["de-AT"])
        ]);

        var language = resolver.Resolve(Guid.Empty, "de-AT");

        Assert.Equal("austrian", language?.Id);
        Assert.Equal(new Locale("de", "DE"), language?.MetadataLocale);
    }

    [Fact]
    public void LanguageWithMalformedMetadataLocale_IsDropped()
    {
        var resolver = Create([ConfiguredLanguage("german", "", ["de"])]);

        Assert.Null(resolver.Resolve(Guid.Empty, "de"));
        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void LanguageWithoutMatchLocales_RemainsUsable()
    {
        var resolver = Create(
            [ConfiguredLanguage("german", "de-DE", [])],
            [User(UserId.ToString(), "german")]);

        Assert.Null(resolver.Resolve(Guid.Empty, "de"));
        Assert.Equal("german", resolver.Resolve(UserId, "en")?.Id);
    }

    [Fact]
    public void LanguagesWithDuplicateIds_AreDropped()
    {
        var resolver = Create([
            ConfiguredLanguage("german", "de-DE", ["de"]),
            ConfiguredLanguage("german", "fr-FR", ["fr"])
        ]);

        Assert.Null(resolver.Resolve(Guid.Empty, "de"));
        Assert.Null(resolver.Resolve(Guid.Empty, "fr"));
        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void UserAssignmentToDuplicateLanguageId_IsDropped()
    {
        var resolver = Create(
            [
                ConfiguredLanguage("german", "de-DE", ["de"]),
                ConfiguredLanguage("german", "fr-FR", ["fr"]),
                ConfiguredLanguage("english", "en-US", ["en"])
            ],
            [User(UserId.ToString(), "german")]);

        Assert.Equal("english", resolver.Resolve(UserId, "en")?.Id);
    }

    [Fact]
    public void MalformedMatchLocale_IsDropped()
    {
        Create([ConfiguredLanguage("german", "de-DE", ["de-AT,de-CH"])]);

        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void LanguageWithMalformedMatchLocale_RemainsUsable()
    {
        var resolver = Create([ConfiguredLanguage("german", "de-DE", ["de-AT,de-CH", "de"])]);

        Assert.Equal("german", resolver.Resolve(Guid.Empty, "de")?.Id);
        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void DuplicateMatchLocale_IsDropped()
    {
        var resolver = Create(
        [
            ConfiguredLanguage("german", "de-DE", ["de", "de-DE"]),
            ConfiguredLanguage("austrian", "de-AT", ["de", "de-AT"])
        ]);

        Assert.Null(resolver.Resolve(Guid.Empty, "de"));
        Assert.Equal("german", resolver.Resolve(Guid.Empty, "de-DE")?.Id);
        Assert.Equal("austrian", resolver.Resolve(Guid.Empty, "de-AT")?.Id);
        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void RepeatedMatchLocale_RemainsUsable()
    {
        var resolver = Create([ConfiguredLanguage("german", "de-DE", ["de", "de"])]);

        Assert.Equal("german", resolver.Resolve(Guid.Empty, "de")?.Id);
    }

    [Fact]
    public void Resolve_PrefersUserOverHeader()
    {
        var resolver = Create(
            [ConfiguredLanguage("german", "de-DE", ["de"]), ConfiguredLanguage("english", "en-US", ["en"])],
            [User(UserId.ToString(), "english")]);

        Assert.Equal("english", resolver.Resolve(UserId, "de")?.Id);
    }

    [Fact]
    public void UnassignedUser_UsesHeader()
    {
        var resolver = Create(
            [ConfiguredLanguage("german", "de-DE", ["de"]), ConfiguredLanguage("english", "en-US", ["en"])],
            [User(UserId.ToString(), "english")]);

        Assert.Equal("german", resolver.Resolve(Guid.NewGuid(), "de")?.Id);
        Assert.Equal("german", resolver.Resolve(Guid.Empty, "de")?.Id);
    }

    [Fact]
    public void InvalidUserAssignment_IsNotUsed()
    {
        var resolver = Create(
            [ConfiguredLanguage("german", "de-DE", ["de"])],
            [User(UserId.ToString(), "klingon")]
        );

        Assert.Equal("german", resolver.Resolve(UserId, "de")?.Id);
        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void UserAssignmentWithMalformedUserId_IsDropped()
    {
        var resolver = Create(
            [ConfiguredLanguage("german", "de-DE", ["de"]), ConfiguredLanguage("english", "en-US", ["en"])],
            [User("not-a-guid", "english"), User(UserId.ToString(), "english")]);

        Assert.Equal("english", resolver.Resolve(UserId, "de")?.Id);
        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void ConflictingUserAssignments_AreDropped()
    {
        var resolver = Create(
            [ConfiguredLanguage("german", "de-DE", ["de"]), ConfiguredLanguage("english", "en-US", ["en"])],
            [User(UserId.ToString(), "english"), User(UserId.ToString(), "german")]);

        Assert.Null(resolver.Resolve(UserId, "fr"));
        Assert.Equal("english", resolver.Resolve(UserId, "en")?.Id);
        Assert.Equal(1, WarningCount());
    }

    [Fact]
    public void RepeatedUserAssignment_RemainsUsable()
    {
        var resolver = Create(
            [ConfiguredLanguage("german", "de-DE", ["de"]), ConfiguredLanguage("english", "en-US", ["en"])],
            [User(UserId.ToString(), "english"), User(UserId.ToString(), "english")]);

        Assert.Equal("english", resolver.Resolve(UserId, "de")?.Id);
    }
}
