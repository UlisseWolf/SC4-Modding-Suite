using System;
using System.Collections.Generic;
using System.Reflection;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Replaces <c>DBPFEntryEXMP.Encode(bool compress)</c>, which has three bugs in this csDBPF
/// build:
///
/// <list type="number">
/// <item><description>It unconditionally calls <c>QFS.Compress(ByteData)</c> regardless of
/// the <c>compress</c> argument, and never null-checks the result.
/// <c>QfsCompression.Compress</c> returns <see langword="null"/> for any payload under 50
/// bytes (<c>UncompressedDataMinSize</c>), so <c>Encode()</c> throws a bare
/// <see cref="NullReferenceException"/> for any small/near-empty Exemplar.</description></item>
/// <item><description><c>DBPFEntryEXMP.ParentCohort</c> is never read from the file and
/// defaults to a random Group/Instance, which <c>Encode()</c> then writes into the header
/// as a (non-existent) parent cohort - making SC4 reject the exemplar. See
/// <see cref="ReadParentCohort"/>.</description></item>
/// <item><description><c>DBPFPropertyString.ToBytes()</c> writes a STRING property's
/// characters as UTF-16 while its length field is measured in characters, not bytes -
/// desynchronizing the property from the single-byte-ANSI, length-in-bytes layout real
/// Exemplar STRING properties use on disk. <c>DBPFPropertyString.GetData(int)</c> is
/// separately broken - it returns the literal text "System.Char[]" instead of the value.
/// See <see cref="EncodeProperty"/>, which builds the correct layout directly and reads
/// the value via <c>GetTypedData()</c> instead.</description></item>
/// </list>
///
/// <para>
/// Otherwise reproduces <c>Encode()</c>'s real logic (same id string, same
/// parent-cohort/property-count/property-bytes framing) for the
/// <see cref="DBPF.Encoding.Binary"/> case this app uses.
/// </para>
/// </summary>
public static class ExemplarEncodeWorkaround
{
    private static readonly FieldInfo? IsDecodedField =
        typeof(DBPFEntryEXMP).GetField("_isDecoded", BindingFlags.NonPublic | BindingFlags.Instance);

    // DBPFEntry.ByteData's setter is `protected`; reflection bypasses that. The setter's
    // own body derives IsCompressed/UncompressedSize/CompressedSize from the QFS magic
    // header (0x10FB) in the bytes it's given, so invoking it with the final byte array is
    // all that's needed.
    private static readonly MethodInfo? ByteDataSetter =
        typeof(DBPFEntry).GetProperty("ByteData")?.GetSetMethod(nonPublic: true);

    public static void Encode(DBPFEntryEXMP exemplar, bool compress)
    {
        if (exemplar.Encoding == DBPF.Encoding.Text)
        {
            // Text-encoded Exemplars/Cohorts are rare (this app only ever builds Binary-
            // encoded properties - see BuildProperty in DbpfTools.cs/
            // PropertyEditDialogViewModel.cs). Falls back to the original Encode() here
            // rather than reimplementing an untested path.
            IsDecodedField?.SetValue(exemplar, true);
            exemplar.Encode(compress);
            return;
        }

        // Same id string Encode() itself builds: "E"/"C" + "QZB1###".
        var id = (exemplar.IsCohort ? "C" : "E") + "QZB1###";

        // NOT exemplar.ParentCohort: csDBPF never reads the parent cohort out of the file, and
        // its TGI constructor replaces any zero Group/Instance with a fresh random value - so
        // every Exemplar/Cohort re-saved through it silently gained a bogus, random parent
        // cohort reference (e.g. 00000000/EE862B54/D7EFEE0F) where the original said
        // 00000000/00000000/00000000 (= no parent). SC4 then tries to resolve that
        // non-existent cohort, rejects the exemplar, and the lot vanishes from the game -
        // even after the edit that triggered the save is undone. The entry's own existing
        // bytes are the only reliable source for the real value.
        var parent = ReadParentCohort(exemplar);

        var bytes = new List<byte>();
        bytes.AddRange(id.ToBytes(true));
        bytes.AddRange(BitConverter.GetBytes(parent.Type));
        bytes.AddRange(BitConverter.GetBytes(parent.Group));
        bytes.AddRange(BitConverter.GetBytes(parent.Instance));
        bytes.AddRange(BitConverter.GetBytes(exemplar.ListOfProperties.Count));
        foreach (var property in exemplar.ListOfProperties.Values)
        {
            bytes.AddRange(EncodeProperty(property));
        }

        var uncompressed = bytes.ToArray();
        var compressed = compress ? QFS.Compress(uncompressed) : null;

        if (ByteDataSetter is null)
        {
            throw new InvalidOperationException("csDBPF's DBPFEntry.ByteData setter wasn't found by reflection - csDBPF version mismatch?");
        }

        ByteDataSetter.Invoke(exemplar, new object[] { compressed ?? uncompressed });

        // Encode() itself starts with "if (!_isDecoded) return;" - set unconditionally so a
        // later real Encode() call on this same object doesn't silently no-op.
        IsDecodedField?.SetValue(exemplar, true);
    }

    /// <summary>
    /// The parent cohort TGI stored at bytes 8..19 of the entry's current (decompressed)
    /// binary Exemplar/Cohort header, or all zeros ("no parent cohort", what the vast
    /// majority of building/lot exemplars use) if the entry has no existing bytes yet - e.g.
    /// a brand-new exemplar built from scratch - or they aren't a binary EQZB/CQZB header.
    /// </summary>
    private static (uint Type, uint Group, uint Instance) ReadParentCohort(DBPFEntryEXMP exemplar)
    {
        try
        {
            var raw = RawEntryBytes.GetDecompressed(exemplar);
            if (raw is { Length: >= 20 }
                && (raw[0] == (byte)'E' || raw[0] == (byte)'C')
                && raw[1] == (byte)'Q' && raw[2] == (byte)'Z' && raw[3] == (byte)'B')
            {
                return (BitConverter.ToUInt32(raw, 8), BitConverter.ToUInt32(raw, 12), BitConverter.ToUInt32(raw, 16));
            }
        }
        catch
        {
            // Unreadable/corrupt existing bytes: fall through to "no parent" rather than fail the save.
        }

        return (0u, 0u, 0u);
    }

    /// <summary>
    /// <c>property.ToBytes()</c>, except for STRING properties (see this class's own doc
    /// comment for why): builds the layout directly instead - ID, DataType, KeyType
    /// (always 0x80 for String), an unused flag byte, then the string's byte length and
    /// single-byte-ANSI bytes, matching <see cref="ExemplarBinaryParser"/>'s STRING case.
    /// </summary>
    private static byte[] EncodeProperty(DBPFProperty property)
    {
        if (property is not DBPFPropertyString stringProperty)
        {
            return property.ToBytes();
        }

        var text = new string((char[])stringProperty.GetTypedData());
        var textBytes = text.ToBytes(true);

        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(property.ID));
        bytes.AddRange(BitConverter.GetBytes((ushort)property.DataType));
        bytes.AddRange(BitConverter.GetBytes((ushort)0x80));
        bytes.Add(0);
        bytes.AddRange(BitConverter.GetBytes((uint)textBytes.Length));
        bytes.AddRange(textBytes);
        return bytes.ToArray();
    }
}
