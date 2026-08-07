using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly DbpfService _service = new();

    public MainWindowViewModel(
        PropertyDefinitionsRegistry propertyRegistry,
        PropertySourceService propertySourceService,
        AppOptionsService appOptionsService,
        AppOptions appOptions,
        ThemeService themeService,
        LocalizationService localizationService)
    {
        PropertyRegistry = propertyRegistry;
        PropertySourceService = propertySourceService;
        AppOptionsService = appOptionsService;
        AppOptions = appOptions;
        ThemeService = themeService;
        LocalizationService = localizationService;

        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedEntry is not null);
        ApplyTgiCommand = new RelayCommand(_ => ApplyTgiEdit(), _ => SelectedEntry is not null);
        RandomizeBothCommand = new RelayCommand(_ => RandomizeBoth(), _ => SelectedEntry is not null);

        RemoveSelectedPropertyCommand = new RelayCommand(_ => RemoveSelectedProperty(), _ => SelectedProperty is not null);

        ZoomInCommand = new RelayCommand(_ => ZoomFactor = Math.Min(ZoomFactor * 1.25, 8.0));
        ZoomOutCommand = new RelayCommand(_ => ZoomFactor = Math.Max(ZoomFactor / 1.25, 0.1));
        ZoomResetCommand = new RelayCommand(_ => ZoomFactor = 1.0);

        PlayWavCommand = new RelayCommand(_ => PlayWav(), _ => HasWavPreview && WavPlayer.IsSupportedPlatform);
        StopWavCommand = new RelayCommand(_ => WavPlayer.Stop(), _ => HasWavPreview && WavPlayer.IsSupportedPlatform);

        LaunchPimXCommand = new RelayCommand(_ => LaunchTool(AppOptions.PimXPath, "SC4 PIM-X"));
        LaunchDataNodeCommand = new RelayCommand(_ => LaunchTool(AppOptions.DataNodePath, "SC4 DataNode"));
        LaunchMapperCommand = new RelayCommand(_ => LaunchTool(AppOptions.MapperPath, "SC4 Mapper"));
        LaunchTerraformerCommand = new RelayCommand(_ => LaunchTool(AppOptions.TerraformerPath, "SC4 Terraformer"));
        LaunchSc4PacEditorCommand = new RelayCommand(_ => LaunchTool(AppOptions.Sc4PacEditorPath, "SC4pac Editor"));
        LaunchNamDevelopmentSuiteCommand = new RelayCommand(_ => LaunchTool(AppOptions.NamDevelopmentSuitePath, "NAM Development Suite"));

        IsSc4EditorMode = true;
        RefreshDisplayedEntries();
    }

    /// <summary>
    /// True only if the hidden, unshipped developer flag file is present and set - see
    /// <see cref="Models.DevFeatureFlags"/>. Checked once at startup; everything gated on
    /// this (the External Tools button, its path field in Options) stays fully hidden,
    /// not just disabled, when false.
    /// </summary>
    public bool IsNamDevelopmentSuiteEnabled { get; } = DevFeatureFlags.IsNamDevelopmentSuiteEnabled();

    public RelayCommand LaunchSc4PacEditorCommand { get; }
    public RelayCommand LaunchNamDevelopmentSuiteCommand { get; }

    /// <summary>Currently loaded new_properties.xml database, used to resolve friendly property names.</summary>
    public PropertyDefinitionsRegistry PropertyRegistry { get; private set; }

    public PropertySourceService PropertySourceService { get; }
    public AppOptionsService AppOptionsService { get; }
    public AppOptions AppOptions { get; }
    public ThemeService ThemeService { get; }
    public LocalizationService LocalizationService { get; }

    public RelayCommand LaunchPimXCommand { get; }
    public RelayCommand LaunchDataNodeCommand { get; }
    public RelayCommand LaunchMapperCommand { get; }
    public RelayCommand LaunchTerraformerCommand { get; }

    private void LaunchTool(string? path, string toolName)
    {
        StatusMessage = ExternalToolLauncher.TryLaunch(path, out var error)
            ? $"{toolName} launched."
            : $"{toolName}: {error}";
    }

    /// <summary>Called after the person picks a (possibly different) source from the reopenable dialog.</summary>
    public void SetPropertyRegistry(PropertyDefinitionsRegistry registry)
    {
        PropertyRegistry = registry;
        OnPropertyChanged(nameof(PropertyRegistry));
        StatusMessage = $"Property database: {registry.SourceDescription}.";

        // Re-resolve names for whatever properties are currently displayed.
        foreach (var propertyVm in Properties)
        {
            propertyVm.SetDefinition(PropertyRegistry.FindById(propertyVm.Property.ID));
        }
    }

    /// <summary>Every entry currently in the open package, unfiltered.</summary>
    public ObservableCollection<EntryItemViewModel> Entries { get; } = new();

    /// <summary>
    /// The entries actually shown in the list, after applying the current "editor" mode
    /// filter (see the <c>IsXxxEditorMode</c> properties below). Equal to <see cref="Entries"/>
    /// unfiltered when "SC4 Editor" (the default) is selected.
    /// </summary>
    public ObservableCollection<EntryItemViewModel> DisplayedEntries { get; } = new();

    // ---------------------------------------------------------------
    // "Editor" mode filter buttons (a second row of "dot buttons" below External Tools):
    // quick filters over the entry list by format, mirroring the type-filter checkboxes
    // in Ilive Reader's own DlgFilters.cpp. "SC4 Editor" - the default, active as soon as
    // a file is opened - means "no filter, show everything" (the normal DBPF/TGI editing
    // view this app already provides); the other five each filter down to one specific
    // format. T21 is not a distinct TGI Type ID - it's a regular Exemplar (Type
    // 0x6534284A) with Group ID 0x89AC5643, confirmed via csDBPF's own
    // DBPFTGI.EXEMPLAR_T21 constant ("Network Lots, often referred to as T21 Exemplars").
    // ---------------------------------------------------------------

    private const uint S3DTypeId = 0x5AD0E817;
    private const uint LuaTypeId = 0xCA63E2A3;
    private const uint T21GroupId = 0x89AC5643;

    private bool _isSc4EditorMode;
    public bool IsSc4EditorMode
    {
        get => _isSc4EditorMode;
        set
        {
            if (SetField(ref _isSc4EditorMode, value) && value)
            {
                _isLtextEditorMode = _isS3DEditorMode = _isLuaEditorMode = _isUiEditorMode = _isT21EditorMode = false;
                OnPropertyChanged(nameof(IsLtextEditorMode));
                OnPropertyChanged(nameof(IsS3DEditorMode));
                OnPropertyChanged(nameof(IsLuaEditorMode));
                OnPropertyChanged(nameof(IsUiEditorMode));
                OnPropertyChanged(nameof(IsT21EditorMode));
                RefreshDisplayedEntries();
            }
        }
    }

    private bool _isLtextEditorMode;
    public bool IsLtextEditorMode
    {
        get => _isLtextEditorMode;
        set
        {
            if (SetField(ref _isLtextEditorMode, value) && value)
            {
                _isSc4EditorMode = _isS3DEditorMode = _isLuaEditorMode = _isUiEditorMode = _isT21EditorMode = false;
                OnPropertyChanged(nameof(IsSc4EditorMode));
                OnPropertyChanged(nameof(IsS3DEditorMode));
                OnPropertyChanged(nameof(IsLuaEditorMode));
                OnPropertyChanged(nameof(IsUiEditorMode));
                OnPropertyChanged(nameof(IsT21EditorMode));
                RefreshDisplayedEntries();
            }
        }
    }

    private bool _isS3DEditorMode;
    public bool IsS3DEditorMode
    {
        get => _isS3DEditorMode;
        set
        {
            if (SetField(ref _isS3DEditorMode, value) && value)
            {
                _isSc4EditorMode = _isLtextEditorMode = _isLuaEditorMode = _isUiEditorMode = _isT21EditorMode = false;
                OnPropertyChanged(nameof(IsSc4EditorMode));
                OnPropertyChanged(nameof(IsLtextEditorMode));
                OnPropertyChanged(nameof(IsLuaEditorMode));
                OnPropertyChanged(nameof(IsUiEditorMode));
                OnPropertyChanged(nameof(IsT21EditorMode));
                RefreshDisplayedEntries();
            }
        }
    }

    private bool _isLuaEditorMode;
    public bool IsLuaEditorMode
    {
        get => _isLuaEditorMode;
        set
        {
            if (SetField(ref _isLuaEditorMode, value) && value)
            {
                _isSc4EditorMode = _isLtextEditorMode = _isS3DEditorMode = _isUiEditorMode = _isT21EditorMode = false;
                OnPropertyChanged(nameof(IsSc4EditorMode));
                OnPropertyChanged(nameof(IsLtextEditorMode));
                OnPropertyChanged(nameof(IsS3DEditorMode));
                OnPropertyChanged(nameof(IsUiEditorMode));
                OnPropertyChanged(nameof(IsT21EditorMode));
                RefreshDisplayedEntries();
            }
        }
    }

    private bool _isUiEditorMode;
    public bool IsUiEditorMode
    {
        get => _isUiEditorMode;
        set
        {
            if (SetField(ref _isUiEditorMode, value) && value)
            {
                _isSc4EditorMode = _isLtextEditorMode = _isS3DEditorMode = _isLuaEditorMode = _isT21EditorMode = false;
                OnPropertyChanged(nameof(IsSc4EditorMode));
                OnPropertyChanged(nameof(IsLtextEditorMode));
                OnPropertyChanged(nameof(IsS3DEditorMode));
                OnPropertyChanged(nameof(IsLuaEditorMode));
                OnPropertyChanged(nameof(IsT21EditorMode));
                RefreshDisplayedEntries();
            }
        }
    }

    private bool _isT21EditorMode;
    public bool IsT21EditorMode
    {
        get => _isT21EditorMode;
        set
        {
            if (SetField(ref _isT21EditorMode, value) && value)
            {
                _isSc4EditorMode = _isLtextEditorMode = _isS3DEditorMode = _isLuaEditorMode = _isUiEditorMode = false;
                OnPropertyChanged(nameof(IsSc4EditorMode));
                OnPropertyChanged(nameof(IsLtextEditorMode));
                OnPropertyChanged(nameof(IsS3DEditorMode));
                OnPropertyChanged(nameof(IsLuaEditorMode));
                OnPropertyChanged(nameof(IsUiEditorMode));
                RefreshDisplayedEntries();
            }
        }
    }

    private void RefreshDisplayedEntries()
    {
        DisplayedEntries.Clear();

        foreach (var vm in Entries)
        {
            if (MatchesCurrentEditorMode(vm))
            {
                DisplayedEntries.Add(vm);
            }
        }
    }

    private bool MatchesCurrentEditorMode(EntryItemViewModel vm)
    {
        if (IsSc4EditorMode)
        {
            return true;
        }

        var tgi = vm.Entry.TGI;

        if (IsS3DEditorMode)
        {
            return tgi.TypeID == S3DTypeId;
        }

        if (IsLuaEditorMode)
        {
            return tgi.TypeID == LuaTypeId;
        }

        if (IsUiEditorMode)
        {
            return tgi.TypeID == 0;
        }

        if (IsT21EditorMode)
        {
            return vm.Entry is DBPFEntryEXMP && tgi.GroupID == T21GroupId;
        }

        if (IsLtextEditorMode)
        {
            if (!EntryTypeClassifier.IsLtextWavXaType(tgi))
            {
                return vm.Entry is DBPFEntryLTEXT;
            }

            if (vm.Entry is DBPFEntryLTEXT)
            {
                return true;
            }

            var bytes = RawEntryBytes.GetDecompressed(vm.Entry);
            return !EntryTypeClassifier.LooksLikeRiffWav(bytes) && EntryTypeClassifier.TryDecodeAsLtext(bytes) is not null;
        }

        return true;
    }

    private EntryItemViewModel? _selectedEntry;
    public EntryItemViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetField(ref _selectedEntry, value))
            {
                OnSelectedEntryChanged();
            }
        }
    }

    private string _details = "Open a .dat / .sc4lot / .sc4desc / .sc4model file to get started.";
    public string Details
    {
        get => _details;
        private set => SetField(ref _details, value);
    }

    private string _statusMessage = "No file open.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    private string _windowTitle = "SC4 Modding Suite";
    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetField(ref _windowTitle, value);
    }

    // --- TGI edit fields bound to the detail panel ---

    private string _newTypeText = "0x00000000";
    public string NewTypeText
    {
        get => _newTypeText;
        set => SetField(ref _newTypeText, value);
    }

    private string _newGroupText = "0x00000000";
    public string NewGroupText
    {
        get => _newGroupText;
        set => SetField(ref _newGroupText, value);
    }

    private string _newInstanceText = "0x00000000";
    public string NewInstanceText
    {
        get => _newInstanceText;
        set => SetField(ref _newInstanceText, value);
    }

    private bool _randomizeGroup;
    public bool RandomizeGroup
    {
        get => _randomizeGroup;
        set => SetField(ref _randomizeGroup, value);
    }

    private bool _randomizeInstance;
    public bool RandomizeInstance
    {
        get => _randomizeInstance;
        set => SetField(ref _randomizeInstance, value);
    }

    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand ApplyTgiCommand { get; }
    public RelayCommand RandomizeBothCommand { get; }
    public RelayCommand RemoveSelectedPropertyCommand { get; }

    public bool HasOpenFile => _service.HasOpenFile;
    public bool CanSaveInPlace => _service.CanSaveInPlace;

    // ---------------------------------------------------------------
    // Exemplar/Cohort property list (populated when the selected entry is one)
    // ---------------------------------------------------------------

    public ObservableCollection<PropertyItemViewModel> Properties { get; } = new();

    /// <summary>The exemplar/cohort currently selected, or null if the selected entry isn't one.</summary>
    public DBPFEntryEXMP? SelectedExemplar => SelectedEntry?.Entry as DBPFEntryEXMP;

    public bool IsExemplarSelected => SelectedExemplar is not null;

    private PropertyItemViewModel? _selectedProperty;
    public PropertyItemViewModel? SelectedProperty
    {
        get => _selectedProperty;
        set
        {
            if (SetField(ref _selectedProperty, value))
            {
                RemoveSelectedPropertyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // ---------------------------------------------------------------
    // Image preview (PNG / FSH entries)
    // ---------------------------------------------------------------

    public ObservableCollection<ImageItemViewModel> PreviewImages { get; } = new();

    public bool HasImagePreview => PreviewImages.Count > 0;

    private ImageItemViewModel? _selectedPreviewImage;
    public ImageItemViewModel? SelectedPreviewImage
    {
        get => _selectedPreviewImage;
        set => SetField(ref _selectedPreviewImage, value);
    }

    private double _zoomFactor = 1.0;
    public double ZoomFactor
    {
        get => _zoomFactor;
        private set => SetField(ref _zoomFactor, value);
    }

    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ZoomResetCommand { get; }

    // ---------------------------------------------------------------
    // S3D model preview (wireframe viewer)
    // ---------------------------------------------------------------

    private S3DModel? _selectedS3DModel;
    public S3DModel? SelectedS3DModel
    {
        get => _selectedS3DModel;
        private set => SetField(ref _selectedS3DModel, value);
    }

    public bool HasS3DPreview => SelectedS3DModel is not null;

    /// <summary>True when none of the special preview panels (image/S3D/WAV/simple text-or-hex) apply, so the plain text Details box should show instead.</summary>
    public bool ShowTextDetails => !HasImagePreview && !HasS3DPreview && !HasWavPreview && !HasSimplePreview;

    private string _s3DInfo = string.Empty;
    public string S3DInfo
    {
        get => _s3DInfo;
        private set => SetField(ref _s3DInfo, value);
    }

    private SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>? _s3DTexture;
    public SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>? S3DTexture
    {
        get => _s3DTexture;
        private set => SetField(ref _s3DTexture, value);
    }

    // Two independent "dot button" (RadioButton) toggle pairs for the S3D viewer.
    private bool _isS3DWireframe = true;
    public bool IsS3DWireframe
    {
        get => _isS3DWireframe;
        set
        {
            if (SetField(ref _isS3DWireframe, value) && value)
            {
                IsS3DSolid = false;
            }
        }
    }

    private bool _isS3DSolid;
    public bool IsS3DSolid
    {
        get => _isS3DSolid;
        set
        {
            if (SetField(ref _isS3DSolid, value) && value)
            {
                IsS3DWireframe = false;
            }
        }
    }

    private bool _isS3DDay = true;
    public bool IsS3DDay
    {
        get => _isS3DDay;
        set
        {
            if (SetField(ref _isS3DDay, value) && value)
            {
                IsS3DNight = false;
            }
        }
    }

    private bool _isS3DNight;
    public bool IsS3DNight
    {
        get => _isS3DNight;
        set
        {
            if (SetField(ref _isS3DNight, value) && value)
            {
                IsS3DDay = false;
            }
        }
    }

    // ---------------------------------------------------------------
    // "Simple" read-only preview: plain text for LTEXT/UI, hex dump for
    // Directory/Unknown entries. Display only - deliberately no editor for these, unlike
    // TGI/properties elsewhere in the app.
    // ---------------------------------------------------------------

    private string _simplePreviewLabel = string.Empty;
    public string SimplePreviewLabel
    {
        get => _simplePreviewLabel;
        private set => SetField(ref _simplePreviewLabel, value);
    }

    private string _simplePreviewContent = string.Empty;
    public string SimplePreviewContent
    {
        get => _simplePreviewContent;
        private set => SetField(ref _simplePreviewContent, value);
    }

    public bool HasSimplePreview => SimplePreviewLabel.Length > 0;

    // ---------------------------------------------------------------
    // WAV audio preview/playback
    // ---------------------------------------------------------------

    private byte[]? _currentWavBytes;

    public bool HasWavPreview => _currentWavBytes is not null;

    public RelayCommand PlayWavCommand { get; }
    public RelayCommand StopWavCommand { get; }

    // ---------------------------------------------------------------
    // File operations (called from MainWindow code-behind after the
    // relevant Avalonia storage-provider dialog has produced a path).
    // ---------------------------------------------------------------

    public void OpenFile(string path)
    {
        try
        {
            _service.Open(path);
            ReloadEntries();
            WindowTitle = $"SC4 Modding Suite - {Path.GetFileName(path)}";
            StatusMessage = _service.IsCurrentFileProtected
                ? $"Opened: {path} ({Entries.Count} entries) — PROTECTED SC4 SYSTEM FILE: use \"Save As...\" to save changes."
                : $"Opened: {path} ({Entries.Count} entries)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening file: {ex.Message}";
        }
        OnPropertyChanged(nameof(HasOpenFile));
        OnPropertyChanged(nameof(CanSaveInPlace));
    }

    public void CreateNewPackage()
    {
        _service.CreateNew();
        ReloadEntries();
        WindowTitle = "SC4 Modding Suite - (new file)";
        StatusMessage = "New empty DBPF package created.";
        OnPropertyChanged(nameof(HasOpenFile));
        OnPropertyChanged(nameof(CanSaveInPlace));
    }

    public void SaveInPlace()
    {
        try
        {
            _service.Save();
            StatusMessage = "File saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while saving: {ex.Message}";
        }
    }

    public void SaveToPath(string path)
    {
        try
        {
            _service.SaveAs(path);
            WindowTitle = $"SC4 Modding Suite - {Path.GetFileName(path)}";
            StatusMessage = $"File saved to: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while saving: {ex.Message}";
        }
        OnPropertyChanged(nameof(CanSaveInPlace));
    }

    // ---------------------------------------------------------------
    // Import/export of individual entries (files inside the SC4 package)
    // ---------------------------------------------------------------

    /// <summary>Suggested filename for exporting the selected entry, e.g. for a save-file dialog.</summary>
    public string? SuggestedExportFileName => SelectedEntry is null ? null : EntryExporter.FileNameFor(SelectedEntry.Entry);

    /// <summary>Exports the selected entry's raw bytes to <paramref name="filePath"/>.</summary>
    public void ExportSelectedEntry(string filePath)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            EntryExporter.ExportEntryTo(SelectedEntry.Entry, filePath);
            StatusMessage = $"Entry exported to: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while exporting: {ex.Message}";
        }
    }

    /// <summary>Replaces the selected entry's raw bytes with the content of <paramref name="filePath"/>.</summary>
    public void ImportIntoSelectedEntry(string filePath)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var oldVm = SelectedEntry;
            var index = Entries.IndexOf(oldVm);

            var newEntry = _service.ReplaceEntryBytes(oldVm.Entry, bytes);

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
            StatusMessage = $"Imported into {newVm.TypeHex}/{newVm.GroupHex}/{newVm.InstanceHex} from: {filePath} (remember to save).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while importing: {ex.Message}";
        }
    }

    /// <summary>Exports every entry currently in the package into <paramref name="folder"/>.</summary>
    public void ExportAllEntries(string folder)
    {
        try
        {
            var (succeeded, failed) = EntryExporter.ExportAll(_service.Entries, folder);
            StatusMessage = failed == 0
                ? $"{succeeded} entries exported to: {folder}"
                : $"{succeeded} entries exported, {failed} skipped (error), to: {folder}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while exporting: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------
    // Entry list / details / editing
    // ---------------------------------------------------------------

    private void ReloadEntries()
    {
        Entries.Clear();
        foreach (var entry in _service.Entries)
        {
            Entries.Add(new EntryItemViewModel(entry));
        }
        RefreshDisplayedEntries();
        SelectedEntry = null;
        Details = "Select an entry from the list to see its details.";
    }

    private void OnSelectedEntryChanged()
    {
        RemoveSelectedCommand.RaiseCanExecuteChanged();
        ApplyTgiCommand.RaiseCanExecuteChanged();
        RandomizeBothCommand.RaiseCanExecuteChanged();

        Properties.Clear();
        SelectedProperty = null;
        OnPropertyChanged(nameof(SelectedExemplar));
        OnPropertyChanged(nameof(IsExemplarSelected));

        PreviewImages.Clear();
        SelectedPreviewImage = null;
        ZoomFactor = 1.0;
        OnPropertyChanged(nameof(HasImagePreview));
        OnPropertyChanged(nameof(ShowTextDetails));

        SelectedS3DModel = null;
        S3DInfo = string.Empty;
        DisposeS3DTexture();
        IsS3DWireframe = true;
        IsS3DSolid = false;
        OnPropertyChanged(nameof(HasS3DPreview));
        OnPropertyChanged(nameof(ShowTextDetails));

        WavPlayer.Stop();
        _currentWavBytes = null;
        OnPropertyChanged(nameof(HasWavPreview));
        OnPropertyChanged(nameof(ShowTextDetails));
        PlayWavCommand.RaiseCanExecuteChanged();
        StopWavCommand.RaiseCanExecuteChanged();

        SimplePreviewLabel = string.Empty;
        SimplePreviewContent = string.Empty;
        OnPropertyChanged(nameof(HasSimplePreview));
        OnPropertyChanged(nameof(ShowTextDetails));

        if (SelectedEntry is null)
        {
            Details = "Select an entry from the list to see its details.";
            return;
        }

        NewTypeText = SelectedEntry.TypeHex;
        NewGroupText = SelectedEntry.GroupHex;
        NewInstanceText = SelectedEntry.InstanceHex;
        RandomizeGroup = false;
        RandomizeInstance = false;

        try
        {
            Details = EntryDescriber.Describe(SelectedEntry.Entry, PropertyRegistry);
        }
        catch (Exception ex)
        {
            Details = $"(Error reading details: {ex.Message})";
        }

        LoadPropertiesForSelectedEntry();
        LoadImagePreview();
        LoadS3DPreview();
        LoadWavPreview();
        LoadSimplePreview();
    }

    /// <summary>
    /// Populates a clean, dedicated **read-only** preview for entry types that don't get
    /// their own richer panel elsewhere: the plain text of LTEXT/UI entries, or a hex+ASCII
    /// dump for the package's internal Directory subfile and any other entry csDBPF has no
    /// structured decoder for (<c>DBPFEntryUnknown</c>). Display only, by design - unlike
    /// TGI or Exemplar properties elsewhere in the app, nothing here is editable.
    /// </summary>
    private void LoadSimplePreview()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var entry = SelectedEntry.Entry;

        try
        {
            switch (entry)
            {
                case DBPFEntryLTEXT ltext:
                    entry.Decode();
                    SimplePreviewLabel = "LTEXT TEXT";
                    SimplePreviewContent = ltext.Text ?? string.Empty;
                    break;

                case DBPFEntryUI ui:
                    entry.Decode();
                    SimplePreviewLabel = "UI DEFINITION";
                    SimplePreviewContent = ui.Definition ?? string.Empty;
                    break;

                case DBPFEntryDIR:
                    SimplePreviewLabel = "DIRECTORY (INTERNAL)";
                    SimplePreviewContent =
                        "This is the package's internal Directory subfile, listing every " +
                        "compressed entry's uncompressed size. It is rebuilt automatically " +
                        "whenever the package is saved, so its raw content isn't meaningful " +
                        "to inspect by hand.";
                    break;

                case DBPFEntryEXMP:
                case DBPFEntryPNG:
                case DBPFEntryFSH:
                    // These already have their own dedicated panel (properties / image
                    // preview) - nothing to show here for them.
                    break;

                default:
                    // Covers DBPFEntryUnknown (internal to csDBPF, so it can't be named in
                    // a case label from here) and any other entry type with no structured
                    // decoder. S3D and WAV also come through as this same internal type,
                    // but already have their own dedicated panels (HasS3DPreview/
                    // HasWavPreview) - excluded here so this panel doesn't try to show a
                    // hex dump on top of them. "Special"/non-standard-group LTEXT entries
                    // (shared Type ID, not RIFF, but matching the LTEXT binary layout) get
                    // their text extracted the same way EntryDescriber's fallback does.
                    // Everything else gets labeled with its real format name when
                    // recognized (KnownFormats, cross-referenced against the community's
                    // official SC4 file format list and Ilive Reader's own ENT_* constants),
                    // and shown as readable text or a hex+ASCII dump, whichever fits the
                    // actual bytes.
                    if (entry.TGI.TypeID == 0x5AD0E817)
                    {
                        break;
                    }

                    var bytes = RawEntryBytes.GetDecompressed(entry);

                    if (EntryTypeClassifier.IsLtextWavXaType(entry.TGI))
                    {
                        if (EntryTypeClassifier.LooksLikeRiffWav(bytes))
                        {
                            break;
                        }

                        var text = EntryTypeClassifier.TryDecodeAsLtext(bytes);
                        if (text is not null)
                        {
                            SimplePreviewLabel = "LTEXT TEXT (non-standard variant)";
                            SimplePreviewContent = text;
                            break;
                        }

                        // Not RIFF, not LTEXT-shaped: this Type ID is also shared by
                        // Maxis' compressed "XA" audio format (ENT_XA in Ilive Reader).
                        // XA is a proprietary compressed codec this app doesn't implement
                        // - so instead of a meaningless hex dump, say so plainly.
                        SimplePreviewLabel = "XA - Maxis Extendable Audio (compressed)";
                        SimplePreviewContent =
                            "This entry is compressed EA \"XA\" audio, which shares its Type " +
                            "ID with WAV and LTEXT. SC4 Modding Suite doesn't implement the XA " +
                            "codec, so playback isn't available here - only standard RIFF/WAVE " +
                            "audio (regular WAV entries) can be played.";
                        break;
                    }

                    var knownName = KnownFormats.TryGetName(entry.TGI.TypeID);

                    if (EntryTypeClassifier.LooksLikePlainText(bytes))
                    {
                        // Covers Lua scripts (ENT_LUA), network intersection rule files
                        // (ENT_RUL), and any other text-based format Ilive Reader
                        // recognizes that csDBPF has no structured decoder for at all -
                        // detected generically by content instead of a hardcoded Type ID
                        // list (see EntryTypeClassifier.LooksLikePlainText).
                        SimplePreviewLabel = knownName is not null ? $"{knownName} (text)" : "TEXT";
                        SimplePreviewContent = Encoding.UTF8.GetString(bytes!);
                        break;
                    }

                    SimplePreviewLabel = knownName is not null ? $"{knownName} (raw data, hex)" : "RAW DATA (HEX)";
                    SimplePreviewContent = HexDump.Format(bytes);
                    break;
            }
        }
        catch (Exception ex)
        {
            SimplePreviewLabel = "PREVIEW ERROR";
            SimplePreviewContent = $"(Error reading preview: {ex.Message})";
        }

        OnPropertyChanged(nameof(HasSimplePreview));
        OnPropertyChanged(nameof(ShowTextDetails));
    }

    /// <summary>
    /// Populates <see cref="SelectedS3DModel"/> when the selected entry's Type ID matches
    /// S3D (0x5AD0E817 - csDBPF has no structured decoder for this format, so the raw bytes
    /// are decompressed manually and parsed with <see cref="S3DParser"/>, a port of Ilive
    /// Reader's own s3d module). If the model references a texture
    /// (<see cref="S3DModel.PrimaryTextureId"/>), also resolves and decodes it from the
    /// currently open package for the "Solid" render mode - by SC4 modding convention, a
    /// model's texture is an FSH entry sharing the model's own Group ID, with an Instance
    /// ID matching the material's texture reference.
    /// </summary>
    private void LoadS3DPreview()
    {
        if (SelectedEntry is null || SelectedEntry.Entry.TGI.TypeID != 0x5AD0E817)
        {
            return;
        }

        try
        {
            var bytes = RawEntryBytes.GetDecompressed(SelectedEntry.Entry);
            var model = bytes is null ? null : S3DParser.Parse(bytes);

            if (model is null)
            {
                S3DInfo = "(could not parse the S3D model)";
                return;
            }

            SelectedS3DModel = model;
            var triCount = 0;
            foreach (var _ in model.EnumerateTriangles())
            {
                triCount++;
            }

            var textureStatus = ResolveS3DTexture(model, SelectedEntry.Entry.TGI.GroupID);

            S3DInfo =
                $"S3D v{model.MajorRevision}.{model.MinorRevision} — " +
                $"{model.VertexBlocks.Count} groups, {model.TotalVertexCount:N0} vertices, {triCount:N0} triangles, " +
                $"{model.MaterialCount} materials{(model.HasAnimation ? ", animated" : "")}. " +
                $"{textureStatus} Drag to rotate, scroll to zoom.";
        }
        catch (Exception ex)
        {
            S3DInfo = $"(Error reading S3D model: {ex.Message})";
        }

        OnPropertyChanged(nameof(HasS3DPreview));
        OnPropertyChanged(nameof(ShowTextDetails));
    }

    /// <summary>
    /// Looks up and decodes the FSH texture referenced by the model's primary material, if
    /// any, storing the result in <see cref="S3DTexture"/>. Returns a short status phrase
    /// for the info line ("Texture found.", "No texture reference.", ...).
    /// </summary>
    private string ResolveS3DTexture(S3DModel model, uint modelGroupId)
    {
        DisposeS3DTexture();

        if (model.PrimaryTextureId is not { } textureId)
        {
            return "No texture reference in the model.";
        }

        const uint FshTypeId = 0x7AB50E44;
        DBPFEntry? fshEntry = null;
        foreach (var entry in _service.Entries)
        {
            if (entry.TGI.TypeID == FshTypeId && entry.TGI.GroupID == modelGroupId && entry.TGI.InstanceID == textureId)
            {
                fshEntry = entry;
                break;
            }
        }

        if (fshEntry is null)
        {
            return "Referenced texture not found in this package.";
        }

        try
        {
            fshEntry.Decode();
            var image = (fshEntry as DBPFEntryFSH)?.Image;
            if (image is null)
            {
                return "Could not decode the referenced texture.";
            }

            S3DTexture = image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
            return "Texture found.";
        }
        catch
        {
            return "Could not decode the referenced texture.";
        }
    }

    private void DisposeS3DTexture()
    {
        S3DTexture?.Dispose();
        S3DTexture = null;
    }

    /// <summary>
    /// Populates the WAV preview when the selected entry is actually a WAV. TGI Type ID
    /// 0x2026960B is <b>shared between WAV, LTEXT, and the rarer "XA" audio format</b>
    /// (confirmed both in Ilive Reader's own constants, <c>or_dat/sim015.h</c>, and in
    /// csDBPF's docs: <c>DBPFTGI.WAV</c> = "(0x2026960b, 0xaa4d1933, #)",
    /// <c>DBPFTGI.LTEXT</c> = "(0x2026960b, #, #)") - so Type ID alone previously caused
    /// LTEXT text entries to be misidentified as playable WAV audio. Fixed by additionally
    /// sniffing the decompressed bytes for the "RIFF" magic every WAV file starts with,
    /// exactly like Ilive Reader's own <c>_entry::SetFlag</c> (<c>or_dat/cl_entry.cpp</c>)
    /// does when it can't tell them apart from Group ID alone.
    /// </summary>
    private void LoadWavPreview()
    {
        if (SelectedEntry is null || !EntryTypeClassifier.IsLtextWavXaType(SelectedEntry.Entry.TGI))
        {
            return;
        }

        try
        {
            var bytes = RawEntryBytes.GetDecompressed(SelectedEntry.Entry);
            if (EntryTypeClassifier.LooksLikeRiffWav(bytes))
            {
                _currentWavBytes = bytes;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error reading WAV file: {ex.Message}";
        }

        OnPropertyChanged(nameof(HasWavPreview));
        OnPropertyChanged(nameof(ShowTextDetails));
        PlayWavCommand.RaiseCanExecuteChanged();
        StopWavCommand.RaiseCanExecuteChanged();
    }

    private void PlayWav()
    {
        if (_currentWavBytes is null)
        {
            return;
        }

        if (!WavPlayer.Play(_currentWavBytes))
        {
            StatusMessage = OperatingSystem.IsLinux()
                ? "Could not play the WAV file: install 'paplay' (pulseaudio-utils), 'aplay' (alsa-utils), or ffmpeg."
                : "Could not play the WAV file.";
        }
    }

    /// <summary>
    /// Populates <see cref="PreviewImages"/> when the selected entry is a PNG or FSH image,
    /// using csDBPF's own decoders (<c>DBPFEntryPNG.PNGImage</c> /
    /// <c>DBPFEntryFSH.Entries[].Image</c> - csDBPF already fully implements FSH decoding,
    /// including DXT1/DXT3 and every uncompressed bit depth, so no decoding logic needs to
    /// be ported from Ilive Reader here). A multi-image FSH file (several named sub-images
    /// packed into one entry, e.g. a building's texture set) yields one <see cref="ImageItemViewModel"/>
    /// per sub-image, mirroring the sub-image selector in Ilive Reader's own image viewer.
    /// </summary>
    private void LoadImagePreview()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            switch (SelectedEntry.Entry)
            {
                case DBPFEntryPNG png:
                {
                    Avalonia.Media.Imaging.Bitmap? bitmap = null;
                    try
                    {
                        png.Decode();
                        bitmap = ImageConversion.ToAvaloniaBitmap(png.PNGImage);
                    }
                    catch
                    {
                        // TGI Type 0x856DDBAC is shared between PNG, BMP, and JPEG in the
                        // DBPF format (confirmed in Ilive Reader's own constants -
                        // ENT_PNG/ENT_BMP/ENT_JPEG are all 0x856DDBAC); csDBPF always
                        // builds a DBPFEntryPNG for it and decodes specifically as PNG,
                        // which throws for a genuine BMP/JPEG under this shared type.
                        // Fall through to the generic, format-sniffing decode below
                        // instead of giving up.
                    }

                    if (bitmap is null)
                    {
                        bitmap = ImageConversion.TryDecodeAnyFormat(RawEntryBytes.GetDecompressed(png));
                    }

                    if (bitmap is not null)
                    {
                        PreviewImages.Add(new ImageItemViewModel { Label = "Image", Bitmap = bitmap });
                    }

                    break;
                }

                case DBPFEntryFSH fsh:
                {
                    fsh.Decode();
                    var addedAny = false;

                    if (fsh.Entries is not null)
                    {
                        foreach (var fshEntry in fsh.Entries)
                        {
                            var bitmap = ImageConversion.ToAvaloniaBitmap(fshEntry.Image);
                            if (bitmap is null)
                            {
                                continue;
                            }

                            PreviewImages.Add(new ImageItemViewModel
                            {
                                Label = $"{fshEntry.Name} ({fshEntry.Width}x{fshEntry.Height})",
                                Bitmap = bitmap,
                            });
                            addedAny = true;
                        }
                    }

                    if (!addedAny)
                    {
                        var bitmap = ImageConversion.ToAvaloniaBitmap(fsh.Image);
                        if (bitmap is not null)
                        {
                            PreviewImages.Add(new ImageItemViewModel { Label = "FSH", Bitmap = bitmap });
                        }
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error decoding image: {ex.Message}";
        }

        SelectedPreviewImage = PreviewImages.Count > 0 ? PreviewImages[0] : null;
        OnPropertyChanged(nameof(HasImagePreview));
        OnPropertyChanged(nameof(ShowTextDetails));
    }

    private void LoadPropertiesForSelectedEntry()
    {
        if (SelectedEntry?.Entry is not DBPFEntryEXMP exemplar)
        {
            return;
        }

        try
        {
            exemplar.Decode();
            foreach (var property in exemplar.ListOfProperties.Values)
            {
                Properties.Add(new PropertyItemViewModel(property, PropertyRegistry.FindById(property.ID)));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error reading properties: {ex.Message}";
        }
    }

    private void RefreshPropertiesAfterEdit()
    {
        Properties.Clear();
        SelectedProperty = null;
        LoadPropertiesForSelectedEntry();
        SelectedEntry?.Refresh();

        if (SelectedEntry is not null)
        {
            try
            {
                Details = EntryDescriber.Describe(SelectedEntry.Entry, PropertyRegistry);
            }
            catch (Exception ex)
            {
                Details = $"(Error reading details: {ex.Message})";
            }
        }
    }

    /// <summary>Called from MainWindow code-behind after the Add/Edit property dialog returns a result.</summary>
    public void AddOrUpdateProperty(DBPFProperty property)
    {
        if (SelectedExemplar is null)
        {
            return;
        }

        try
        {
            _service.AddOrUpdateProperty(SelectedExemplar, property);
            RefreshPropertiesAfterEdit();
            StatusMessage = $"Property 0x{property.ID:X8} saved (remember to save the file).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving property: {ex.Message}";
        }
    }

    private void RemoveSelectedProperty()
    {
        if (SelectedExemplar is null || SelectedProperty is null)
        {
            return;
        }

        try
        {
            var id = SelectedProperty.Property.ID;
            _service.RemoveProperty(SelectedExemplar, id);
            RefreshPropertiesAfterEdit();
            StatusMessage = $"Property 0x{id:X8} removed (remember to save the file).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing property: {ex.Message}";
        }
    }

    private void RemoveSelected()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        var toRemove = SelectedEntry;
        _service.RemoveEntry(toRemove.Entry);
        Entries.Remove(toRemove);
        RefreshDisplayedEntries();
        SelectedEntry = null;
        StatusMessage = "Entry removed (remember to save to make the change permanent).";
    }

    // ---------------------------------------------------------------
    // Copy/paste (full entries and TGI-only) - lets entries be transferred between two
    // different package files opened in separate sessions of the app (via the system
    // clipboard), since this app edits one file at a time. Actual clipboard I/O happens in
    // MainWindow.axaml.cs (which has access to Avalonia's IClipboard); these methods only
    // deal in plain strings so the ViewModel stays clipboard-API-agnostic.
    // ---------------------------------------------------------------

    /// <summary>Builds the clipboard text for "Copy" (full entries) over the given selection.</summary>
    public string? BuildEntriesClipboardText(IReadOnlyCollection<EntryItemViewModel> selected) =>
        selected.Count == 0 ? null : EntryClipboard.SerializeEntries(selected.Select(s => s.Entry));

    /// <summary>Builds the clipboard text for "Copy TGI" over the given selection.</summary>
    public string? BuildTgiClipboardText(IReadOnlyCollection<EntryItemViewModel> selected) =>
        selected.Count == 0 ? null : EntryClipboard.SerializeTgiOnly(selected.Select(s => s.Entry.TGI));

    /// <summary>
    /// Pastes entries previously copied with "Copy" (possibly from a different package
    /// opened in another session) into the currently open package as new entries, keeping
    /// their original TGI and content. Entries whose concrete type can no longer be
    /// resolved/constructed are skipped and counted separately rather than aborting the
    /// whole paste.
    /// </summary>
    public void PasteEntriesFromClipboardText(string? clipboardText)
    {
        var payloads = EntryClipboard.TryDeserializeEntries(clipboardText);
        if (payloads is null)
        {
            StatusMessage = "Clipboard doesn't contain entries copied from SC4 Modding Suite.";
            return;
        }

        if (!HasOpenFile)
        {
            StatusMessage = "Open or create a package before pasting entries into it.";
            return;
        }

        var succeeded = 0;
        var failed = 0;

        foreach (var payload in payloads)
        {
            try
            {
                var tgi = new TGI(ParseHex(payload.TypeHex), ParseHex(payload.GroupHex), ParseHex(payload.InstanceHex));
                var bytes = Convert.FromBase64String(payload.DataBase64);
                var newEntry = _service.AddEntryFromClipboard(payload.TypeName, tgi, bytes);

                if (newEntry is null)
                {
                    failed++;
                    continue;
                }

                Entries.Add(new EntryItemViewModel(newEntry));
                succeeded++;
            }
            catch
            {
                failed++;
            }
        }

        RefreshDisplayedEntries();
        StatusMessage = failed == 0
            ? $"Pasted {succeeded} entry(ies) (remember to save)."
            : $"Pasted {succeeded} entry(ies), {failed} skipped (remember to save).";
    }

    /// <summary>
    /// Applies a TGI previously copied with "Copy TGI" to the currently selected entry -
    /// the paste target is always a single entry (a TGI must stay unique), unlike full
    /// entry paste which can add several at once.
    /// </summary>
    public void PasteTgiFromClipboardText(string? clipboardText)
    {
        if (SelectedEntry is null)
        {
            StatusMessage = "Select an entry first to paste a TGI onto it.";
            return;
        }

        var parsed = EntryClipboard.TryParseSingleTgi(clipboardText);
        if (parsed is null)
        {
            StatusMessage = "Clipboard doesn't contain a TGI copied from SC4 Modding Suite.";
            return;
        }

        NewTypeText = $"0x{parsed.Value.Type:X8}";
        NewGroupText = $"0x{parsed.Value.Group:X8}";
        NewInstanceText = $"0x{parsed.Value.Instance:X8}";
        RandomizeGroup = false;
        RandomizeInstance = false;
        ApplyTgiEdit(randomizeGroup: false, randomizeInstance: false);
    }

    private void ApplyTgiEdit() => ApplyTgiEdit(RandomizeGroup, RandomizeInstance);

    private void RandomizeBoth() => ApplyTgiEdit(randomizeGroup: true, randomizeInstance: true);

    private void ApplyTgiEdit(bool randomizeGroup, bool randomizeInstance)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            // Type ID is always taken literally from the text box: it is never randomized,
            // only Group/Instance support the "Casuale" checkbox.
            uint type = ParseHex(NewTypeText);
            uint group = randomizeGroup ? 0u : ParseHex(NewGroupText);
            uint instance = randomizeInstance ? 0u : ParseHex(NewInstanceText);

            var oldVm = SelectedEntry;
            var index = Entries.IndexOf(oldVm);

            // ChangeEntryTgi swaps in a brand-new DBPFEntry instance (TGI is read-only in
            // csDBPF), so the old EntryItemViewModel/entry reference is no longer part of
            // the package; replace it in the list instead of trying to refresh it in place.
            var newEntry = _service.ChangeEntryTgi(
                oldVm.Entry,
                type,
                group,
                instance,
                randomizeGroup,
                randomizeInstance);

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
            RandomizeGroup = false;
            RandomizeInstance = false;

            StatusMessage =
                $"TGI updated -> T:{newVm.TypeHex} G:{newVm.GroupHex} I:{newVm.InstanceHex} (remember to save).";
        }
        catch (FormatException)
        {
            StatusMessage = "Invalid Type/Group/Instance: use a hex value, e.g. 0x1A2B3C4D.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error editing TGI: {ex.Message}";
        }
    }

    private static uint ParseHex(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        if (text.Length == 0)
        {
            return 0;
        }

        return uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
