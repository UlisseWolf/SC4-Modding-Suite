using System;
using System.Collections.Generic;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Group Patch" dialog (Ilive Reader's DlgGroupPatch): applies one new Group ID
/// to every entry currently selected in the main list.
///
/// <para>
/// Scoped down from the original: Ilive Reader's version auto-discovered a lot's whole
/// "family" by walking Cohort parent-chain links and a handful of hardcoded Exemplar
/// property IDs (User Visible Name 0x8A416A99, Description Key 0xCA416AB5, Default Plop
/// Sound 0xC9B93A56, Item Icon Key 0x8A2602B8, Query Exemplar GUID 0x2A499F85, Lot
/// Resource Key 0x2A57C516) to pull in every related sub-exemplar automatically. csDBPF's
/// public API doesn't expose a Cohort's parent-cohort TGI as a distinct field (only
/// <c>DBPFEntryEXMP.ListOfProperties</c>, the documented property table), so that
/// auto-discovery can't be reproduced without guessing at binary layout that can't be
/// verified here. The entry list's own Ctrl/Shift multi-select already covers picking a
/// family manually - point this dialog at that selection instead.
/// </para>
/// </summary>
public sealed class GroupPatchDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;
    private readonly IReadOnlyList<DBPFEntry> _targetEntries;

    public GroupPatchDialogViewModel(MainWindowViewModel document, IReadOnlyList<DBPFEntry> targetEntries)
    {
        _document = document;
        _targetEntries = targetEntries;
        _newGroupHex = targetEntries.Count == 1 ? $"0x{targetEntries[0].TGI.GroupID:X8}" : "0x00000000";
        ApplyCommand = new RelayCommand(_ => Apply(), _ => _targetEntries.Count > 0);
    }

    public int TargetCount => _targetEntries.Count;

    private string _newGroupHex;
    public string NewGroupHex
    {
        get => _newGroupHex;
        set => SetField(ref _newGroupHex, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand ApplyCommand { get; }

    private void Apply()
    {
        if (_targetEntries.Count == 0)
        {
            StatusMessage = "Select at least one entry in the main list first.";
            return;
        }

        try
        {
            var newGroup = EntryClipboard.ParseHex(NewGroupHex);
            var applied = 0;

            foreach (var entry in _targetEntries)
            {
                var tgi = entry.TGI;
                _document.Service.ChangeEntryTgi(entry, tgi.TypeID, newGroup, tgi.InstanceID, randomizeGroup: false, randomizeInstance: false);
                applied++;
            }

            _document.ReloadEntries();
            StatusMessage = $"Patched Group ID on {applied} entr{(applied == 1 ? "y" : "ies")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while patching: {ex.Message}";
        }
    }
}
