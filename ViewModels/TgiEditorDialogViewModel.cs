using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the standalone "TGI Editor" dialog (Ilive Reader's DlgTGIEditor): unlike the
/// inline TGI editor in the main workspace (which only ever edits the one selected
/// entry), this applies a masked Type/Group/Instance pattern to every selected entry at
/// once. Each mask is 8 hex digits (an optional leading "0x" is ignored); a digit forces
/// that nibble, '#' (or any other non-hex character) leaves the entry's existing nibble
/// alone - e.g. Group mask "0x1#######" forces the top nibble of every selected entry's
/// Group ID to 1 and leaves the rest untouched, matching Ilive Reader's CHexMaskEdit
/// mask/match semantics exactly.
/// </summary>
public sealed class TgiEditorDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;

    public TgiEditorDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
        ApplyCommand = new RelayCommand(_ => Apply(), _ => SelectedEntries.Count > 0);
    }

    /// <summary>Same live collection as the main workspace's entry list - stays in sync automatically.</summary>
    public ObservableCollection<EntryItemViewModel> Entries => _document.Entries;

    private IReadOnlyList<EntryItemViewModel> _selectedEntries = Array.Empty<EntryItemViewModel>();
    public IReadOnlyList<EntryItemViewModel> SelectedEntries
    {
        get => _selectedEntries;
        set
        {
            _selectedEntries = value;
            OnPropertyChanged();
            ApplyCommand.RaiseCanExecuteChanged();
        }
    }

    private string _typeMask = "########";
    public string TypeMask
    {
        get => _typeMask;
        set => SetField(ref _typeMask, value);
    }

    private string _groupMask = "########";
    public string GroupMask
    {
        get => _groupMask;
        set => SetField(ref _groupMask, value);
    }

    private string _instanceMask = "########";
    public string InstanceMask
    {
        get => _instanceMask;
        set => SetField(ref _instanceMask, value);
    }

    private string _statusMessage = "Select one or more entries above, set a mask ('#' = keep existing digit), then Apply.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand ApplyCommand { get; }

    private void Apply()
    {
        if (SelectedEntries.Count == 0)
        {
            StatusMessage = "Select at least one entry.";
            return;
        }

        try
        {
            var (typeMask, typeMatch) = ParseMask(TypeMask);
            var (groupMask, groupMatch) = ParseMask(GroupMask);
            var (instanceMask, instanceMatch) = ParseMask(InstanceMask);

            var targets = SelectedEntries.Select(vm => vm.Entry).ToList();
            var applied = 0;

            foreach (var entry in targets)
            {
                var tgi = entry.TGI;
                var newType = (tgi.TypeID & ~typeMask) | typeMatch;
                var newGroup = (tgi.GroupID & ~groupMask) | groupMatch;
                var newInstance = (tgi.InstanceID & ~instanceMask) | instanceMatch;
                _document.Service.ChangeEntryTgi(entry, newType, newGroup, newInstance, randomizeGroup: false, randomizeInstance: false);
                applied++;
            }

            _document.ReloadEntries();
            StatusMessage = $"Applied to {applied} entr{(applied == 1 ? "y" : "ies")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying mask: {ex.Message}";
        }
    }

    /// <summary>Parses an 8-hex-digit mask (Ilive Reader's CHexMaskEdit::GetMatchMask) into a (bits-to-clear, bits-to-set) pair.</summary>
    internal static (uint Mask, uint Match) ParseMask(string text)
    {
        var digits = (text ?? string.Empty).Trim();
        if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            digits = digits[2..];
        }

        digits = digits.Length >= 8 ? digits[..8] : digits.PadLeft(8, '#');

        uint mask = 0;
        uint match = 0;
        for (var i = 0; i < 8; i++)
        {
            var shift = (7 - i) * 4;
            var c = digits[i];
            if (!Uri.IsHexDigit(c))
            {
                continue; // '#' or anything else non-hex: leave this nibble alone
            }

            var nibble = (uint)Convert.ToInt32(c.ToString(), 16);
            mask |= 0xFu << shift;
            match |= nibble << shift;
        }

        return (mask, match);
    }
}
