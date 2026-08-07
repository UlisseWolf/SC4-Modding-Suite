using System;
using System.Linq;
using System.Text;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Produces a readable summary of a DBPFEntry's payload for the details panel.
/// Attempts to decode the entry first; falls back to raw size info if decoding
/// isn't supported/implemented for that entry type or the data is malformed.
/// </summary>
public static class EntryDescriber
{
    public static string Describe(DBPFEntry entry, PropertyDefinitionsRegistry? propertyRegistry)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"TGI: {entry.TGI.ToString()}");
        sb.AppendLine($"Entry type: {entry.TGI.GetEntryType()}");
        sb.AppendLine($"Entry detail: {entry.TGI.GetEntryDetail()}");
        sb.AppendLine($"Compressed: {entry.IsCompressed}");
        sb.AppendLine($"Size: {entry.GetSize():N0} bytes");
        sb.AppendLine();

        try
        {
            entry.Decode();
        }
        catch (Exception ex)
        {
            sb.AppendLine("(Could not decode entry payload for preview)");
            sb.AppendLine(ex.Message);
            return sb.ToString();
        }

        switch (entry)
        {
            case DBPFEntryEXMP exmp:
                DescribeExemplar(sb, exmp, propertyRegistry);
                break;

            case DBPFEntryLTEXT ltext:
                sb.AppendLine("--- LTEXT ---");
                sb.AppendLine(ltext.Text);
                break;

            case DBPFEntryUI ui:
                sb.AppendLine("--- UI ---");
                sb.AppendLine(ui.Definition);
                break;

            case DBPFEntryPNG png when png.PNGImage is not null:
                sb.AppendLine("--- PNG ---");
                sb.AppendLine($"{png.PNGImage.Width} x {png.PNGImage.Height} px");
                break;

            case DBPFEntryFSH fsh:
                DescribeFsh(sb, fsh);
                break;

            default:
                DescribeUnstructured(sb, entry);
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Handles entries csDBPF didn't map to one of its own structured types. S3D and WAV
    /// have no csDBPF decoder at all (identified purely by Type ID here). The Type ID
    /// 0x2026960B family (WAV/LTEXT/XA - see <see cref="EntryTypeClassifier"/>) needs
    /// extra care: if csDBPF didn't already recognize an entry under this Type ID as
    /// <see cref="DBPFEntryLTEXT"/> above, it's a "special"/non-standard-group LTEXT
    /// entry (the common case) or WAV audio, told apart here by sniffing the actual
    /// bytes - never shown as "Unknown" just because of this Type ID ambiguity.
    /// </summary>
    private static void DescribeUnstructured(StringBuilder sb, DBPFEntry entry)
    {
        if (entry.TGI.TypeID == 0x5AD0E817)
        {
            sb.AppendLine("--- S3D (3D model) ---");
            sb.AppendLine("See the 3D viewer in the preview panel.");
            return;
        }

        if (EntryTypeClassifier.IsLtextWavXaType(entry.TGI))
        {
            var bytes = RawEntryBytes.GetDecompressed(entry);

            if (EntryTypeClassifier.LooksLikeRiffWav(bytes))
            {
                sb.AppendLine("--- WAV (audio) ---");
                sb.AppendLine("Use the playback controls in the preview panel.");
                return;
            }

            var text = EntryTypeClassifier.TryDecodeAsLtext(bytes);
            if (text is not null)
            {
                sb.AppendLine("--- LTEXT (non-standard variant) ---");
                sb.AppendLine(text);
                return;
            }
        }

        sb.AppendLine("(No structured preview available for this entry type)");
    }

    private static void DescribeFsh(StringBuilder sb, DBPFEntryFSH fsh)
    {
        sb.AppendLine("--- FSH ---");

        if (fsh.Entries is null)
        {
            sb.AppendLine("(no sub-image decoded)");
            return;
        }

        var count = 0;
        foreach (var fshEntry in fsh.Entries)
        {
            count++;
            sb.AppendLine(
                $"[{fshEntry.Name}] {fshEntry.Width}x{fshEntry.Height}px  code=0x{(int)fshEntry.Code:X2}  mipmap={fshEntry.Mipmaps?.Count ?? 0}");
        }

        if (count == 0)
        {
            sb.AppendLine("(no sub-image found)");
        }
    }

    private static void DescribeExemplar(StringBuilder sb, DBPFEntryEXMP exmp, PropertyDefinitionsRegistry? propertyRegistry)
    {
        sb.AppendLine(exmp.IsCohort ? "--- Cohort ---" : "--- Exemplar ---");

        try
        {
            var name = exmp.GetExemplarName();
            if (!string.IsNullOrEmpty(name))
            {
                sb.AppendLine($"Name: {name}");
            }
        }
        catch
        {
            // Property may be missing; ignore.
        }

        sb.AppendLine($"Property count: {exmp.ListOfProperties.Count}");
        sb.AppendLine();

        // ListOfProperties is keyed by property ID (uint) -> DBPFProperty.
        foreach (var kvp in exmp.ListOfProperties.OrderBy(p => p.Key))
        {
            var propertyId = kvp.Key;
            var prop = kvp.Value;
            var definition = propertyRegistry?.FindById(propertyId);
            var name = definition?.Name ?? "(not present in the property database)";
            var preferHex = definition is { Options.Count: > 0 };

            string values;
            try
            {
                // GetData() is obsolete in favour of GetTypedData(), which returns the
                // property's values as an array of their exact CLR type (byte[], uint[],
                // float[], bool[], or char[] for STRING-typed properties - see csDBPF docs).
                var data = prop.GetTypedData();
                if (data is char[] chars)
                {
                    values = new string(chars);
                }
                else
                {
                    values = string.Join(
                        ", ",
                        data.Cast<object>().Select(v => PropertyValueFormatter.Format(v, prop.DataType, preferHex)));
                }
            }
            catch
            {
                values = "(unreadable)";
            }

            if (values.Length > 120)
            {
                values = values[..120] + "...";
            }

            sb.AppendLine($"0x{propertyId:X8}  {name}  [{prop.DataType}]  {values}");
        }
    }
}
