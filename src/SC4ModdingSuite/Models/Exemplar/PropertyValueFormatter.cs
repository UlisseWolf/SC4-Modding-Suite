using System;
using System.Globalization;
using System.Linq;
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

        // FLOAT32 (Occupant Size, ...) always with a '.' decimal point, whatever the Windows
        // locale: with the current culture a German/Italian PC showed "16,5, 12,5, 16" -
        // decimal commas mixed up with the ", " between values - and the edit dialog
        // (which parses with InvariantCulture) then rejected its own pre-filled text.
        if (value is float or double)
        {
            return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
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

    // Words in a property name that mean its value is an identifier (TGI part, family,
    // resource key...) - a decimal rendering of those is meaningless, so they stay hex.
    private static readonly string[] IdWords =
    {
        "id", "ids", "iid", "iids", "key", "keys", "family", "families", "tgi", "instance", "group", "groups",
        "parent", "resource", "texture", "model", "sound", "ltext", "cohort", "query", "type", "guid",
    };

    /// <summary>
    /// "Readable values" mode (see <see cref="AppOptions.ShowReadablePropertyValues"/>): an
    /// integer value that matches one of the property's named options (new_properties.xml
    /// OPTION) is shown by that name; otherwise it's shown in decimal, unless it looks like
    /// an identifier (ID-ish property name, or a UINT32/SINT64 value of 0x01000000 or more,
    /// which in practice is always a random instance/group ID) - those keep the hex form.
    /// Non-integer types are formatted exactly as <see cref="Format"/> does.
    /// </summary>
    public static string FormatReadable(object? value, DBPFProperty.PropertyDataType dataType, PropertyDefinition? definition)
    {
        if (value is null)
        {
            return string.Empty;
        }

        long number;
        try
        {
            var raw = Convert.ToInt64(value);
            number = dataType switch
            {
                DBPFProperty.PropertyDataType.UINT8 => (byte)raw,
                DBPFProperty.PropertyDataType.UINT16 => (ushort)raw,
                DBPFProperty.PropertyDataType.UINT32 => (uint)raw,
                DBPFProperty.PropertyDataType.SINT32 => (int)raw,
                DBPFProperty.PropertyDataType.SINT64 => raw,
                _ => throw new FormatException(),
            };
        }
        catch
        {
            return Format(value, dataType);
        }

        var option = definition?.Options.FirstOrDefault(o => OptionMatches(o.Value, number));
        if (option is not null && !string.IsNullOrWhiteSpace(option.Name))
        {
            return option.Name;
        }

        var bigUnsigned = dataType is DBPFProperty.PropertyDataType.UINT32 or DBPFProperty.PropertyDataType.SINT64
            && (ulong)number >= 0x01000000UL;
        if (bigUnsigned || HasIdWord(definition?.Name))
        {
            return Format(value, dataType);
        }

        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static bool OptionMatches(string optionValue, long number)
    {
        var text = optionValue.Trim();
        long parsed;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
            {
                return false;
            }
        }
        else if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return false;
        }

        // An option written as e.g. 0xFFFFFFFF must still match a SINT32 value of -1.
        return parsed == number || (number < 0 && parsed >= 0 && parsed <= uint.MaxValue && (uint)parsed == (uint)number);
    }

    // Word boundaries: separators, plus camelCase joins ("OccupantGroups" -> Occupant, Groups).
    private static readonly System.Text.RegularExpressions.Regex WordSplit =
        new(@"[\s/\-_():,]+|(?<=[a-z])(?=[A-Z])", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool HasIdWord(string? propertyName) =>
        propertyName is not null
        && WordSplit.Split(propertyName).Any(word => IdWords.Contains(word.ToLowerInvariant()));
}
