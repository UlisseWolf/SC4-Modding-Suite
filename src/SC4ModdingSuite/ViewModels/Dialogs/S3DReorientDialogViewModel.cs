using System;
using System.Collections.Generic;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Reorient" dialog: a clean-room reimplementation (from documented/observed
/// behavior only, no decompiled code) of a well-known, now-abandoned third-party SC4
/// modding utility's "Reorient" command for 20-Zoom/Rotation S3D model sets.
///
/// <para>
/// Unlike Flip/Mirror/Move/Scale/Rotate (which edit one model's own vertex data), Reorient
/// is documented as *not* touching any model's geometry, and *not* renumbering any entry's
/// Type/Group/Instance - it only changes which rotation slot each model's content occupies.
/// The exact Instance ID bit-layout BAT uses to encode "zoom N, rotation N" is not something
/// this app has reliable, verified knowledge of (getting a byte-level ID convention wrong
/// would silently corrupt real files), so rather than guess at it, this operates purely on
/// the raw bytes of the entries the user selects, in the order they're given (ascending by
/// Instance ID is suggested in the dialog's own text, but not enforced) - it cyclically
/// swaps each entry's raw content with another selected entry's, leaving every entry's own
/// TGI exactly where it was. That reproduces the documented effect (geometry unchanged,
/// TGIs unchanged, only the rotation-to-content mapping moves) without this app needing to
/// understand or reconstruct the underlying ID scheme at all.
/// </para>
/// </summary>
public sealed class S3DReorientDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;
    private readonly IReadOnlyList<DBPFEntry> _entries;

    public S3DReorientDialogViewModel(MainWindowViewModel document, IReadOnlyList<DBPFEntry> entries)
    {
        _document = document;

        // Stable, deterministic order - ascending Instance ID, which is how BAT-exported
        // rotation variants are conventionally numbered in practice (see this class's own
        // remarks on why this app doesn't assume a specific bit-level encoding beyond that).
        _entries = entries.OrderBy(e => e.TGI.InstanceID).ToList();

        var nonS3D = _entries.Count(e => e.TGI.TypeID != 0x5AD0E817);
        StatusMessage = nonS3D > 0
            ? $"Warning: {nonS3D} of {_entries.Count} selected entr(y/ies) are not S3D models - Reorient only makes sense for S3D model sets."
            : $"{_entries.Count} S3D model(s) selected, ordered by ascending Instance ID.";

        ReorientCommand = new RelayCommand(_ => Reorient(), _ => _entries.Count >= 2);
    }

    public int EntryCount => _entries.Count;

    public IReadOnlyList<string> EntryInstanceHexInOrder => _entries.Select(e => $"0x{e.TGI.InstanceID:X8}").ToList();

    private int _steps = 1;
    public int Steps
    {
        get => _steps;
        set => SetField(ref _steps, value);
    }

    private bool _clockwise = true;
    public bool Clockwise
    {
        get => _clockwise;
        set => SetField(ref _clockwise, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand ReorientCommand { get; }

    private void Reorient()
    {
        if (_entries.Count < 2)
        {
            StatusMessage = "Select at least 2 entries (the rotation variants to reassign) first.";
            return;
        }

        try
        {
            var n = _entries.Count;
            var signedSteps = Clockwise ? Steps : -Steps;

            // entry[i] receives the raw bytes that entry[(i - steps) mod n] currently has -
            // a cyclic rotation of content across slots, TGIs untouched.
            var originalBytes = _entries.Select(e => e.ByteData ?? Array.Empty<byte>()).ToList();

            for (var i = 0; i < n; i++)
            {
                var sourceIndex = ((i - signedSteps) % n + n) % n;
                if (sourceIndex == i)
                {
                    continue;
                }

                _document.Service.ReplaceEntryBytes(_entries[i], originalBytes[sourceIndex]);
            }

            _document.ReloadEntries();
            StatusMessage = $"Reassigned content across {n} rotation slot(s), {Math.Abs(Steps)} step(s) {(Clockwise ? "clockwise" : "counter-clockwise")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while reorienting: {ex.Message}";
        }
    }
}
