using csDBPF;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Presents a single DBPFEntry's TGI in a list-friendly, editable way.
/// The underlying entry object is mutated in place when the TGI is changed
/// (see DbpfService.ChangeEntryTgi); this wrapper just re-reads its fields afterwards.
/// </summary>
public sealed class EntryItemViewModel : ViewModelBase
{
    public EntryItemViewModel(DBPFEntry entry)
    {
        Entry = entry;
        Refresh();
    }

    public DBPFEntry Entry { get; }

    private string _typeHex = string.Empty;
    public string TypeHex
    {
        get => _typeHex;
        private set => SetField(ref _typeHex, value);
    }

    private string _groupHex = string.Empty;
    public string GroupHex
    {
        get => _groupHex;
        private set => SetField(ref _groupHex, value);
    }

    private string _instanceHex = string.Empty;
    public string InstanceHex
    {
        get => _instanceHex;
        private set => SetField(ref _instanceHex, value);
    }

    private string _entryType = string.Empty;
    public string EntryType
    {
        get => _entryType;
        private set => SetField(ref _entryType, value);
    }

    private long _sizeBytes;
    public long SizeBytes
    {
        get => _sizeBytes;
        private set => SetField(ref _sizeBytes, value);
    }

    private bool _isCompressed;
    public bool IsCompressed
    {
        get => _isCompressed;
        private set => SetField(ref _isCompressed, value);
    }

    private string _compressionLabel = string.Empty;
    public string CompressionLabel
    {
        get => _compressionLabel;
        private set => SetField(ref _compressionLabel, value);
    }

    /// <summary>Re-reads the wrapped entry's current TGI/size/compression into the display fields.</summary>
    public void Refresh()
    {
        var tgi = Entry.TGI;
        TypeHex = $"0x{tgi.TypeID:X8}";
        GroupHex = $"0x{tgi.GroupID:X8}";
        InstanceHex = $"0x{tgi.InstanceID:X8}";

        // Trust csDBPF's own TGI.GetEntryType()/GetEntryDetail() for everything,
        // including Type ID 0x00000000 - that is the legitimate, documented Type ID for
        // SC4 "UI" entries (csDBPF's own DBPFTGI.UI / DBPFEntryUI), not a "blank/unknown"
        // marker. An earlier version of this app incorrectly force-labeled it "Unknown".
        var detail = tgi.GetEntryDetail();
        var general = tgi.GetEntryType();
        EntryType = string.IsNullOrEmpty(detail) ? general : $"{general} / {detail}";

        SizeBytes = Entry.GetSize();
        IsCompressed = Entry.IsCompressed;
        CompressionLabel = IsCompressed ? "compresso (QFS)" : "non compresso";
    }
}
