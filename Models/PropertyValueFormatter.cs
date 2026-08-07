using System;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Formats a single property value, choosing hexadecimal - the SC4 modding community's own
/// convention, matching how <c>new_properties.xml</c> itself writes every <c>OPTION</c>
/// value - for categorical/enum-like properties (those with a matching
/// <see cref="PropertyDefinition"/> that declares named <see cref="PropertyDefinition.Options"/>),
/// and plain decimal for everything else (counts, sizes, coordinates, ...), where a hex
/// rendering would only make an ordinary number harder to read.
/// </summary>
public static class PropertyValueFormatter
{
    public static string Format(object? value, DBPFProperty.PropertyDataType dataType, bool preferHex)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (!preferHex)
        {
            return value.ToString() ?? string.Empty;
        }

        try
        {
            var number = Convert.ToInt64(value);
            return dataType switch
            {
                DBPFProperty.PropertyDataType.UINT8 => $"0x{(byte)number:X2}",
                DBPFProperty.PropertyDataType.UINT16 => $"0x{(ushort)number:X4}",
                DBPFProperty.PropertyDataType.UINT32 or DBPFProperty.PropertyDataType.SINT32 => $"0x{(uint)number:X8}",
                DBPFProperty.PropertyDataType.SINT64 => $"0x{number:X16}",
                _ => value.ToString() ?? string.Empty,
            };
        }
        catch
        {
            // Not actually convertible to a number (e.g. this DataType isn't numeric after
            // all) - fall back to the value's own default string form.
            return value.ToString() ?? string.Empty;
        }
    }
}
