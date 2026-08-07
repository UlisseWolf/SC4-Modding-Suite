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

        return new ThemeDefinition
        {
            Key = key,
            Name = Get("name", key),
            WindowBackground = windowBackground,
            WindowForeground = windowForeground,
            Border = Get("border", "#555555"),
            ButtonBackground = Get("button_background", windowBackground),
            ButtonForeground = Get("button_foreground", windowForeground),
            ButtonBorder = Get("button_border", "#777777"),
            TextboxBackground = Get("textbox_background", "#252525"),
            TextboxForeground = Get("textbox_foreground", windowForeground),
            SelectionBackground = Get("selection_background", "#3A3A3A"),
            SelectionForeground = Get("selection_foreground", "#FFFFFF"),
            ListBoxBackground = Get("listbox_background", windowBackground),
            ListBoxItemSelectedBackground = Get("listboxitem_selected_background", "#333333"),
            StatusForeground = Get("status_foreground", "#66CC66"),
            DangerForeground = Get("danger_foreground", "#CC5555"),
        };
    }
}
