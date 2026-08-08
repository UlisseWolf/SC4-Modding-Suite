using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Writes a DBPF package to disk from scratch, working directly off each entry's raw
/// <c>ByteData</c> (whatever QFS compression state it is currently in) instead of relying
/// on csDBPF's own <c>DBPFFile.Save()</c>/<c>SaveAs()</c>, which was found to produce
/// corrupted files once entries had been swapped out via reflection (see
/// <see cref="DbpfService.ChangeEntryTgi"/> - TGI is read-only on <c>DBPFEntry</c>, so
/// editing it means replacing the entry object; csDBPF's internal index bookkeeping does
/// not appear to cope with that correctly).
///
/// <para>
/// The on-disk layout and every field offset implemented here is a direct C# port of the
/// proven save routine from <b>Ilive Reader</b>'s <c>CSave::Save()</c>
/// (<c>or_dat/sim015.cpp</c>), cross-referenced against its <c>HeaderRecord</c> comment
/// and <c>ReIndex()</c>/<c>RebuildDirectory()</c>/<c>GenerateDirectory()</c> functions:
/// </para>
///
/// <code>
/// [Header: 96 bytes]
/// [Entry 1 raw bytes][Entry 2 raw bytes] ... [Entry N raw bytes]
/// [Directory subfile: 16 bytes per compressed entry - only written if at least one
///  entry is compressed]
/// [Index table: 20 bytes per entry (incl. Directory): TypeID, GroupID, InstanceID,
///  Offset, Size]
/// </code>
///
/// <para>
/// The Directory subfile (TGI 0xE86B1EEF, 0xE86B1EEF, 0x286B1F03, matching csDBPF's
/// <c>DBPFTGI.DIRECTORY</c> and Ilive Reader's <c>ENT_DIR</c>) lists the *uncompressed*
/// size of every currently-compressed entry, and is always rebuilt fresh on save - any
/// pre-existing Directory entry loaded from the source file is discarded first, exactly
/// like Ilive Reader's <c>RebuildDirectory()</c>/<c>GenerateDirectory()</c>. This is also
/// what makes QFS (de)compression "automatic": whatever raw bytes and
/// <see cref="DBPFEntry.IsCompressed"/> state each entry currently has (either untouched
/// from when the file was opened, or explicitly changed via
/// <see cref="DbpfService.SetEntryCompression"/>) is written out correctly without any
/// further action needed from the caller.
/// </para>
/// </summary>
public static class DbpfWriter
{
    private const int HeaderSize = 96;
    private const int IndexEntrySize = 20;
    private const int DirectoryRecordSize = 16;

    private const uint DirTypeId = 0xE86B1EEF;
    private const uint DirGroupId = 0xE86B1EEF;
    private const uint DirInstanceId = 0x286B1F03;

    /// <summary>
    /// Writes <paramref name="sourceEntries"/> to <paramref name="path"/> as a complete,
    /// self-contained DBPF package. Writes to a temporary file first and only replaces
    /// the destination once writing succeeds, so a failed/interrupted save never leaves
    /// behind a half-written, corrupted file at <paramref name="path"/> - mirroring the
    /// ".bak then swap" safety net in Ilive Reader's own <c>CSave::Save()</c>.
    /// </summary>
    public static void WritePackage(IEnumerable<DBPFEntry> sourceEntries, string path)
    {
        // Any pre-existing Directory entry is always discarded and rebuilt fresh below,
        // exactly like Ilive Reader does on every save (RebuildDirectory/GenerateDirectory).
        var entries = sourceEntries.Where(e => !IsDirectoryEntry(e)).ToList();

        // Make sure every entry actually has its raw bytes ready to write.
        foreach (var entry in entries)
        {
            if (entry.ByteData is null || entry.ByteData.Length == 0)
            {
                entry.Decode();
                entry.Encode(entry.IsCompressed);
            }
        }

        var directoryBytes = BuildDirectoryBytes(entries);

        // Entry offsets are simply cumulative sizes starting right after the header -
        // same as Ilive Reader's ReIndex().
        var offsets = new uint[entries.Count];
        uint runningOffset = HeaderSize;
        for (int i = 0; i < entries.Count; i++)
        {
            offsets[i] = runningOffset;
            runningOffset += (uint)(entries[i].ByteData?.Length ?? 0);
        }

        uint directoryOffset = 0;
        if (directoryBytes is not null)
        {
            directoryOffset = runningOffset;
            runningOffset += (uint)directoryBytes.Length;
        }

        uint indexPosition = runningOffset;
        int indexEntryCount = entries.Count + (directoryBytes is not null ? 1 : 0);
        uint indexLength = (uint)(indexEntryCount * IndexEntrySize);

        var tempPath = path + ".tmp";

        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
        {
            WriteHeader(stream, indexEntryCount, indexPosition, indexLength);

            foreach (var entry in entries)
            {
                var bytes = entry.ByteData ?? Array.Empty<byte>();
                stream.Write(bytes, 0, bytes.Length);
            }

            if (directoryBytes is not null)
            {
                stream.Write(directoryBytes, 0, directoryBytes.Length);
            }

            Span<byte> indexRecord = stackalloc byte[IndexEntrySize];

            for (int i = 0; i < entries.Count; i++)
            {
                var size = (uint)(entries[i].ByteData?.Length ?? 0);
                WriteIndexRecord(stream, indexRecord, entries[i].TGI, offsets[i], size);
            }

            if (directoryBytes is not null)
            {
                var dirTgi = new TGI(DirTypeId, DirGroupId, DirInstanceId);
                WriteIndexRecord(stream, indexRecord, dirTgi, directoryOffset, (uint)directoryBytes.Length);
            }
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }

    private static bool IsDirectoryEntry(DBPFEntry entry)
    {
        var tgi = entry.TGI;
        return tgi.TypeID == DirTypeId && tgi.GroupID == DirGroupId && tgi.InstanceID == DirInstanceId;
    }

    /// <summary>
    /// Builds the raw bytes of the Directory subfile (16 bytes per compressed entry:
    /// TGI + uncompressed size), or <see langword="null"/> if no entry is compressed
    /// (in which case no Directory subfile is written at all - same as Ilive Reader's
    /// <c>GenerateDirectory()</c>, which bails out early when <c>iBlock == 0</c>).
    /// </summary>
    private static byte[]? BuildDirectoryBytes(List<DBPFEntry> entries)
    {
        var compressed = entries.Where(e => e.IsCompressed).ToList();
        if (compressed.Count == 0)
        {
            return null;
        }

        var buffer = new byte[compressed.Count * DirectoryRecordSize];
        for (int i = 0; i < compressed.Count; i++)
        {
            var entry = compressed[i];
            var tgi = entry.TGI;
            var bytes = entry.ByteData ?? Array.Empty<byte>();
            uint uncompressedSize = ToUInt32(QFS.GetDecompressedSize(bytes));

            var span = buffer.AsSpan(i * DirectoryRecordSize, DirectoryRecordSize);
            BinaryPrimitives.WriteUInt32LittleEndian(span[..4], tgi.TypeID);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), tgi.GroupID);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), tgi.InstanceID);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), uncompressedSize);
        }

        return buffer;
    }

    /// <summary>
    /// Writes the 96-byte DBPF header. Field offsets match Ilive Reader's
    /// <c>HeaderRecord</c>/<c>CSave::Save()</c> exactly (0x1C DateModified, 0x24 index
    /// entry count, 0x28 index position, 0x2C index length, 0x30/0x34/0x38 hole record -
    /// always zeroed here since a freshly written package never has holes).
    /// </summary>
    private static void WriteHeader(FileStream stream, int indexEntryCount, uint indexPosition, uint indexLength)
    {
        Span<byte> header = stackalloc byte[HeaderSize];
        header.Clear();

        "DBPF"u8.CopyTo(header[..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(4, 4), 1);  // MajorVersion
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(8, 4), 0); // MinorVersion
        // 12..24: user major/minor version + option flags - unused, left zeroed.

        var now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(24, 4), now); // DateCreated
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(28, 4), now); // DateModified

        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(32, 4), 7); // IndexMajorVersion
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(36, 4), (uint)indexEntryCount);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(40, 4), indexPosition);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(44, 4), indexLength);

        // Hole record: always empty for a freshly written package.
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(48, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(52, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(56, 4), 0);
        // 60..96: reserved, left zeroed.

        stream.Write(header);
    }

    private static void WriteIndexRecord(FileStream stream, Span<byte> scratch, TGI tgi, uint offset, uint size)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(0, 4), tgi.TypeID);
        BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(4, 4), tgi.GroupID);
        BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(8, 4), tgi.InstanceID);
        BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(12, 4), offset);
        BinaryPrimitives.WriteUInt32LittleEndian(scratch.Slice(16, 4), size);
        stream.Write(scratch);
    }

    /// <summary>
    /// Converts a boxed numeric csDBPF return value (which may be uint, int, long, etc.)
    /// to a uint, mirroring the same helper in <see cref="DbpfService"/>.
    /// </summary>
    internal static uint ToUInt32(object? value) => value is null ? 0u : Convert.ToUInt32(value);
}
