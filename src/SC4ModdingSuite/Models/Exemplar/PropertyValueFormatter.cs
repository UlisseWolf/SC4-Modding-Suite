using System;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Formats a single property value as hexadecimal - the SC4 modding community's own
/// convention, matching how <c>new_properties.xml</c> itself writes every <c>OPTION</c>
/// value, Ilive Reader's own property grid, and how every other SC4 modding tool
/// displays Exemplar/Cohort property values - for every integer property type (UINT8/16/32,
/// SINT32/64), regardless of whether the property has a matching, named
/// <see cref="PropertyDefinition"/> in the database. FLOAT32, STRING and BOOL values are
/// shown in their normal form (a hex rendering of a float's bit pattern, or of "true"/
/// "false", would not be more readable).
/// </summary>
public static class PropertyValueFormatter
{
    public static string Format(object? value, DBPFProperty.PropertyDataType dataType)
    {
        if (value is null)
        {
            return string.Empty;
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
