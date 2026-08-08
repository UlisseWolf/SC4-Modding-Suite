using System;
using System.Collections.Generic;
using System.Text;
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
    public string? Error { get; init; }
    public List<ParsedExemplarProperty> Properties { get; init; } = new();
}

/// <summary>
/// Independently parses a binary Exemplar/Cohort ("EQZB1###" format) directly from raw
/// bytes, ported and cross-checked byte-for-byte against Ilive Reader's own
/// <c>_examplar::ExemplarDecodeEQZB</c> (<c>or_dat/cl_exemplar.cpp</c>).
///
/// <para>
/// <b>Why this exists instead of just using csDBPF's own <c>DBPFEntryEXMP.ListOfProperties</c></b>:
/// testing against a real Lot Configuration Exemplar (a large one, ~90 properties, many of
/// them the "array" form used by e.g. <c>LotConfigPropertyLotObject</c> - one entry per
/// building/prop/texture/flora/network placement on the lot) showed csDBPF's own property
/// dictionary contains implausible IDs (<c>0x00008001</c>, <c>0x00008003</c>, ...) that
/// don't correspond to anything in the actual file - independently re-decoding the exact
/// same bytes with this class (and, separately, with <see cref="ExemplarBinaryValidator"/>)
/// shows the file itself is perfectly well-formed: every property decodes cleanly and the
/// last byte read lands exactly at end-of-file. The "unknown" IDs turned out to sit exactly
/// at the boundary between two adjacent 2-byte header fields of an *array-mode* property
/// (its DataType + ArrayFlag words, read together as a bogus 4-byte ID) - strongly
/// suggesting csDBPF's own decoder mis-steps specifically on the array encoding used
/// heavily by Lot Configuration Exemplars, and desyncs for every property after the first
/// one it mishandles. Since csDBPF ships as a compiled DLL with no available source to fix,
/// <see cref="MainWindowViewModel.LoadPropertiesForSelectedEntry"/> uses this independent
/// parser for the Properties panel instead, falling back to csDBPF's own decode only if
/// this parser can't make sense of the bytes either.
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

        if (!(Matches(data, "EQZB") || Matches(data, "CQZB")))
        {
            // Not binary-encoded (e.g. rare EQZT text format) - nothing this parser can read.
            return new ParsedExemplar { Error = "Not a binary-encoded (EQZB) Exemplar." };
        }

        var result = new ParsedExemplar();

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
