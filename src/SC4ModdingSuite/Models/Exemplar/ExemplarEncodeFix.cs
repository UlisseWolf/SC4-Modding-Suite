using System.Reflection;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// <b>Superseded by <see cref="ExemplarEncodeWorkaround"/></b> for every call site this app
/// actually has (all replaced once a second, more serious csDBPF Encode() bug was found -
/// see that class's own doc comment: an unconditional, never-null-checked QFS.Compress()
/// call that crashes outright on any small/near-empty Exemplar). Kept here, unused, purely
/// as a record of the first bug found and how it was diagnosed - the fix below (forcing
/// <c>_isDecoded</c> true via reflection) is still correct as far as it goes, just no
/// longer sufficient on its own once <c>ExemplarEncodeWorkaround</c> replaces the
/// <c>Encode()</c> call entirely rather than merely unblocking the real one.
///
/// Works around a defect in csDBPF's compiled <c>DBPFEntryEXMP</c> (Exemplar/Cohort):
/// <c>Decode()</c> checks its private <c>_isDecoded</c> field to avoid re-decoding, but
/// never actually sets it to <see langword="true"/> anywhere in the method - so it stays
/// <see langword="false"/> forever, for every Exemplar/Cohort, no matter how many times
/// <c>Decode()</c> is called. <c>Encode(bool compress)</c> in turn starts with
/// <c>if (!_isDecoded) return;</c>, so it silently does nothing: <c>ListOfProperties</c>
/// can be freely mutated (added/updated/removed) and it looks like it worked, but
/// <c>ByteData</c> - the bytes actually written to disk - is never rebuilt from that
/// mutated state. Every property edit in this app (Add/Edit/Remove Property, the T21
/// Editor's SAVE, RHD → LHD mirroring, and the Compress/Decompress toggle) ends up a no-op
/// on Exemplar/Cohort entries as a result: the edit shows correctly in the UI (which reads
/// the in-memory <c>ListOfProperties</c> directly) but is silently dropped the moment the
/// package is actually saved.
///
/// <para>
/// Confirmed isolated to <c>DBPFEntryEXMP</c> specifically - <c>DBPFEntryDIR</c> and
/// <c>DBPFEntryLTEXT</c> both correctly set their own <c>_isDecoded</c> at the end of
/// <c>Decode()</c>.
/// </para>
///
/// <para>
/// Originally written against csDBPF as a compiled DLL with no available source to patch
/// directly (see <see cref="DbpfService.FindReadingConstructor"/> for the same "reflect
/// into what the DLL doesn't expose" situation elsewhere in this app) - the real source
/// was later obtained (see <c>ExemplarEncodeWorkaround</c>'s own doc comment), confirming
/// this diagnosis exactly and revealing the second, more serious bug that superseded this
/// file's own fix.
/// </para>
/// </summary>
public static class ExemplarEncodeFix
{
    private static readonly FieldInfo? IsDecodedField =
        typeof(DBPFEntryEXMP).GetField("_isDecoded", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    /// Forces <paramref name="exemplar"/>'s private <c>_isDecoded</c> flag true, so a
    /// following <c>Encode(...)</c> call actually rebuilds <c>ByteData</c> instead of
    /// silently no-op'ing. Call this right before every <see cref="DBPFEntryEXMP.Encode"/>
    /// call in this app - it is cheap and safe to call unconditionally (a no-op via
    /// reflection failure, e.g. if some future csDBPF version renames the field, just means
    /// the original buggy no-op behavior returns rather than throwing).
    /// </summary>
    public static void EnsureEncodable(DBPFEntryEXMP exemplar) => IsDecodedField?.SetValue(exemplar, true);
}
