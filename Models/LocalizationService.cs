using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace SC4ModdingSuite.Models;

/// <summary>One selectable language: its file code and display name (as declared in its own .toml file).</summary>
public sealed class LocalizationEntry
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>
/// Loads UI translation strings from .toml files under
/// <c>%APPDATA%\SC4ModdingSuite\Localization\</c> (<c>it.toml</c>, <c>en.toml</c>, ...)
/// using <see cref="TomlParser"/> - the same parent folder as <c>new_properties.xml</c>
/// (see <see cref="PropertySourceService"/>), so every user-editable data file the app
/// uses lives in one predictable place. The built-in defaults are embedded in the
/// assembly and copied out here on first run (<see cref="EmbeddedResourceSeeder"/>)
/// without ever overwriting a file the person has since edited - adding or editing a
/// language needs no rebuild, just editing a <c>.toml</c> file in that folder.
///
/// Each file is a flat set of <c>key = "value"</c> pairs, plus a required
/// <c>language_name</c> key giving the language's own display name (e.g. "Italiano",
/// "English") shown in the language picker.
///
/// <para>
/// <b>Live switching</b>: this class implements <see cref="INotifyPropertyChanged"/> and
/// exposes one named property per translation key (<see cref="ToolbarNew"/>,
/// <see cref="OptionsTitle"/>, ...) instead of only a generic <see cref="Get"/> indexer.
/// XAML bindings like <c>{Binding LocalizationService.ToolbarNew}</c> therefore update
/// live the moment <see cref="SetLanguage"/> reloads a different file: it raises a single
/// "everything on this object changed" notification
/// (<c>PropertyChangedEventArgs(string.Empty)</c>), the standard convention every common
/// .NET binding engine (WPF, Avalonia included) honors as "refresh every property bound to
/// this instance". An earlier version of this app only exposed <see cref="Get"/> and never
/// actually bound any XAML text to it, which is why picking a different language visibly
/// did nothing - that gap is what this rewrite fixes for the app's main window and Options
/// dialog specifically (see the README for which parts of the UI are covered).
/// </para>
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private readonly string _folder;
    private Dictionary<string, string> _strings = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _folder = Path.Combine(appData, "SC4ModdingSuite", "Localization");
        EmbeddedResourceSeeder.SeedFolder(_folder, "Localization");
    }

    public string CurrentLanguageCode { get; private set; } = "en";

    public List<LocalizationEntry> AvailableLanguages()
    {
        var list = new List<LocalizationEntry>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.toml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var code = Path.GetFileNameWithoutExtension(file);
            var name = TryReadLanguageName(file) ?? code;
            list.Add(new LocalizationEntry { Code = code, DisplayName = name });
        }

        return list;
    }

    /// <summary>Switches the active language, reloading its strings and notifying every bound "LocXxx" property.</summary>
    public void SetLanguage(string code)
    {
        CurrentLanguageCode = code;
        _strings = TryLoad(code) ?? new Dictionary<string, string>();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Resolves a translation key, falling back to the key itself if missing.</summary>
    public string Get(string key) => _strings.TryGetValue(key, out var value) ? value : key;

    // --- Named properties, one per translation key, for direct XAML binding ---
    // (see the class-level remarks above for why these exist instead of only Get(key)).

    public string ToolbarNew => Get("toolbar_new");
    public string ToolbarOpen => Get("toolbar_open");
    public string ToolbarSave => Get("toolbar_save");
    public string ToolbarSaveAs => Get("toolbar_save_as");
    public string ToolbarExportEntry => Get("toolbar_export_entry");
    public string ToolbarImportEntry => Get("toolbar_import_entry");
    public string ToolbarExportAll => Get("toolbar_export_all");
    public string ToolbarOptions => Get("toolbar_options");
    public string ExternalToolsHeader => Get("external_tools_header");

    public string LuaCompile => Get("lua_compile");
    public string LuaRun => Get("lua_run");
    public string LuaClearOutput => Get("lua_clear_output");
    public string LuaSave => Get("lua_save");

    public string OptionsTitle => Get("options_title");
    public string OptionsSectionProtectedFiles => Get("options_section_protected_files");
    public string OptionsSimCityLocaleLabel => Get("options_simcitylocale_label");
    public string OptionsSc4InstallFolderLabel => Get("options_sc4_install_folder_label");
    public string OptionsPluginsFolderLabel => Get("options_plugins_folder_label");
    public string OptionsSectionExternalTools => Get("options_section_external_tools");
    public string OptionsPimXLabel => Get("options_pimx_label");
    public string OptionsDataNodeLabel => Get("options_datanode_label");
    public string OptionsMapperLabel => Get("options_mapper_label");
    public string OptionsTerraformerLabel => Get("options_terraformer_label");
    public string OptionsSc4PacEditorLabel => Get("options_sc4pac_editor_label");
    public string OptionsNamDevelopmentSuiteHeader => Get("options_nam_development_suite_header");
    public string OptionsNamDevelopmentSuiteLabel => Get("options_nam_development_suite_label");
    public string OptionsProtectedFilesNote => Get("options_protected_files_note");
    public string OptionsAppearanceNote => Get("options_appearance_note");
    public string OptionsSectionProperties => Get("options_section_properties");
    public string OptionsChangePropertySource => Get("options_change_property_source");
    public string OptionsSectionAppearance => Get("options_section_appearance");
    public string OptionsLanguageLabel => Get("options_language_label");
    public string OptionsThemeLabel => Get("options_theme_label");
    public string OptionsBrowse => Get("options_browse");
    public string OptionsSave => Get("options_save");
    public string OptionsClose => Get("options_close");

    private Dictionary<string, string>? TryLoad(string code)
    {
        var path = Path.Combine(_folder, $"{code}.toml");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return TomlParser.Parse(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadLanguageName(string path)
    {
        try
        {
            var map = TomlParser.Parse(File.ReadAllText(path));
            return map.TryGetValue("language_name", out var name) ? name : null;
        }
        catch
        {
            return null;
        }
    }
}
