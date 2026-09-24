using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.Polyfin.Configuration;

/// <summary>
/// A language offered by the administrator, as stored in the plugin's configuration.
/// </summary>
public class ConfiguredLanguage
{
    /// <summary>
    /// Gets or sets the stable unique identifier for the language.
    /// This is used to remember which language a viewer selected and must remain unchanged
    /// when <see cref="Name"/> changes.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name shown to users, e.g. "Deutsch".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the locale used when requesting metadata, e.g. "de-DE".
    /// All viewers using this language get metadata requested with this locale.
    /// </summary>
    public string MetadataLocale { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the locale tags that match this language, e.g. "de" for every
    /// German-speaking region or "de-AT" for Austria only.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Jellyfin serializes this to XML and JSON, which both need a settable array.")]
    public string[] MatchLocales { get; set; } = [];
}
