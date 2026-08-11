using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Exports entries as raw files on disk, mirroring Ilive Reader's own "Export" feature
/// (<c>CChildFrame::OnFileExport</c> in <c>reader/ChildFrm.cpp</c>): each file is named
/// <c>TTTTTTTT-GGGGGGGG-IIIIIIII.ext</c> (hex TGI joined by dashes, matching Ilive Reader's
/// own <c>"%08X-%08X-%08X"</c> format string) with a companion <c>.TGI</c> text file
/// listing the three hex IDs on separate lines - the same convention Ilive Reader uses,
/// so an export from either tool is recognizable/re-importable by the other.
/// </summary>
public static class EntryExporter
{
    /// <summary>Base filename (without extension) for an entry, matching Ilive Reader's own format string.</summary>
    public static string BaseFileName(DBPFEntry entry)
    {
        var tgi = entry.TGI;
        return $"{tgi.TypeID:X8}-{tgi.GroupID:X8}-{tgi.InstanceID:X8}";
    }

    /// <summary>
    /// Extension guessed from the entry's runtime type/TGI, mirroring Ilive Reader's
    /// <c>GetExt()</c> (<c>or_dat/sim015.cpp</c>) for the handful of formats this app
    /// specifically knows how to preview (PNG, FSH, S3D, WAV, Exemplar/Cohort, LTEXT, UI);
    /// everything else falls back to <c>.bin</c>, matching Ilive Reader's own default.
    ///
    /// TGI Type ID 0x2026960B is shared between WAV, LTEXT, and XA audio (see
    /// <see cref="EntryTypeClassifier"/>), so - matching Ilive Reader's own
    /// <c>_entry::SetFlag</c> - the ".wav" guess additionally checks that the bytes
    /// actually start with the RIFF magic, instead of trusting the Type ID alone; anything
    /// else under that Type ID exports as ".ltext" (it is LTEXT in the overwhelming
    /// majority of real packages - see <see cref="EntryTypeClassifier.TryDecodeAsLtext"/>).
    /// Type ID 0x00000000 is the legitimate "UI" format, not a "blank/unknown" marker, so
    /// it is not special-cased here - <see cref="DBPFEntryUI"/> covers it below.
    /// </summary>
    public static string ExtensionFor(DBPFEntry entry) => entry switch
    {
        DBPFEntryPNG => ".png",
        DBPFEntryFSH => ".fsh",
        DBPFEntryLTEXT => ".txt",
        DBPFEntryUI => ".ui.txt",
        DBPFEntryEXMP exmp => exmp.IsCohort ? ".cohort" : ".exmp",
        _ when entry.TGI.TypeID == 0x5AD0E817 => ".s3d",
        _ when EntryTypeClassifier.IsLtextWavXaType(entry.TGI) => IsRiffWav(entry) ? ".wav" : ".ltext",
        _ => ".bin",
    };

    private static bool IsRiffWav(DBPFEntry entry)
    {
        try
        {
            return EntryTypeClassifier.LooksLikeRiffWav(RawEntryBytes.GetDecompressed(entry));
        }
        catch
        {
            return false;
        }
    }

    public static string FileNameFor(DBPFEntry entry) => BaseFileName(entry) + ExtensionFor(entry);

    /// <summary>
    /// Writes one entry's raw (decompressed) bytes to <paramref name="filePath"/>. For
    /// Exemplar/Cohort entries, also runs <see cref="ExemplarBinaryValidator"/> against the
    /// bytes actually being written - the file is still written either way (so a suspected
    /// problem never costs the person their data), but a non-null return value means the
    /// bytes didn't decode cleanly as a well-formed binary Exemplar and the caller should
    /// surface that as a warning instead of assuming the export is trustworthy.
    ///
    /// <para>
    /// LTEXT is special-cased to write its <b>decoded</b> plain text (UTF-8) instead of the
    /// raw on-disk bytes: unlike every other format here, an LTEXT entry's raw layout is a
    /// 2-byte character count plus a 2-byte 0x1000 control marker in front of the actual
    /// UTF-16LE string - not plain text - so writing those raw bytes to a file that
    /// <see cref="ExtensionFor"/> already names ".txt" produced a handful of garbage bytes
    /// up front and, without a UTF-16 byte-order-mark, an inconsistent encoding guess by
    /// whatever text editor opens it. This is the one raw-bytes-vs-decoded exception -
    /// every other exported format (PNG, FSH, S3D, WAV, UI, and binary Exemplar/Cohort) is
    /// either already meant to be opened by a dedicated tool, or - UI's case - is stored as
    /// plain UTF-8 text on disk already, so the raw bytes already are the readable form.
    /// For a readable dump of formats that are still genuinely binary on disk (Exemplar/
    /// Cohort above all), see <see cref="MainWindowViewModel.ExportSelectedEntriesReadable"/>
    /// instead, which decodes the whole property list by name/value rather than just the
    /// payload bytes.
    /// </para>
    /// </summary>
    /// <returns>A human-readable warning if validation found a problem; null if the export looks clean (or isn't an Exemplar/Cohort at all).</returns>
    public static string? ExportEntryTo(DBPFEntry entry, string filePath)
    {
        if (entry is DBPFEntryLTEXT ltext)
        {
            entry.Decode();
            File.WriteAllText(filePath, ltext.Text ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return null;
        }

        var bytes = RawEntryBytes.GetDecompressed(entry) ?? Array.Empty<byte>();
        File.WriteAllBytes(filePath, bytes);

        if (entry is not DBPFEntryEXMP)
        {
            return null;
        }

        var result = ExemplarBinaryValidator.Validate(bytes);
        return result.IsValid
            ? null
            : $"exported Exemplar data may be malformed ({result.Error})";
    }

    /// <summary>
    /// Writes one entry's raw bytes plus its <c>.TGI</c> sidecar file into
    /// <paramref name="folder"/>, using the standard <c>TTTTTTTT-GGGGGGGG-IIIIIIII</c> name.
    /// </summary>
    /// <returns>The written file's path, and a validation warning if one applies (see <see cref="ExportEntryTo"/>).</returns>
    public static (string FilePath, string? Warning) ExportEntryWithSidecar(DBPFEntry entry, string folder)
    {
        var baseName = BaseFileName(entry);
        var filePath = Path.Combine(folder, baseName + ExtensionFor(entry));
        var warning = ExportEntryTo(entry, filePath);

        var tgi = entry.TGI;
        var tgiPath = Path.Combine(folder, baseName + ".TGI");
        File.WriteAllText(tgiPath, $"{tgi.TypeID:X8}\r\n{tgi.GroupID:X8}\r\n{tgi.InstanceID:X8}\r\n");

        return (filePath, warning);
    }

    /// <summary>
    /// Exports every entry in <paramref name="entries"/> into <paramref name="folder"/>,
    /// mirroring Ilive Reader's bulk "Export" ribbon button. Continues past individual
    /// failures (e.g. an entry that fails to decompress) instead of aborting the whole
    /// batch, returning counts of successes/failures/warnings for the caller to report.
    /// </summary>
    public static (int Succeeded, int Failed, int Warnings) ExportAll(IEnumerable<DBPFEntry> entries, string folder)
    {
        var succeeded = 0;
        var failed = 0;
        var warnings = 0;

        foreach (var entry in entries)
        {
            try
            {
                var (_, warning) = ExportEntryWithSidecar(entry, folder);
                succeeded++;
                if (warning is not null)
                {
                    warnings++;
                }
            }
            catch
            {
                failed++;
            }
        }

        return (succeeded, failed, warnings);
    }
}
