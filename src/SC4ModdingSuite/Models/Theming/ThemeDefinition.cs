namespace SC4ModdingSuite.Models;

/// <summary>A theme's color palette, parsed from a <c>Themes/&lt;key&gt;.toml</c> file by <see cref="ThemeDefinitionParser"/>.</summary>
public sealed class ThemeDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }

    public string WindowBackground { get; init; } = "#1E1E1E";
    public string WindowForeground { get; init; } = "#DDDDDD";
    public string Border { get; init; } = "#555555";

    public string ButtonBackground { get; init; } = "#1E1E1E";
    public string ButtonForeground { get; init; } = "#DDDDDD";
    public string ButtonBorder { get; init; } = "#777777";

    public string TextboxBackground { get; init; } = "#252525";
    public string TextboxForeground { get; init; } = "#DDDDDD";

    public string SelectionBackground { get; init; } = "#3A3A3A";
    public string SelectionForeground { get; init; } = "#FFFFFF";

    public string ListBoxBackground { get; init; } = "#1E1E1E";
    public string ListBoxItemSelectedBackground { get; init; } = "#333333";

    public string StatusForeground { get; init; } = "#66CC66";
    public string DangerForeground { get; init; } = "#CC5555";

    // --- Added to cover DataGrid/TabControl chrome, which previously fell back to plain
    // FluentTheme colors regardless of the active palette (see Styles/AppTheme.axaml). All
    // four fall back to an existing field's value when a theme .toml doesn't define them,
    // so every theme file written before this addition keeps rendering exactly as before. ---

    /// <summary>DataGrid column headers, and the unselected-tab "chrome" strip of TabControl/TabItem.</summary>
    public string HeaderBackground { get; init; } = "#1E1E1E";
    public string HeaderForeground { get; init; } = "#DDDDDD";

    /// <summary>DataGrid zebra-striping for every other row - defaults to the plain row background (no visible stripe) so older theme files render unchanged.</summary>
    public string AlternatingRowBackground { get; init; } = "#1E1E1E";

    /// <summary>DataGrid horizontal/vertical cell divider lines.</summary>
    public string GridLines { get; init; } = "#555555";
}

/// <summary>A theme entry for display in a selector: its file key and human-readable name.</summary>
public sealed class ThemeChoice
{
    public required string Key { get; init; }
    public required string Name { get; init; }
}
