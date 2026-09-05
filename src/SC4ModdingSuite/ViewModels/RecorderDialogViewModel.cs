using System.Collections.ObjectModel;
using System.Linq;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Ilive Reader's "Recorder"/Patch Manager (DlgRecorder): a running list of entries added
/// or changed in the currently open package this session (<see cref="DbpfService.RecordedEntries"/>),
/// exportable as a standalone patch .dat containing just those entries.
///
/// ponytail: exports a normal .dat, not Ilive Reader's own ".kbi" patch format - any SC4
/// tool (including Ilive Reader itself) already reads a plain .dat, so there's no reason to
/// invent/parse a second on-disk format just to keep the ".kbi" extension. "Convert patch to
/// DAT" and "Merge patches" are consequently moot here (a "patch" already IS a .dat, and
/// combining several is exactly what the existing Merge dialog/Turn-1 DlgMerge port already
/// does) - not reproduced separately.
/// </summary>
public sealed class RecorderDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;

    public RecorderDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
        RemoveCommand = new RelayCommand(_ => Remove(), _ => SelectedEntry is not null);
        Refresh();
    }

    public ObservableCollection<EntryItemViewModel> RecordedEntries { get; } = new();

    private EntryItemViewModel? _selectedEntry;
    public EntryItemViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetField(ref _selectedEntry, value))
            {
                RemoveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public RelayCommand RemoveCommand { get; }

    public void Refresh()
    {
        RecordedEntries.Clear();
        var recorded = _document.Service.RecordedEntries;
        foreach (var vm in _document.Entries)
        {
            if (recorded.Contains(vm.Entry))
            {
                RecordedEntries.Add(vm);
            }
        }

        StatusMessage = $"{RecordedEntries.Count} entr{(RecordedEntries.Count == 1 ? "y" : "ies")} added/changed this session.";
    }

    /// <summary>"Remove" here only drops the entry from the recorded/patch list - same as Ilive Reader's DlgRecorder::OnRemove, which never touched the actual package, just what would go into the next exported patch.</summary>
    private void Remove()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        _document.Service.RecordedEntries.Remove(SelectedEntry.Entry);
        RecordedEntries.Remove(SelectedEntry);
        SelectedEntry = null;
        StatusMessage = $"{RecordedEntries.Count} entr{(RecordedEntries.Count == 1 ? "y" : "ies")} added/changed this session.";
    }

    /// <summary>Writes every currently recorded entry out as a standalone patch .dat (Ilive Reader's DlgRecorder::OnCreate/OnCreateDat).</summary>
    public void ExportPatch(string path)
    {
        if (RecordedEntries.Count == 0)
        {
            StatusMessage = "Nothing recorded to export.";
            return;
        }

        try
        {
            DbpfWriter.WritePackage(RecordedEntries.Select(vm => vm.Entry), path);
            StatusMessage = $"Patch with {RecordedEntries.Count} entr{(RecordedEntries.Count == 1 ? "y" : "ies")} saved to: {path}";
        }
        catch (System.Exception ex)
        {
            StatusMessage = $"Error saving patch: {ex.Message}";
        }
    }
}
