using System.Reflection;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Workarounds for two more confirmed bugs in csDBPF's compiled <c>DBPFFile</c> (filed
/// upstream, no fix available yet - csDBPF ships as a compiled DLL with no source to
/// patch directly, same situation as <see cref="ExemplarEncodeFix"/>):
///
/// <list type="bullet">
/// <item><description><b>RemoveEntry(int position)</b> reads
/// <c>ListOfEntries[position]</c> to compute how much to subtract from
/// <c>DataSize</c> *after* already calling <c>ListOfEntries.RemoveAt(position)</c> -
/// so it either subtracts a completely different entry's size (every other index just
/// shifted down by one), or throws <c>ArgumentOutOfRangeException</c> outright if
/// <c>position</c> was the last index.</description></item>
/// <item><description><b>UpdateEntry(DBPFEntry entry)</b> - the method
/// <c>AddOrUpdateEntry</c> calls whenever a matching TGI is already present - is an
/// empty, unimplemented stub. Every "add or update" call for a TGI that already exists
/// in the file silently does nothing: the old entry stays, the new one is discarded,
/// with no error or exception to signal it.</description></item>
/// </list>
///
/// Both replacements below work directly against <see cref="DBPFFile.ListOfEntries"/>
/// and <see cref="DBPFFile.ListOfTGIs"/> - both public, directly mutable
/// <c>List&lt;T&gt;</c> instances (only their own container property has a private
/// setter, not the lists themselves), so no reflection is needed for the actual
/// add/remove. Reflection is used only to keep <see cref="DBPFFile.DataSize"/> (a
/// private-set property) accurate afterwards - which has no effect on what
/// <see cref="DbpfWriter"/> actually writes to disk (it recomputes every offset from
/// scratch from <c>ListOfEntries</c> itself, never from <c>DataSize</c>), but keeping it
/// correct avoids surprising any other code that might read it later.
/// </summary>
public static class DbpfFileFixes
{
    private static readonly PropertyInfo? DataSizeProperty =
        typeof(DBPFFile).GetProperty(nameof(DBPFFile.DataSize));

    /// <summary>
    /// Safe replacement for <c>DBPFFile.RemoveEntry(TGI)</c>/<c>RemoveEntry(int)</c>.
    /// Finds the entry first, captures its size, then removes it from both
    /// <c>ListOfEntries</c> and <c>ListOfTGIs</c> at the correct index - unlike csDBPF's
    /// own version, which reads the removed entry's size only after it's already gone.
    /// </summary>
    /// <returns><see langword="true"/> if a matching entry was found and removed; <see langword="false"/> if no entry had that TGI.</returns>
    public static bool RemoveEntry(DBPFFile file, TGI tgi)
    {
        var index = file.ListOfEntries.FindIndex(e => e.TGI.Matches(tgi));
        if (index < 0)
        {
            return false;
        }

        var removedSize = file.ListOfEntries[index].ByteData?.LongLength ?? 0L;

        file.ListOfEntries.RemoveAt(index);
        file.ListOfTGIs.RemoveAt(index);

        AdjustDataSize(file, -removedSize);
        return true;
    }

    /// <summary>
    /// Safe replacement for <c>DBPFFile.AddOrUpdateEntry(entry)</c>. Behaves exactly like
    /// the original when no entry with <paramref name="entry"/>'s TGI exists yet (falls
    /// through to the file's own, non-buggy <c>AddEntry</c>) - but when a matching TGI
    /// *is* already present, this actually replaces it in place instead of silently
    /// discarding <paramref name="entry"/> the way csDBPF's own <c>UpdateEntry</c> stub does.
    /// </summary>
    public static void AddOrUpdateEntry(DBPFFile file, DBPFEntry entry)
    {
        var index = file.ListOfEntries.FindIndex(e => e.TGI.Matches(entry.TGI));
        if (index < 0)
        {
            file.AddEntry(entry);
            return;
        }

        var oldSize = file.ListOfEntries[index].ByteData?.LongLength ?? 0L;
        var newSize = entry.ByteData?.LongLength ?? 0L;

        file.ListOfEntries[index] = entry;
        file.ListOfTGIs[index] = entry.TGI;

        AdjustDataSize(file, newSize - oldSize);
    }

    private static void AdjustDataSize(DBPFFile file, long delta)
    {
        if (DataSizeProperty is null)
        {
            return;
        }

        var current = (long)(DataSizeProperty.GetValue(file) ?? 0L);
        DataSizeProperty.SetValue(file, current + delta);
    }
}
