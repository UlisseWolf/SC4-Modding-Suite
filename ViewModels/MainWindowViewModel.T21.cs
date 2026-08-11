using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// T21 Editor: only active/shown while <see cref="IsT21EditorMode"/> is selected (see
/// <c>DbpfWorkspaceView.axaml</c>'s "T21 Editor" radio button and the panel bound to
/// <c>IsT21EditorMode</c>). Edits a T21 Exemplar (Type 0x6534284A, Group
/// <see cref="T21GroupId"/> - "Network Lots"/"Base Texture" Prop&amp;Flora placement
/// exemplars) with a dedicated form instead of the raw Properties list, exactly the way
/// Jondor's own standalone "T21 Editor" tool does (see the bundled
/// "jondor-t21-editor-main" source, in particular <c>T21EditWindow.java</c> - every field
/// and the on-disk property layout below is a direct, verified port of that class, just
/// re-implemented against this app's own Avalonia UI, csDBPF property model and
/// <see cref="ExemplarBinaryParser"/> instead of Jondor's Swing/jDBPF stack).
///
/// Buffered like the LUA/S3D/UI editors elsewhere in this app: editing the fields below
/// only changes this in-memory buffer; nothing is written back to the entry until
/// <see cref="SaveT21Command"/> runs (Jondor's own SAVE/SAVE+CLOSE buttons).
/// </summary>
public sealed partial class MainWindowViewModel
{
    // ---------------------------------------------------------------
    // Header fields (Jondor's nameText/iidText/cancelCheck/tileText/minSlopeText/
    // maxSlopeText/patternSize3-4/patternButton[]/zonesCheck[]/wealthNone-High/
    // flipsCombo/rotsNorth-West)
    // ---------------------------------------------------------------

    private string _t21Name = "Untitled";
    public string T21Name
    {
        get => _t21Name;
        set => SetField(ref _t21Name, value);
    }

    private string _t21IidHex = "0x00000000";
    public string T21IidHex
    {
        get => _t21IidHex;
        set => SetField(ref _t21IidHex, value);
    }

    /// <summary>Jondor's "cancelCheck" - when set, only Type/Name/IID/Version are written and every other T21 property (tile/slope/pattern/zones/wealth/flips/rotations/objects) is dropped.</summary>
    private bool _t21NoLotConfig;
    public bool T21NoLotConfig
    {
        get => _t21NoLotConfig;
        set => SetField(ref _t21NoLotConfig, value);
    }

    private string _t21TileIidHex = "0x00000000";
    public string T21TileIidHex
    {
        get => _t21TileIidHex;
        set => SetField(ref _t21TileIidHex, value);
    }

    private string _t21MinSlopeText = "0";
    public string T21MinSlopeText
    {
        get => _t21MinSlopeText;
        set => SetField(ref _t21MinSlopeText, value);
    }

    private string _t21MaxSlopeText = "64";
    public string T21MaxSlopeText
    {
        get => _t21MaxSlopeText;
        set => SetField(ref _t21MaxSlopeText, value);
    }

    private bool _t21PatternSizeIs3;
    /// <summary>
    /// Backed by a single field, exposed as two independent-but-linked bool properties
    /// (this one and <see cref="T21PatternSizeIs4"/>) instead of one property plus a
    /// <c>!Binding</c> negation on the second RadioButton - the negated-binding version
    /// didn't reliably flip both ways in the running app (switching to "4" from "3"
    /// wouldn't stick), so this uses the same "each option is its own real property"
    /// shape already proven throughout this app (see the six IsXxxEditorMode properties).
    /// </summary>
    public bool T21PatternSizeIs3
    {
        get => _t21PatternSizeIs3;
        set
        {
            if (SetField(ref _t21PatternSizeIs3, value))
            {
                OnPropertyChanged(nameof(T21PatternSizeIs4));
                RefreshT21PatternHex();
            }
        }
    }

    public bool T21PatternSizeIs4
    {
        get => !_t21PatternSizeIs3;
        set
        {
            if (value)
            {
                T21PatternSizeIs3 = false;
            }
        }
    }

    /// <summary>16 toggles (4 groups of 4 bits - Jondor's <c>patternButton[0..15]</c>). No label text (matches Jondor's plain checkbox grid) - laid out as an explicit 4x4 grid in XAML, not this collection's order.</summary>
    public ObservableCollection<T21ToggleViewModel> T21PatternToggles { get; } =
        new(Enumerable.Range(0, 16).Select(i => new T21ToggleViewModel(i, string.Empty)));

    private string _t21PatternHexText = "Hex:";
    /// <summary>"Hex: 0x_, 0x_, 0x_, 0x_" (4 groups) or "Hex: 0x_, 0x_, 0x_" (3 groups, each masked to 3 bits) depending on <see cref="T21PatternSizeIs3"/> - Jondor's own <c>patternHexLabel</c>.</summary>
    public string T21PatternHexText
    {
        get => _t21PatternHexText;
        private set => SetField(ref _t21PatternHexText, value);
    }

    private void RefreshT21PatternHex()
    {
        var groups = new long[4];
        foreach (var toggle in T21PatternToggles.Where(t => t.IsSelected))
        {
            groups[toggle.Code / 4] |= 1L << (toggle.Code % 4);
        }

        T21PatternHexText = T21PatternSizeIs3
            ? "Hex: " + string.Join(", ", groups.Take(3).Select(g => $"0x{g & 0x7:X1}"))
            : "Hex: " + string.Join(", ", groups.Select(g => $"0x{g:X1}"));
    }

    public ObservableCollection<T21ToggleViewModel> T21ZoneToggles { get; } =
        new(T21Constants.ZoneNames.Select((name, i) => new T21ToggleViewModel(i, name)));

    public ObservableCollection<T21ToggleViewModel> T21WealthToggles { get; } =
        new(T21Constants.WealthNames.Select((name, i) => new T21ToggleViewModel(i, name)));

    /// <summary>Bit order matches <see cref="T21Constants.Rots"/>'s encoding directly: North=bit0, East=bit1, South=bit2, West=bit3.</summary>
    public ObservableCollection<T21ToggleViewModel> T21RotationToggles { get; } =
        new(new[] { "North", "East", "South", "West" }.Select((name, i) => new T21ToggleViewModel(i, name)));

    public IReadOnlyList<string> T21FlipOptions => T21Constants.FlipOptions;

    /// <summary>Instance-bindable copies of the static option lists in <see cref="T21Constants"/> - a plain <c>{Binding}</c> path can't reach a static member, so the detail panel's Type/LOD/Rotation combo boxes bind to these instead.</summary>
    public IReadOnlyList<string> T21ObjectTypeOptions => T21Constants.ObjectTypeOptions;
    public IReadOnlyList<string> T21LodOptions => T21Constants.LodOptions;
    public IReadOnlyList<string> T21RotationOptions => T21Constants.RotationOptions;

    private string _t21FlipsSelected = T21Constants.FlipOptions[0];
    public string T21FlipsSelected
    {
        get => _t21FlipsSelected;
        set => SetField(ref _t21FlipsSelected, value ?? T21Constants.FlipOptions[0]);
    }

    // ---------------------------------------------------------------
    // Lot objects (Jondor's propFloraList / propTable + the position/rotation/bounds
    // detail fields bound to the selected row)
    // ---------------------------------------------------------------

    public ObservableCollection<T21ObjectRowViewModel> T21Objects { get; } = new();

    private T21ObjectRowViewModel? _selectedT21Object;
    public T21ObjectRowViewModel? SelectedT21Object
    {
        get => _selectedT21Object;
        set
        {
            if (SetField(ref _selectedT21Object, value))
            {
                RemoveT21ObjectCommand.RaiseCanExecuteChanged();
                MoveT21ObjectUpCommand.RaiseCanExecuteChanged();
                MoveT21ObjectDownCommand.RaiseCanExecuteChanged();
                DuplicateT21ObjectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _t21Status = string.Empty;
    /// <summary>Short inline status for the T21 panel (validation errors, save confirmation) - kept separate from the shared <see cref="StatusMessage"/> bar so it can sit right next to the SAVE button.</summary>
    public string T21Status
    {
        get => _t21Status;
        private set => SetField(ref _t21Status, value);
    }

    public RelayCommand AddT21ObjectCommand { get; private set; } = null!;
    public RelayCommand RemoveT21ObjectCommand { get; private set; } = null!;
    public RelayCommand DuplicateT21ObjectCommand { get; private set; } = null!;
    public RelayCommand MoveT21ObjectUpCommand { get; private set; } = null!;
    public RelayCommand MoveT21ObjectDownCommand { get; private set; } = null!;
    public RelayCommand SaveT21Command { get; private set; } = null!;
    public RelayCommand ChangeT21IidCommand { get; private set; } = null!;

    /// <summary>
    /// Wires up the T21 commands. Called once from the main constructor (see
    /// <c>MainWindowViewModel(...)</c>).
    /// </summary>
    private void InitializeT21Commands()
    {
        foreach (var toggle in T21PatternToggles)
        {
            toggle.PropertyChanged += (_, _) => RefreshT21PatternHex();
        }

        AddT21ObjectCommand = new RelayCommand(_ => AddT21Object());
        RemoveT21ObjectCommand = new RelayCommand(_ => RemoveT21Object(), _ => SelectedT21Object is not null);
        DuplicateT21ObjectCommand = new RelayCommand(_ => DuplicateT21Object(), _ => SelectedT21Object is not null);
        MoveT21ObjectUpCommand = new RelayCommand(_ => MoveT21Object(-1), _ => SelectedT21Object is not null && T21Objects.IndexOf(SelectedT21Object) > 0);
        MoveT21ObjectDownCommand = new RelayCommand(_ => MoveT21Object(1), _ => SelectedT21Object is not null && T21Objects.IndexOf(SelectedT21Object) < T21Objects.Count - 1);
        SaveT21Command = new RelayCommand(_ => SaveT21(), _ => SelectedExemplar is not null);
        ChangeT21IidCommand = new RelayCommand(_ => ApplyT21IidOnly(), _ => SelectedExemplar is not null);

        RefreshT21PatternHex();
    }

    private void AddT21Object()
    {
        var row = new T21ObjectRowViewModel();
        T21Objects.Add(row);
        SelectedT21Object = row;
    }

    private void RemoveT21Object()
    {
        if (SelectedT21Object is null)
        {
            return;
        }

        var index = T21Objects.IndexOf(SelectedT21Object);
        T21Objects.Remove(SelectedT21Object);
        SelectedT21Object = T21Objects.Count == 0 ? null : T21Objects[Math.Min(index, T21Objects.Count - 1)];
    }

    private void DuplicateT21Object()
    {
        if (SelectedT21Object is not { } source)
        {
            return;
        }

        var copy = new T21ObjectRowViewModel
        {
            ObjectType = source.ObjectType,
            Lod = source.Lod,
            Flag = source.Flag,
            Rotation = source.Rotation,
            X = source.X,
            Y = source.Y,
            Z = source.Z,
            XMin = source.XMin,
            ZMin = source.ZMin,
            XMax = source.XMax,
            ZMax = source.ZMax,
            ObjectKeyHex = source.ObjectKeyHex,
            IidsText = source.IidsText,
        };

        var index = T21Objects.IndexOf(source);
        T21Objects.Insert(index + 1, copy);
        SelectedT21Object = copy;
    }

    private void MoveT21Object(int delta)
    {
        if (SelectedT21Object is not { } row)
        {
            return;
        }

        var index = T21Objects.IndexOf(row);
        var newIndex = index + delta;
        if (newIndex < 0 || newIndex >= T21Objects.Count)
        {
            return;
        }

        T21Objects.Move(index, newIndex);
        MoveT21ObjectUpCommand.RaiseCanExecuteChanged();
        MoveT21ObjectDownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Populates every T21 field from the selected entry, independently of whether "T21
    /// Editor" mode is currently active - cheap, and keeps the panel correct the instant
    /// the person switches into T21 mode without needing to re-select the entry. Reads via
    /// <see cref="ExemplarBinaryParser"/> (not csDBPF's own <c>ListOfProperties</c> decode)
    /// exactly like <see cref="LoadPropertiesForSelectedEntry"/> does, for the same reason:
    /// T21 exemplars are exactly the array-property-heavy kind of Exemplar where csDBPF's
    /// own decode has been observed to desync (see that method's remarks) - this
    /// independent parser is the one already verified byte-for-byte correct for
    /// <c>LotConfigPropertyLotObject</c>-style repeating properties.
    /// </summary>
    private void LoadT21EditorForSelectedEntry()
    {
        T21Objects.Clear();
        SelectedT21Object = null;
        T21Status = string.Empty;
        ResetT21TogglesTo(false);

        if (SelectedEntry?.Entry is not DBPFEntryEXMP exemplar)
        {
            T21Name = "Untitled";
            T21IidHex = "0x00000000";
            T21NoLotConfig = false;
            T21TileIidHex = "0x00000000";
            T21MinSlopeText = "0";
            T21MaxSlopeText = "64";
            T21PatternSizeIs3 = false;
            T21FlipsSelected = T21Constants.FlipOptions[0];
            return;
        }

        // The exemplar's own TGI Instance ID is always the source of truth for the IID
        // field, regardless of what the (redundant, Jondor also writes it) ExemplarID
        // property says.
        T21IidHex = $"0x{exemplar.TGI.InstanceID:X8}";

        byte[]? rawBytes;
        try
        {
            rawBytes = RawEntryBytes.GetDecompressed(exemplar);
        }
        catch (Exception ex)
        {
            T21Status = $"Could not read this entry's bytes: {ex.Message}";
            return;
        }

        var parsed = ExemplarBinaryParser.Parse(rawBytes);
        if (!parsed.IsWellFormed)
        {
            // Not (yet) a well-formed Exemplar this parser can make sense of - leave the
            // form at its blank/default state so the person can still fill it in and SAVE
            // to build a brand-new T21 from scratch, same as Jondor's own initData() does
            // for any exemplar that isn't already ExemplarType 0x21.
            T21Name = "Untitled";
            T21NoLotConfig = false;
            T21TileIidHex = "0x00000000";
            T21MinSlopeText = "0";
            T21MaxSlopeText = "64";
            T21PatternSizeIs3 = false;
            T21FlipsSelected = T21Constants.FlipOptions[0];
            return;
        }

        long[]? FindLongs(uint id) => parsed.Properties
            .FirstOrDefault(p => p.Id == id)?.Values
            .Select(Convert.ToInt64)
            .ToArray();

        string? FindString(uint id) => parsed.Properties.FirstOrDefault(p => p.Id == id)?.Values.FirstOrDefault() as string;

        var typeValues = FindLongs(T21Constants.ExemplarTypeProp);
        if (typeValues is not { Length: > 0 } || typeValues[0] != 0x21)
        {
            // Not a well-formed T21 (ExemplarType != 0x21, e.g. a blank/template entry
            // just inserted into this group by mistake) - same blank-form reset as above.
            T21Name = "Untitled";
            T21NoLotConfig = false;
            T21TileIidHex = "0x00000000";
            T21MinSlopeText = "0";
            T21MaxSlopeText = "64";
            T21PatternSizeIs3 = false;
            T21FlipsSelected = T21Constants.FlipOptions[0];
            T21Status = "This entry's ExemplarType isn't 0x21 (T21) - showing a blank form; SAVE will turn it into a proper T21.";
            return;
        }

        T21Name = FindString(T21Constants.ExemplarName) ?? "Untitled";

        // Jondor's own heuristic for "this T21 only cancels/overrides the tile's default
        // config and carries no lot config of its own": fewer than 13 properties total
        // (Type/Name/IID/Version + at least one of everything else would be well over 13).
        T21NoLotConfig = parsed.Properties.Count < 13;

        var tile = FindLongs(T21Constants.TileIid);
        T21TileIidHex = tile is { Length: > 0 } ? $"0x{(uint)tile[0]:X8}" : "0x00000000";

        var minSlope = parsed.Properties.FirstOrDefault(p => p.Id == T21Constants.MinSlope)?.Values.FirstOrDefault();
        T21MinSlopeText = minSlope is float f1 ? f1.ToString(CultureInfo.InvariantCulture) : "0";

        var maxSlope = parsed.Properties.FirstOrDefault(p => p.Id == T21Constants.MaxSlope)?.Values.FirstOrDefault();
        T21MaxSlopeText = maxSlope is float f2 ? f2.ToString(CultureInfo.InvariantCulture) : "64";

        var patternSize = FindLongs(T21Constants.PatternSize);
        T21PatternSizeIs3 = patternSize is { Length: > 0 } && patternSize[0] == 3;

        var pattern = FindLongs(T21Constants.Pattern);
        if (pattern is { Length: >= 4 })
        {
            for (var group = 0; group < 4; group++)
            {
                for (var bit = 0; bit < 4; bit++)
                {
                    var index = 4 * group + bit;
                    T21PatternToggles[index].IsSelected = ((pattern[group] >> bit) & 1) != 0;
                }
            }
        }

        var zones = FindLongs(T21Constants.Zones);
        if (zones is not null)
        {
            foreach (var zone in zones)
            {
                if (zone is >= 0 and < 16)
                {
                    T21ZoneToggles[(int)zone].IsSelected = true;
                }
            }
        }

        var wealths = FindLongs(T21Constants.Wealths);
        if (wealths is not null)
        {
            foreach (var wealth in wealths)
            {
                if (wealth is >= 0 and < 4)
                {
                    T21WealthToggles[(int)wealth].IsSelected = true;
                }
            }
        }

        var flips = FindLongs(T21Constants.Flips);
        var flipsIndex = flips is { Length: > 0 } ? (int)flips[0] : 0;
        T21FlipsSelected = flipsIndex is >= 0 and < 3 ? T21Constants.FlipOptions[flipsIndex] : T21Constants.FlipOptions[0];

        var rots = FindLongs(T21Constants.Rots);
        var rotsMask = rots is { Length: > 0 } ? rots[0] : 0;
        for (var bit = 0; bit < 4; bit++)
        {
            T21RotationToggles[bit].IsSelected = ((rotsMask >> bit) & 1) != 0;
        }

        foreach (var property in parsed.Properties
                     .Where(p => p.Id >= T21Constants.ObjectsBase)
                     .OrderBy(p => p.Id))
        {
            var values = property.Values.Select(Convert.ToInt64).ToArray();
            if (values.Length < 12 || (values[0] != 0x1L && values[0] != 0x4L))
            {
                continue; // Not actually a lot-object row (Jondor: typeEnum.byCode(...) == null → skip).
            }

            T21Objects.Add(T21ObjectRowViewModel.FromRawValues(values));
        }

        T21Status = $"Loaded {T21Objects.Count} lot object(s).";
    }

    private void ResetT21TogglesTo(bool value)
    {
        foreach (var toggle in T21PatternToggles) toggle.IsSelected = value;
        foreach (var toggle in T21ZoneToggles) toggle.IsSelected = value;
        foreach (var toggle in T21WealthToggles) toggle.IsSelected = value;
        foreach (var toggle in T21RotationToggles) toggle.IsSelected = value;
    }

    /// <summary>Applies just an IID change (Group is always kept at <see cref="T21GroupId"/>), without touching any property - for when the person only wants to re-key the entry, mirroring Jondor's own free-standing IID field/editor.</summary>
    private void ApplyT21IidOnly()
    {
        if (SelectedEntry is null || SelectedExemplar is null)
        {
            return;
        }

        uint newInstance;
        try
        {
            newInstance = EntryClipboard.ParseHex(T21IidHex);
        }
        catch (Exception ex)
        {
            T21Status = $"Invalid IID: {ex.Message}";
            return;
        }

        ApplyT21NewTgiIfNeeded(newInstance);
    }

    private void ApplyT21NewTgiIfNeeded(uint newInstance)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var current = SelectedEntry.Entry.TGI;
        if (current.InstanceID == newInstance && current.GroupID == T21GroupId)
        {
            return;
        }

        var oldVm = SelectedEntry;
        var index = Entries.IndexOf(oldVm);

        var newEntry = _service.ChangeEntryTgi(
            oldVm.Entry,
            current.TypeID,
            T21GroupId,
            newInstance,
            randomizeGroup: false,
            randomizeInstance: false);

        var newVm = new EntryItemViewModel(newEntry);
        if (index >= 0)
        {
            Entries[index] = newVm;
        }
        else
        {
            Entries.Add(newVm);
        }

        RefreshDisplayedEntries();
        SelectedEntry = newVm;
    }

    /// <summary>
    /// Writes every T21 field back to the selected exemplar (Jondor's own <c>saveData</c>/
    /// <c>triggerSave</c>), replacing its entire property list from scratch (see
    /// <see cref="DbpfService.ReplaceAllProperties"/>), then - if the IID field changed -
    /// re-keys the entry's TGI last, exactly the order Jondor's own code follows
    /// (<c>ex.clearProperties()</c> + re-add everything, then <c>ex.setTGI(...)</c> right
    /// after the IID property is added).
    /// </summary>
    private void SaveT21()
    {
        if (SelectedEntry is null || SelectedExemplar is not { } exemplar)
        {
            return;
        }

        uint newInstance;
        uint tileIid = 0;
        float minSlope = 0, maxSlope = 64;

        try
        {
            newInstance = EntryClipboard.ParseHex(T21IidHex);
            if (!T21NoLotConfig)
            {
                tileIid = EntryClipboard.ParseHex(T21TileIidHex);
                minSlope = float.Parse(T21MinSlopeText, CultureInfo.InvariantCulture);
                maxSlope = float.Parse(T21MaxSlopeText, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex)
        {
            T21Status = $"Could not save: {ex.Message} (check IID / Tile IID / slopes).";
            return;
        }

        var properties = new List<DBPFProperty>
        {
            NewLong(T21Constants.ExemplarTypeProp, DBPFProperty.PropertyDataType.UINT32, 0x21L),
            new DBPFPropertyString(T21Name, DBPF.Encoding.Binary) { ID = T21Constants.ExemplarName },
            NewLong(T21Constants.ExemplarId, DBPFProperty.PropertyDataType.UINT32, newInstance),
            NewLong(T21Constants.Version, DBPFProperty.PropertyDataType.UINT8, 2L),
        };

        if (!T21NoLotConfig)
        {
            properties.Add(NewLong(T21Constants.TileIid, DBPFProperty.PropertyDataType.UINT32, tileIid));
            properties.Add(new DBPFPropertyFloat(new[] { minSlope }, DBPF.Encoding.Binary) { ID = T21Constants.MinSlope });
            properties.Add(new DBPFPropertyFloat(new[] { maxSlope }, DBPF.Encoding.Binary) { ID = T21Constants.MaxSlope });
            properties.Add(NewLong(T21Constants.PatternSize, DBPFProperty.PropertyDataType.UINT8, T21PatternSizeIs3 ? 3L : 4L));

            var pattern = new long[4];
            foreach (var toggle in T21PatternToggles.Where(t => t.IsSelected))
            {
                pattern[toggle.Code / 4] |= 1L << (toggle.Code % 4);
            }

            properties.Add(NewLong(T21Constants.Pattern, DBPFProperty.PropertyDataType.UINT8, pattern));

            var zones = T21ZoneToggles.Where(t => t.IsSelected).Select(t => (long)t.Code).ToArray();
            if (zones.Length == 0)
            {
                zones = new[] { 0L };
                T21ZoneToggles[0].IsSelected = true;
            }

            properties.Add(NewLong(T21Constants.Zones, DBPFProperty.PropertyDataType.UINT8, zones));

            var wealths = T21WealthToggles.Where(t => t.IsSelected).Select(t => (long)t.Code).ToArray();
            if (wealths.Length == 0)
            {
                wealths = new[] { 0L };
                T21WealthToggles[0].IsSelected = true;
            }

            properties.Add(NewLong(T21Constants.Wealths, DBPFProperty.PropertyDataType.UINT8, wealths));

            var flipsIndex = Math.Max(0, T21Constants.FlipOptions.ToList().IndexOf(T21FlipsSelected));
            properties.Add(NewLong(T21Constants.Flips, DBPFProperty.PropertyDataType.UINT8, flipsIndex));

            long rotsMask = 0;
            foreach (var toggle in T21RotationToggles.Where(t => t.IsSelected))
            {
                rotsMask |= 1L << toggle.Code;
            }

            properties.Add(NewLong(T21Constants.Rots, DBPFProperty.PropertyDataType.UINT8, rotsMask));

            var objectId = T21Constants.ObjectsBase;
            foreach (var row in T21Objects)
            {
                properties.Add(NewLong(objectId, DBPFProperty.PropertyDataType.UINT32, row.ToRawValues()));
                objectId++;
            }
        }

        try
        {
            _service.ReplaceAllProperties(exemplar, properties);
        }
        catch (Exception ex)
        {
            T21Status = $"Could not save: {ex.Message}";
            return;
        }

        ApplyT21NewTgiIfNeeded(newInstance);
        RefreshPropertiesAfterEdit();

        T21Status = $"Saved ({T21Objects.Count} lot object(s)). Remember to save the file.";
        StatusMessage = $"T21 '{T21Name}' saved (remember to save the file).";
    }

    private static DBPFPropertyLong NewLong(uint id, DBPFProperty.PropertyDataType type, long value) =>
        new(type, new[] { value }, DBPF.Encoding.Binary) { ID = id };

    private static DBPFPropertyLong NewLong(uint id, DBPFProperty.PropertyDataType type, long[] values) =>
        new(type, values, DBPF.Encoding.Binary) { ID = id };
}
