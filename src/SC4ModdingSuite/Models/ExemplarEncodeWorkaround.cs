using System;
using System.Collections.Generic;
using System.Reflection;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Replaces <c>DBPFEntryEXMP.Encode(bool compress)</c>, which has two bugs in this csDBPF
/// build:
///
/// <list type="number">
/// <item><description>It unconditionally calls <c>QFS.Compress(ByteData)</c> regardless of
/// the <c>compress</c> argument, and never null-checks the result.
/// <c>QfsCompression.Compress</c> returns <see langword="null"/> for any payload under 50
/// bytes (<c>UncompressedDataMinSize</c>), so <c>Encode()</c> throws a bare
/// <see cref="NullReferenceException"/> for any small/near-empty Exemplar.</description></item>
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
/// ParentCohort/property-count/property-bytes framing) for the
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

        var bytes = new List<byte>();
        bytes.AddRange(id.ToBytes(true));
        bytes.AddRange(BitConverter.GetBytes(exemplar.ParentCohort.TypeID));
        bytes.AddRange(BitConverter.GetBytes(exemplar.ParentCohort.GroupID));
        bytes.AddRange(BitConverter.GetBytes(exemplar.ParentCohort.InstanceID));
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
