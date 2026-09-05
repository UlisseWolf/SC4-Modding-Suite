using System;
using System.Collections.Generic;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Port of Ilive Reader's <c>_tkd::DecodeTKD</c>/<c>EncodeTKD</c> (<c>or_dat/cl_tkd.cpp</c>)
/// - the binary layout behind TRK entries (ENT_TRK 0x5D73A611 / ENT_TRK2 0x0B8D821A): a
/// fixed-size header, a comma-separated ASCII field list, and a 4-byte "ETKD" footer magic.
/// Uses Latin-1 (ISO-8859-1), not UTF-8, to map the header's raw bytes to/from a .NET
/// string - Latin-1 maps every byte value 0-255 to the identically-numbered character with
/// no loss, matching how an MFC <c>CString</c> (single-byte-per-char, which is what wrote
/// this format) treats arbitrary bytes; UTF-8 would corrupt the header's non-ASCII/binary
/// bytes (a length field, not real text) on the way back out.
/// </summary>
public static class TkdCodec
{
    private const string LongHeaderMagic = "2DKT";
    private const string FooterMagic = "ETKD";
    private static readonly Encoding Latin1 = Encoding.Latin1;

    public sealed record TkdData(byte[] Header, int HeaderSize, List<string> Fields);

    /// <summary>
    /// Mirrors <c>_tkd::DecodeTKD(char *input, int size)</c>, including one quirk: it
    /// searches for "ETKD" anywhere in the content to confirm the file is TKD-shaped, but
    /// then unconditionally trims the <i>last</i> 4 characters regardless of where "ETKD"
    /// was actually found. Reproduced as-is for byte-for-byte compatibility with real
    /// Maxis-written files, which always do end with "ETKD".
    /// </summary>
    public static bool TryDecode(byte[] input, out TkdData data)
    {
        data = new TkdData(new byte[12], 4, new List<string>());

        if (input.Length < 4)
        {
            return false;
        }

        var hasLongHeader = Latin1.GetString(input, 0, 4) == LongHeaderMagic;
        var headerSize = hasLongHeader ? 12 : 4;

        if (input.Length < headerSize)
        {
            return false;
        }

        var header = new byte[12];
        Array.Copy(input, header, headerSize);

        var content = Latin1.GetString(input, headerSize, input.Length - headerSize);

        if (content.IndexOf(FooterMagic, StringComparison.Ordinal) == -1)
        {
            data = new TkdData(header, headerSize, new List<string>());
            return false;
        }

        var trimmed = content[..^4]; // drop the trailing "ETKD" - see doc comment above
        var fields = new List<string>();

        var prev = 1; // starts at 1, not 0 - the content always starts with a leading comma
                      // (EncodeTKD always writes one), so scanning from index 1 skips it
                      // instead of producing a spurious empty first field.
        for (var i = 1; i < trimmed.Length; i++)
        {
            if (trimmed[i] == ',')
            {
                fields.Add(trimmed.Substring(prev, i - prev));
                prev = i + 1;
            }
        }

        data = new TkdData(header, headerSize, fields);
        return true;
    }

    /// <summary>
    /// Mirrors <c>_tkd::EncodeTKD</c>: rebuilds the comma-separated content from
    /// <paramref name="data"/>'s fields, recomputes the embedded length field at header
    /// bytes 4-7 (only present for a 12-byte header), and reassembles header+content+"ETKD".
    /// </summary>
    public static byte[] Encode(TkdData data)
    {
        var content = new StringBuilder();
        foreach (var field in data.Fields)
        {
            content.Append(',').Append(field);
        }

        content.Append(',');

        var contentBytes = Latin1.GetBytes(content.ToString());
        var header = (byte[])data.Header.Clone();

        if (data.HeaderSize == 12)
        {
            // *(DWORD*)(header+4) = csTdk.GetLength() + 8;  (little-endian, matching x86)
            var sizeField = contentBytes.Length + 8;
            BitConverter.GetBytes(sizeField).CopyTo(header, 4);
        }

        var totalSize = contentBytes.Length + data.HeaderSize + 4;
        var result = new byte[totalSize];
        Array.Copy(header, 0, result, 0, data.HeaderSize);
        Array.Copy(contentBytes, 0, result, data.HeaderSize, contentBytes.Length);
        Latin1.GetBytes(FooterMagic).CopyTo(result, totalSize - 4);
        return result;
    }

    /// <summary>Whole-file view with NUL bytes shown as '.' - matches Ilive Reader's CFormTKD::Display raw text box (informational only; the field list is what's actually editable/saved).</summary>
    public static string FormatRawWithDots(byte[] input)
    {
        var chars = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            chars[i] = input[i] == 0 ? '.' : (char)input[i];
        }

        return new string(chars);
    }
}
