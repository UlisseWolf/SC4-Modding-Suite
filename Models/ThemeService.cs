using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Loads and applies theme color palettes defined as <c>%APPDATA%\SC4ModdingSuite\Themes\&lt;key&gt;.toml</c>
/// files (parsed by <see cref="ThemeDefinitionParser"/>) - the same parent folder as
/// <c>new_properties.xml</c> (see <see cref="PropertySourceService"/>), so every
/// user-editable data file the app uses lives in one predictable place. The built-in
/// defaults are embedded in the assembly and copied out here on first run
/// (<see cref="EmbeddedResourceSeeder"/>) without ever overwriting a file the person has
/// since edited. The selected theme is applied at runtime by writing
/// <c>SolidColorBrush</c> values into <see cref="Application.Resources"/> under
/// well-known "ThemeXxx" keys that <c>Styles/AppTheme.axaml</c> references via
/// <c>{DynamicResource}</c> - see that file's header comment for the full explanation of
/// how the two halves fit together.
///
/// Per request, the plain-FluentTheme "Predefinito"/"default" choice has been removed
/// from the selectable list entirely - the app always applies one of the real TOML
/// palettes (Bloomberg Terminal by default). If a theme key can't be resolved for any
/// reason (e.g. a leftover "default" value from an older settings file, or a theme file
/// that was deleted), <see cref="Apply"/> falls back to <see cref="FallbackThemeKey"/>
/// instead of silently leaving plain Fluent active.
/// </summary>
public sealed class ThemeService
{
    /// <summary>Theme applied when the requested key can't be resolved, instead of ever falling back to plain Fluent.</summary>
    public const string FallbackThemeKey = "bloomberg";

    private readonly string _folder;
    private IStyle? _appliedStyle;

    public ThemeService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _folder = Path.Combine(appData, "SC4ModdingSuite", "Themes");
        EmbeddedResourceSeeder.SeedFolder(_folder, "Themes");
    }

    /// <summary>Every real theme (TOML palette) available - the plain-Fluent "default" choice is intentionally not listed.</summary>
    public List<ThemeChoice> AvailableThemes()
    {
        var list = new List<ThemeChoice>();

        foreach (var file in Directory.EnumerateFiles(_folder, "*.toml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var key = Path.GetFileNameWithoutExtension(file);
            var def = TryLoad(key);
            list.Add(new ThemeChoice { Key = key, Name = def?.Name ?? key });
        }

        return list;
    }

    /// <summary>
    /// Applies the given theme. If <paramref name="themeKey"/> is the legacy "default"
    /// value or otherwise can't be resolved to a real theme file, applies
    /// <see cref="FallbackThemeKey"/> instead - plain FluentTheme with no palette is never
    /// left active.
    /// </summary>
    public void Apply(string themeKey)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (_appliedStyle is not null)
        {
            app.Styles.Remove(_appliedStyle);
            _appliedStyle = null;
        }

        var def = TryLoad(themeKey);
        if (def is null && themeKey != FallbackThemeKey)
        {
            def = TryLoad(FallbackThemeKey);
        }

        if (def is null)
        {
            return;
        }

        SetBrush(app, "ThemeWindowBackground", def.WindowBackground);
        SetBrush(app, "ThemeWindowForeground", def.WindowForeground);
        SetBrush(app, "ThemeBorder", def.Border);
        SetBrush(app, "ThemeButtonBackground", def.ButtonBackground);
        SetBrush(app, "ThemeButtonForeground", def.ButtonForeground);
        SetBrush(app, "ThemeButtonBorder", def.ButtonBorder);
        SetBrush(app, "ThemeTextboxBackground", def.TextboxBackground);
        SetBrush(app, "ThemeTextboxForeground", def.TextboxForeground);
        SetBrush(app, "ThemeSelectionBackground", def.SelectionBackground);
        SetBrush(app, "ThemeSelectionForeground", def.SelectionForeground);
        SetBrush(app, "ThemeListBoxBackground", def.ListBoxBackground);
        SetBrush(app, "ThemeListBoxItemSelectedBackground", def.ListBoxItemSelectedBackground);
        SetBrush(app, "ThemeStatusForeground", def.StatusForeground);
        SetBrush(app, "ThemeDangerForeground", def.DangerForeground);
        SetBrush(app, "ThemeHeaderBackground", def.HeaderBackground);
        SetBrush(app, "ThemeHeaderForeground", def.HeaderForeground);
        SetBrush(app, "ThemeAlternatingRowBackground", def.AlternatingRowBackground);
        SetBrush(app, "ThemeGridLines", def.GridLines);

        var style = new StyleInclude(new Uri("avares://SC4ModdingSuite/"))
        {
            Source = new Uri("avares://SC4ModdingSuite/Styles/AppTheme.axaml"),
        };

        app.Styles.Add(style);
        _appliedStyle = style;
    }

    private ThemeDefinition? TryLoad(string key)
    {
        var path = Path.Combine(_folder, $"{key}.toml");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return ThemeDefinitionParser.Parse(key, File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void SetBrush(Application app, string resourceKey, string hex)
    {
        try
        {
            app.Resources[resourceKey] = new SolidColorBrush(Color.Parse(hex));
        }
        catch
        {
            // Invalid hex in a hand-edited theme file - leave whatever was there before
            // rather than crashing the theme switch over one bad color.
        }
    }
}
