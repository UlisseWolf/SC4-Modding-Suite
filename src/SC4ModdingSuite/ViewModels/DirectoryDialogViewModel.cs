using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Directory sync" dialog (Ilive Reader's DlgDirectory): decodes the active
/// tab's on-disk Directory (DIR) subfile - the table of TGI + uncompressed-size records
/// csDBPF/DbpfWriter otherwise treat as an internal implementation detail, always rebuilt
/// fresh on save (see <see cref="DbpfWriter"/>) - and cross-checks it against the file's
/// actual entries, exactly like DlgDirectory::Display's per-record lookup. Because this
/// app's own writer always regenerates the Directory subfile correctly at save time, any
/// mismatch found here can only describe how the file looked *before* it was opened (e.g.
/// hand-edited, or written by another tool) - there is no separate "repair" action needed
/// beyond the Save/Save As this app already provides, which is called out in
/// <see cref="StatusMessage"/>. "Select in list" reproduces DlgDirectory::OnMenuSync,
/// which just selects the corresponding entry back in the main window.
/// </summary>
public sealed class DirectoryDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;

    public DirectoryDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
        SelectCommand = new RelayCommand(_ => SelectInList(), _ => SelectedRow?.CanSelect == true);
        Refresh();
    }

    public ObservableCollection<DirectoryRowViewModel> Rows { get; } = new();

    private DirectoryRowViewModel? _selectedRow;
    public DirectoryRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetField(ref _selectedRow, value))
            {
                SelectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand SelectCommand { get; }

    private void Refresh()
    {
        Rows.Clear();

        var file = _document.Service.CurrentFile;
        if (file is null)
        {
            StatusMessage = "No document is open in this tab.";
            return;
        }

        var dirEntry = file.ListOfEntries.FirstOrDefault(e => SameTgi(e.TGI, DbpfWriter.DirectoryTgi));
        var declared = dirEntry is null ? new List<(TGI Tgi, uint Size)>() : ParseDirectory(dirEntry);

        var actualByTgi = file.ListOfEntries
            .Where(e => !SameTgi(e.TGI, DbpfWriter.DirectoryTgi))
            .ToDictionary(e => Key(e.TGI));

        var declaredKeys = new HashSet<(uint, uint, uint)>();

        foreach (var (tgi, size) in declared)
        {
            var key = Key(tgi);
            declaredKeys.Add(key);
            actualByTgi.TryGetValue(key, out var actual);

            DirectorySyncStatus status;
            if (actual is null)
            {
                status = DirectorySyncStatus.DeclaredButMissing;
            }
            else
            {
                var actualBytes = RawEntryBytes.GetDecompressed(actual);
                status = (actualBytes?.Length ?? 0) == size ? DirectorySyncStatus.Ok : DirectorySyncStatus.SizeMismatch;
            }

            Rows.Add(new DirectoryRowViewModel { Tgi = tgi, DeclaredUncompressedSize = size, ActualEntry = actual, Status = status });
        }

        // Entries that are compressed right now but weren't declared in the on-disk
        // Directory subfile at all - e.g. compressed by a tool that forgot to (re)write
        // that table.
        foreach (var entry in file.ListOfEntries.Where(e => e.IsCompressed && !SameTgi(e.TGI, DbpfWriter.DirectoryTgi)))
        {
            if (declaredKeys.Add(Key(entry.TGI)))
            {
                Rows.Add(new DirectoryRowViewModel { Tgi = entry.TGI, ActualEntry = entry, Status = DirectorySyncStatus.CompressedButNotDeclared });
            }
        }

        var problems = Rows.Count(r => r.Status != DirectorySyncStatus.Ok);
        StatusMessage = dirEntry is null
            ? $"This file has no Directory subfile on disk (no entry was compressed the last time it was saved). {file.ListOfEntries.Count} entries in file - nothing to cross-check."
            : $"{declared.Count} record(s) declared in the Directory subfile, {problems} mismatch(es) found. "
              + "Save/Save As in this app always rewrites the Directory subfile from scratch, so any mismatch here only describes the file as it was loaded - not something a separate \"repair\" step is needed for.";

        SelectCommand.RaiseCanExecuteChanged();
    }

    private void SelectInList()
    {
        if (SelectedRow?.ActualEntry is { } entry)
        {
            _document.SelectEntryByTgi(entry.TGI);
        }
    }

    private static (uint, uint, uint) Key(TGI tgi) => (tgi.TypeID, tgi.GroupID, tgi.InstanceID);

    private static bool SameTgi(TGI a, TGI b) => a.TypeID == b.TypeID && a.GroupID == b.GroupID && a.InstanceID == b.InstanceID;

    private static List<(TGI Tgi, uint Size)> ParseDirectory(DBPFEntry dirEntry)
    {
        var bytes = RawEntryBytes.GetDecompressed(dirEntry) ?? Array.Empty<byte>();
        var records = new List<(TGI, uint)>();

        for (var offset = 0; offset + 16 <= bytes.Length; offset += 16)
        {
            var span = bytes.AsSpan(offset, 16);
            var type = BinaryPrimitives.ReadUInt32LittleEndian(span[..4]);
            var group = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4));
            var instance = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8, 4));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4));
            records.Add((new TGI(type, group, instance), size));
        }

        return records;
    }
}
