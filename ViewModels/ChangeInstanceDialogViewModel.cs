using System;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Change Instance" dialog (Ilive Reader's DlgChangeInstance): changes just the
/// Instance ID of one entry. Unlike the original dialog - which never checked whether the
/// new Instance ID would collide with another entry already using the same Type+Group -
/// this one checks live as you type and blocks Apply unless "Allow duplicate TGI" is
/// explicitly ticked, since two entries sharing a TGI is normally a bug, not something to
/// silently allow.
/// </summary>
public sealed class ChangeInstanceDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;
    private readonly DBPFEntry _entry;

    public ChangeInstanceDialogViewModel(MainWindowViewModel document, DBPFEntry entry)
    {
        _document = document;
        _entry = entry;
        _newInstanceHex = $"0x{entry.TGI.InstanceID:X8}";
        ApplyCommand = new RelayCommand(_ => Apply(), _ => !HasConflict || AllowConflict);
        UpdateConflict();
    }

    public string CurrentTgiText => $"{_entry.TGI.TypeID:X8}-{_entry.TGI.GroupID:X8}-{_entry.TGI.InstanceID:X8}";

    private string _newInstanceHex;
    public string NewInstanceHex
    {
        get => _newInstanceHex;
        set
        {
            if (SetField(ref _newInstanceHex, value))
            {
                UpdateConflict();
            }
        }
    }

    private bool _hasConflict;
    public bool HasConflict
    {
        get => _hasConflict;
        private set
        {
            if (SetField(ref _hasConflict, value))
            {
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _allowConflict;
    public bool AllowConflict
    {
        get => _allowConflict;
        set
        {
            if (SetField(ref _allowConflict, value))
            {
                ApplyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand ApplyCommand { get; }

    public bool Applied { get; private set; }

    public event EventHandler? Closed;

    private void UpdateConflict()
    {
        try
        {
            var newInstance = EntryClipboard.ParseHex(NewInstanceHex);
            var conflictTgi = new TGI(_entry.TGI.TypeID, _entry.TGI.GroupID, newInstance);
            HasConflict = newInstance != _entry.TGI.InstanceID && _document.Service.TgiExists(conflictTgi);
            StatusMessage = HasConflict
                ? "Another entry already uses this Type+Group+Instance. Tick \"Allow duplicate TGI\" to force it anyway (not recommended)."
                : string.Empty;
        }
        catch
        {
            HasConflict = false;
        }
    }

    private void Apply()
    {
        try
        {
            var newInstance = EntryClipboard.ParseHex(NewInstanceHex);
            _document.Service.ChangeEntryTgi(_entry, _entry.TGI.TypeID, _entry.TGI.GroupID, newInstance, randomizeGroup: false, randomizeInstance: false);
            _document.ReloadEntries();
            Applied = true;
            StatusMessage = "Instance ID changed.";
            Closed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
