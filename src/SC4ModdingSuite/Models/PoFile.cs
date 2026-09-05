using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>One translatable string, round-tripped to/from a gettext .po/.pot file.</summary>
/// <param name="Msgctxt">
/// The LTEXT's own TGI, formatted as <c>"TTTTTTTT-GGGGGGGG-IIIIIIII"</c> (upper-case hex,
/// dash-separated - matches how existing SC4 LTEXT .po files in the wild, e.g. a bridge
/// pack's own bridges.pot/bridges.po, already key their strings). This is always the
/// *source* entry's TGI (the one the text was exported from), never a language-offset one -
/// see <see cref="LtextTgiLanguage"/> for how a translation's real Group ID is derived from
/// it on import.
/// </param>
/// <param name="Msgid">Source-language text (what Poedit shows as the original string).</param>
/// <param name="Msgstr">Translated text (empty for an untranslated .pot template entry).</param>
public sealed record PoEntry(string Msgctxt, string Msgid, string Msgstr);

/// <summary>
/// Reads and writes the small subset of the gettext .po/.pot format this app needs: a
/// header block (<c>msgid ""</c> / <c>msgstr "..."</c> metadata) followed by
/// <c>msgctxt</c>/<c>msgid</c>/<c>msgstr</c> triples, one blank line apart. Good enough to
/// round-trip real Poedit-authored files (quoted-string escaping, adjacent string-literal
/// concatenation) without pulling in a full gettext library dependency for what is, in the
/// end, a handful of straightforward text fields per entry.
/// </summary>
public static class PoFile
{
    /// <summary>
    /// Parses a .po/.pot file's translatable entries. The leading header entry (empty
    /// <c>msgid</c>) is skipped - it carries file metadata (language, charset, generator),
    /// not an LTEXT string.
    /// </summary>
    public static List<PoEntry> Parse(string path)
    {
        var entries = new List<PoEntry>();
        var lines = File.ReadAllLines(path);

        string? msgctxt = null;
        string? msgid = null;
        string? msgstr = null;

        // Which of the three fields the next bare quoted-string continuation line (Poedit
        // wraps long strings across several "..." lines with no keyword) belongs to.
        var field = 0; // 0 = none, 1 = msgctxt, 2 = msgid, 3 = msgstr

        void FlushEntry()
        {
            if (msgid is not null && msgctxt is not null)
            {
                entries.Add(new PoEntry(msgctxt, msgid, msgstr ?? string.Empty));
            }

            msgctxt = null;
            msgid = null;
            msgstr = null;
            field = 0;
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                FlushEntry();
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("msgctxt ", StringComparison.Ordinal))
            {
                msgctxt = (msgctxt ?? string.Empty) + UnescapeQuoted(line["msgctxt ".Length..]);
                field = 1;
            }
            else if (line.StartsWith("msgid ", StringComparison.Ordinal))
            {
                msgid = (msgid ?? string.Empty) + UnescapeQuoted(line["msgid ".Length..]);
                field = 2;
            }
            else if (line.StartsWith("msgstr ", StringComparison.Ordinal))
            {
                msgstr = (msgstr ?? string.Empty) + UnescapeQuoted(line["msgstr ".Length..]);
                field = 3;
            }
            else if (line.StartsWith('"') && line.EndsWith('"'))
            {
                // A continuation line for whichever field we were last reading.
                var chunk = UnescapeQuoted(line);
                switch (field)
                {
                    case 1: msgctxt += chunk; break;
                    case 2: msgid += chunk; break;
                    case 3: msgstr += chunk; break;
                }
            }
            // Anything else (msgid_plural, msgstr[0], stray comments, ...) is outside the
            // small subset this app writes/needs, and is silently ignored rather than
            // rejecting the whole file - Poedit tolerates the same kind of forward
            // compatibility from other tools.
        }

        FlushEntry();

        // The very first entry is the file's own header (msgid ""/msgstr "meta...") -
        // strip it out; it has no msgctxt to key an LTEXT entry with, but a defensive
        // check on the msgid being empty catches it even if a msgctxt somehow slipped in.
        entries.RemoveAll(e => e.Msgid.Length == 0);

        return entries;
    }

    /// <summary>
    /// Writes a .po/.pot file: the standard header block (declaring <paramref name="languageCode"/>),
    /// then one msgctxt/msgid/msgstr block per <paramref name="entries"/>, blank-line separated -
    /// the same shape Poedit itself produces, so round-tripping through Poedit and back works.
    /// </summary>
    public static void Write(string path, IEnumerable<PoEntry> entries, string languageCode)
    {
        var sb = new StringBuilder();
        sb.Append("msgid \"\"\n");
        sb.Append("msgstr \"\"\n");
        sb.Append("\"Project-Id-Version: \\n\"\n");
        sb.Append("\"POT-Creation-Date: \\n\"\n");
        sb.Append("\"PO-Revision-Date: \\n\"\n");
        sb.Append("\"Last-Translator: \\n\"\n");
        sb.Append("\"Language-Team: \\n\"\n");
        sb.Append($"\"Language: {languageCode}\\n\"\n");
        sb.Append("\"MIME-Version: 1.0\\n\"\n");
        sb.Append("\"Content-Type: text/plain; charset=UTF-8\\n\"\n");
        sb.Append("\"Content-Transfer-Encoding: 8bit\\n\"\n");
        sb.Append('\n');

        foreach (var entry in entries)
        {
            sb.Append("msgctxt \"").Append(Escape(entry.Msgctxt)).Append("\"\n");
            sb.Append("msgid \"").Append(Escape(entry.Msgid)).Append("\"\n");
            sb.Append("msgstr \"").Append(Escape(entry.Msgstr)).Append("\"\n");
            sb.Append('\n');
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Builds the <c>"TTTTTTTT-GGGGGGGG-IIIIIIII"</c> msgctxt key for a TGI (see <see cref="PoEntry.Msgctxt"/>).</summary>
    public static string FormatMsgctxt(uint typeId, uint groupId, uint instanceId) =>
        $"{typeId:X8}-{groupId:X8}-{instanceId:X8}";

    /// <summary>Parses a <see cref="FormatMsgctxt"/> key back into its Type/Group/Instance triplet. Returns false if it isn't in that shape.</summary>
    public static bool TryParseMsgctxt(string msgctxt, out uint typeId, out uint groupId, out uint instanceId)
    {
        typeId = groupId = instanceId = 0;
        var parts = msgctxt.Split('-');
        if (parts.Length != 3)
        {
            return false;
        }

        return uint.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out typeId)
            && uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out groupId)
            && uint.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out instanceId);
    }

    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': break; // normalized away - written lines already use \n
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Un-escapes and strips the surrounding quotes from one <c>"..."</c> literal.</summary>
    private static string UnescapeQuoted(string quoted)
    {
        quoted = quoted.Trim();
        if (quoted.Length < 2 || quoted[0] != '"' || quoted[^1] != '"')
        {
            return string.Empty;
        }

        var inner = quoted[1..^1];
        var sb = new StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length)
            {
                i++;
                sb.Append(inner[i] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '"' => '"',
                    '\\' => '\\',
                    _ => inner[i],
                });
            }
            else
            {
                sb.Append(inner[i]);
            }
        }

        return sb.ToString();
    }
}
