namespace Jellyfin.Plugin.Polyfin.Models;

/// <summary>
/// A validated language configuration used by the application.
/// </summary>
/// <param name="Id">The stable unique identifier for the language.</param>
/// <param name="Name">The display name shown to viewers, e.g. "Deutsch".</param>
/// <param name="MetadataLocale">The validated locale used when requesting metadata.</param>
public sealed record Language(string Id, string Name, Locale MetadataLocale);
