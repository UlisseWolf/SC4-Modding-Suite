using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "RHD -> LHD (S3D)" dialog: applies <see cref="S3DRhdLhdConverter"/> to the
/// selected S3D models of a 20-view set in the open package. The offset is entered once,
/// for the South (slot 0) view, in meters; each view gets it rotated for its own slot.
/// Entries that aren't S3D models, or whose Instance ID doesn't follow the 20-view layout,
/// are skipped and counted in the status line.
/// </summary>
public sealed class S3DRhdLhdDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;
    private readonly IReadOnlyList<DBPFEntry> _entries;

    public S3DRhdLhdDialogViewModel(MainWindowViewModel document, IReadOnlyList<DBPFEntry> entries)
    {
        _document = document;
        _entries = entries;
        var eligible = _entries.Count(IsEligible);
        _statusMessage = $"{eligible} of {_entries.Count} selected entr(y/ies) are S3D views that can be converted.";
        if (eligible % 20 != 0)
        {
            // A complete BAT set is 20 views; single models (e.g. an Instance ID ending in 00)
            // also pass the Instance ID check and would be treated as the South view.
            _statusMessage += " Warning: not a multiple of 20 - a complete set has 20 views, check the selection.";
        }
        ConvertCommand = new RelayCommand(_ => Convert(), _ => eligible > 0);
    }

    private static bool IsEligible(DBPFEntry e) =>
        e.TGI.TypeID == 0x5AD0E817 && S3DRhdLhdConverter.RotationSlot(e.TGI.InstanceID) is not null;

    private string _offsetXText = "0";
    public string OffsetXText { get => _offsetXText; set => SetField(ref _offsetXText, value); }

    private string _offsetZText = "0";
    public string OffsetZText { get => _offsetZText; set => SetField(ref _offsetZText, value); }

    private bool _renameRhdToLhd = true;
    public bool RenameRhdToLhd { get => _renameRhdToLhd; set => SetField(ref _renameRhdToLhd, value); }

    private string _statusMessage;
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }

    public RelayCommand ConvertCommand { get; }

    private void Convert()
    {
        if (!float.TryParse(OffsetXText, NumberStyles.Float, CultureInfo.InvariantCulture, out var dx) ||
            !float.TryParse(OffsetZText, NumberStyles.Float, CultureInfo.InvariantCulture, out var dz))
        {
            StatusMessage = "X/Z offsets must be valid numbers (use a dot as decimal separator).";
            return;
        }

        var converted = 0;
        var failed = 0;
        foreach (var entry in _entries.Where(IsEligible).ToList())
        {
            try
            {
                var bytes = RawEntryBytes.GetDecompressed(entry);
                var model = bytes is null ? null : S3DParser.Parse(bytes);
                if (model is null)
                {
                    failed++;
                    continue;
                }

                var slot = S3DRhdLhdConverter.RotationSlot(entry.TGI.InstanceID)!.Value;
                S3DRhdLhdConverter.Convert(model, dx, dz, slot, RenameRhdToLhd);

                var newEntry = _document.Service.ReplaceEntryBytes(entry, S3DWriter.Encode(model));
                try
                {
                    _document.Service.SetEntryCompression(newEntry, false);
                }
                catch
                {
                    // Not fatal - the converted bytes are already stored correctly.
                }

                converted++;
            }
            catch
            {
                failed++;
            }
        }

        _document.ReloadEntries();
        var skipped = _entries.Count - converted - failed;
        StatusMessage = $"Converted {converted} view(s); {failed} failed, {skipped} skipped (not a 20-view S3D). Remember to save the package.";
    }
}
