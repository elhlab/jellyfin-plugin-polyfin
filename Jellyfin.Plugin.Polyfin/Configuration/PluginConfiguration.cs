using System;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Polyfin.Configuration;

/// <summary>
/// Raw plugin configuration as stored in the plugin's XML file and edited on the settings page.
/// Changing a property only changes this object; it does not save or reload the configuration.
/// To change the configuration from code, pass it to
/// <see cref="MediaBrowser.Common.Plugins.BasePlugin{TConfigurationType}.UpdateConfiguration"/>.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    private int _providerTimeoutSeconds = 10;

    /// <summary>
    /// Gets or sets the maximum time, in seconds, a metadata provider may take before it is skipped.
    /// </summary>
    public int ProviderTimeoutSeconds
    {
        get => _providerTimeoutSeconds;
        set => _providerTimeoutSeconds = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets the languages offered by the administrator.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Jellyfin serializes this to XML and JSON, which both need a settable array.")]
    public ConfiguredLanguage[] Languages { get; set; } = [];

    /// <summary>
    /// Gets or sets the language settings configured for individual users.
    /// </summary>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Jellyfin serializes this to XML and JSON, which both need a settable array.")]
    public ConfiguredUser[] Users { get; set; } = [];
}
