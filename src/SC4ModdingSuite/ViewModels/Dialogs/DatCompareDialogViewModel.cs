using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Compare DATs" dialog (Ilive Reader's DlgCompare): loads two independent
/// packages (not the currently-open document(s) - a standalone pair, exactly like the
/// original), matches entries between them by TGI, and reports for each pair whether
/// they're identical, differ in size, differ in content, or only exist on one side.
/// Double-clicking a matched row (or "VIEW DIFF") opens <see cref="Views.HexCompareDialog"/>
/// for a byte-level look, porting DlgCompare::OnGridDblClick/DlgHexCmp.
/// </summary>
public sealed class DatCompareDialogViewModel : ViewModelBase
{
    private readonly List<DatCompareRowViewModel> _allRows = new();

    private string? _filePath1;
    public string? FilePath1
    {
        get => _filePath1;
        set => SetField(ref _filePath1, value);
    }

    private string? _filePath2;
    public string? FilePath2
    {
        get => _filePath2;
        set => SetField(ref _filePath2, value);
    }

    private bool _showSame = true;
    public bool ShowSame
    {
        get => _showSame;
        set { if (SetField(ref _showSame, value)) ApplyFilter(); }
    }

    private bool _showSizeDiffers = true;
    public bool ShowSizeDiffers
    {
        get => _showSizeDiffers;
        set { if (SetField(ref _showSizeDiffers, value)) ApplyFilter(); }
    }

    private bool _showContentDiffers = true;
    public bool ShowContentDiffers
    {
        get => _showContentDiffers;
        set { if (SetField(ref _showContentDiffers, value)) ApplyFilter(); }
    }

    private string _statusMessage = "Choose two .dat files and click COMPARE.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public ObservableCollection<DatCompareRowViewModel> Rows { get; } = new();

    private DatCompareRowViewModel? _selectedRow;
    public DatCompareRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetField(ref _selectedRow, value);
    }

    public RelayCommand CompareCommand { get; }

    public DatCompareDialogViewModel()
    {
        CompareCommand = new RelayCommand(_ => Compare());
    }

    private void Compare()
    {
        _allRows.Clear();
        Rows.Clear();

        if (string.IsNullOrWhiteSpace(FilePath1) && string.IsNullOrWhiteSpace(FilePath2))
        {
            StatusMessage = "Choose at least one file.";
            return;
        }

        try
        {
            var entriesA = LoadComparableEntries(FilePath1);
            var entriesB = LoadComparableEntries(FilePath2);

            var matchedB = new HashSet<DBPFEntry>();

            foreach (var entryA in entriesA)
            {
                var entryB = entriesB.FirstOrDefault(b => !matchedB.Contains(b) && SameTgi(entryA, b));
                if (entryB is null)
                {
                    _allRows.Add(new DatCompareRowViewModel { EntryA = entryA, Result = DatCompareResult.OnlyInFirst });
                    continue;
                }

                matchedB.Add(entryB);
                _allRows.Add(new DatCompareRowViewModel { EntryA = entryA, EntryB = entryB, Result = CompareBytes(entryA, entryB) });
            }

            foreach (var entryB in entriesB.Where(b => !matchedB.Contains(b)))
            {
                _allRows.Add(new DatCompareRowViewModel { EntryB = entryB, Result = DatCompareResult.OnlyInSecond });
            }

            ApplyFilter();
            StatusMessage = $"{_allRows.Count} entries compared "
                + $"({_allRows.Count(r => r.Result == DatCompareResult.Same)} identical, "
                + $"{_allRows.Count(r => r.Result != DatCompareResult.Same)} different).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error comparing files: {ex.Message}";
        }
    }

    private static List<DBPFEntry> LoadComparableEntries(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new List<DBPFEntry>();
        }

        var file = new DBPFFile(path);
        return file.ListOfEntries.Where(e => !IsDirectoryEntry(e)).ToList();
    }

    private static bool IsDirectoryEntry(DBPFEntry entry) =>
        entry.TGI.TypeID == DbpfWriter.DirectoryTgi.TypeID
        && entry.TGI.GroupID == DbpfWriter.DirectoryTgi.GroupID
        && entry.TGI.InstanceID == DbpfWriter.DirectoryTgi.InstanceID;

    private static bool SameTgi(DBPFEntry a, DBPFEntry b) =>
        a.TGI.TypeID == b.TGI.TypeID && a.TGI.GroupID == b.TGI.GroupID && a.TGI.InstanceID == b.TGI.InstanceID;

    /// <summary>Same three-way result as Ilive Reader's DlgCompare::OnSearch (identical / size differs / content differs).</summary>
    private static DatCompareResult CompareBytes(DBPFEntry a, DBPFEntry b)
    {
        var bytesA = RawEntryBytes.GetDecompressed(a) ?? Array.Empty<byte>();
        var bytesB = RawEntryBytes.GetDecompressed(b) ?? Array.Empty<byte>();

        if (bytesA.Length != bytesB.Length)
        {
            return DatCompareResult.SizeDiffers;
        }

        return bytesA.AsSpan().SequenceEqual(bytesB) ? DatCompareResult.Same : DatCompareResult.ContentDiffers;
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var row in _allRows)
        {
            var visible = row.Result switch
            {
                DatCompareResult.Same => ShowSame,
                DatCompareResult.SizeDiffers => ShowSizeDiffers,
                DatCompareResult.ContentDiffers => ShowContentDiffers,
                _ => true, // "only in file N" rows aren't gated by the same/size/text checkboxes
            };

            if (visible)
            {
                Rows.Add(row);
            }
        }
    }
}
