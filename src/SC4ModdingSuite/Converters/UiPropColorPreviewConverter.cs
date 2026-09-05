using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.Converters;

/// <summary>
/// Converts a color-kind <see cref="UiLegacyProp"/>'s raw "(r,g,b)" <c>Value</c> text into
/// an <see cref="IBrush"/> swatch, for <c>UiNodePropertiesDialog.axaml</c>'s color-kind
/// editor row. This used to be a plain <c>ColorPreview</c> property directly on
/// <see cref="UiLegacyProp"/> itself - moved out into this converter (bound to
/// <c>{Binding Value, Converter={StaticResource UiPropColorPreviewConverter}}</c> instead
/// of a property binding) so <c>Models/UiLegacyParser.cs</c> has zero Avalonia dependency -
/// useful now that MCP server mode (<c>Mcp/Tools/DbpfTools.cs</c>) is compiled into this
/// same project too, since it reuses that same file for reading legacy UI entries and has
/// no need for Avalonia's own UI framework at all.
/// </summary>
public sealed class UiPropColorPreviewConverter : IValueConverter
{
    public static readonly UiPropColorPreviewConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (r, g, b) = UiValueFormat.ParseColor(value as string);
        return new SolidColorBrush(new Color(255, r, g, b));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
