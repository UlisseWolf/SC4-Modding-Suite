namespace SC4ModdingSuite.Models;

/// <summary>
/// General app options, persisted as a single JSON file by <see cref="AppOptionsService"/>.
/// </summary>
public sealed class AppOptions
{
    /// <summary>Path to SimCityLocale.DAT, for reference/quick-open convenience.</summary>
    public string? SimCityLocalePath { get; set; }

    /// <summary>SC4 installation folder - used as the starting folder when opening files.</summary>
    public string? Sc4InstallFolder { get; set; }

    /// <summary>
    /// The user's SC4 Plugins folder (typically <c>Documents\SimCity 4\Plugins</c>) - where
    /// custom/downloaded mod files actually live, as opposed to <see cref="Sc4InstallFolder"/>
    /// (the base game's own install folder). Preferred over <see cref="Sc4InstallFolder"/> as
    /// the starting folder when opening files, since that's what someone using this app to
    /// edit their own mods will need most often.
    /// </summary>
    public string? PluginsFolder { get; set; }

    public string? PimXPath { get; set; }
    public string? DataNodePath { get; set; }
    public string? MapperPath { get; set; }
    public string? TerraformerPath { get; set; }
    public string? Sc4PacEditorPath { get; set; }

    /// <summary>
    /// Path to the "NAM Development Suite" tool. Only ever surfaced in the UI (Options
    /// path field, External Tools button) when <see cref="DevFeatureFlags.IsNamDevelopmentSuiteEnabled"/>
    /// is true - see that class for how the feature is unlocked.
    /// </summary>
    public string? NamDevelopmentSuitePath { get; set; }

    /// <summary>Language code (matches a Localization/&lt;code&gt;.toml file). English is primary/default.</summary>
    public string Language { get; set; } = "en";

    /// <summary>Theme key ("default" for plain Fluent, or a Themes/&lt;key&gt;.toml file).</summary>
    public string Theme { get; set; } = "bloomberg";
}
