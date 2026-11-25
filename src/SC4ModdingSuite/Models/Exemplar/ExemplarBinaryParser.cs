using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>One property as independently decoded by <see cref="ExemplarBinaryParser"/>, before being converted to a real <see cref="DBPFProperty"/>.</summary>
public sealed class ParsedExemplarProperty
{
    public required uint Id { get; init; }
    public required DBPFProperty.PropertyDataType DataType { get; init; }

    /// <summary>Numeric values (boxed <c>uint</c>/<c>int</c>/<c>float</c>/<c>long</c>/<c>bool</c>), or a single boxed <c>string</c> for STRING properties.</summary>
    public required object[] Values { get; init; }
}

public sealed record ParsedExemplar
{
    public bool IsWellFormed { get; init; }

    /// <summary>Parent cohort TGI from the header (all zeros = no parent).</summary>
    public (uint Type, uint Group, uint Instance) ParentCohort { get; init; }
    public string? Error { get; init; }
    public List<ParsedExemplarProperty> Properties { get; init; } = new();
}

/// <summary>
/// Independently parses a binary Exemplar/Cohort ("EQZB1###" format) directly from raw
/// bytes, ported from Ilive Reader's own <c>_examplar::ExemplarDecodeEQZB</c>
/// (<c>or_dat/cl_exemplar.cpp</c>).
///
/// <para>
/// <b>Why this exists instead of just using csDBPF's own <c>DBPFEntryEXMP.ListOfProperties</c></b>:
/// csDBPF's own property dictionary produces implausible IDs (<c>0x00008001</c>,
/// <c>0x00008003</c>, ...) for large Exemplars with many "array-mode" properties (the
/// encoding <c>LotConfigPropertyLotObject</c> and similar repeating properties use) - these
/// IDs sit exactly at the boundary between two adjacent 2-byte header fields of an
/// array-mode property (its DataType + ArrayFlag words, read together as a bogus 4-byte
/// ID), and the decoder desyncs for every property after the first one it mishandles. Since
/// csDBPF ships as a compiled DLL with no available source to fix,
/// <see cref="MainWindowViewModel.LoadPropertiesForSelectedEntry"/> uses this independent
/// parser for the Properties panel instead, falling back to csDBPF's own decode only if
/// this parser can't make sense of the bytes either.
/// </para>
///
/// <para>
/// Also reads the text format ("EQZT1###"/"CQZT1###", one <c>0xID:{"Name"}=Type:Count:{values}</c>
/// line per property - common in growable building exemplars). csDBPF's own text decode
/// parses floats with the PC's regional settings, so on a German/Italian Windows
/// <c>Occupant Size=Float32:3:{15.997,43.539001,20.351999}</c> came out as 15997, ...;
/// everything here is parsed with <see cref="CultureInfo.InvariantCulture"/>.
/// </para>
/// </summary>
public static class ExemplarBinaryParser
{
    public static ParsedExemplar Parse(byte[]? data)
    {
        if (data is null || data.Length < 24)
        {
            return new ParsedExemplar { Error = "File is too short to contain a valid Exemplar header." };
        }

        if (Matches(data, "EQZT") || Matches(data, "CQZT"))
        {
            return ParseText(data);
        }

        if (!(Matches(data, "EQZB") || Matches(data, "CQZB")))
        {
            return new ParsedExemplar { Error = "Not an Exemplar/Cohort (no EQZB/EQZT header)." };
        }

        var result = new ParsedExemplar
        {
            ParentCohort = (BitConverter.ToUInt32(data, 8), BitConverter.ToUInt32(data, 12), BitConverter.ToUInt32(data, 16)),
        };

        try
        {
            var propCount = BitConverter.ToUInt32(data, 20);
            var offset = 24;

            for (uint i = 0; i < propCount; i++)
            {
                if (offset + 8 > data.Length)
                {
                    return result with { Error = $"Property {i}: header runs past end of file at offset 0x{offset:X}." };
                }

                var id = BitConverter.ToUInt32(data, offset);
                offset += 4;
                var rawType = BitConverter.ToUInt16(data, offset);
                offset += 2;
                var arrayFlag = BitConverter.ToUInt16(data, offset);
                offset += 2;

                object[] values;
                DBPFProperty.PropertyDataType dataType;

                switch (rawType)
                {
                    case 0x300: // Uint32
                        dataType = DBPFProperty.PropertyDataType.UINT32;
                        if (!TryReadNumeric(data, ref offset, arrayFlag, 4, b => BitConverter.ToUInt32(b), out values, out var e1))
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): {e1}" };
                        }

                        break;

                    case 0x700: // Sint32
                        dataType = DBPFProperty.PropertyDataType.SINT32;
                        if (!TryReadNumeric(data, ref offset, arrayFlag, 4, b => BitConverter.ToInt32(b), out values, out var e2))
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): {e2}" };
                        }

                        break;

                    case 0x900: // Float32
                        dataType = DBPFProperty.PropertyDataType.FLOAT32;
                        if (!TryReadNumeric(data, ref offset, arrayFlag, 4, b => BitConverter.ToSingle(b), out values, out var e3))
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): {e3}" };
                        }

                        break;

                    case 0x200: // Uint16
                        dataType = DBPFProperty.PropertyDataType.UINT16;
                        if (!TryReadNumeric(data, ref offset, arrayFlag, 2, b => BitConverter.ToUInt16(b), out values, out var e7))
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): {e7}" };
                        }

                        break;

                    case 0x100: // Uint8
                        dataType = DBPFProperty.PropertyDataType.UINT8;
                        if (!TryReadNumeric(data, ref offset, arrayFlag, 1, b => b[0], out values, out var e4))
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): {e4}" };
                        }

                        break;

                    case 0xB00: // Sint8/Bool
                        dataType = DBPFProperty.PropertyDataType.BOOL;
                        if (!TryReadNumeric(data, ref offset, arrayFlag, 1, b => b[0] != 0, out values, out var e5))
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): {e5}" };
                        }

                        break;

                    case 0x800: // Sint64
                        dataType = DBPFProperty.PropertyDataType.SINT64;
                        if (!TryReadNumeric(data, ref offset, arrayFlag, 8, b => BitConverter.ToInt64(b), out values, out var e6))
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): {e6}" };
                        }

                        break;

                    case 0xC00: // String
                        dataType = DBPFProperty.PropertyDataType.STRING;
                        if (offset + 5 > data.Length)
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): string length header runs past end of file." };
                        }

                        offset += 1; // rep byte, unused for strings
                        var length = BitConverter.ToUInt32(data, offset);
                        offset += 4;
                        if (offset + length > data.Length)
                        {
                            return result with { Error = $"Property {i} (ID 0x{id:X8}): string data ({length} bytes) runs past end of file." };
                        }

                        values = new object[] { Encoding.Latin1.GetString(data, offset, (int)length) };
                        offset += (int)length;
                        break;

                    default:
                        return result with
                        {
                            Error = $"Property {i} (ID 0x{id:X8}) at offset 0x{offset - 8:X}: unrecognized data type 0x{rawType:X}.",
                        };
                }

                result.Properties.Add(new ParsedExemplarProperty { Id = id, DataType = dataType, Values = values });
            }

            if (offset != data.Length)
            {
                return result with
                {
                    Error = $"Decoded all {propCount} properties, but {data.Length - offset} trailing byte(s) remain unaccounted for.",
                };
            }

            return result with { IsWellFormed = true };
        }
        catch (Exception ex)
        {
            return result with { Error = $"Unexpected error while parsing: {ex.Message}" };
        }
    }

    /// <summary>
    /// Reads either a single value (<paramref name="arrayFlag"/> == 0: 1-byte rep count,
    /// then one value) or an array of values (non-zero: 1 skipped byte, a uint16 repetition
    /// count, 2 more skipped bytes, then that many values) - the exact layout shared by
    /// every fixed-width numeric property type in the format.
    /// </summary>
    private static bool TryReadNumeric(
        byte[] data,
        ref int offset,
        ushort arrayFlag,
        int elementSize,
        Func<byte[], object> readOne,
        out object[] values,
        out string error)
    {
        error = string.Empty;
        values = Array.Empty<object>();

        if (arrayFlag != 0)
        {
            if (offset + 5 > data.Length)
            {
                error = "array header runs past end of file.";
                return false;
            }

            offset += 1;
            var rep = BitConverter.ToUInt16(data, offset);
            offset += 2;
            offset += 2;

            var needed = rep * elementSize;
            if (offset + needed > data.Length)
            {
                error = $"array of {rep} value(s) runs past end of file.";
                return false;
            }

            var results = new object[rep];
            for (var i = 0; i < rep; i++)
            {
                results[i] = readOne(data.AsSpan(offset, elementSize).ToArray());
                offset += elementSize;
            }

            values = results;
            return true;
        }

        if (offset + 1 + elementSize > data.Length)
        {
            error = "value runs past end of file.";
            return false;
        }

        offset += 1; // rep byte, always exactly one value follows regardless of its content
        values = new[] { readOne(data.AsSpan(offset, elementSize).ToArray()) };
        offset += elementSize;
        return true;
    }

    // 0x27812810:{"Occupant Size"}=Float32:3:{15.997,43.539001,20.351999}
    private static readonly Regex TextPropertyLine = new(
        @"^0x(?<id>[0-9a-fA-F]{1,8}):\{""(?<name>.*?)""\}=(?<type>\w+):(?<count>\d+):\{(?<values>.*)\}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex TextParentCohort = new(
        @"^ParentCohort=Key:\{0x(?<t>[0-9a-fA-F]+),0x(?<g>[0-9a-fA-F]+),0x(?<i>[0-9a-fA-F]+)\}$",
        RegexOptions.CultureInvariant);

    /// <summary>Text-format (EQZT/CQZT) Exemplar/Cohort - see the class remarks.</summary>
    private static ParsedExemplar ParseText(byte[] data)
    {
        var result = new ParsedExemplar();
        var lines = Encoding.Latin1.GetString(data).Split('\n');
        int? expectedCount = null;

        try
        {
            for (var n = 1; n < lines.Length; n++) // line 0 = "EQZT1###"
            {
                var line = lines[n].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var parent = TextParentCohort.Match(line);
                if (parent.Success)
                {
                    result = result with { ParentCohort = (ParseHexU32(parent.Groups["t"].Value), ParseHexU32(parent.Groups["g"].Value), ParseHexU32(parent.Groups["i"].Value)) };
                    continue;
                }

                if (line.StartsWith("PropCount=", StringComparison.OrdinalIgnoreCase))
                {
                    var countText = line["PropCount=".Length..].Trim();
                    expectedCount = countText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? (int)ParseHexU32(countText[2..])
                        : int.Parse(countText, CultureInfo.InvariantCulture);
                    continue;
                }

                var match = TextPropertyLine.Match(line);
                if (!match.Success)
                {
                    return result with { Error = $"Line {n + 1}: not a property line: {line}" };
                }

                var id = ParseHexU32(match.Groups["id"].Value);
                var typeName = match.Groups["type"].Value;
                var valuesText = match.Groups["values"].Value;

                if (typeName.Equals("String", StringComparison.OrdinalIgnoreCase))
                {
                    var text = valuesText.Length >= 2 && valuesText[0] == '"' && valuesText[^1] == '"'
                        ? valuesText[1..^1]
                        : valuesText;
                    result.Properties.Add(new ParsedExemplarProperty { Id = id, DataType = DBPFProperty.PropertyDataType.STRING, Values = new object[] { text } });
                    continue;
                }

                if (!TryMapTextType(typeName, out var dataType))
                {
                    return result with { Error = $"Line {n + 1} (ID 0x{id:X8}): unrecognized data type '{typeName}'." };
                }

                var parts = valuesText.Length == 0 ? Array.Empty<string>() : valuesText.Split(',');
                var values = new object[parts.Length];
                for (var v = 0; v < parts.Length; v++)
                {
                    values[v] = ParseTextValue(parts[v].Trim(), dataType);
                }

                result.Properties.Add(new ParsedExemplarProperty { Id = id, DataType = dataType, Values = values });
            }
        }
        catch (Exception ex)
        {
            return result with { Error = $"Unexpected error while parsing text exemplar: {ex.Message}" };
        }

        if (expectedCount is { } expected && expected != result.Properties.Count)
        {
            return result with { Error = $"PropCount says {expected} properties, but {result.Properties.Count} were found." };
        }

        return result with { IsWellFormed = true };
    }

    private static bool TryMapTextType(string name, out DBPFProperty.PropertyDataType type)
    {
        type = name.ToLowerInvariant() switch
        {
            "uint8" => DBPFProperty.PropertyDataType.UINT8,
            "uint16" => DBPFProperty.PropertyDataType.UINT16,
            "uint32" => DBPFProperty.PropertyDataType.UINT32,
            "sint32" => DBPFProperty.PropertyDataType.SINT32,
            "sint64" => DBPFProperty.PropertyDataType.SINT64,
            "float32" => DBPFProperty.PropertyDataType.FLOAT32,
            "bool" => DBPFProperty.PropertyDataType.BOOL,
            _ => DBPFProperty.PropertyDataType.UNKNOWN,
        };
        return type != DBPFProperty.PropertyDataType.UNKNOWN;
    }

    /// <summary>Boxed the same way the binary path boxes each type (byte/ushort/uint/int/long/float/bool).</summary>
    private static object ParseTextValue(string text, DBPFProperty.PropertyDataType type)
    {
        if (type == DBPFProperty.PropertyDataType.FLOAT32)
        {
            return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        if (type == DBPFProperty.PropertyDataType.BOOL
            && (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase)))
        {
            return text.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        var raw = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : unchecked((ulong)long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture));

        return type switch
        {
            DBPFProperty.PropertyDataType.UINT8 => (byte)raw,
            DBPFProperty.PropertyDataType.UINT16 => (ushort)raw,
            DBPFProperty.PropertyDataType.UINT32 => (uint)raw,
            DBPFProperty.PropertyDataType.SINT32 => unchecked((int)(uint)raw),
            DBPFProperty.PropertyDataType.SINT64 => unchecked((long)raw),
            DBPFProperty.PropertyDataType.BOOL => raw != 0,
            _ => throw new FormatException($"Unsupported type {type}."),
        };
    }

    private static uint ParseHexU32(string hex) => uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static bool Matches(byte[] data, string tag) =>
        data.Length >= tag.Length && data.AsSpan(0, tag.Length).SequenceEqual(Encoding.ASCII.GetBytes(tag));

    /// <summary>
    /// Builds a real csDBPF <see cref="DBPFProperty"/> from an independently-parsed
    /// property, so the rest of the property editing pipeline (the property panel, the
    /// Add/Edit dialog, Remove) keeps working completely unmodified regardless of where
    /// the property data actually came from.
    /// </summary>
    public static DBPFProperty ToDbpfProperty(ParsedExemplarProperty parsed)
    {
        DBPFProperty property = parsed.DataType switch
        {
            DBPFProperty.PropertyDataType.FLOAT32 => new DBPFPropertyFloat(
                Array.ConvertAll(parsed.Values, v => (float)v), DBPF.Encoding.Binary),

            DBPFProperty.PropertyDataType.STRING => new DBPFPropertyString(
                (string)parsed.Values[0], DBPF.Encoding.Binary),

            _ => new DBPFPropertyLong(
                parsed.DataType,
                Array.ConvertAll(parsed.Values, Convert.ToInt64),
                DBPF.Encoding.Binary),
        };

        property.ID = parsed.Id;
        return property;
    }
}
