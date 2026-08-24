using System.Globalization;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Parses/formats Ilive Reader's own parenthesized-comma-separated UI property value text
/// (<c>ui_common.cpp</c>'s <c>TextToRect</c>/<c>TextToColor</c>/<c>TextToCPoint</c>) - the
/// one place this format's exact text shape lives, shared by
/// MainWindowViewModel (the 2D preview) and UiLegacyProp (the type-aware property editor),
/// so both always agree on what a given "(...)" string means.
/// </summary>
public static class UiValueFormat
{
    /// <summary>Parses "(left,top,right,bottom)" - a null/malformed value is treated as all-zero, same as Ilive Reader's own TextToRect (which likewise never fails, just leaves a partially/fully zeroed CRect).</summary>
    public static (int Left, int Top, int Right, int Bottom) ParseRect(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0, 0, 0);
        }

        var parts = text.Trim('(', ')').Split(',');
        if (parts.Length != 4 ||
            !int.TryParse(parts[0], out var l) || !int.TryParse(parts[1], out var t) ||
            !int.TryParse(parts[2], out var r) || !int.TryParse(parts[3], out var b))
        {
            return (0, 0, 0, 0);
        }

        return (l, t, r, b);
    }

    public static string FormatRect(int left, int top, int right, int bottom) =>
        $"({left.ToString(CultureInfo.InvariantCulture)},{top.ToString(CultureInfo.InvariantCulture)},{right.ToString(CultureInfo.InvariantCulture)},{bottom.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>Parses "(x,y)" - Ilive Reader's TextToCPoint, also used for every "xy"-kind property (gutters, textoffsets, minmax, ...), all of which share this same 2-number shape.</summary>
    public static (int X, int Y) ParseXy(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0);
        }

        var parts = text.Trim('(', ')').Split(',');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y))
        {
            return (0, 0);
        }

        return (x, y);
    }

    public static string FormatXy(int x, int y) =>
        $"({x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)})";

    /// <summary>Parses "(r,g,b)" - Ilive Reader's TextToColor.</summary>
    public static (byte R, byte G, byte B) ParseColor(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0, 0);
        }

        var parts = text.Trim('(', ')').Split(',');
        if (parts.Length != 3 ||
            !byte.TryParse(parts[0], out var r) || !byte.TryParse(parts[1], out var g) || !byte.TryParse(parts[2], out var b))
        {
            return (0, 0, 0);
        }

        return (r, g, b);
    }

    public static string FormatColor(byte r, byte g, byte b) => $"({r},{g},{b})";

    /// <summary>
    /// Parses a boolean-kind property's raw text tolerantly - real files may use "Y"
    /// (<see cref="UiLegacyParser"/>'s own default for a bare flag token with no explicit
    /// "=value"), "yes" (seen directly in Ilive Reader's own ui_common.cpp,
    /// e.g. edgeimage="yes"), "true", or "1". Anything else (including empty/absent) reads as false.
    /// </summary>
    public static bool ParseBool(string? text) =>
        text is not null &&
        (text.Equals("Y", System.StringComparison.OrdinalIgnoreCase) ||
         text.Equals("yes", System.StringComparison.OrdinalIgnoreCase) ||
         text.Equals("true", System.StringComparison.OrdinalIgnoreCase) ||
         text == "1");

    /// <summary>Written as "Y"/"N" - matching UiLegacyParser's own bare-flag-token default ("Y") exactly, so a property this editor sets true and one a hand-authored file left as a bare flag look identical.</summary>
    public static string FormatBool(bool value) => value ? "Y" : "N";
}
