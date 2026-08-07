using System;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Formats raw bytes as a classic hex + ASCII dump, for read-only inspection of entries
/// with no structured decoder in csDBPF (e.g. <c>DBPFEntryUnknown</c>) - display only, no
/// editing capability by design.
/// </summary>
public static class HexDump
{
    private const int BytesPerLine = 16;
    private const int MaxBytes = 8192; // cap to keep the preview responsive for large entries

    public static string Format(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return "(empty)";
        }

        var sb = new StringBuilder();
        var limit = Math.Min(data.Length, MaxBytes);

        for (var offset = 0; offset < limit; offset += BytesPerLine)
        {
            var count = Math.Min(BytesPerLine, limit - offset);
            sb.Append(offset.ToString("X8")).Append("  ");

            for (var i = 0; i < BytesPerLine; i++)
            {
                if (i < count)
                {
                    sb.Append(data[offset + i].ToString("X2")).Append(' ');
                }
                else
                {
                    sb.Append("   ");
                }

                if (i == 7)
                {
                    sb.Append(' ');
                }
            }

            sb.Append(' ');
            for (var i = 0; i < count; i++)
            {
                var b = data[offset + i];
                sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }

            sb.AppendLine();
        }

        if (data.Length > MaxBytes)
        {
            sb.AppendLine();
            sb.Append($"... ({data.Length - MaxBytes:N0} more bytes not shown)");
        }

        return sb.ToString();
    }
}
