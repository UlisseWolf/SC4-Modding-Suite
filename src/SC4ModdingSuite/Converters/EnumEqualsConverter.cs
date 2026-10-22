using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SC4ModdingSuite.Converters;

/// <summary>
/// Compares a bound enum value against <c>ConverterParameter</c> (its member name as a
/// string) for an <c>IsVisible</c>-style binding - used by <c>S3DBatchToolsDialog.axaml</c>
/// to show only the parameter panel matching the selected <c>S3DBatchToolMode</c>.
/// </summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string name && string.Equals(value.ToString(), name, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
