using System;
using System.Collections.Generic;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Port of Ilive Reader's <c>_lev::DecodeLev</c>/<c>EncodeLev</c> (<c>or_dat/cl_lev.cpp</c>,
/// ENT_LEV - shares Type ID 0x2026960B with LTEXT/WAV/XA, see
/// <see cref="EntryTypeClassifier.IsLtextWavXaType"/>) - a header string, then a
/// <c>#</c>-delimited sequence of tab-separated "table" sections: each section's first
/// row is column titles (tab-separated, CRLF-terminated), each following row is that
/// many tab-separated values (one column each), until the next <c>#</c> or the end of the
/// content.
/// </summary>
public static class LevCodec
{
    public sealed record LevColumn(string Title, List<string> Values);
    public sealed record LevSection(List<LevColumn> Columns);
    public sealed record LevData(string Header, List<LevSection> Sections);

    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// Loose, content-based check for whether <paramref name="bytes"/> is LEV-shaped -
    /// used to distinguish it from the other formats sharing Type ID 0x2026960B (LTEXT's
    /// own binary layout, RIFF/WAVE, or an unsupported XA codec): LEV content always has a
    /// '#' section marker, and (being a plain-text tabular format) is otherwise
    /// printable/CRLF/TAB text throughout.
    /// </summary>
    public static bool LooksLikeLev(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0 || Array.IndexOf(bytes, (byte)'#') < 0)
        {
            return false;
        }

        foreach (var b in bytes)
        {
            var isPrintable = b is >= 0x20 and < 0x7F;
            var isTextControl = b is (byte)'\r' or (byte)'\n' or (byte)'\t';
            if (!isPrintable && !isTextControl)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryDecode(byte[] input, out LevData data)
    {
        data = new LevData(string.Empty, new List<LevSection>());

        var content = Latin1.GetString(input);
        var hashIndex = content.IndexOf('#');
        if (hashIndex == -1)
        {
            return false;
        }

        var header = content[..hashIndex];
        // DecodeLev's own boundary (everything up to "#") includes the trailing "\r\n" that
        // precedes it, and EncodeLev unconditionally re-adds its own "\r\n" before "#" -
        // decoding a file and re-encoding it (with no edits) would double that CRLF, then
        // double again next time. Stripping the trailing CRLF here (Encode below always
        // adds back exactly one) avoids that drift.
        if (header.EndsWith("\r\n", StringComparison.Ordinal))
        {
            header = header[..^2];
        }
        else if (header.EndsWith('\n'))
        {
            header = header[..^1];
        }

        var rest = content[(hashIndex + 1)..];
        var sections = new List<LevSection>();

        while (rest.Length > 0)
        {
            var nextHash = rest.IndexOf('#');
            var lineBlock = nextHash == -1 ? rest : rest[..nextHash];
            rest = nextHash == -1 ? string.Empty : rest[(nextHash + 1)..];

            var section = new LevSection(new List<LevColumn>());
            var isTitle = true;
            var length = lineBlock.Length;

            var i = 0;
            if (length > 0 && lineBlock[0] == '\t')
            {
                i = 1; // skip the leading tab, matching DecodeLev exactly
            }

            var col = 0;
            var prev = i;
            for (; i < length; i++)
            {
                var c = lineBlock[i];
                if (c != '\t' && c != '\r')
                {
                    continue;
                }

                var token = lineBlock.Substring(prev, i - prev);
                prev = i + 1;

                if (isTitle)
                {
                    section.Columns.Add(new LevColumn(token, new List<string>()));
                }
                else
                {
                    while (col >= section.Columns.Count)
                    {
                        section.Columns.Add(new LevColumn(string.Empty, new List<string>()));
                    }

                    section.Columns[col].Values.Add(token);
                }

                col++;

                if (c == '\r')
                {
                    if (isTitle)
                    {
                        isTitle = false;
                    }

                    col = 0;
                    i++; // skip the \r we just matched, then also skip a following \n and \t
                    if (i < length && lineBlock[i] == '\n')
                    {
                        i++;
                    }

                    if (i < length && lineBlock[i] == '\t')
                    {
                        i++;
                    }

                    prev = i;
                    i--; // the for-loop's own i++ will land back on `prev`
                }
            }

            sections.Add(section);
        }

        data = new LevData(header, sections);
        return true;
    }

    public static byte[] Encode(LevData data)
    {
        const string tab = "\t";
        const string crlf = "\r\n";

        var lev = new StringBuilder();
        lev.Append(data.Header).Append(crlf).Append('#');

        for (var s = 0; s < data.Sections.Count; s++)
        {
            var section = data.Sections[s];
            var rowCount = 0;

            foreach (var column in section.Columns)
            {
                lev.Append(tab).Append(column.Title);
                rowCount = Math.Max(rowCount, column.Values.Count);
            }

            lev.Append(crlf);

            for (var row = 0; row < rowCount; row++)
            {
                foreach (var column in section.Columns)
                {
                    if (row < column.Values.Count)
                    {
                        lev.Append(tab).Append(column.Values[row]);
                    }
                }

                lev.Append(crlf);
            }

            if (s < data.Sections.Count - 1)
            {
                // Ilive Reader's own EncodeLev appends "\r\n#\t" here (a section boundary
                // CRLF+'#', plus an extra leading tab), but the next section's own
                // column-title loop then appends its own "\t"+title for the first column -
                // producing a double tab at the start of every section after the first.
                // DecodeLev only skips one leading tab, so the second becomes a spurious
                // empty first column that misaligns every later column's data. Only the
                // leading tab here is dropped; the following section's own title loop still
                // supplies its own, so the boundary ends up with exactly one tab, matching
                // the first section's own leading tab.
                lev.Append('#');
            }
        }

        return Latin1.GetBytes(lev.ToString());
    }
}
