namespace Jellyfin.Plugin.Polyfin.Configuration;

/// <summary>
/// Language settings configured by the administrator for a Jellyfin user.
/// </summary>
public class ConfiguredUser
{
    /// <summary>
    /// Gets or sets the Jellyfin user's identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the <see cref="ConfiguredLanguage"/> to use for this user.
    /// This language takes precedence over the language requested by the client.
    /// </summary>
    public string LanguageId { get; set; } = string.Empty;
}
