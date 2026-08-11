using csDBPF;

namespace SC4ModdingSuite.ViewModels;

public enum DatCompareResult
{
    Same,
    SizeDiffers,
    ContentDiffers,
    OnlyInFirst,
    OnlyInSecond,
}

/// <summary>
/// One row of the Compare grid (Ilive Reader's DlgCompare, <c>_dlg_compare</c>/<c>m_Grid</c>):
/// an entry from file 1, its matching entry (by TGI) in file 2 if any, and the comparison
/// result between them.
/// </summary>
public sealed class DatCompareRowViewModel
{
    public DBPFEntry? EntryA { get; init; }
    public DBPFEntry? EntryB { get; init; }
    public DatCompareResult Result { get; init; }

    public string LeftTgi => EntryA is null ? string.Empty : FormatTgi(EntryA.TGI);
    public string LeftType => EntryA is null ? string.Empty : EntryA.TGI.GetEntryType();
    public string LeftSize => EntryA is null ? string.Empty : $"{EntryA.GetSize():N0} B";

    public string RightTgi => EntryB is null ? string.Empty : FormatTgi(EntryB.TGI);
    public string RightType => EntryB is null ? string.Empty : EntryB.TGI.GetEntryType();
    public string RightSize => EntryB is null ? string.Empty : $"{EntryB.GetSize():N0} B";

    public string ResultLabel => Result switch
    {
        DatCompareResult.Same => "== identical",
        DatCompareResult.SizeDiffers => "size !=",
        DatCompareResult.ContentDiffers => "bytes !=",
        DatCompareResult.OnlyInFirst => "only in file 1",
        DatCompareResult.OnlyInSecond => "only in file 2",
        _ => string.Empty,
    };

    /// <summary>Can this row's two sides be opened in the byte-level hex diff viewer (double-click/VIEW DIFF)?</summary>
    public bool CanViewDiff => EntryA is not null && EntryB is not null;

    private static string FormatTgi(TGI tgi) => $"{tgi.TypeID:X8}-{tgi.GroupID:X8}-{tgi.InstanceID:X8}";
}
