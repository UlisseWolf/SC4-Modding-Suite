namespace SC4ModdingSuite.Models;

/// <summary>Parses a <c>Themes/&lt;key&gt;.toml</c> color palette file into a <see cref="ThemeDefinition"/>.</summary>
public static class ThemeDefinitionParser
{
    public static ThemeDefinition Parse(string key, string tomlText)
    {
        var map = TomlParser.Parse(tomlText);

        string Get(string mapKey, string fallback) => map.TryGetValue(mapKey, out var v) ? v : fallback;

        var windowBackground = Get("window_background", "#1E1E1E");
        var windowForeground = Get("window_foreground", "#DDDDDD");
        var buttonBackground = Get("button_background", windowBackground);
        var buttonForeground = Get("button_foreground", windowForeground);
        var listBoxBackground = Get("listbox_background", windowBackground);
        var border = Get("border", "#555555");

        return new ThemeDefinition
        {
            Key = key,
            Name = Get("name", key),
            WindowBackground = windowBackground,
            WindowForeground = windowForeground,
            Border = border,
            ButtonBackground = buttonBackground,
            ButtonForeground = buttonForeground,
            ButtonBorder = Get("button_border", "#777777"),
            TextboxBackground = Get("textbox_background", "#252525"),
            TextboxForeground = Get("textbox_foreground", windowForeground),
            SelectionBackground = Get("selection_background", "#3A3A3A"),
            SelectionForeground = Get("selection_foreground", "#FFFFFF"),
            ListBoxBackground = listBoxBackground,
            ListBoxItemSelectedBackground = Get("listboxitem_selected_background", "#333333"),
            StatusForeground = Get("status_foreground", "#66CC66"),
            DangerForeground = Get("danger_foreground", "#CC5555"),
            // Fall back to an already-established field's value, not a new hardcoded
            // default, so a theme .toml written before these keys existed still renders
            // pixel-for-pixel the same as it always did (header chrome looks like a
            // button, no visible zebra-striping, grid lines match the existing border color).
            HeaderBackground = Get("header_background", buttonBackground),
            HeaderForeground = Get("header_foreground", buttonForeground),
            AlternatingRowBackground = Get("alternating_row_background", listBoxBackground),
            GridLines = Get("gridlines", border),
        };
    }
}
