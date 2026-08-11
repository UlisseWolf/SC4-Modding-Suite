using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Avalonia.Threading;
using csDBPF;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.Views;
using ImgSharpImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace SC4ModdingSuite.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly DbpfService _service = new();
    private readonly AnalysisFilterService _filterService = new();

    public MainWindowViewModel(
        PropertyDefinitionsRegistry propertyRegistry,
        PropertySourceService propertySourceService,
        AppOptionsService appOptionsService,
        AppOptions appOptions,
        ThemeService themeService,
        LocalizationService localizationService,
        UiElementIndexService uiElementIndex,
        Func<string, MainWindowViewModel>? openDocumentInNewTab = null)
    {
        PropertyRegistry = propertyRegistry;
        PropertySourceService = propertySourceService;
        AppOptionsService = appOptionsService;
        AppOptions = appOptions;
        ThemeService = themeService;
        LocalizationService = localizationService;
        UiElementIndex = uiElementIndex;
        OpenDocumentInNewTab = openDocumentInNewTab;

        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedEntry is not null);
        ApplyTgiCommand = new RelayCommand(_ => ApplyTgiEdit(), _ => SelectedEntry is not null);
        RandomizeBothCommand = new RelayCommand(_ => RandomizeBoth(), _ => SelectedEntry is not null);

        RemoveSelectedPropertyCommand = new RelayCommand(_ => RemoveSelectedProperty(), _ => SelectedProperty is not null);

        ZoomInCommand = new RelayCommand(_ => ZoomFactor = Math.Min(ZoomFactor * 1.25, 8.0));
        ZoomOutCommand = new RelayCommand(_ => ZoomFactor = Math.Max(ZoomFactor / 1.25, 0.1));
        ZoomResetCommand = new RelayCommand(_ => ZoomFactor = 1.0);

        PlayWavCommand = new RelayCommand(_ => PlayWav(), _ => HasWavPreview);
        StopWavCommand = new RelayCommand(_ => WavPlayer.Stop(), _ => HasWavPreview);

        LaunchPimXCommand = new RelayCommand(_ => LaunchTool(AppOptions.PimXPath, "SC4 PIM-X"));
        LaunchDataNodeCommand = new RelayCommand(_ => LaunchTool(AppOptions.DataNodePath, "SC4 DataNode"));
        LaunchMapperCommand = new RelayCommand(_ => LaunchTool(AppOptions.MapperPath, "SC4 Mapper"));
        LaunchTerraformerCommand = new RelayCommand(_ => LaunchTool(AppOptions.TerraformerPath, "SC4 Terraformer"));
        LaunchSc4PacEditorCommand = new RelayCommand(_ => LaunchTool(AppOptions.Sc4PacEditorPath, "SC4pac Editor"));
        LaunchNamDevelopmentSuiteCommand = new RelayCommand(_ => LaunchTool(AppOptions.NamDevelopmentSuitePath, "NAM Development Suite"));

        LuaCompileCommand = new RelayCommand(_ => LuaCompile());
        LuaRunCommand = new RelayCommand(_ => LuaRun());
        LuaClearOutputCommand = new RelayCommand(_ => LuaOutput = string.Empty);
        LuaSaveCommand = new RelayCommand(_ => SaveLuaScriptToSelectedEntry(LuaCode), _ => SelectedEntry is not null);

        ApplyS3DCommand = new RelayCommand(_ => SaveS3DModel(), _ => SelectedS3DModel is not null);
        MergeS3DCommand = new RelayCommand(_ => MergeS3DModel(), _ => SelectedS3DModel is not null && SelectedMergeCandidate is not null);

        PlayS3DCommand = new RelayCommand(_ => PlayS3DAnimation(), _ => SelectedS3DModel is not null && S3DFrameCount > 1 && !IsS3DPlaying);
        PauseS3DCommand = new RelayCommand(_ => PauseS3DAnimation(), _ => IsS3DPlaying);
        StopS3DCommand = new RelayCommand(_ => StopS3DAnimation(), _ => IsS3DPlaying || S3DCurrentFrame != 0);

        AddS3DVertexCommand = new RelayCommand(_ => AddS3DVertexPoints(), _ => SelectedS3DModel is { } vm1 && S3DEditGroupIndex < vm1.VertexBlocks.Count);
        AddS3DIndexCommand = new RelayCommand(_ => AddS3DIndexTriangles(), _ => SelectedS3DModel is { } im1 && S3DEditGroupIndex < im1.IndexBlocks.Count);
        AddS3DPrimCommand = new RelayCommand(_ => AddS3DPrimRow(), _ => SelectedS3DModel is { } pm1 && S3DEditGroupIndex < pm1.PrimBlocks.Count);
        AddS3DGroupCommand = new RelayCommand(_ => AddS3DGroup(), _ => SelectedS3DModel is not null);
        DeleteS3DGroupCommand = new RelayCommand(_ => DeleteS3DGroup(), _ => SelectedS3DModel is { } dm1 && S3DEditGroupIndex < dm1.VertexBlocks.Count);
        FlipS3DXyCommand = new RelayCommand(_ => FlipS3D('x', 'y'), _ => SelectedS3DModel is not null);
        FlipS3DXzCommand = new RelayCommand(_ => FlipS3D('x', 'z'), _ => SelectedS3DModel is not null);
        FlipS3DYzCommand = new RelayCommand(_ => FlipS3D('y', 'z'), _ => SelectedS3DModel is not null);
        RemapS3DIndicesCommand = new RelayCommand(_ => RemapS3DIndices(), _ => SelectedS3DModel is { } rm1 && S3DEditGroupIndex < rm1.IndexBlocks.Count);
        PickS3DTriangleCommand = new RelayCommand(param => OnS3DTrianglePicked(param as (int Group, int A, int B, int C)?));

        ChangeS3DMaterialTgiCommand = new RelayCommand(_ => ChangeS3DMaterialTgi(), _ => SelectedS3DMaterialRow is not null);
        AddS3DTextureFromPackageCommand = new RelayCommand(_ => AddS3DMaterialTextureFromPackage(), _ => SelectedS3DModel is not null && SelectedFshEntryForMaterial is not null);

        AddS3DAnimMeshCommand = new RelayCommand(_ => AddS3DAnimMesh(), _ => SelectedS3DModel is not null);
        DeleteS3DAnimMeshCommand = new RelayCommand(_ => DeleteS3DAnimMesh(), _ => SelectedS3DAnimMesh is not null);
        AddS3DAnimFrameCommand = new RelayCommand(_ => AddS3DAnimFrame(), _ => SelectedS3DAnimMesh is not null);

        AddS3DPropCommand = new RelayCommand(_ => AddS3DProp(), _ => SelectedS3DModel is not null);

        AddS3DRegPointCommand = new RelayCommand(_ => AddS3DRegPoint(), _ => SelectedS3DModel is not null);
        DeleteS3DRegPointCommand = new RelayCommand(_ => DeleteS3DRegPoint(), _ => SelectedS3DRegPoint is not null);
        AddS3DRegPointTransformCommand = new RelayCommand(_ => AddS3DRegPointTransform(), _ => SelectedS3DRegPoint is not null);

        S3DUVZoomInCommand = new RelayCommand(_ => S3DUVZoom = Math.Min(S3DUVZoom * 1.25, 8.0));
        S3DUVZoomOutCommand = new RelayCommand(_ => S3DUVZoom = Math.Max(S3DUVZoom / 1.25, 0.1));
        S3DUVPointChangedCommand = new RelayCommand(_ => OnS3DUVPointChanged());

        FindSearchCommand = new RelayCommand(_ => RunFind());
        PluginsAnalyserCommand = new RelayCommand(_ => RunPluginsAnalyser());
        ExemplarAnalyserCommand = new RelayCommand(_ => RunExemplarAnalyser());
        PropertyFindCommand = new RelayCommand(_ => RunPropertyFind());
        RefreshPropertyManagerResults();

        SaveFilterCommand = new RelayCommand(_ => SaveFilter(), _ => !string.IsNullOrWhiteSpace(NewFilterName));
        LoadFilterCommand = new RelayCommand(_ => LoadFilter(), _ => SelectedSavedFilter is not null);
        DeleteFilterCommand = new RelayCommand(_ => DeleteFilter(), _ => SelectedSavedFilter is not null);
        foreach (var filter in _filterService.Load())
        {
            SavedFilters.Add(filter);
        }

        AddUiChildNodeCommand = new RelayCommand(_ => AddUiChildNode());
        RemoveUiNodeCommand = new RelayCommand(_ => RemoveUiNode(), _ => SelectedUiNode is not null);
        AddUiPropertyCommand = new RelayCommand(_ => AddUiProperty(), _ => SelectedUiNode is not null);
        RemoveUiPropertyCommand = new RelayCommand(_ => RemoveUiProperty(), _ => SelectedUiProperty is not null);
        RefreshUiPreviewCommand = new RelayCommand(_ => RefreshUiPreview());
        SaveUiEditorCommand = new RelayCommand(_ => SaveUiEditor(), _ => SelectedEntry is not null && _uiRoot is not null);
        SelectUiNodeCommand = new RelayCommand(param => SelectedUiNode = param is UiLegacyNode node ? FindUiNodeViewModel(node) : null);
        MoveUiNodeCommand = new RelayCommand(param =>
        {
            if (param is ValueTuple<UiLegacyNode, int, int> move)
            {
                MoveUiNode(move.Item1, move.Item2, move.Item3);
            }
        });

        InitializeT21Commands();
        InitializeLtextCommands();

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

    /// <summary>Shared, app-wide index of every UI element across the SC4 installation folder
    /// and the Plugins folder - one instance for the whole app (see App.axaml.cs), not per-tab.
    /// Bound directly in Views/DbpfWorkspaceView.axaml's Analysis "UI Elements" tool.</summary>
    public UiElementIndexService UiElementIndex { get; }

    /// <summary>Opens <c>path</c> into a brand-new MDI tab and returns that tab's own document -
    /// null if this document isn't hosted in the shell (e.g. tests). Used by
    /// SelectAnalysisResult/SelectUiFinderResult to jump to a "whole folder" scan hit that came
    /// from a different file than the one open in this tab, instead of silently doing nothing.</summary>
    public Func<string, MainWindowViewModel>? OpenDocumentInNewTab { get; }

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

    private enum EditorMode { None, Sc4, Ltext, S3D, Lua, Ui, T21, Analysis }

    private EditorMode _editorMode;

    /// <summary>
    /// Switches to <paramref name="mode"/>: notifies all six <c>IsXxxEditorMode</c>
    /// properties (only one of which is ever true at a time) and refreshes the filtered
    /// entry list once. A no-op if <paramref name="mode"/> is already active, matching the
    /// old per-property setters which only acted on an actual change to <c>true</c>.
    /// </summary>
    private void SetEditorMode(EditorMode mode)
    {
        if (_editorMode == mode)
        {
            return;
        }

        _editorMode = mode;
        OnPropertyChanged(nameof(IsSc4EditorMode));
        OnPropertyChanged(nameof(IsLtextEditorMode));
        OnPropertyChanged(nameof(IsS3DEditorMode));
        OnPropertyChanged(nameof(IsLuaEditorMode));
        OnPropertyChanged(nameof(IsUiEditorMode));
        OnPropertyChanged(nameof(IsT21EditorMode));
        OnPropertyChanged(nameof(IsAnalysisMode));
        OnPropertyChanged(nameof(ShowMainEditorPanel));
        OnPropertyChanged(nameof(ShowExemplarPropertiesPanel));
        RefreshDisplayedEntries();
    }

    public bool IsSc4EditorMode
    {
        get => _editorMode == EditorMode.Sc4;
        set { if (value) SetEditorMode(EditorMode.Sc4); }
    }

    public bool IsLtextEditorMode
    {
        get => _editorMode == EditorMode.Ltext;
        set { if (value) SetEditorMode(EditorMode.Ltext); }
    }

    public bool IsS3DEditorMode
    {
        get => _editorMode == EditorMode.S3D;
        set { if (value) SetEditorMode(EditorMode.S3D); }
    }

    public bool IsLuaEditorMode
    {
        get => _editorMode == EditorMode.Lua;
        set { if (value) SetEditorMode(EditorMode.Lua); }
    }

    public bool IsUiEditorMode
    {
        get => _editorMode == EditorMode.Ui;
        set { if (value) SetEditorMode(EditorMode.Ui); }
    }

    public bool IsT21EditorMode
    {
        get => _editorMode == EditorMode.T21;
        set { if (value) SetEditorMode(EditorMode.T21); }
    }

    /// <summary>
    /// "Analysis" mode (same radio group as SC4/LUA Editor etc.): swaps the TGI editor/
    /// preview panel for the Analysis tools panel (Find/Index Analyser, Plugins Analyser,
    /// Exemplar/Cohort Analyser, Property Find/Count, Property Manager - Ilive Reader's
    /// DlgIdxAnalyser/DlgPropFind*/DlgPropCount/FormProperty). No entry-list filtering of
    /// its own (behaves like SC4 Editor for the list itself), it's just a different tools
    /// panel.
    /// </summary>
    public bool IsAnalysisMode
    {
        get => _editorMode == EditorMode.Analysis;
        set { if (value) SetEditorMode(EditorMode.Analysis); }
    }

    // ---------------------------------------------------------------
    // Analysis mode's own five tools (Find/Index Analyser, Plugins Analyser, Exemplar/Cohort
    // Analyser, Property Find/Count, Property Manager) used to be TabItems inside a nested
    // TabControl, with their own tab strip - that strip wrapped onto a second line as soon
    // as the window wasn't wide enough for all five headers, which looked broken. Replaced
    // with the same "radio buttons in a box below the EDITOR: row" pattern already used for
    // the outer Sc4/Ltext/S3D/Lua/Ui/T21/Analysis switch, for one consistent look
    // and no more wrapping tab strip.
    // ---------------------------------------------------------------

    private enum AnalysisSubMode { Find, PluginsAnalyser, ExemplarAnalyser, PropertyFind, PropertyManager, UiElements }

    private AnalysisSubMode _analysisSubMode = AnalysisSubMode.Find;

    private void SetAnalysisSubMode(AnalysisSubMode mode)
    {
        if (_analysisSubMode == mode)
        {
            return;
        }

        _analysisSubMode = mode;
        OnPropertyChanged(nameof(IsAnalysisFindMode));
        OnPropertyChanged(nameof(IsAnalysisPluginsMode));
        OnPropertyChanged(nameof(IsAnalysisExemplarMode));
        OnPropertyChanged(nameof(IsAnalysisPropertyFindMode));
        OnPropertyChanged(nameof(IsAnalysisPropertyManagerMode));
        OnPropertyChanged(nameof(IsAnalysisUiElementsMode));
    }

    public bool IsAnalysisFindMode
    {
        get => _analysisSubMode == AnalysisSubMode.Find;
        set { if (value) SetAnalysisSubMode(AnalysisSubMode.Find); }
    }

    public bool IsAnalysisPluginsMode
    {
        get => _analysisSubMode == AnalysisSubMode.PluginsAnalyser;
        set { if (value) SetAnalysisSubMode(AnalysisSubMode.PluginsAnalyser); }
    }

    public bool IsAnalysisExemplarMode
    {
        get => _analysisSubMode == AnalysisSubMode.ExemplarAnalyser;
        set { if (value) SetAnalysisSubMode(AnalysisSubMode.ExemplarAnalyser); }
    }

    public bool IsAnalysisPropertyFindMode
    {
        get => _analysisSubMode == AnalysisSubMode.PropertyFind;
        set { if (value) SetAnalysisSubMode(AnalysisSubMode.PropertyFind); }
    }

    public bool IsAnalysisPropertyManagerMode
    {
        get => _analysisSubMode == AnalysisSubMode.PropertyManager;
        set { if (value) SetAnalysisSubMode(AnalysisSubMode.PropertyManager); }
    }

    public bool IsAnalysisUiElementsMode
    {
        get => _analysisSubMode == AnalysisSubMode.UiElements;
        set { if (value) SetAnalysisSubMode(AnalysisSubMode.UiElements); }
    }


    /// <summary>True while the ordinary TGI editor/preview panel (Grid.Column="1") should show - hidden for LUA Editor, Analysis and UI Editor modes, which each replace that whole panel with their own.</summary>
    public bool ShowMainEditorPanel => !IsLuaEditorMode && !IsAnalysisMode && !IsUiEditorMode && !IsT21EditorMode;


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
            return IsLtextFamilyEntry(vm);
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

    /// <summary>
    /// The underlying DBPF service for this document. Exposed so multi-file tools that need to
    /// work across several open documents at once - Compare/Merge/Directory sync, all reached
    /// from the MDI shell (<see cref="Views.MainWindow"/>/MainWindowShellViewModel) - can read
    /// this document's entries/path without duplicating DbpfService's own logic.
    /// </summary>
    public DbpfService Service => _service;

    /// <summary>Short label for this document's MDI tab header (just the file name, or "(new file)").</summary>
    public string DocumentTitle => _service.CurrentPath is null ? "(new file)" : Path.GetFileName(_service.CurrentPath);

    /// <summary>
    /// Selects the entry with the given TGI in the main list, if present - used by the
    /// Directory sync dialog's "Select in list" action (Ilive Reader's DlgDirectory::OnMenuSync).
    /// </summary>
    public void SelectEntryByTgi(TGI tgi) =>
        SelectedEntry = Entries.FirstOrDefault(e =>
            e.Entry.TGI.TypeID == tgi.TypeID && e.Entry.TGI.GroupID == tgi.GroupID && e.Entry.TGI.InstanceID == tgi.InstanceID);

    /// <summary>
    /// Lets shell-level batch tools (Clone/TGI Editor/Group Patch/Insert Batch/Insert
    /// Template/Reindex - opened from DbpfWorkspaceView's code-behind, same pattern as
    /// every other dialog in this app) post a one-line update to this document's own
    /// status bar, without needing their own separate notification UI.
    /// </summary>
    public void SetStatusMessage(string message) => StatusMessage = message;

    // ---------------------------------------------------------------
    // Exemplar/Cohort property list (populated when the selected entry is one)
    // ---------------------------------------------------------------

    public ObservableCollection<PropertyItemViewModel> Properties { get; } = new();

    /// <summary>The exemplar/cohort currently selected, or null if the selected entry isn't one.</summary>
    public DBPFEntryEXMP? SelectedExemplar => SelectedEntry?.Entry as DBPFEntryEXMP;

    public bool IsExemplarSelected => SelectedExemplar is not null;

    /// <summary>
    /// Whether the raw EXEMPLAR/COHORT PROPERTIES panel (Grid.Column="2") should show.
    /// True under the same condition as before (<see cref="IsExemplarSelected"/>) except
    /// while the T21 Editor is active - the T21 Editor's own dedicated fields replace it
    /// there, and showing both side by side was redundant/confusing (and the raw panel's
    /// ADD/EDIT/REMOVE buttons let a person accidentally edit properties SAVE T21 would
    /// then immediately overwrite anyway).
    /// </summary>
    public bool ShowExemplarPropertiesPanel => IsExemplarSelected && !IsT21EditorMode;

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
        private set
        {
            if (SetField(ref _selectedS3DModel, value))
            {
                ApplyS3DCommand?.RaiseCanExecuteChanged();
                MergeS3DCommand?.RaiseCanExecuteChanged();
            }
        }
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

    // ---------------------------------------------------------------
    // S3D Editor: per-group visibility toggles + animation playback. Both are gated to
    // "S3D Editor mode only" in MainWindow.axaml (IsEnabled="{Binding IsS3DEditorMode}"),
    // same as APPLY/SAVE, MERGE, IMPORT/EXPORT 3DS.
    // ---------------------------------------------------------------

    public ObservableCollection<S3DGroupToggleViewModel> S3DGroupToggles { get; } = new();

    private HashSet<int> _s3DHiddenGroupIndices = new();
    public IReadOnlySet<int> S3DHiddenGroupIndices => _s3DHiddenGroupIndices;

    private void OnS3DGroupToggleChanged()
    {
        _s3DHiddenGroupIndices = S3DGroupToggles.Where(g => !g.IsVisible).Select(g => g.Index).ToHashSet();
        OnPropertyChanged(nameof(S3DHiddenGroupIndices));
    }

    /// <summary>Rebuilds the group toggle list from the current model - one row per animation mesh (or raw group, for non-animated models), matching the grouping <see cref="S3DModel.EnumerateTriangles"/> iterates over. Called whenever the group count could have changed (load, merge, 3DS import).</summary>
    private void RefreshS3DGroupToggles()
    {
        S3DGroupToggles.Clear();
        _s3DHiddenGroupIndices = new HashSet<int>();

        if (SelectedS3DModel is { } model)
        {
            if (model.Animation.Meshes.Count > 0)
            {
                for (var i = 0; i < model.Animation.Meshes.Count; i++)
                {
                    var name = model.Animation.Meshes[i].Name;
                    S3DGroupToggles.Add(new S3DGroupToggleViewModel(i, string.IsNullOrWhiteSpace(name) ? $"Group {i}" : name, OnS3DGroupToggleChanged));
                }
            }
            else
            {
                var groupCount = Math.Min(model.IndexBlocks.Count, model.PrimBlocks.Count);
                for (var i = 0; i < groupCount; i++)
                {
                    S3DGroupToggles.Add(new S3DGroupToggleViewModel(i, $"Group {i}", OnS3DGroupToggleChanged));
                }
            }
        }

        OnPropertyChanged(nameof(S3DHiddenGroupIndices));
        StopS3DAnimation();
        OnPropertyChanged(nameof(S3DFrameCount));
        PlayS3DCommand?.RaiseCanExecuteChanged();
    }

    /// <summary>Highest per-mesh frame count in the current model's animation - 1 for a non-animated (or unselected) model, in which case Play/Pause/Stop have nothing to do.</summary>
    public int S3DFrameCount =>
        SelectedS3DModel is { } model && model.Animation.Meshes.Count > 0
            ? model.Animation.Meshes.Max(m => m.Frames.Count)
            : 1;

    private int _s3DCurrentFrame;
    public int S3DCurrentFrame
    {
        get => _s3DCurrentFrame;
        private set => SetField(ref _s3DCurrentFrame, value);
    }

    private bool _isS3DPlaying;
    public bool IsS3DPlaying
    {
        get => _isS3DPlaying;
        private set
        {
            if (SetField(ref _isS3DPlaying, value))
            {
                PlayS3DCommand.RaiseCanExecuteChanged();
                PauseS3DCommand.RaiseCanExecuteChanged();
                StopS3DCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand PlayS3DCommand { get; }
    public RelayCommand PauseS3DCommand { get; }
    public RelayCommand StopS3DCommand { get; }

    private DispatcherTimer? _s3DPlaybackTimer;

    /// <summary>"PLAY" - advances S3DCurrentFrame on a timer at the model's own FrameRate (Ilive Reader's ANIM chunk field; falls back to a sane 10 fps default for the rare model with FrameRate == 0).</summary>
    private void PlayS3DAnimation()
    {
        if (SelectedS3DModel is not { } model || S3DFrameCount <= 1)
        {
            return;
        }

        var fps = model.Animation.FrameRate > 0 ? model.Animation.FrameRate : 10;
        _s3DPlaybackTimer ??= new DispatcherTimer();
        _s3DPlaybackTimer.Interval = TimeSpan.FromSeconds(1.0 / fps);
        _s3DPlaybackTimer.Tick -= OnS3DPlaybackTick;
        _s3DPlaybackTimer.Tick += OnS3DPlaybackTick;
        _s3DPlaybackTimer.Start();
        IsS3DPlaying = true;
    }

    private void OnS3DPlaybackTick(object? sender, EventArgs e)
    {
        var frameCount = S3DFrameCount;
        S3DCurrentFrame = frameCount <= 1 ? 0 : (S3DCurrentFrame + 1) % frameCount;
    }

    private void PauseS3DAnimation()
    {
        _s3DPlaybackTimer?.Stop();
        IsS3DPlaying = false;
    }

    private void StopS3DAnimation()
    {
        _s3DPlaybackTimer?.Stop();
        IsS3DPlaying = false;
        S3DCurrentFrame = 0;
    }

    // ---------------------------------------------------------------
    // S3D Editor: VERT/INDX/PRIM grid editors (Tab3DMVert/Indx/Prim equivalents - see
    // Models/S3DEditOps.cs for the actual mutation logic and S3DEditRowViewModels.cs for
    // the row wrappers bound to the DataGrids in MainWindow.axaml). All editing operates
    // on one "editing group" at a time (S3DEditGroupIndex), independent of the per-group
    // visibility toggles/animation playback above - editing always shows the raw VERT/
    // INDX/PRIM block content regardless of which animation frame is currently playing.
    // ---------------------------------------------------------------

    private int _s3DEditGroupIndex;
    public int S3DEditGroupIndex
    {
        get => _s3DEditGroupIndex;
        set
        {
            var clamped = Math.Max(0, value);
            if (SetField(ref _s3DEditGroupIndex, clamped))
            {
                RefreshS3DEditRows();
                S3DUVSelectedIndex = -1;
            }
        }
    }

    /// <summary>Highest valid <see cref="S3DEditGroupIndex"/> for the current model - the largest of the three (usually equal) block counts, so switching there and adding is always possible even if the arrays are momentarily uneven.</summary>
    public int S3DEditGroupMaxIndex =>
        SelectedS3DModel is { } model
            ? Math.Max(0, Math.Max(model.VertexBlocks.Count, Math.Max(model.IndexBlocks.Count, model.PrimBlocks.Count)) - 1)
            : 0;

    private int _s3DAddCount = 1;

    /// <summary>"N" - how many points/triangle rows/primitive rows the next Add click appends. Doubles as the single-vs-multiple control from the feature request: leave at 1 for a single add, raise it to add several at once.</summary>
    public int S3DAddCount
    {
        get => _s3DAddCount;
        set => SetField(ref _s3DAddCount, Math.Max(1, value));
    }

    public ObservableCollection<S3DVertexRowViewModel> S3DVertexRows { get; } = new();
    public ObservableCollection<S3DIndexRowViewModel> S3DIndexRows { get; } = new();
    public ObservableCollection<S3DPrimRowViewModel> S3DPrimRows { get; } = new();

    private S3DIndexRowViewModel? _selectedS3DIndexRow;

    /// <summary>Bound to the Indices DataGrid's SelectedItem - the grid->viewer half of the bidirectional selection link (see also <see cref="OnS3DTrianglePicked"/> for the viewer->grid half).</summary>
    public S3DIndexRowViewModel? SelectedS3DIndexRow
    {
        get => _selectedS3DIndexRow;
        set
        {
            if (SetField(ref _selectedS3DIndexRow, value))
            {
                S3DHighlightTriangle = value is null ? null : (S3DEditGroupIndex, value.T1, value.T2, value.T3);
            }
        }
    }

    private (int Group, int A, int B, int C)? _s3DHighlightTriangle;

    /// <summary>Bound (one-way, VM->view) to S3DViewerControl.HighlightTriangle - draws a highlighted outline over the selected/picked triangle.</summary>
    public (int Group, int A, int B, int C)? S3DHighlightTriangle
    {
        get => _s3DHighlightTriangle;
        private set => SetField(ref _s3DHighlightTriangle, value);
    }

    public RelayCommand AddS3DVertexCommand { get; }
    public RelayCommand AddS3DIndexCommand { get; }
    public RelayCommand AddS3DPrimCommand { get; }
    public RelayCommand AddS3DGroupCommand { get; }
    public RelayCommand DeleteS3DGroupCommand { get; }
    public RelayCommand FlipS3DXyCommand { get; }
    public RelayCommand FlipS3DXzCommand { get; }
    public RelayCommand FlipS3DYzCommand { get; }
    public RelayCommand RemapS3DIndicesCommand { get; }

    /// <summary>Invoked by S3DViewerControl.PickCommand on a plain click (see S3DViewerControl.OnPointerReleased) - the viewer->grid half of the selection link.</summary>
    public RelayCommand PickS3DTriangleCommand { get; }

    /// <summary>Rebuilds the three grids' row collections from the current model's editing group - called on load/merge/3DS-import (group content/count can change) and whenever <see cref="S3DEditGroupIndex"/> changes. A single-cell edit inside a row (e.g. typing a new X) does NOT call this - it only repaints the viewer (see the row ViewModels' onChanged callback), so the grids don't lose their scroll position/selection on every keystroke.</summary>
    private void RefreshS3DEditRows()
    {
        S3DVertexRows.Clear();
        S3DIndexRows.Clear();
        S3DPrimRows.Clear();
        SelectedS3DIndexRow = null;

        if (SelectedS3DModel is { } model)
        {
            var g = S3DEditGroupIndex;

            if (g < model.VertexBlocks.Count)
            {
                var block = model.VertexBlocks[g];
                for (var i = 0; i < block.Positions.Count; i++)
                {
                    S3DVertexRows.Add(new S3DVertexRowViewModel(block, i, OnS3DGeometryEdited));
                }
            }

            if (g < model.IndexBlocks.Count)
            {
                var block = model.IndexBlocks[g];
                var triCount = block.Indices.Count / 3;
                for (var i = 0; i < triCount; i++)
                {
                    S3DIndexRows.Add(new S3DIndexRowViewModel(block, i, OnS3DGeometryEdited));
                }
            }

            if (g < model.PrimBlocks.Count)
            {
                var block = model.PrimBlocks[g];
                for (var i = 0; i < block.Primitives.Count; i++)
                {
                    S3DPrimRows.Add(new S3DPrimRowViewModel(block, i, OnS3DGeometryEdited));
                }
            }
        }

        OnPropertyChanged(nameof(S3DEditGroupMaxIndex));

        // UV Editor operates on the same "editing group" as the Vertices/Indices grids
        // above - re-notify its bound block references here too, one place instead of at
        // every RefreshS3DEditRows() call site (load/merge/3DS-import/group index change).
        OnPropertyChanged(nameof(S3DUVVertexBlock));
        OnPropertyChanged(nameof(S3DUVIndexBlock));
    }

    private void OnS3DGeometryEdited() => ForceS3DViewerRefresh();

    private void AddS3DVertexPoints()
    {
        if (SelectedS3DModel is not { } model || S3DEditGroupIndex >= model.VertexBlocks.Count)
        {
            return;
        }

        S3DEditOps.AddVertexPoints(model.VertexBlocks[S3DEditGroupIndex], S3DAddCount);
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = $"Added {S3DAddCount} point(s) to group {S3DEditGroupIndex}.";
    }

    /// <summary>"Delete point (single or multiple)" - called from MainWindow.axaml.cs with the Vertices grid's currently selected row indices.</summary>
    public void DeleteS3DVertexPoints(IReadOnlyList<int> indices)
    {
        if (SelectedS3DModel is not { } model || S3DEditGroupIndex >= model.VertexBlocks.Count || indices.Count == 0)
        {
            return;
        }

        S3DEditOps.RemoveVertexPoints(model.VertexBlocks[S3DEditGroupIndex], indices);
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = $"Deleted {indices.Count} point(s) from group {S3DEditGroupIndex}.";
    }

    private void AddS3DIndexTriangles()
    {
        if (SelectedS3DModel is not { } model || S3DEditGroupIndex >= model.IndexBlocks.Count)
        {
            return;
        }

        S3DEditOps.AddIndexTriangles(model.IndexBlocks[S3DEditGroupIndex], S3DAddCount);
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = $"Added {S3DAddCount} triangle row(s) to group {S3DEditGroupIndex}.";
    }

    public void DeleteS3DIndexTriangles(IReadOnlyList<int> rowIndices)
    {
        if (SelectedS3DModel is not { } model || S3DEditGroupIndex >= model.IndexBlocks.Count || rowIndices.Count == 0)
        {
            return;
        }

        S3DEditOps.RemoveIndexTriangles(model.IndexBlocks[S3DEditGroupIndex], rowIndices);
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = $"Deleted {rowIndices.Count} triangle row(s) from group {S3DEditGroupIndex}.";
    }

    private void AddS3DPrimRow()
    {
        if (SelectedS3DModel is not { } model || S3DEditGroupIndex >= model.PrimBlocks.Count)
        {
            return;
        }

        for (var i = 0; i < S3DAddCount; i++)
        {
            S3DEditOps.AddPrimRow(model.PrimBlocks[S3DEditGroupIndex]);
        }

        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = $"Added {S3DAddCount} primitive row(s) to group {S3DEditGroupIndex}.";
    }

    public void DeleteS3DPrimRows(IReadOnlyList<int> rowIndices)
    {
        if (SelectedS3DModel is not { } model || S3DEditGroupIndex >= model.PrimBlocks.Count || rowIndices.Count == 0)
        {
            return;
        }

        S3DEditOps.RemovePrimRows(model.PrimBlocks[S3DEditGroupIndex], rowIndices);
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = $"Deleted {rowIndices.Count} primitive row(s) from group {S3DEditGroupIndex}.";
    }

    private void AddS3DGroup()
    {
        if (SelectedS3DModel is not { } model)
        {
            return;
        }

        S3DEditOps.AddGroup(model);
        S3DEditGroupIndex = model.VertexBlocks.Count - 1;
        RefreshS3DGroupToggles();
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = "Added a new empty group.";
    }

    private void DeleteS3DGroup()
    {
        if (SelectedS3DModel is not { } model)
        {
            return;
        }

        var g = S3DEditGroupIndex;
        if (g < 0 || g >= model.VertexBlocks.Count)
        {
            return;
        }

        var hadAnimation = model.Animation.Meshes.Count > 0;
        S3DEditOps.RemoveGroup(model, g);
        S3DEditGroupIndex = Math.Max(0, g - 1);
        RefreshS3DGroupToggles();
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = hadAnimation
            ? $"Deleted group {g}. Note: this model has animation data - frame block references were not renumbered, check the ANIM chunk before saving."
            : $"Deleted group {g}.";
    }

    private void FlipS3D(char axisA, char axisB)
    {
        if (SelectedS3DModel is not { } model)
        {
            return;
        }

        S3DEditOps.FlipAxes(model, axisA, axisB);
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = $"Flipped {char.ToUpperInvariant(axisA)}{char.ToUpperInvariant(axisB)} across every group.";
    }

    private void RemapS3DIndices()
    {
        if (SelectedS3DModel is not { } model || S3DEditGroupIndex >= model.IndexBlocks.Count || S3DEditGroupIndex >= model.VertexBlocks.Count)
        {
            return;
        }

        var vertexCount = model.VertexBlocks[S3DEditGroupIndex].Positions.Count;
        var fixedCount = S3DEditOps.RemapIndices(model.IndexBlocks[S3DEditGroupIndex], vertexCount);
        RefreshS3DEditRows();
        ForceS3DViewerRefresh();
        StatusMessage = fixedCount > 0
            ? $"Remapped {fixedCount} out-of-range index/indices in group {S3DEditGroupIndex} to the valid [0, {vertexCount}) range."
            : $"All indices in group {S3DEditGroupIndex} already reference valid vertices - nothing to remap.";
    }

    /// <summary>Viewer->grid half of the selection link: a plain click in the S3DViewerControl picked this triangle (or missed everything, if null). See MainWindow.axaml's S3DViewerControl.PickCommand binding.</summary>
    private void OnS3DTrianglePicked((int Group, int A, int B, int C)? picked)
    {
        if (picked is not { } p)
        {
            SelectedS3DIndexRow = null;
            S3DHighlightTriangle = null;
            return;
        }

        if (p.Group != S3DEditGroupIndex)
        {
            S3DEditGroupIndex = p.Group;
        }

        var match = S3DIndexRows.FirstOrDefault(r =>
        {
            var set = new HashSet<int> { r.T1, r.T2, r.T3 };
            return set.Count == 3 && set.Contains(p.A) && set.Contains(p.B) && set.Contains(p.C);
        });

        if (match is not null)
        {
            SelectedS3DIndexRow = match;
        }
        else
        {
            SelectedS3DIndexRow = null;
            S3DHighlightTriangle = (p.Group, p.A, p.B, p.C);
            StatusMessage = "Picked triangle is part of a strip/fan primitive, so no single Indices grid row matches it exactly (same limitation Ilive Reader's own index grid has for non-triangle-list primitives) - highlighted in the viewer only.";
        }
    }

    private SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>? _s3DTexture;
    public SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>? S3DTexture
    {
        get => _s3DTexture;
        private set => SetField(ref _s3DTexture, value);
    }

    /// <summary>
    /// Every material's own resolved texture (material index -&gt; decoded bitmap), day/night
    /// aware like <see cref="S3DTexture"/> - unlike that single "primary" texture (used by the
    /// UV/Material editors, which always work on one editing group at a time), this is what the
    /// 3D viewer's Solid render mode needs: multi-material models must sample each group's UVs
    /// against that group's own material texture (see <see cref="S3DModel.GetMaterialIndex"/>),
    /// not one texture applied to every group. A material with no resolvable texture is simply
    /// absent from the dictionary (falls back to the flat placeholder color in the viewer).
    /// </summary>
    private Dictionary<int, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>> _s3DMaterialTextures = new();
    public IReadOnlyDictionary<int, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>> S3DMaterialTextures
    {
        get => _s3DMaterialTextures;
        private set => SetField(ref _s3DMaterialTextures, (Dictionary<int, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>>)value);
    }

    /// <summary>
    /// Every material's own <b>dedicated</b> night-lighting texture (instance ID = day ID +
    /// <see cref="NightInstanceOffset"/>) - sparse: a material with no separate night FSH
    /// simply has no entry here, regardless of <see cref="IsS3DNight"/>. Unlike
    /// <see cref="S3DMaterialTextures"/> (which holds whichever *one* texture the Day/Night
    /// toggle currently selects, for the UV Editor and the simple flat texture fallback),
    /// this is resolved unconditionally so the viewer's Night render can genuinely blend
    /// both the day and the dedicated night texture together per the "Night Render = DayModel
    /// * NightColour * (1 - NightAlpha) + NightModel * NightAlpha" formula, instead of just
    /// swapping which single texture is shown.
    /// </summary>
    private Dictionary<int, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>> _s3DMaterialNightTextures = new();
    public IReadOnlyDictionary<int, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>> S3DMaterialNightTextures
    {
        get => _s3DMaterialNightTextures;
        private set => SetField(ref _s3DMaterialNightTextures, (Dictionary<int, SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>>)value);
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
                RefreshS3DTexture();
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
                RefreshS3DTexture();
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
        OnPropertyChanged(nameof(DocumentTitle));
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
            OnPropertyChanged(nameof(DocumentTitle));
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
            var warning = EntryExporter.ExportEntryTo(SelectedEntry.Entry, filePath);
            StatusMessage = warning is null
                ? $"Entry exported to: {filePath}"
                : $"Entry exported to: {filePath} — WARNING: {warning}";
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

    /// <summary>
    /// Writes <paramref name="code"/> (UTF-8) back into the selected entry's raw bytes -
    /// used by the LUA Editor's "SAVE" button. Same swap-the-entry-object mechanics as
    /// <see cref="ImportIntoSelectedEntry"/> (TGI is preserved; only the payload changes).
    /// Like every other entry edit in this app, this only updates the in-memory package -
    /// the main toolbar's Save/Save As is still what writes it to disk.
    /// </summary>
    public void SaveLuaScriptToSelectedEntry(string code)
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(code);
            var oldVm = SelectedEntry;
            var index = Entries.IndexOf(oldVm);

            var newEntry = _service.ReplaceEntryBytes(oldVm.Entry, bytes);

            // Belt-and-suspenders: if the entry's IsCompressed flag ends up out of sync with
            // the plain-text bytes just written (would make DbpfWriter tag uncompressed bytes
            // as "compressed" on disk, and break the next read via QFS.Decompress on non-QFS
            // data - same per-entry try/catch as SetAllEntriesCompression, since Decode/Encode
            // aren't guaranteed to be supported for every entry type csDBPF can construct).
            try
            {
                _service.SetEntryCompression(newEntry, false);
            }
            catch
            {
                // Not fatal - the script bytes themselves are already saved correctly above.
            }

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
            StatusMessage = $"LUA script saved into {newVm.TypeHex}/{newVm.GroupHex}/{newVm.InstanceHex} (remember to save the package).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while saving LUA script: {ex.Message}";
        }
    }

    /// <summary>Exports every entry currently in the package into <paramref name="folder"/>.</summary>
    public void ExportAllEntries(string folder)
    {
        try
        {
            var (succeeded, failed, warnings) = EntryExporter.ExportAll(_service.Entries, folder);
            StatusMessage = (failed, warnings) switch
            {
                (0, 0) => $"{succeeded} entries exported to: {folder}",
                (0, > 0) => $"{succeeded} entries exported to: {folder} — {warnings} with a validation warning, see each entry's export individually",
                _ => $"{succeeded} entries exported ({warnings} with a warning), {failed} skipped (error), to: {folder}",
            };
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while exporting: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------
    // Entry list / details / editing
    // ---------------------------------------------------------------

    /// <summary>
    /// Re-reads every entry from <see cref="Service"/> into <see cref="Entries"/>/
    /// <see cref="DisplayedEntries"/>. Public (not just used internally after Open/New) so
    /// tools that mutate <see cref="Service"/>.CurrentFile directly - e.g. the Merge dialog's
    /// "merge into this open document" mode - can refresh the list afterwards without going
    /// through OpenFile/CreateNewPackage.
    /// </summary>
    public void ReloadEntries()
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
        LuaSaveCommand.RaiseCanExecuteChanged();

        Properties.Clear();
        SelectedProperty = null;
        OnPropertyChanged(nameof(SelectedExemplar));
        OnPropertyChanged(nameof(IsExemplarSelected));
        OnPropertyChanged(nameof(ShowExemplarPropertiesPanel));

        PreviewImages.Clear();
        SelectedPreviewImage = null;
        ZoomFactor = 1.0;
        OnPropertyChanged(nameof(HasImagePreview));
        OnPropertyChanged(nameof(ShowTextDetails));

        StopS3DAnimation();
        SelectedS3DModel = null;
        S3DInfo = string.Empty;
        DisposeS3DTexture();
        IsS3DWireframe = true;
        IsS3DSolid = false;
        RefreshS3DGroupToggles();
        S3DEditGroupIndex = 0;
        RefreshS3DEditRows();
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
            LoadLtextEditorForSelectedEntry();
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
        LoadLuaEditorForSelectedEntry();
        LoadUiEditorForSelectedEntry();
        LoadT21EditorForSelectedEntry();
        LoadLtextEditorForSelectedEntry();
    }

    // ---------------------------------------------------------------
    // LUA Editor: inline in the main window (Grid.Column 1), only shown while
    // IsLuaEditorMode is active - see MainWindow.axaml. Loads the selected script entry's
    // text on selection; COMPILE/RUN go through Models/LuaScriptRunner (MoonSharp); SAVE
    // writes back into the entry via SaveLuaScriptToSelectedEntry, same as every other
    // in-place entry edit in this app.
    // ---------------------------------------------------------------

    public RelayCommand LuaCompileCommand { get; }
    public RelayCommand LuaRunCommand { get; }
    public RelayCommand LuaClearOutputCommand { get; }
    public RelayCommand LuaSaveCommand { get; }

    private string _luaCode = string.Empty;
    public string LuaCode
    {
        get => _luaCode;
        set => SetField(ref _luaCode, value);
    }

    private string _luaOutput = string.Empty;
    public string LuaOutput
    {
        get => _luaOutput;
        private set => SetField(ref _luaOutput, value);
    }

    private void LoadLuaEditorForSelectedEntry()
    {
        LuaOutput = string.Empty;

        if (SelectedEntry is null)
        {
            LuaCode = string.Empty;
            return;
        }

        // Same try/catch-and-report pattern as LoadSimplePreview/LoadWavPreview: QFS.Decompress
        // can throw on bytes it doesn't recognize as actually QFS-compressed (e.g. right after
        // SaveLuaScriptToSelectedEntry writes plain-text bytes into an entry whose IsCompressed
        // flag is still whatever it was before) - this must never crash the save/select flow.
        try
        {
            var bytes = RawEntryBytes.GetDecompressed(SelectedEntry.Entry) ?? Array.Empty<byte>();
            LuaCode = Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            LuaCode = string.Empty;
            StatusMessage = $"Error reading LUA script: {ex.Message}";
        }
    }

    private void AppendLuaOutput(string line)
    {
        LuaOutput = LuaOutput.Length == 0 ? line : LuaOutput + Environment.NewLine + line;
    }

    private void LuaCompile()
    {
        LuaScriptRunner.TryCompile(LuaCode, out var message);
        AppendLuaOutput($"[compile] {message}");
    }

    private void LuaRun()
    {
        AppendLuaOutput("--- run ---");
        LuaScriptRunner.TryRun(LuaCode, AppendLuaOutput, out var message);
        AppendLuaOutput($"[run] {message}");
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
            var triCount = model.EnumerateTriangles().Count();

            _s3DModelGroupId = SelectedEntry.Entry.TGI.GroupID;
            var textureStatus = ResolveS3DTexture(model, _s3DModelGroupId, IsS3DNight);

            S3DInfo =
                $"S3D v{model.MajorRevision}.{model.MinorRevision} — " +
                $"{model.VertexBlocks.Count} groups, {model.TotalVertexCount:N0} vertices, {triCount:N0} triangles, " +
                $"{model.MaterialCount} materials{(model.HasAnimation ? ", animated" : "")}. " +
                $"{textureStatus} Drag to rotate, scroll to zoom.";

            RefreshS3DMergeCandidates();
            RefreshS3DGroupToggles();
            S3DEditGroupIndex = 0;
            RefreshS3DEditRows();
            RefreshS3DMaterialRows();
            RefreshS3DPackageFshEntries();
            RefreshS3DAnimMeshes();
            RefreshS3DPropRows();
            RefreshS3DRegPoints();
        }
        catch (Exception ex)
        {
            S3DInfo = $"(Error reading S3D model: {ex.Message})";
        }

        OnPropertyChanged(nameof(HasS3DPreview));
        OnPropertyChanged(nameof(ShowTextDetails));
    }

    /// <summary>Group ID of the currently previewed S3D model - needed to re-resolve its texture when Day/Night is toggled without reparsing the model.</summary>
    private uint _s3DModelGroupId;

    private const uint FshTypeId = 0x7AB50E44;

    /// <summary>
    /// Night texture instance ID offset from the day one, per SC4 modding convention (also
    /// how Ilive Reader itself resolves night textures - see <c>GlViewS3D::InitTexture</c>,
    /// <c>reader/GlViewS3D.cpp</c>: <c>if (m_bNight) dwInstance += 0x8000;</c>, falling back
    /// to the day instance ID if no dedicated night texture exists).
    /// </summary>
    private const uint NightInstanceOffset = 0x8000;

    /// <summary>Re-resolves the S3D texture for the currently previewed model - called when the Day/Night toggle changes, without reparsing the model itself.</summary>
    private void RefreshS3DTexture()
    {
        if (SelectedS3DModel is not { } model)
        {
            return;
        }

        var textureStatus = ResolveS3DTexture(model, _s3DModelGroupId, IsS3DNight);
        var triCount = model.EnumerateTriangles().Count();
        S3DInfo =
            $"S3D v{model.MajorRevision}.{model.MinorRevision} — " +
            $"{model.VertexBlocks.Count} groups, {model.TotalVertexCount:N0} vertices, {triCount:N0} triangles, " +
            $"{model.MaterialCount} materials{(model.HasAnimation ? ", animated" : "")}. " +
            $"{textureStatus} Drag to rotate, scroll to zoom.";
    }

    /// <summary>
    /// Looks up and decodes the FSH texture referenced by the model's primary material, if
    /// any, storing the result in <see cref="S3DTexture"/> - and, alongside it, resolves every
    /// other material's own texture into <see cref="S3DMaterialTextures"/> (see that
    /// property's remarks for why both are needed). When <paramref name="night"/> is
    /// true, looks for the dedicated night-lighting texture first (instance ID = day ID +
    /// <see cref="NightInstanceOffset"/> - many SC4 building/prop models ship a separate,
    /// pre-lit FSH for night), falling back to the day texture if no night-specific one
    /// exists. Returns a short status phrase for the info line (about the primary texture only).
    /// </summary>
    private string ResolveS3DTexture(S3DModel model, uint modelGroupId, bool night)
    {
        DisposeS3DTexture();

        for (var materialIndex = 0; materialIndex < model.Materials.Count; materialIndex++)
        {
            var textureId = model.Materials[materialIndex].Textures.Select(t => t.TextureId).FirstOrDefault(id => id != 0);
            if (textureId == 0)
            {
                continue;
            }

            if (TryDecodeS3DTexture(textureId, modelGroupId, night, out var materialImage, out _))
            {
                _s3DMaterialTextures[materialIndex] = materialImage!.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
            }

            // Resolved unconditionally (not gated on `night`) so the viewer always has both
            // halves of the day/night blend formula ready the moment Night mode is toggled
            // on, without needing a full re-resolve. Sparse by design: a material with no
            // dedicated night FSH at textureId + NightInstanceOffset simply gets no entry.
            if (FindFshEntry(textureId + NightInstanceOffset, modelGroupId) is { } nightFshEntry)
            {
                try
                {
                    nightFshEntry.Decode();
                    if ((nightFshEntry as DBPFEntryFSH)?.Image is { } nightImage)
                    {
                        _s3DMaterialNightTextures[materialIndex] = nightImage.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                    }
                }
                catch
                {
                    // No dedicated night texture usable for this material - the viewer falls
                    // back to the day-only tinted render for it, same as before this feature.
                }
            }
        }

        OnPropertyChanged(nameof(S3DMaterialTextures));
        OnPropertyChanged(nameof(S3DMaterialNightTextures));

        if (model.PrimaryTextureId is not { } dayTextureId)
        {
            return "No texture reference in the model.";
        }

        if (!TryDecodeS3DTexture(dayTextureId, modelGroupId, night, out var image, out var usedNightTexture))
        {
            return image is null && usedNightTexture is null
                ? "Referenced texture not found in this package."
                : "Could not decode the referenced texture.";
        }

        S3DTexture = image!.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
        return night
            ? (usedNightTexture == true ? "Night texture found." : "No dedicated night texture - showing day texture.")
            : "Texture found.";
    }

    /// <summary>
    /// Shared day/night FSH lookup+decode for one texture instance ID - the common core of
    /// resolving the primary texture (<see cref="S3DTexture"/>) and every per-material
    /// texture (<see cref="S3DMaterialTextures"/>) alike. Returns false if nothing could be
    /// decoded; <paramref name="usedNightTexture"/> is null when no FSH entry was found at
    /// all (day or night), so callers can tell "not found" apart from "found but undecodable".
    /// </summary>
    private bool TryDecodeS3DTexture(uint dayTextureId, uint modelGroupId, bool night, out ImgSharpImage? image, out bool? usedNightTexture)
    {
        image = null;
        usedNightTexture = null;

        var fshEntry = night ? FindFshEntry(dayTextureId + NightInstanceOffset, modelGroupId) : null;
        if (fshEntry is not null)
        {
            usedNightTexture = true;
        }
        else
        {
            fshEntry = FindFshEntry(dayTextureId, modelGroupId);
            if (fshEntry is not null)
            {
                usedNightTexture = false;
            }
        }

        if (fshEntry is null)
        {
            return false;
        }

        try
        {
            fshEntry.Decode();
            image = (fshEntry as DBPFEntryFSH)?.Image;
            return image is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Finds an FSH entry by instance ID, preferring the model's own Group ID but falling back to any group that has a matching instance (some texture families live in a shared group).</summary>
    private DBPFEntry? FindFshEntry(uint instanceId, uint preferredGroupId)
    {
        DBPFEntry? fallback = null;
        foreach (var entry in _service.Entries)
        {
            if (entry.TGI.TypeID != FshTypeId || entry.TGI.InstanceID != instanceId)
            {
                continue;
            }

            if (entry.TGI.GroupID == preferredGroupId)
            {
                return entry;
            }

            fallback ??= entry;
        }

        return fallback;
    }

    private void DisposeS3DTexture()
    {
        S3DTexture?.Dispose();
        S3DTexture = null;

        foreach (var image in _s3DMaterialTextures.Values)
        {
            image.Dispose();
        }

        _s3DMaterialTextures.Clear();
        OnPropertyChanged(nameof(S3DMaterialTextures));

        foreach (var image in _s3DMaterialNightTextures.Values)
        {
            image.Dispose();
        }

        _s3DMaterialNightTextures.Clear();
        OnPropertyChanged(nameof(S3DMaterialNightTextures));
    }

    // ---------------------------------------------------------------
    // S3D Editor: Apply/Save (write the edited model back to its entry), Merge (combine with
    // another S3D entry already in the open package), Import/Export 3DS (Models/Autodesk3ds.cs).
    // All of these mutate SelectedS3DModel in place and then call RefreshS3DTexture() (info
    // line + texture) and ForceS3DViewerRefresh() (the viewer control doesn't otherwise
    // notice an in-place mutation of the same model object it's already bound to).
    // ---------------------------------------------------------------

    public ObservableCollection<EntryItemViewModel> S3DMergeCandidates { get; } = new();

    private EntryItemViewModel? _selectedMergeCandidate;
    public EntryItemViewModel? SelectedMergeCandidate
    {
        get => _selectedMergeCandidate;
        set
        {
            if (SetField(ref _selectedMergeCandidate, value))
            {
                MergeS3DCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand ApplyS3DCommand { get; }
    public RelayCommand MergeS3DCommand { get; }

    /// <summary>Other S3D-type entries in the currently open package, excluding the one being previewed - candidates for "MERGE".</summary>
    private void RefreshS3DMergeCandidates()
    {
        S3DMergeCandidates.Clear();
        foreach (var vm in Entries)
        {
            if (vm != SelectedEntry && vm.Entry.TGI.TypeID == S3DTypeId)
            {
                S3DMergeCandidates.Add(vm);
            }
        }

        SelectedMergeCandidate = null;
    }

    /// <summary>"APPLY/SAVE" - encodes the (possibly merged/imported) in-memory model and writes it back into the selected entry, same "replace bytes, keep TGI, fix up compression" mechanics as <see cref="SaveLuaScriptToSelectedEntry"/>.</summary>
    private void SaveS3DModel()
    {
        if (SelectedEntry is null || SelectedS3DModel is null)
        {
            return;
        }

        try
        {
            var bytes = S3DWriter.Encode(SelectedS3DModel);
            var oldVm = SelectedEntry;
            var index = Entries.IndexOf(oldVm);

            var newEntry = _service.ReplaceEntryBytes(oldVm.Entry, bytes);

            try
            {
                _service.SetEntryCompression(newEntry, false);
            }
            catch
            {
                // Not fatal - the model bytes themselves are already saved correctly above.
            }

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
            StatusMessage = $"S3D model saved into {newVm.TypeHex}/{newVm.GroupHex}/{newVm.InstanceHex} (remember to save the package).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while saving S3D model: {ex.Message}";
        }
    }

    /// <summary>"MERGE" - appends <see cref="SelectedMergeCandidate"/>'s groups into the in-memory model (see <see cref="S3DWriter.Merge"/>). Not written to the entry until "APPLY/SAVE".</summary>
    private void MergeS3DModel()
    {
        if (SelectedS3DModel is null || SelectedMergeCandidate is null)
        {
            return;
        }

        try
        {
            var sourceBytes = RawEntryBytes.GetDecompressed(SelectedMergeCandidate.Entry);
            var sourceModel = sourceBytes is null ? null : S3DParser.Parse(sourceBytes);
            if (sourceModel is null)
            {
                StatusMessage = "Could not parse the model to merge.";
                return;
            }

            S3DWriter.Merge(SelectedS3DModel, sourceModel);
            RefreshS3DTexture();
            RefreshS3DGroupToggles();
            RefreshS3DEditRows();
            RefreshS3DMaterialRows();
            RefreshS3DAnimMeshes();
            ForceS3DViewerRefresh();
            StatusMessage = $"Merged {SelectedMergeCandidate.TypeHex}/{SelectedMergeCandidate.GroupHex}/{SelectedMergeCandidate.InstanceHex} in - click APPLY/SAVE to write it to the entry.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while merging: {ex.Message}";
        }
    }

    /// <summary>"IMPORT 3DS..." - replaces the in-memory model's geometry from a .3ds file (see <see cref="Autodesk3ds.ApplyToModel"/>). Not written to the entry until "APPLY/SAVE".</summary>
    public void ImportS3DFrom3ds(string path)
    {
        if (SelectedS3DModel is null)
        {
            return;
        }

        try
        {
            var meshes = Autodesk3ds.Import(path);
            Autodesk3ds.ApplyToModel(meshes, SelectedS3DModel);
            RefreshS3DTexture();
            RefreshS3DGroupToggles();
            RefreshS3DEditRows();
            ForceS3DViewerRefresh();
            StatusMessage = $"Imported {meshes.Count} mesh group(s) from: {path} - click APPLY/SAVE to write it to the entry.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while importing 3DS: {ex.Message}";
        }
    }

    /// <summary>"EXPORT 3DS..." - writes the currently previewed model's geometry out to a .3ds file (see <see cref="Autodesk3ds.Export"/>).</summary>
    public void ExportS3DTo3ds(string path)
    {
        if (SelectedS3DModel is null)
        {
            return;
        }

        try
        {
            Autodesk3ds.Export(SelectedS3DModel, path);
            StatusMessage = $"Exported the S3D model to: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while exporting 3DS: {ex.Message}";
        }
    }

    /// <summary>"EXPORT 3DS (GROUP)..." - writes only the current editing group (S3DEditGroupIndex, shared with the Geometry/UV editors) out to a .3ds file, instead of the whole model (see <see cref="Autodesk3ds.ExportGroup"/>) - Ilive Reader's own separate group-only export command.</summary>
    public void ExportS3DGroupTo3ds(string path)
    {
        if (SelectedS3DModel is null)
        {
            return;
        }

        try
        {
            Autodesk3ds.ExportGroup(SelectedS3DModel, S3DEditGroupIndex, path);
            StatusMessage = $"Exported group {S3DEditGroupIndex} to: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while exporting group to 3DS: {ex.Message}";
        }
    }

    /// <summary>
    /// S3DViewerControl's Model property only re-renders when it's actually assigned a
    /// different reference (an in-place mutation of the same S3DModel - Merge, 3DS import -
    /// wouldn't otherwise be noticed) - a null/reassign bounce is the simplest way to force
    /// that without adding change notification to S3DModel itself.
    /// </summary>
    private void ForceS3DViewerRefresh()
    {
        var model = SelectedS3DModel;
        SelectedS3DModel = null;
        SelectedS3DModel = model;
    }

    // ---------------------------------------------------------------
    // S3D Editor: Material Editor (Dlg3DMMat equivalent). One grid row per texture
    // reference across every material (Group column = material index, matching Ilive
    // Reader's own flattening - see S3DMaterialRowViewModel). Render state edits commit
    // immediately into the in-memory model, same "APPLY/SAVE persists it" deferred
    // pattern as the Geometry Editor; Replace/Add Texture instead write straight into the
    // package's FSH entry (or add a new one) since those are separate DBPF entries, not
    // part of the S3D entry's own bytes - mirrors Ilive's OnMenuReplaceTexture/
    // OnMenuAddTextureExtFile, which call pSave->UpdateInput/AddFile directly rather than
    // going through the S3D block save path.
    // ---------------------------------------------------------------

    private const uint GrpFshGroupId = 0x1ABE787D;

    public ObservableCollection<S3DMaterialRowViewModel> S3DMaterialRows { get; } = new();

    private S3DMaterialRowViewModel? _selectedS3DMaterialRow;
    public S3DMaterialRowViewModel? SelectedS3DMaterialRow
    {
        get => _selectedS3DMaterialRow;
        set
        {
            if (SetField(ref _selectedS3DMaterialRow, value))
            {
                RefreshS3DMaterialTexturePreview();
                ChangeS3DMaterialTgiCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Every FSH entry in the currently open package - source list for "Add Texture (from package)".</summary>
    public ObservableCollection<EntryItemViewModel> S3DPackageFshEntries { get; } = new();

    private EntryItemViewModel? _selectedFshEntryForMaterial;
    public EntryItemViewModel? SelectedFshEntryForMaterial
    {
        get => _selectedFshEntryForMaterial;
        set
        {
            if (SetField(ref _selectedFshEntryForMaterial, value))
            {
                AddS3DTextureFromPackageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _s3DMaterialNewInstanceText = string.Empty;

    /// <summary>New instance ID text for "Change TGI"/"Add Texture" - hex, ignored (a random ID is generated instead) when <see cref="RandomizeS3DMaterialInstance"/> is checked. Mirrors the main "EDIT TGI" panel's own Random-checkbox convention.</summary>
    public string S3DMaterialNewInstanceText
    {
        get => _s3DMaterialNewInstanceText;
        set => SetField(ref _s3DMaterialNewInstanceText, value);
    }

    private bool _randomizeS3DMaterialInstance = true;
    public bool RandomizeS3DMaterialInstance
    {
        get => _randomizeS3DMaterialInstance;
        set => SetField(ref _randomizeS3DMaterialInstance, value);
    }

    private string _s3DNewTextureName = string.Empty;

    /// <summary>New texture name for "Change TGI"/"Add Texture" (purely descriptive - csDBPF/S3D don't require it, Ilive Reader stores it alongside the texture ID for the grid's "Material name" column).</summary>
    public string S3DNewTextureName
    {
        get => _s3DNewTextureName;
        set => SetField(ref _s3DNewTextureName, value);
    }

    public RelayCommand ChangeS3DMaterialTgiCommand { get; }
    public RelayCommand AddS3DTextureFromPackageCommand { get; }

    private Avalonia.Media.Imaging.Bitmap? _s3DMaterialTexturePreview;
    public Avalonia.Media.Imaging.Bitmap? S3DMaterialTexturePreview
    {
        get => _s3DMaterialTexturePreview;
        private set => SetField(ref _s3DMaterialTexturePreview, value);
    }

    private string _s3DMaterialTgiInfo = "(no texture selected)";
    public string S3DMaterialTgiInfo
    {
        get => _s3DMaterialTgiInfo;
        private set => SetField(ref _s3DMaterialTgiInfo, value);
    }

    /// <summary>Rebuilds <see cref="S3DMaterialRows"/> from the current model - one row per texture reference across every material. Called on load, and after any operation that can add/remove materials or textures (merge, Add Texture, Change TGI).</summary>
    private void RefreshS3DMaterialRows()
    {
        var previouslySelected = SelectedS3DMaterialRow?.Texture;
        S3DMaterialRows.Clear();

        if (SelectedS3DModel is { } model)
        {
            for (var m = 0; m < model.Materials.Count; m++)
            {
                var material = model.Materials[m];
                foreach (var texture in material.Textures)
                {
                    S3DMaterialRows.Add(new S3DMaterialRowViewModel(material, m, texture, OnS3DMaterialEdited));
                }
            }
        }

        SelectedS3DMaterialRow = previouslySelected is null
            ? null
            : S3DMaterialRows.FirstOrDefault(r => r.Texture == previouslySelected);
    }

    /// <summary>Refreshes <see cref="S3DPackageFshEntries"/> from the currently open package - called on load and whenever a texture is added, since that changes the FSH entry list too.</summary>
    private void RefreshS3DPackageFshEntries()
    {
        S3DPackageFshEntries.Clear();
        foreach (var vm in Entries)
        {
            if (vm.Entry.TGI.TypeID == FshTypeId)
            {
                S3DPackageFshEntries.Add(vm);
            }
        }
    }

    private void OnS3DMaterialEdited() => ForceS3DViewerRefresh();

    /// <summary>
    /// Finds the FSH entry a material texture reference resolves to, mirroring Ilive
    /// Reader's own 3-tier search exactly (Dlg3DMMat::OnSelchangeFsh:
    /// <c>SearchFastInstance(id, ENT_FSH, GRP_FSH)</c>, then the model's own group, then any
    /// group) - SC4's shared texture group (GRP_FSH, 0x1ABE787D) is checked first since
    /// that's where most building/prop textures actually live, the model's own Group ID
    /// second for the (rarer) case of a texture bundled alongside its model instead.
    /// </summary>
    private DBPFEntry? FindMaterialFshEntry(uint instanceId, uint modelGroupId)
    {
        DBPFEntry? ownGroup = null;
        DBPFEntry? any = null;

        foreach (var entry in _service.Entries)
        {
            if (entry.TGI.TypeID != FshTypeId || entry.TGI.InstanceID != instanceId)
            {
                continue;
            }

            if (entry.TGI.GroupID == GrpFshGroupId)
            {
                return entry;
            }

            if (entry.TGI.GroupID == modelGroupId)
            {
                ownGroup ??= entry;
            }

            any ??= entry;
        }

        return ownGroup ?? any;
    }

    /// <summary>Resolves and decodes the selected row's texture (live FSH preview + TGI info line), same lookup Ilive Reader's grid selection handler does.</summary>
    private void RefreshS3DMaterialTexturePreview()
    {
        S3DMaterialTexturePreview?.Dispose();
        S3DMaterialTexturePreview = null;

        if (SelectedS3DMaterialRow is not { } row)
        {
            S3DMaterialTgiInfo = "(no texture selected)";
            return;
        }

        var fshEntry = FindMaterialFshEntry(row.Texture.TextureId, _s3DModelGroupId);
        if (fshEntry is null)
        {
            S3DMaterialTgiInfo = $"Instance {row.TextureIdHex} - not found in this package.";
            return;
        }

        S3DMaterialTgiInfo = $"Type: 0x{fshEntry.TGI.TypeID:X8}   Group: 0x{fshEntry.TGI.GroupID:X8}   Instance: 0x{fshEntry.TGI.InstanceID:X8}";

        try
        {
            fshEntry.Decode();
            var image = (fshEntry as DBPFEntryFSH)?.Image;
            S3DMaterialTexturePreview = ImageConversion.ToAvaloniaBitmap(image);
        }
        catch
        {
            // Preview is best-effort - the TGI info line above already told the user the
            // entry was found even if it can't be decoded/displayed.
        }
    }

    /// <summary>"CHANGE TGI" - repoints the selected row's texture reference at a different FSH instance ID (Ilive Reader's OnChangeTGI, minus its own nested "change instance" dialog - the instance/name fields live inline in the Material Editor instead). In-memory only, like every other Material Editor edit - APPLY/SAVE persists it.</summary>
    private void ChangeS3DMaterialTgi()
    {
        if (SelectedS3DMaterialRow is not { } row)
        {
            return;
        }

        try
        {
            var newId = RandomizeS3DMaterialInstance ? TgiGenerator.GenerateRandomId() : EntryClipboard.ParseHex(S3DMaterialNewInstanceText);
            row.Texture.TextureId = newId;
            if (!string.IsNullOrWhiteSpace(S3DNewTextureName))
            {
                row.Texture.Name = S3DNewTextureName;
            }

            RefreshS3DMaterialRows();
            OnS3DMaterialEdited();
            StatusMessage = $"Material texture reference changed to 0x{newId:X8} - click APPLY/SAVE on the model to persist it.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error changing the texture reference: {ex.Message}";
        }
    }

    /// <summary>Target material for "Add Texture": the currently selected row's material, or a brand-new (or the model's first) material if none is selected - matches Ilive's OnMenuAddTexture, which happily adds a texture even with no grid selection.</summary>
    private S3DMaterial? GetOrCreateTargetMaterial()
    {
        if (SelectedS3DMaterialRow is { } row)
        {
            return row.Material;
        }

        if (SelectedS3DModel is not { } model)
        {
            return null;
        }

        if (model.Materials.Count == 0)
        {
            model.Materials.Add(new S3DMaterial());
        }

        return model.Materials[0];
    }

    /// <summary>"ADD FROM PACKAGE" - references an FSH entry already in the open package as a new texture on the target material, no file import needed.</summary>
    private void AddS3DMaterialTextureFromPackage()
    {
        if (SelectedFshEntryForMaterial is not { } fshVm)
        {
            return;
        }

        var material = GetOrCreateTargetMaterial();
        if (material is null)
        {
            return;
        }

        material.Textures.Add(new S3DMaterialTexture
        {
            TextureId = fshVm.Entry.TGI.InstanceID,
            Name = string.IsNullOrWhiteSpace(S3DNewTextureName) ? fshVm.InstanceHex : S3DNewTextureName,
        });

        RefreshS3DMaterialRows();
        OnS3DMaterialEdited();
        StatusMessage = $"Added texture reference to {fshVm.InstanceHex} - click APPLY/SAVE on the model to persist it.";
    }

    /// <summary>"ADD FROM FILE..." - imports an external .fsh file as a brand-new package entry (in SC4's shared texture group, GRP_FSH, matching Ilive Reader's own OnMenuAddTextureExtFile) and references it as a new texture on the target material.</summary>
    public void AddS3DMaterialTextureFromFile(string filePath)
    {
        var material = GetOrCreateTargetMaterial();
        if (material is null)
        {
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var instanceId = RandomizeS3DMaterialInstance || string.IsNullOrWhiteSpace(S3DMaterialNewInstanceText)
                ? TgiGenerator.GenerateRandomId()
                : EntryClipboard.ParseHex(S3DMaterialNewInstanceText);

            var tgi = new TGI(FshTypeId, GrpFshGroupId, instanceId);
            var newEntry = _service.AddEntryFromClipboard(typeof(DBPFEntryFSH).AssemblyQualifiedName!, tgi, bytes);
            if (newEntry is null)
            {
                StatusMessage = "Could not create the new FSH entry.";
                return;
            }

            Entries.Add(new EntryItemViewModel(newEntry));
            RefreshDisplayedEntries();
            RefreshS3DPackageFshEntries();

            material.Textures.Add(new S3DMaterialTexture
            {
                TextureId = instanceId,
                Name = string.IsNullOrWhiteSpace(S3DNewTextureName) ? Path.GetFileNameWithoutExtension(filePath) : S3DNewTextureName,
            });

            RefreshS3DMaterialRows();
            OnS3DMaterialEdited();
            StatusMessage = $"Added {filePath} as a new texture (0x{instanceId:X8}) - click APPLY/SAVE on the model to persist it.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error adding texture from file: {ex.Message}";
        }
    }

    /// <summary>"REPLACE TEXTURE..." - overwrites the selected row's resolved FSH package entry with a new file's bytes (TGI unchanged), Ilive Reader's OnMenuReplaceTexture. Applied straight to the package, not deferred to APPLY/SAVE, since it edits a different entry than the one the Material Editor's "model" belongs to.</summary>
    public void ReplaceS3DMaterialTexture(string filePath)
    {
        if (SelectedS3DMaterialRow is not { } row)
        {
            return;
        }

        var fshEntry = FindMaterialFshEntry(row.Texture.TextureId, _s3DModelGroupId);
        if (fshEntry is null)
        {
            StatusMessage = $"Texture {row.TextureIdHex} is not in this package - use Add Texture instead.";
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var index = Entries.ToList().FindIndex(e => e.Entry == fshEntry);
            var newEntry = _service.ReplaceEntryBytes(fshEntry, bytes);

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
            RefreshS3DPackageFshEntries();
            RefreshS3DMaterialTexturePreview();
            StatusMessage = $"Replaced texture {row.TextureIdHex} from: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error replacing texture: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------
    // S3D Editor: Animation Editor (Tab3DMAnim equivalent). FrameRate/AnimMode/Displacement
    // edit the model's single Animation header directly; the mesh list below edits
    // Animation.Meshes (name/flags) and, per selected mesh, its per-frame VERT/INDX/PRIM/
    // MATS block mapping (Tab3DMAnim's tree grid, flattened here into a mesh list plus a
    // linked frame list for whichever mesh is selected - same "master list -> selection ->
    // detail list" shape as the Material Editor's material/texture split). Everything
    // commits immediately into SelectedS3DModel, same deferred "APPLY/SAVE persists it"
    // convention as the Geometry/Material editors.
    // ---------------------------------------------------------------

    public int S3DAnimFrameRate
    {
        get => SelectedS3DModel?.Animation.FrameRate ?? 0;
        set
        {
            if (SelectedS3DModel is { } model)
            {
                model.Animation.FrameRate = (ushort)Math.Clamp(value, 0, ushort.MaxValue);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Setting this pads/truncates every mesh's frame list to match, exactly like Ilive Reader's Tab3DMAnim::OnKillfocusframe.</summary>
    public int S3DAnimFrameCount
    {
        get => SelectedS3DModel?.Animation.FrameCount ?? 0;
        set
        {
            if (SelectedS3DModel is not { } model)
            {
                return;
            }

            var count = Math.Clamp(value, 0, ushort.MaxValue);
            foreach (var mesh in model.Animation.Meshes)
            {
                while (mesh.Frames.Count < count) mesh.Frames.Add(new S3DAnimFrame());
                while (mesh.Frames.Count > count) mesh.Frames.RemoveAt(mesh.Frames.Count - 1);
            }

            model.Animation.FrameCount = (ushort)count;
            OnPropertyChanged();
            RefreshS3DAnimMeshes();
            RefreshS3DGroupToggles();
            OnS3DAnimEdited();
        }
    }

    public double S3DAnimDisplacement
    {
        get => SelectedS3DModel?.Animation.Displacement ?? 0;
        set
        {
            if (SelectedS3DModel is { } model)
            {
                model.Animation.Displacement = (float)value;
                OnPropertyChanged();
            }
        }
    }

    public S3DMaterialOption S3DAnimModeOption
    {
        get => S3DAnimModeOptions.Values.FirstOrDefault(o => o.Value == (SelectedS3DModel?.Animation.AnimMode ?? 1)) ?? S3DAnimModeOptions.Values[0];
        set
        {
            if (SelectedS3DModel is { } model)
            {
                model.Animation.AnimMode = value.Value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<S3DAnimMeshRowViewModel> S3DAnimMeshes { get; } = new();

    private S3DAnimMeshRowViewModel? _selectedS3DAnimMesh;
    public S3DAnimMeshRowViewModel? SelectedS3DAnimMesh
    {
        get => _selectedS3DAnimMesh;
        set
        {
            if (SetField(ref _selectedS3DAnimMesh, value))
            {
                RefreshS3DAnimFrameRows();
                DeleteS3DAnimMeshCommand.RaiseCanExecuteChanged();
                AddS3DAnimFrameCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Frames of <see cref="SelectedS3DAnimMesh"/> only - empty when nothing is selected.</summary>
    public ObservableCollection<S3DAnimFrameRowViewModel> S3DAnimFrames { get; } = new();

    public RelayCommand AddS3DAnimMeshCommand { get; }
    public RelayCommand DeleteS3DAnimMeshCommand { get; }
    public RelayCommand AddS3DAnimFrameCommand { get; }

    /// <summary>Rebuilds <see cref="S3DAnimMeshes"/> (and the header fields' display) from the current model - called on load and after any structural change (merge, add/delete mesh or frame).</summary>
    private void RefreshS3DAnimMeshes()
    {
        var previouslySelected = SelectedS3DAnimMesh?.Mesh;
        S3DAnimMeshes.Clear();

        if (SelectedS3DModel is { } model)
        {
            for (var i = 0; i < model.Animation.Meshes.Count; i++)
            {
                S3DAnimMeshes.Add(new S3DAnimMeshRowViewModel(model.Animation.Meshes[i], i, OnS3DAnimEdited));
            }
        }

        SelectedS3DAnimMesh = previouslySelected is null ? null : S3DAnimMeshes.FirstOrDefault(r => r.Mesh == previouslySelected);

        OnPropertyChanged(nameof(S3DAnimFrameRate));
        OnPropertyChanged(nameof(S3DAnimFrameCount));
        OnPropertyChanged(nameof(S3DAnimDisplacement));
        OnPropertyChanged(nameof(S3DAnimModeOption));
    }

    private void RefreshS3DAnimFrameRows()
    {
        S3DAnimFrames.Clear();
        if (SelectedS3DAnimMesh is { } row)
        {
            for (var i = 0; i < row.Mesh.Frames.Count; i++)
            {
                S3DAnimFrames.Add(new S3DAnimFrameRowViewModel(row.Mesh, i, OnS3DAnimEdited));
            }
        }
    }

    private void OnS3DAnimEdited() => ForceS3DViewerRefresh();

    /// <summary>"ADD GROUP" - a new mesh with a single frame (matching Ilive's OnMenuAddGroup exactly - it does not pad to the header's FrameCount).</summary>
    private void AddS3DAnimMesh()
    {
        if (SelectedS3DModel is not { } model)
        {
            return;
        }

        var mesh = new S3DAnimMesh { Name = $"mesh{model.Animation.Meshes.Count}" };
        mesh.Frames.Add(new S3DAnimFrame());
        model.Animation.Meshes.Add(mesh);

        RefreshS3DAnimMeshes();
        RefreshS3DGroupToggles();
        OnS3DAnimEdited();
        StatusMessage = "Added animation mesh group - click APPLY/SAVE on the model to persist it.";
    }

    /// <summary>"DEL GROUP" - removes the selected mesh entirely (Ilive's OnMenuDelGroup).</summary>
    private void DeleteS3DAnimMesh()
    {
        if (SelectedS3DModel is null || SelectedS3DAnimMesh is not { } row)
        {
            return;
        }

        SelectedS3DModel.Animation.Meshes.Remove(row.Mesh);

        RefreshS3DAnimMeshes();
        RefreshS3DGroupToggles();
        OnS3DAnimEdited();
        StatusMessage = "Deleted animation mesh group - click APPLY/SAVE on the model to persist it.";
    }

    /// <summary>"ADD ANIM" - appends one zero-initialized frame to the selected mesh (Ilive's AddAnim).</summary>
    private void AddS3DAnimFrame()
    {
        if (SelectedS3DAnimMesh is not { } row)
        {
            return;
        }

        row.Mesh.Frames.Add(new S3DAnimFrame());

        RefreshS3DAnimMeshes();
        OnS3DAnimEdited();
        StatusMessage = "Added animation frame - click APPLY/SAVE on the model to persist it.";
    }

    /// <summary>"DELETE ANIM" - removes the given frame rows from the selected mesh (Ilive's DeleteAnim, extended to multi-select like the Geometry Editor's own row deletes).</summary>
    public void DeleteS3DAnimFrames(IReadOnlyList<int> frameIndices)
    {
        if (SelectedS3DAnimMesh is not { } row || frameIndices.Count == 0)
        {
            return;
        }

        foreach (var i in frameIndices.OrderByDescending(x => x))
        {
            if (i >= 0 && i < row.Mesh.Frames.Count)
            {
                row.Mesh.Frames.RemoveAt(i);
            }
        }

        RefreshS3DAnimMeshes();
        OnS3DAnimEdited();
        StatusMessage = $"Deleted {frameIndices.Count} animation frame(s) - click APPLY/SAVE on the model to persist it.";
    }

    // ---------------------------------------------------------------
    // S3D Editor: PROP Editor (Tab3DMProp equivalent) - a flat grid of the PROP chunk's
    // arbitrary key/value string pairs (mesh index/frame number/key/value, ported from
    // TAB3DPROP_COL0..4). Ilive Reader's own tab only edits existing rows in place; Add/
    // Delete are added here since they were explicitly requested and cost nothing extra -
    // same immediate-commit convention as every other S3D Editor grid.
    // ---------------------------------------------------------------

    public ObservableCollection<S3DPropRowViewModel> S3DPropRows { get; } = new();

    public RelayCommand AddS3DPropCommand { get; }

    private void RefreshS3DPropRows()
    {
        S3DPropRows.Clear();
        if (SelectedS3DModel is { } model)
        {
            foreach (var block in model.Props)
            {
                S3DPropRows.Add(new S3DPropRowViewModel(block, OnS3DAnimEdited));
            }
        }
    }

    private void AddS3DProp()
    {
        if (SelectedS3DModel is not { } model)
        {
            return;
        }

        model.Props.Add(new S3DPropBlock());
        RefreshS3DPropRows();
        OnS3DAnimEdited();
        StatusMessage = "Added PROP entry - click APPLY/SAVE on the model to persist it.";
    }

    /// <summary>"DELETE" - removes the given PROP rows (multi-select, same convention as the Geometry Editor's own row deletes).</summary>
    public void DeleteS3DProps(IReadOnlyList<int> rowIndices)
    {
        if (SelectedS3DModel is not { } model || rowIndices.Count == 0)
        {
            return;
        }

        foreach (var i in rowIndices.OrderByDescending(x => x))
        {
            if (i >= 0 && i < model.Props.Count)
            {
                model.Props.RemoveAt(i);
            }
        }

        RefreshS3DPropRows();
        OnS3DAnimEdited();
        StatusMessage = $"Deleted {rowIndices.Count} PROP entry(ies) - click APPLY/SAVE on the model to persist it.";
    }

    // ---------------------------------------------------------------
    // S3D Editor: UV Editor (Tab3DMUV equivalent) - a 2D view of the current "editing
    // group"'s (S3DEditGroupIndex, shared with the Vertices/Indices/Primitives grids above)
    // UV coordinates over its resolved texture (S3DTexture, the same one already resolved
    // for the "Solid" 3D preview - see ResolveS3DTexture), with drag-to-move points and
    // zoom in/out. See Views/S3DUVEditorControl.cs for the actual drawing/drag logic.
    // ---------------------------------------------------------------

    /// <summary>The VERT block the UV Editor draws/edits - same group as the Geometry Editor's grids, kept in sync via RefreshS3DEditRows (see its notify calls at the end).</summary>
    public S3DVertexBlock? S3DUVVertexBlock =>
        SelectedS3DModel is { } model && S3DEditGroupIndex < model.VertexBlocks.Count ? model.VertexBlocks[S3DEditGroupIndex] : null;

    /// <summary>The INDX block the UV Editor draws triangle edges from - same group as <see cref="S3DUVVertexBlock"/>.</summary>
    public S3DIndexBlock? S3DUVIndexBlock =>
        SelectedS3DModel is { } model && S3DEditGroupIndex < model.IndexBlocks.Count ? model.IndexBlocks[S3DEditGroupIndex] : null;

    private double _s3DUVZoom = 1.0;
    public double S3DUVZoom
    {
        get => _s3DUVZoom;
        set => SetField(ref _s3DUVZoom, Math.Clamp(value, 0.1, 8.0));
    }

    public RelayCommand S3DUVZoomInCommand { get; }
    public RelayCommand S3DUVZoomOutCommand { get; }

    /// <summary>Bound to S3DUVEditorControl.ChangedCommand - fired after a drag-move finishes changing a point's U/V.</summary>
    public RelayCommand S3DUVPointChangedCommand { get; }

    private int _s3DUVSelectedIndex = -1;
    public int S3DUVSelectedIndex
    {
        get => _s3DUVSelectedIndex;
        set
        {
            if (SetField(ref _s3DUVSelectedIndex, value))
            {
                OnPropertyChanged(nameof(HasS3DUVSelection));
                OnPropertyChanged(nameof(S3DUVSelectedU));
                OnPropertyChanged(nameof(S3DUVSelectedV));
            }
        }
    }

    public bool HasS3DUVSelection => S3DUVVertexBlock is { } block && S3DUVSelectedIndex >= 0 && S3DUVSelectedIndex < block.Uvs.Count;

    /// <summary>Numeric U edit for the selected point (Ilive's ED_u) - alternative to dragging, for exact values.</summary>
    public double S3DUVSelectedU
    {
        get => HasS3DUVSelection ? S3DUVVertexBlock!.Uvs[S3DUVSelectedIndex].X : 0;
        set
        {
            if (HasS3DUVSelection)
            {
                var block = S3DUVVertexBlock!;
                var uv = block.Uvs[S3DUVSelectedIndex];
                block.Uvs[S3DUVSelectedIndex] = new Vector2((float)value, uv.Y);
                OnPropertyChanged();
                OnS3DUVPointChanged();
            }
        }
    }

    public double S3DUVSelectedV
    {
        get => HasS3DUVSelection ? S3DUVVertexBlock!.Uvs[S3DUVSelectedIndex].Y : 0;
        set
        {
            if (HasS3DUVSelection)
            {
                var block = S3DUVVertexBlock!;
                var uv = block.Uvs[S3DUVSelectedIndex];
                block.Uvs[S3DUVSelectedIndex] = new Vector2(uv.X, (float)value);
                OnPropertyChanged();
                OnS3DUVPointChanged();
            }
        }
    }

    private void OnS3DUVPointChanged()
    {
        OnPropertyChanged(nameof(S3DUVSelectedU));
        OnPropertyChanged(nameof(S3DUVSelectedV));
        ForceS3DViewerRefresh();
        StatusMessage = "UV coordinate changed - click APPLY/SAVE on the model to persist it.";
    }

    // ---------------------------------------------------------------
    // S3D Editor: REGP Editor. Neither Ilive Reader nor SC4ModdingSuite had a working editor
    // for this chunk before now - Ilive's own Tab3DMRegp.cpp exists but was never wired into
    // Form3DM::Display()'s tab list, so it's dead code that has never actually run; there is
    // no original UI behavior to port here. This is a fresh editor over the already-correct
    // S3DRegPointBlock/S3DRegPointTransform read/write support (S3DParser/S3DWriter), same
    // "master list -> selection -> detail list" shape as the Animation Editor (a reg point is
    // structurally the same thing as an animation mesh: a name plus one entry per frame).
    // ---------------------------------------------------------------

    public ObservableCollection<S3DRegPointRowViewModel> S3DRegPoints { get; } = new();

    private S3DRegPointRowViewModel? _selectedS3DRegPoint;
    public S3DRegPointRowViewModel? SelectedS3DRegPoint
    {
        get => _selectedS3DRegPoint;
        set
        {
            if (SetField(ref _selectedS3DRegPoint, value))
            {
                RefreshS3DRegPointTransforms();
                DeleteS3DRegPointCommand.RaiseCanExecuteChanged();
                AddS3DRegPointTransformCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Transforms of <see cref="SelectedS3DRegPoint"/> only - empty when nothing is selected.</summary>
    public ObservableCollection<S3DRegPointTransformRowViewModel> S3DRegPointTransforms { get; } = new();

    public RelayCommand AddS3DRegPointCommand { get; }
    public RelayCommand DeleteS3DRegPointCommand { get; }
    public RelayCommand AddS3DRegPointTransformCommand { get; }

    private void RefreshS3DRegPoints()
    {
        var previouslySelected = SelectedS3DRegPoint?.Block;
        S3DRegPoints.Clear();

        if (SelectedS3DModel is { } model)
        {
            foreach (var block in model.RegPoints)
            {
                S3DRegPoints.Add(new S3DRegPointRowViewModel(block, OnS3DRegPointEdited));
            }
        }

        SelectedS3DRegPoint = previouslySelected is null ? null : S3DRegPoints.FirstOrDefault(r => r.Block == previouslySelected);
    }

    private void RefreshS3DRegPointTransforms()
    {
        S3DRegPointTransforms.Clear();
        if (SelectedS3DRegPoint is { } row)
        {
            for (var i = 0; i < row.Block.Transforms.Count; i++)
            {
                S3DRegPointTransforms.Add(new S3DRegPointTransformRowViewModel(row.Block, i, OnS3DRegPointEdited));
            }
        }
    }

    private void OnS3DRegPointEdited() => ForceS3DViewerRefresh();

    private void AddS3DRegPoint()
    {
        if (SelectedS3DModel is not { } model)
        {
            return;
        }

        var block = new S3DRegPointBlock { Name = $"regp{model.RegPoints.Count}" };
        block.Transforms.Add(new S3DRegPointTransform { Orientation = new float[4] { 0, 0, 0, 1 } });
        model.RegPoints.Add(block);

        RefreshS3DRegPoints();
        OnS3DRegPointEdited();
        StatusMessage = "Added registration point - click APPLY/SAVE on the model to persist it.";
    }

    private void DeleteS3DRegPoint()
    {
        if (SelectedS3DModel is null || SelectedS3DRegPoint is not { } row)
        {
            return;
        }

        SelectedS3DModel.RegPoints.Remove(row.Block);

        RefreshS3DRegPoints();
        OnS3DRegPointEdited();
        StatusMessage = "Deleted registration point - click APPLY/SAVE on the model to persist it.";
    }

    private void AddS3DRegPointTransform()
    {
        if (SelectedS3DRegPoint is not { } row)
        {
            return;
        }

        row.Block.Transforms.Add(new S3DRegPointTransform { Orientation = new float[4] { 0, 0, 0, 1 } });

        RefreshS3DRegPoints();
        OnS3DRegPointEdited();
        StatusMessage = "Added transform - click APPLY/SAVE on the model to persist it.";
    }

    /// <summary>"DELETE" - removes the given transform rows from the selected reg point (multi-select, same convention as every other S3D Editor grid).</summary>
    public void DeleteS3DRegPointTransforms(IReadOnlyList<int> frameIndices)
    {
        if (SelectedS3DRegPoint is not { } row || frameIndices.Count == 0)
        {
            return;
        }

        foreach (var i in frameIndices.OrderByDescending(x => x))
        {
            if (i >= 0 && i < row.Block.Transforms.Count)
            {
                row.Block.Transforms.RemoveAt(i);
            }
        }

        RefreshS3DRegPoints();
        OnS3DRegPointEdited();
        StatusMessage = $"Deleted {frameIndices.Count} transform(s) - click APPLY/SAVE on the model to persist it.";
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
                    // PRIORITY INSTRUCTION: png.Decode() must never be called directly on
                    // possibly-compressed ByteData - confirmed against csDBPF's own source
                    // (Entries/DBPFEntryPNG.cs): its Decode() calls Image.Load(ByteData)
                    // with no IsCompressed/QFS.Decompress check first, unlike every other
                    // structured entry type in csDBPF. For a compressed PNG entry (routine
                    // for base-game assets), that hands ImageSharp's format-sniffing
                    // compressed bytes to parse as if they were already raw pixel data -
                    // this doesn't reliably throw, so a previous version of this code that
                    // only fell back to the safe decompressed-bytes path when png.Decode()
                    // threw could silently show a wrong/garbage-sized image instead of the
                    // real one whenever it "succeeded" on compressed data. Always decoding
                    // via TryDecodeAnyFormat (already decompresses first) sidesteps that -
                    // it also auto-detects the real format for the BMP/JPEG-under-the-same-
                    // shared-TypeID case, so there's no longer a need for a first attempt via
                    // png.Decode()/PNGImage at all.
                    var bitmap = ImageConversion.TryDecodeAnyFormat(RawEntryBytes.GetDecompressed(png));

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

    /// <summary>
    /// Populates the Properties panel for the selected Exemplar/Cohort. Uses an
    /// independent, verified binary parser (<see cref="ExemplarBinaryParser"/>) instead of
    /// csDBPF's own <c>DBPFEntryEXMP.ListOfProperties</c> - testing against a real Lot
    /// Configuration Exemplar showed csDBPF's own decode produces implausible property IDs
    /// for entries with many "array-mode" properties (the encoding
    /// <c>LotConfigPropertyLotObject</c> and similar repeating properties use), while this
    /// independent parser - cross-checked against the same file byte-for-byte - decodes it
    /// perfectly. Falls back to csDBPF's own decode only if this parser can't make sense of
    /// the bytes either (e.g. a genuinely different/unexpected format).
    ///
    /// <c>exemplar.Decode()</c> is still called regardless, since <see cref="SelectedExemplar"/>
    /// and the Add/Edit/Remove commands operate on csDBPF's own internal state - if its
    /// property count doesn't match what this independent parser found, that's a concrete,
    /// checkable sign that editing/saving *this* entry may not be trustworthy, and is
    /// surfaced as a status warning rather than silently risking data loss on save.
    /// </summary>
    private void LoadPropertiesForSelectedEntry()
    {
        if (SelectedEntry?.Entry is not DBPFEntryEXMP exemplar)
        {
            return;
        }

        try
        {
            exemplar.Decode();

            var rawBytes = RawEntryBytes.GetDecompressed(exemplar);
            var parsed = ExemplarBinaryParser.Parse(rawBytes);

            if (parsed.IsWellFormed)
            {
                foreach (var property in parsed.Properties)
                {
                    var dbpfProperty = ExemplarBinaryParser.ToDbpfProperty(property);
                    Properties.Add(new PropertyItemViewModel(dbpfProperty, PropertyRegistry.FindById(property.Id)));
                }

                if (parsed.Properties.Count != exemplar.ListOfProperties.Count)
                {
                    StatusMessage =
                        $"Note: csDBPF reports {exemplar.ListOfProperties.Count} properties for this entry, " +
                        $"but independent parsing found {parsed.Properties.Count} - the properties shown here " +
                        "are the independently-verified ones; editing/saving this entry may not be fully " +
                        "reliable until this discrepancy is understood.";
                }
            }
            else
            {
                // Our own parser couldn't make sense of the bytes either - fall back to
                // csDBPF's own decode rather than showing nothing.
                foreach (var property in exemplar.ListOfProperties.Values)
                {
                    Properties.Add(new PropertyItemViewModel(property, PropertyRegistry.FindById(property.ID)));
                }
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
                var tgi = new TGI(EntryClipboard.ParseHex(payload.TypeHex), EntryClipboard.ParseHex(payload.GroupHex), EntryClipboard.ParseHex(payload.InstanceHex));
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
            uint type = EntryClipboard.ParseHex(NewTypeText);
            uint group = randomizeGroup ? 0u : EntryClipboard.ParseHex(NewGroupText);
            uint instance = randomizeInstance ? 0u : EntryClipboard.ParseHex(NewInstanceText);

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

    // ---------------------------------------------------------------
    // Analysis mode (Ilive Reader's DlgIdxAnalyser/DlgPropFind*/DlgPropFindPCohort/
    // DlgPropCount/FormProperty's ID_MGR_PROP): Find/Index Analyser (TGI-range + optional
    // hex byte-pattern search, current tab or a whole folder), Plugins Analyser (folder
    // scan stats), Exemplar/Cohort Analyser (property usage stats - folds in DlgPropCount),
    // Property Find/Count (search by property ID/value across files) and Property Manager
    // (browse the loaded property database - the "global manager" FormProperty/ID_MGR_PROP
    // opened, reusing PropertyRegistry directly rather than a second copy of the data).
    // All read-only reporting tools; none of them mutate entries. DlgDirectory::Search is
    // already covered by the standalone Directory sync dialog (its own Refresh already does
    // exactly that - decode + re-list on open), and "advanced saveable filters"
    // (DlgFilters/Ex, WorkspaceFilter) are the Save/Load buttons on the Find tab below,
    // backed by AnalysisFilterService.
    // ---------------------------------------------------------------

    private static readonly string[] Sc4FileExtensions = { ".dat", ".sc4lot", ".sc4desc", ".sc4model" };

    private static IEnumerable<string> EnumerateSc4Files(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(f => Sc4FileExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Folder shared by every Analysis tab's "whole folder" scope - defaults to whatever Options has set as the Plugins folder.</summary>
    private string _analysisFolder = string.Empty;
    public string AnalysisFolder
    {
        get => string.IsNullOrEmpty(_analysisFolder) ? AppOptions.PluginsFolder ?? string.Empty : _analysisFolder;
        set => SetField(ref _analysisFolder, value);
    }

    /// <summary>Jumps to this result's TGI: selects it in place if it belongs to the document
    /// already open in this tab, otherwise opens the file it actually came from (a "whole
    /// folder" scan hit) into a brand-new tab and selects it there instead.</summary>
    public void SelectAnalysisResult(AnalysisResultRowViewModel row)
    {
        if (_service.CurrentPath is { } currentPath &&
            string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(row.FilePath), StringComparison.OrdinalIgnoreCase))
        {
            SelectEntryByTgi(row.Tgi);
            return;
        }

        var newDoc = OpenDocumentInNewTab?.Invoke(row.FilePath);
        newDoc?.SelectEntryByTgi(row.Tgi);
    }

    // --- Find / Index Analyser (DlgIdxAnalyser) ---

    private bool _findWholeFolder;
    public bool FindWholeFolder
    {
        get => _findWholeFolder;
        set => SetField(ref _findWholeFolder, value);
    }

    private bool _findFilterType;
    public bool FindFilterType
    {
        get => _findFilterType;
        set => SetField(ref _findFilterType, value);
    }

    private string _findTypeHex = "0x00000000";
    public string FindTypeHex
    {
        get => _findTypeHex;
        set => SetField(ref _findTypeHex, value);
    }

    private bool _findFilterGroup;
    public bool FindFilterGroup
    {
        get => _findFilterGroup;
        set => SetField(ref _findFilterGroup, value);
    }

    private string _findGroupHex = "0x00000000";
    public string FindGroupHex
    {
        get => _findGroupHex;
        set => SetField(ref _findGroupHex, value);
    }

    private bool _findFilterInstance;
    public bool FindFilterInstance
    {
        get => _findFilterInstance;
        set => SetField(ref _findFilterInstance, value);
    }

    private string _findInstanceHex = "0x00000000";
    public string FindInstanceHex
    {
        get => _findInstanceHex;
        set => SetField(ref _findInstanceHex, value);
    }

    /// <summary>Optional raw hex byte pattern (e.g. "DE AD BE EF") to also require somewhere in the entry's decompressed content - Ilive Reader's Hex Find.</summary>
    private string _findHexPattern = string.Empty;
    public string FindHexPattern
    {
        get => _findHexPattern;
        set => SetField(ref _findHexPattern, value);
    }

    public ObservableCollection<AnalysisResultRowViewModel> FindResults { get; } = new();

    private string _findStatusMessage = "Set Type/Group/Instance filters (leave unchecked to match any) and/or a hex byte pattern, then Search.";
    public string FindStatusMessage
    {
        get => _findStatusMessage;
        private set => SetField(ref _findStatusMessage, value);
    }

    public RelayCommand FindSearchCommand { get; private set; } = null!;

    private void RunFind()
    {
        FindResults.Clear();
        try
        {
            var typeFilter = FindFilterType ? EntryClipboard.ParseHex(FindTypeHex) : 0u;
            var groupFilter = FindFilterGroup ? EntryClipboard.ParseHex(FindGroupHex) : 0u;
            var instanceFilter = FindFilterInstance ? EntryClipboard.ParseHex(FindInstanceHex) : 0u;
            var hexPattern = ParseHexPattern(FindHexPattern);

            var matched = 0;
            var scanned = 0;

            void Scan(string path, IEnumerable<DBPFEntry> entries)
            {
                foreach (var entry in entries)
                {
                    scanned++;
                    var tgi = entry.TGI;
                    if (FindFilterType && tgi.TypeID != typeFilter) continue;
                    if (FindFilterGroup && tgi.GroupID != groupFilter) continue;
                    if (FindFilterInstance && tgi.InstanceID != instanceFilter) continue;

                    if (hexPattern is { Length: > 0 })
                    {
                        var bytes = RawEntryBytes.GetDecompressed(entry);
                        if (bytes is null || !ContainsSequence(bytes, hexPattern))
                        {
                            continue;
                        }
                    }

                    FindResults.Add(new AnalysisResultRowViewModel
                    {
                        FilePath = path,
                        Tgi = tgi,
                        EntryType = tgi.GetEntryType(),
                        SizeBytes = entry.GetSize(),
                    });
                    matched++;
                }
            }

            if (FindWholeFolder)
            {
                if (string.IsNullOrWhiteSpace(AnalysisFolder) || !Directory.Exists(AnalysisFolder))
                {
                    FindStatusMessage = "Choose a valid folder first.";
                    return;
                }

                foreach (var path in EnumerateSc4Files(AnalysisFolder))
                {
                    try
                    {
                        Scan(path, new DBPFFile(path).ListOfEntries);
                    }
                    catch
                    {
                        // Unreadable/corrupt file - skip it, doesn't abort the whole scan.
                    }
                }
            }
            else if (_service.CurrentFile is not null)
            {
                Scan(_service.CurrentPath ?? "(unsaved)", _service.Entries);
            }

            FindStatusMessage = $"{matched} match(es) out of {scanned} entr{(scanned == 1 ? "y" : "ies")} scanned.";
        }
        catch (Exception ex)
        {
            FindStatusMessage = $"Error: {ex.Message}";
        }
    }

    private static byte[]? ParseHexPattern(string text)
    {
        var cleaned = new string((text ?? string.Empty).Where(Uri.IsHexDigit).ToArray());
        if (cleaned.Length < 2)
        {
            return null;
        }

        if (cleaned.Length % 2 != 0)
        {
            cleaned = cleaned[..^1];
        }

        var bytes = new byte[cleaned.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length)
        {
            return false;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    // --- Plugins Analyser ---

    private string _pluginsAnalyserReport = string.Empty;
    public string PluginsAnalyserReport
    {
        get => _pluginsAnalyserReport;
        private set => SetField(ref _pluginsAnalyserReport, value);
    }

    public RelayCommand PluginsAnalyserCommand { get; private set; } = null!;

    private void RunPluginsAnalyser()
    {
        if (string.IsNullOrWhiteSpace(AnalysisFolder) || !Directory.Exists(AnalysisFolder))
        {
            PluginsAnalyserReport = "Choose a valid folder first.";
            return;
        }

        var filesScanned = 0;
        var filesFailed = 0;
        var totalEntries = 0;
        var byType = new Dictionary<string, int>();

        foreach (var path in EnumerateSc4Files(AnalysisFolder))
        {
            try
            {
                var file = new DBPFFile(path);
                filesScanned++;
                foreach (var entry in file.ListOfEntries)
                {
                    totalEntries++;
                    var label = entry.TGI.GetEntryType();
                    byType[label] = byType.GetValueOrDefault(label) + 1;
                }
            }
            catch
            {
                filesFailed++;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Folder: {AnalysisFolder}");
        sb.AppendLine($"Files scanned: {filesScanned} ({filesFailed} unreadable/skipped)");
        sb.AppendLine($"Total entries: {totalEntries:N0}");
        sb.AppendLine();
        sb.AppendLine("By type:");
        foreach (var kvp in byType.OrderByDescending(k => k.Value))
        {
            sb.AppendLine($"  {kvp.Value,6:N0}  {kvp.Key}");
        }

        PluginsAnalyserReport = sb.ToString();
    }

    // --- Exemplar/Cohort Analyser (folds in DlgPropCount's property-usage counting) ---

    private bool _exemplarAnalyserWholeFolder;
    public bool ExemplarAnalyserWholeFolder
    {
        get => _exemplarAnalyserWholeFolder;
        set => SetField(ref _exemplarAnalyserWholeFolder, value);
    }

    private string _exemplarAnalyserReport = string.Empty;
    public string ExemplarAnalyserReport
    {
        get => _exemplarAnalyserReport;
        private set => SetField(ref _exemplarAnalyserReport, value);
    }

    public RelayCommand ExemplarAnalyserCommand { get; private set; } = null!;

    private void RunExemplarAnalyser()
    {
        var exemplarCount = 0;
        var cohortCount = 0;
        var propertyTotal = 0;
        var byProperty = new Dictionary<uint, int>();

        void ScanEntries(IEnumerable<DBPFEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry is not DBPFEntryEXMP exmp)
                {
                    continue;
                }

                if (exmp.IsCohort) cohortCount++; else exemplarCount++;

                foreach (var propertyId in exmp.ListOfProperties.Keys)
                {
                    propertyTotal++;
                    byProperty[propertyId] = byProperty.GetValueOrDefault(propertyId) + 1;
                }
            }
        }

        if (ExemplarAnalyserWholeFolder)
        {
            if (string.IsNullOrWhiteSpace(AnalysisFolder) || !Directory.Exists(AnalysisFolder))
            {
                ExemplarAnalyserReport = "Choose a valid folder first.";
                return;
            }

            foreach (var path in EnumerateSc4Files(AnalysisFolder))
            {
                try
                {
                    ScanEntries(new DBPFFile(path).ListOfEntries);
                }
                catch
                {
                    // Unreadable/corrupt file - skip it.
                }
            }
        }
        else if (_service.CurrentFile is not null)
        {
            ScanEntries(_service.Entries);
        }
        else
        {
            ExemplarAnalyserReport = "No document open in this tab.";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Exemplars: {exemplarCount:N0}   Cohorts: {cohortCount:N0}");
        sb.AppendLine($"Total property occurrences: {propertyTotal:N0} across {byProperty.Count:N0} distinct property IDs");
        sb.AppendLine();
        sb.AppendLine("Most common properties:");
        foreach (var kvp in byProperty.OrderByDescending(k => k.Value).Take(30))
        {
            var name = PropertyRegistry.FindById(kvp.Key)?.Name ?? "(unknown)";
            sb.AppendLine($"  {kvp.Value,6:N0}  0x{kvp.Key:X8}  {name}");
        }

        ExemplarAnalyserReport = sb.ToString();
    }

    // --- Property Find/Count (DlgPropFind/DlgPropFindValue/DlgPropFindPCohort) ---

    private string _propertyFindIdHex = "0x00000000";
    public string PropertyFindIdHex
    {
        get => _propertyFindIdHex;
        set => SetField(ref _propertyFindIdHex, value);
    }

    /// <summary>Empty = match presence of the property only (DlgPropFind); non-empty = also require this value (DlgPropFindValue).</summary>
    private string _propertyFindValueText = string.Empty;
    public string PropertyFindValueText
    {
        get => _propertyFindValueText;
        set => SetField(ref _propertyFindValueText, value);
    }

    private bool _propertyFindWholeFolder;
    public bool PropertyFindWholeFolder
    {
        get => _propertyFindWholeFolder;
        set => SetField(ref _propertyFindWholeFolder, value);
    }

    public ObservableCollection<AnalysisResultRowViewModel> PropertyFindResults { get; } = new();

    private string _propertyFindStatusMessage = "Enter a property ID (hex), optionally a value to match, then Search.";
    public string PropertyFindStatusMessage
    {
        get => _propertyFindStatusMessage;
        private set => SetField(ref _propertyFindStatusMessage, value);
    }

    public RelayCommand PropertyFindCommand { get; private set; } = null!;

    private void RunPropertyFind()
    {
        PropertyFindResults.Clear();
        try
        {
            var propertyId = EntryClipboard.ParseHex(PropertyFindIdHex);
            var matchValue = PropertyFindValueText.Trim();
            var matched = 0;

            void Scan(string path, IEnumerable<DBPFEntry> entries)
            {
                foreach (var entry in entries)
                {
                    if (entry is not DBPFEntryEXMP exmp)
                    {
                        continue;
                    }

                    if (!exmp.ListOfProperties.TryGetValue(propertyId, out var prop))
                    {
                        continue;
                    }

                    if (matchValue.Length > 0 && !PropertyValueMatches(prop, matchValue))
                    {
                        continue;
                    }

                    PropertyFindResults.Add(new AnalysisResultRowViewModel
                    {
                        FilePath = path,
                        Tgi = entry.TGI,
                        EntryType = entry.TGI.GetEntryType(),
                        SizeBytes = entry.GetSize(),
                    });
                    matched++;
                }
            }

            if (PropertyFindWholeFolder)
            {
                if (string.IsNullOrWhiteSpace(AnalysisFolder) || !Directory.Exists(AnalysisFolder))
                {
                    PropertyFindStatusMessage = "Choose a valid folder first.";
                    return;
                }

                foreach (var path in EnumerateSc4Files(AnalysisFolder))
                {
                    try
                    {
                        Scan(path, new DBPFFile(path).ListOfEntries);
                    }
                    catch
                    {
                        // Unreadable/corrupt file - skip it.
                    }
                }
            }
            else if (_service.CurrentFile is not null)
            {
                Scan(_service.CurrentPath ?? "(unsaved)", _service.Entries);
            }

            PropertyFindStatusMessage = $"{matched} entr{(matched == 1 ? "y" : "ies")} found.";
        }
        catch (Exception ex)
        {
            PropertyFindStatusMessage = $"Error: {ex.Message}";
        }
    }

    private static bool PropertyValueMatches(DBPFProperty prop, string matchValue)
    {
        try
        {
            var data = prop.GetTypedData();
            foreach (var raw in data)
            {
                if (string.Equals(raw?.ToString(), matchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                try
                {
                    if (string.Equals($"0x{Convert.ToInt64(raw):X}", matchValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Not numeric (e.g. STRING-typed property) - the plain ToString() comparison above already covers it.
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    // --- Property Manager (FormProperty / ID_MGR_PROP) - browses the loaded property database ---

    private string _propertyManagerSearchText = string.Empty;
    public string PropertyManagerSearchText
    {
        get => _propertyManagerSearchText;
        set
        {
            if (SetField(ref _propertyManagerSearchText, value))
            {
                RefreshPropertyManagerResults();
            }
        }
    }

    public ObservableCollection<PropertyDefinition> PropertyManagerResults { get; } = new();

    private void RefreshPropertyManagerResults()
    {
        PropertyManagerResults.Clear();

        var query = PropertyManagerSearchText.Trim();
        IEnumerable<PropertyDefinition> source = PropertyRegistry.All.OrderBy(d => d.Name);
        if (query.Length > 0)
        {
            source = source.Where(d =>
                d.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || $"{d.Id:X8}".Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // Capped so typing a broad/empty query in a 10000+ property database stays snappy.
        foreach (var definition in source.Take(500))
        {
            PropertyManagerResults.Add(definition);
        }
    }

    // --- Saved filter presets (Ilive Reader's DlgFilters/DlgFiltersEx, WorkspaceFilter pane) ---

    public ObservableCollection<SavedAnalysisFilter> SavedFilters { get; } = new();

    private SavedAnalysisFilter? _selectedSavedFilter;
    public SavedAnalysisFilter? SelectedSavedFilter
    {
        get => _selectedSavedFilter;
        set
        {
            if (SetField(ref _selectedSavedFilter, value))
            {
                LoadFilterCommand.RaiseCanExecuteChanged();
                DeleteFilterCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _newFilterName = string.Empty;
    public string NewFilterName
    {
        get => _newFilterName;
        set
        {
            if (SetField(ref _newFilterName, value))
            {
                SaveFilterCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand SaveFilterCommand { get; private set; } = null!;
    public RelayCommand LoadFilterCommand { get; private set; } = null!;
    public RelayCommand DeleteFilterCommand { get; private set; } = null!;

    private void SaveFilter()
    {
        var name = NewFilterName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var filter = new SavedAnalysisFilter
        {
            Name = name,
            FilterType = FindFilterType,
            TypeHex = FindTypeHex,
            FilterGroup = FindFilterGroup,
            GroupHex = FindGroupHex,
            FilterInstance = FindFilterInstance,
            InstanceHex = FindInstanceHex,
            HexPattern = FindHexPattern,
        };

        // Replace an existing preset with the same name rather than piling up duplicates.
        var existingIndex = -1;
        for (var i = 0; i < SavedFilters.Count; i++)
        {
            if (string.Equals(SavedFilters[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            SavedFilters[existingIndex] = filter;
        }
        else
        {
            SavedFilters.Add(filter);
        }

        _filterService.Save(new List<SavedAnalysisFilter>(SavedFilters));
        FindStatusMessage = $"Filter \"{name}\" saved.";
    }

    private void LoadFilter()
    {
        if (SelectedSavedFilter is not { } filter)
        {
            return;
        }

        FindFilterType = filter.FilterType;
        FindTypeHex = filter.TypeHex;
        FindFilterGroup = filter.FilterGroup;
        FindGroupHex = filter.GroupHex;
        FindFilterInstance = filter.FilterInstance;
        FindInstanceHex = filter.InstanceHex;
        FindHexPattern = filter.HexPattern;
        FindStatusMessage = $"Filter \"{filter.Name}\" loaded - press Search to run it.";
    }

    private void DeleteFilter()
    {
        if (SelectedSavedFilter is not { } filter)
        {
            return;
        }

        SavedFilters.Remove(filter);
        _filterService.Save(new List<SavedAnalysisFilter>(SavedFilters));
    }

    // ---------------------------------------------------------------
    // UI Editor (Ilive Reader's FormUI/DlgUIProp/DlgUIProperties/DlgUIImport, only shown
    // while IsUiEditorMode is active): tree of the selected UI entry's element nodes,
    // an editable Prop/Value grid for the selected node (Ilive Reader's own property grid
    // is exactly this - a flat name/value list per node, see DlgUIProperties::Display/OnOK),
    // and a live 2D preview (UiPreviewControl) that also supports drag-to-reposition.
    // Entries in this mode are plain text (UiLegacyParser), not exemplars - a separate
    // panel from the Properties list used everywhere else, same reasoning as the LUA editor.
    //
    // ponytail: no image (CxImage/FSH "image=" prop) rendering in the preview - drawing a
    // flat colored rect with the node's caption covers "does this layout fit together"
    // without also porting an FSH-to-bitmap resolver here (S3DViewerControl.Texture already
    // does this for S3D models; wiring the same resolver into the UI preview is a small
    // follow-up if button/background art needs to be seen, not just placement).
    // ---------------------------------------------------------------

    public ObservableCollection<UiLegacyNodeViewModel> UiRootNodes { get; } = new();

    private UiLegacyNodeViewModel? _selectedUiNode;
    public UiLegacyNodeViewModel? SelectedUiNode
    {
        get => _selectedUiNode;
        set
        {
            if (SetField(ref _selectedUiNode, value))
            {
                RefreshUiProperties();
                RemoveUiNodeCommand.RaiseCanExecuteChanged();
                AddUiChildNodeCommand.RaiseCanExecuteChanged();
                AddUiPropertyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<UiLegacyProp> UiProperties { get; } = new();

    private UiLegacyProp? _selectedUiProperty;
    public UiLegacyProp? SelectedUiProperty
    {
        get => _selectedUiProperty;
        set
        {
            if (SetField(ref _selectedUiProperty, value))
            {
                RemoveUiPropertyCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<UiPreviewControl.PreviewBox> UiPreviewBoxes { get; } = new();

    private double _uiPreviewContentWidth = 400;
    /// <summary>Full extent of the currently loaded dialog after CollectPreviewBoxes' top-left
    /// normalization - bound to UiPreviewControl's own Width/Height in Views/DbpfWorkspaceView.axaml
    /// so the ScrollViewer wrapping it knows how much room to actually make scrollable, instead of
    /// shrinking the control to nothing (a plain Control with no override reports zero desired
    /// size) or clipping a dialog bigger than the visible panel.</summary>
    public double UiPreviewContentWidth
    {
        get => _uiPreviewContentWidth;
        private set => SetField(ref _uiPreviewContentWidth, value);
    }

    private double _uiPreviewContentHeight = 300;
    public double UiPreviewContentHeight
    {
        get => _uiPreviewContentHeight;
        private set => SetField(ref _uiPreviewContentHeight, value);
    }

    private string _uiEditorStatusMessage = string.Empty;
    public string UiEditorStatusMessage
    {
        get => _uiEditorStatusMessage;
        set => SetField(ref _uiEditorStatusMessage, value);
    }

    private UiLegacyNode? _uiRoot;

    public RelayCommand AddUiChildNodeCommand { get; private set; } = null!;
    public RelayCommand RemoveUiNodeCommand { get; private set; } = null!;
    public RelayCommand AddUiPropertyCommand { get; private set; } = null!;
    public RelayCommand RemoveUiPropertyCommand { get; private set; } = null!;
    public RelayCommand RefreshUiPreviewCommand { get; private set; } = null!;
    public RelayCommand SaveUiEditorCommand { get; private set; } = null!;
    public RelayCommand SelectUiNodeCommand { get; private set; } = null!;
    public RelayCommand MoveUiNodeCommand { get; private set; } = null!;

    // ---------------------------------------------------------------
    // UI Finder: jumps to any UI element (TGI Type 0x00000000) found by the shared,
    // app-wide UiElementIndexService (see App.axaml.cs and that class) - the actual scan
    // across the SC4 installation folder and the Plugins folder runs once automatically in
    // the background shortly after the app starts, not per-tab, since it describes those
    // folders themselves rather than any one open document. This document only needs to
    // know how to jump to a hit once the person picks one from that shared list.
    // ---------------------------------------------------------------

    /// <summary>Jumps to a UI Finder hit the same way SelectAnalysisResult does (open a new tab
    /// if it's not the file already open here), and additionally switches the destination tab
    /// into UI Editor mode, since finding a UI element implies wanting to edit it.</summary>
    public void SelectUiFinderResult(AnalysisResultRowViewModel row)
    {
        if (_service.CurrentPath is { } currentPath &&
            string.Equals(Path.GetFullPath(currentPath), Path.GetFullPath(row.FilePath), StringComparison.OrdinalIgnoreCase))
        {
            IsUiEditorMode = true;
            SelectEntryByTgi(row.Tgi);
            return;
        }

        var newDoc = OpenDocumentInNewTab?.Invoke(row.FilePath);
        if (newDoc is null)
        {
            return;
        }

        newDoc.IsUiEditorMode = true;
        newDoc.SelectEntryByTgi(row.Tgi);
    }

    private UiLegacyNodeViewModel? FindUiNodeViewModel(UiLegacyNode node, IEnumerable<UiLegacyNodeViewModel>? roots = null)
    {
        foreach (var vm in roots ?? UiRootNodes)
        {
            if (vm.Node == node)
            {
                return vm;
            }

            if (FindUiNodeViewModel(node, vm.Children) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void LoadUiEditorForSelectedEntry()
    {
        UiRootNodes.Clear();
        UiProperties.Clear();
        UiPreviewBoxes.Clear();
        SelectedUiNode = null;
        _uiRoot = null;
        foreach (var cachedImage in _uiPreviewSourceImageCache.Values)
        {
            cachedImage?.Dispose();
        }

        _uiPreviewSourceImageCache.Clear();
        _uiPreviewFinalImageCache.Clear();

        if (SelectedEntry is null || SelectedEntry.Entry.TGI.TypeID != 0)
        {
            return;
        }

        try
        {
            var bytes = RawEntryBytes.GetDecompressed(SelectedEntry.Entry) ?? Array.Empty<byte>();
            var text = Encoding.UTF8.GetString(bytes);
            _uiRoot = UiLegacyParser.Parse(text);

            foreach (var child in _uiRoot.Children)
            {
                UiRootNodes.Add(new UiLegacyNodeViewModel(child));
            }

            UiEditorStatusMessage = $"{_uiRoot.Children.Count} top-level node(s) parsed.";
            RefreshUiPreview();
        }
        catch (Exception ex)
        {
            UiEditorStatusMessage = $"Error parsing UI entry: {ex.Message}";
        }
    }

    private void RefreshUiProperties()
    {
        UiProperties.Clear();
        SelectedUiProperty = null;

        if (SelectedUiNode is not null)
        {
            foreach (var prop in SelectedUiNode.Node.Properties)
            {
                UiProperties.Add(prop);
            }
        }
    }

    private void AddUiChildNode()
    {
        var parent = SelectedUiNode?.Node ?? _uiRoot;
        if (parent is null)
        {
            return;
        }

        var node = new UiLegacyNode { Parent = parent };
        node.Properties.Add(new UiLegacyProp { Key = "clsid", Value = "\"NewElement\"" });
        parent.Children.Add(node);

        var nodeVm = new UiLegacyNodeViewModel(node);
        if (SelectedUiNode is not null)
        {
            SelectedUiNode.Children.Add(nodeVm);
        }
        else
        {
            UiRootNodes.Add(nodeVm);
        }

        SelectedUiNode = nodeVm;
        RefreshUiPreview();
    }

    private void RemoveUiNode()
    {
        if (SelectedUiNode is null || _uiRoot is null)
        {
            return;
        }

        var parentNode = SelectedUiNode.Node.Parent ?? _uiRoot;
        parentNode.Children.Remove(SelectedUiNode.Node);

        // The tree is fully rebuilt from the model rather than hunting for which
        // ObservableCollection<UiLegacyNodeViewModel> owns this node - the UI trees here
        // are small (hand-authored dialogs, not generated content), so this isn't worth
        // tracking a parent-VM pointer for.
        UiRootNodes.Clear();
        foreach (var child in _uiRoot.Children)
        {
            UiRootNodes.Add(new UiLegacyNodeViewModel(child));
        }

        SelectedUiNode = null;
        RefreshUiPreview();
    }

    private void AddUiProperty()
    {
        if (SelectedUiNode is null)
        {
            return;
        }

        var prop = new UiLegacyProp { Key = "prop", Value = "value" };
        SelectedUiNode.Node.Properties.Add(prop);
        UiProperties.Add(prop);
        SelectedUiNode.RefreshName();
        RefreshUiPreview();
    }

    private void RemoveUiProperty()
    {
        if (SelectedUiNode is null || SelectedUiProperty is null)
        {
            return;
        }

        SelectedUiNode.Node.Properties.Remove(SelectedUiProperty);
        UiProperties.Remove(SelectedUiProperty);
        SelectedUiNode.RefreshName();
        RefreshUiPreview();
    }

    /// <summary>Same "area"/"caption"/"fillcolor" reading and parent-relative offsetting as Ilive Reader's BuildPreviewUI (ui_common.cpp) - see UiPreviewControl for the actual drawing.</summary>
    private void RefreshUiPreview()
    {
        UiPreviewBoxes.Clear();
        if (_uiRoot is null)
        {
            UiPreviewContentWidth = 400;
            UiPreviewContentHeight = 300;
            return;
        }

        try
        {
            var collected = new List<UiPreviewControl.PreviewBox>();
            foreach (var child in _uiRoot.Children)
            {
                CollectPreviewBoxes(child, new Avalonia.PixelRect(0, 0, 0, 0), true, collected);
            }

            if (collected.Count == 0)
            {
                UiPreviewContentWidth = 400;
                UiPreviewContentHeight = 300;
                return;
            }

            // Dialogs are authored with "area" coordinates relative to the game's own screen
            // (e.g. anchored near the bottom-right of a 1024x768+ resolution), which could be
            // far outside this preview panel's own much smaller bounds - drawn at their literal
            // coordinates, only the top-left corner of the panel's own (0,0) origin would ever
            // show anything, with the rest of the dialog clipped off silently. Shift every box
            // by the whole dialog's own top-left corner so it always starts flush at (0,0) of
            // the preview panel, regardless of where it was meant to sit on the real game
            // screen, and size the control (via UiPreviewContentWidth/Height, bound in
            // DbpfWorkspaceView.axaml) to the dialog's own full extent so anything still bigger
            // than the visible panel is reachable by scrolling instead of clipped.
            var minX = collected.Min(b => b.Area.X);
            var minY = collected.Min(b => b.Area.Y);
            var maxRight = 0;
            var maxBottom = 0;
            foreach (var box in collected)
            {
                // PRIORITY INSTRUCTION: always subtract (minX, minY) unconditionally, for
                // every box, with no special case. A previous version skipped the
                // subtraction for whichever box(es) exactly equalled the global (minX, minY)
                // - almost always the outer/topmost chrome box itself, since it's the
                // dialog's own top-left-most element - treating "this box IS the minimum" as
                // "leave it as box.Area unchanged". That's only correct when minX/minY are
                // already 0; in every real case (minX/minY are the dialog's actual raw
                // screen-relative coordinates, e.g. (152,159)), it left the chrome pinned to
                // its RAW unshifted absolute position while every other box (content: text,
                // sliders, buttons - none of which land on both minX AND minY at once) got
                // correctly shifted down near (0,0). That's exactly what made a dialog's
                // background/border render visibly detached from its own content in every
                // screenshot of this bug - not a decode/sizing issue at all, just this one
                // broken "optimization" a few lines up from here.
                var shifted = new Avalonia.PixelRect(box.Area.X - minX, box.Area.Y - minY, box.Area.Width, box.Area.Height);
                UiPreviewBoxes.Add(box with { Area = shifted });
                maxRight = Math.Max(maxRight, shifted.X + shifted.Width);
                maxBottom = Math.Max(maxBottom, shifted.Y + shifted.Height);
            }

            UiPreviewContentWidth = Math.Max(maxRight, 1);
            UiPreviewContentHeight = Math.Max(maxBottom, 1);
        }
        catch (Exception ex)
        {
            // A UI entry's own data (or its referenced images) is arbitrary content someone
            // else authored, sometimes by hand - malformed "area"/"image" values, a corrupt
            // or unusually-encoded referenced PNG, or a pathological coordinate combination
            // this code didn't anticipate, must never be allowed to crash the whole app.
            // PRIORITY INSTRUCTION: RefreshUiPreview must never throw past this point - every
            // caller (entry selection, the node-properties dialog, add/remove node/property)
            // treats a rendering problem as "show what we can, report the rest" instead.
            UiPreviewBoxes.Clear();
            UiPreviewContentWidth = 400;
            UiPreviewContentHeight = 300;
            UiEditorStatusMessage = $"Preview error: {ex.Message}";
        }
    }

    private void CollectPreviewBoxes(UiLegacyNode node, Avalonia.PixelRect parentArea, bool isTopLevel, List<UiPreviewControl.PreviewBox> into)
    {
        var areaRaw = node.GetProp("area");

        // Not every node is a visual control - some are pure logical/grouping wrappers with
        // no "area" prop of their own. Skip drawing a box for these entirely; still recurse
        // into their children, inheriting the nearest real ancestor position unchanged
        // rather than snapping to (0,0)-of-parent.
        if (string.IsNullOrEmpty(areaRaw))
        {
            foreach (var missingAreaChild in node.Children)
            {
                CollectPreviewBoxes(missingAreaChild, parentArea, false, into);
            }

            return;
        }

        var area = ParseRect(areaRaw);
        if (!isTopLevel)
        {
            area = new Avalonia.PixelRect(area.X + parentArea.X, area.Y + parentArea.Y, area.Width, area.Height);
        }

        var captionRaw = node.GetProp("caption");
        var caption = captionRaw is { Length: >= 2 } && captionRaw[0] == '"' && captionRaw[^1] == '"'
            ? captionRaw.Substring(1, captionRaw.Length - 2)
            : captionRaw ?? string.Empty;

        into.Add(new UiPreviewControl.PreviewBox(node, area, caption, node.GetProp("iid") ?? string.Empty,
            ParseColor(node.GetProp("fillcolor")), ParseColor(node.GetProp("colorfontnormal")) ?? ParseColor(node.GetProp("forecolor")),
            node.GetProp("blttype") ?? string.Empty, ResolveUiImage(node, area),
            node.GetProp("style")?.Contains("radiocheck") ?? false));

        foreach (var child in node.Children)
        {
            CollectPreviewBoxes(child, area, false, into);
        }
    }

    private readonly Dictionary<string, ImgSharpImage?> _uiPreviewSourceImageCache = new();

    /// <summary>
    /// Resolves a UI node's "image" prop - Ilive Reader's own <c>TextToCxImage</c>
    /// (ui_common.cpp): the text is a <c>{group,instance}</c> reference to a plain PNG entry
    /// (TGI Type 0x856DDBAC, shared with BMP/JPEG - same constant confirmed in Ilive
    /// Reader's own sim015.h as ENT_PNG/ENT_BMP/ENT_JPEG) stored in this SAME package - not
    /// an external texture file, so csDBPF's own PNG decoder (already used for the regular
    /// image-entry preview - see LoadImagePreview) is all that's needed here too, no format
    /// port required. For <c>blttype="edge"</c> nodes, the source is then re-sliced to fit
    /// this specific node's own area (Ilive Reader's <c>EdgeCxImage</c> - the dialog-chrome
    /// 9-slice scaling that keeps rounded corners/borders crisp at any size instead of
    /// blurring them the way a plain stretch would), which is why the *decoded source* is
    /// cached (by group/instance, cleared per node-tree-load in
    /// LoadUiEditorForSelectedEntry) rather than the final per-node result - several
    /// differently-sized controls can share one source texture.
    /// </summary>
    private readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap?> _uiPreviewFinalImageCache = new();

    private Avalonia.Media.Imaging.Bitmap? ResolveUiImage(UiLegacyNode node, Avalonia.PixelRect area)
    {
        var imageProp = node.GetProp("image");
        if (string.IsNullOrEmpty(imageProp))
        {
            return null;
        }

        var blttype = node.GetProp("blttype");
        var iid = node.GetProp("iid") ?? string.Empty;
        var imagerect = node.GetProp("imagerect");
        var isRadioCheckButton = iid == "IGZWinBtn" && (node.GetProp("style")?.Contains("radiocheck") ?? false);

        // The 9-slice pixel-copy loop in BuildEdgeImage runs once per pixel of the
        // destination size (see its own doc comment) - cheap for any one box, but
        // RefreshUiPreview reruns CollectPreviewBoxes (and so this method) for every node on
        // every refresh, including ones nothing changed for, which added up to a real,
        // repeated cost on top of whatever it took to first resolve/decode the source image.
        // Cache the FINAL per-size result (not just the decoded source, which
        // _uiPreviewSourceImageCache already covers) so re-selecting the same entry or
        // hitting REFRESH PREVIEW without actually editing anything doesn't redo any of that
        // work. Keyed on every input that can change the output - two nodes sharing the same
        // source image but different imagerect/button-state-crop/size must never collide.
        var cacheKey = $"{imageProp}|{blttype}|{iid}|{imagerect}|{area.Width}x{area.Height}";
        if (_uiPreviewFinalImageCache.TryGetValue(cacheKey, out var cachedFinal))
        {
            return cachedFinal;
        }

        if (!_uiPreviewSourceImageCache.TryGetValue(imageProp, out var source))
        {
            source = DecodeUiSourceImage(imageProp);

            // Only cache a SUCCESSFUL decode. A null here is very often transient - most
            // commonly the shared background-index scan (UiElementIndexService, see
            // App.axaml.cs) simply hasn't reached the file this image actually lives in
            // yet, especially right after switching to a UI entry shortly after startup.
            // Caching that miss permanently would mean the image never appears even once
            // the index finishes moments later - the exact "elements disappear and never
            // come back" symptom this is fixing. A genuinely-missing image just gets
            // looked up again next refresh, which is cheap (single dictionary lookup) once
            // the index really doesn't have it.
            if (source is not null)
            {
                _uiPreviewSourceImageCache[imageProp] = source;
            }
        }

        Avalonia.Media.Imaging.Bitmap? result;
        var disposables = new List<ImgSharpImage>();
        try
        {
            if (source is null)
            {
                result = null;
            }
            else
            {
                // Faithful order from Ilive Reader's BuildPreviewUI (ui_common.cpp):
                var working = source;

                // 1) IGZWinBtn's own image is ALWAYS a spritesheet of side-by-side button
                //    states (normal/hover/pressed/disabled, or - for a radiocheck-style
                //    toggle button - 8 narrower check/radio states) - every button crops to
                //    just its own "normal" state before anything else happens to the image,
                //    regardless of whether the node has its own "imagerect". Missing this
                //    is exactly why a button previously showed as a whole row of identical-
                //    looking buttons (all 4, or all 8, states side by side) instead of one.
                if (iid == "IGZWinBtn")
                {
                    // PRIORITY INSTRUCTION: the original C++ (ui_common.cpp) calls this as
                    // Crop(left, top, RIGHT, bottom) - CxImage's Crop uses (l,t,r,b), not
                    // (l,t,width,height). CropImage here takes (left,top,WIDTH,height), so
                    // the standard (non-radiocheck) case's "right edge" of width/2 must be
                    // converted to an actual crop WIDTH of (width/2 - width/4) = width/4 -
                    // passing width/2 directly as the WIDTH parameter (as an earlier version
                    // did) crops a HALF-width span (2 of the 4 button states) instead of a
                    // QUARTER-width span (1 state), which is exactly why a button rendered
                    // as two side-by-side states instead of one. The radiocheck case doesn't
                    // need this adjustment since its own left edge is 0, where "right edge"
                    // and "width" happen to be the same value either way.
                    var cropped = isRadioCheckButton
                        ? CropImage(working, 0, 0, working.Width / 8, working.Height, disposables)
                        : CropImage(working, working.Width / 4, 0, working.Width / 4, working.Height, disposables);
                    working = cropped ?? working;
                }

                // 2) The node's own explicit "imagerect" - a (left,top,right,bottom) crop
                //    into whatever the image now is (post button-state-crop, if any), same
                //    "(l,t,r,b)" convention as "area"/ParseRect.
                if (!string.IsNullOrEmpty(imagerect))
                {
                    var rect = ParseRect(imagerect);
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        var cropped = CropImage(working, rect.X, rect.Y, rect.Width, rect.Height, disposables);
                        working = cropped ?? working;
                    }
                }

                // 3) blttype="edge" 9-slices whatever the image is at this point to this
                //    node's own area size (dialog chrome, mainly - buttons/imagerect icons
                //    essentially never combine with blttype="edge" in practice, but nothing
                //    here assumes they can't).
                if (blttype == "edge" && area.Width > 0 && area.Height > 0)
                {
                    var sliced = BuildEdgeImage(working, area.Width, area.Height);
                    if (sliced is not null)
                    {
                        disposables.Add(sliced);
                        working = sliced;
                    }

                    // PRIORITY INSTRUCTION: never let an "edge" image reach the renderer at
                    // any size other than exactly this node's own area - BuildEdgeImage
                    // above should already guarantee that, but this is the actual visible
                    // symptom being fixed (a dialog's chrome rendering bigger than its area
                    // and visibly detached from its content), so it's worth asserting
                    // directly rather than trusting that guarantee transitively.
                    if (working.Width != area.Width || working.Height != area.Height)
                    {
                        var forced = CropImage(working, 0, 0, area.Width, area.Height, disposables);
                        if (forced is not null)
                        {
                            working = forced;
                        }
                    }
                }

                result = ImageConversion.ToAvaloniaBitmap(working);
            }
        }
        catch
        {
            // Whatever went wrong converting/cropping/9-slicing this one image (unusual
            // pixel data, a pathological destination size, a malformed imagerect, ...) must
            // not take down the whole preview - this one box just shows without an image
            // (falls back to its plain fillcolor) instead.
            result = null;
        }
        finally
        {
            // Every crop/slice step above produces a NEW derived image distinct from the
            // shared, cached `source` - source itself is never mutated or disposed here (it
            // stays owned by _uiPreviewSourceImageCache), only these intermediate ones.
            foreach (var d in disposables)
            {
                d.Dispose();
            }
        }

        // Same "don't cache a miss" reasoning as the source-image cache above - result can
        // be null purely because `source` was (itself not yet cached, for the same
        // transient reason), not because this specific size/blttype combination is
        // actually unresolvable.
        if (result is not null)
        {
            _uiPreviewFinalImageCache[cacheKey] = result;
        }

        return result;
    }

    /// <summary>Crops <paramref name="src"/> to the given (left,top,width,height) region,
    /// clamped defensively to whatever of the source is actually there (a malformed/
    /// out-of-range imagerect or button-state division shouldn't throw). Returns a NEW
    /// image - never mutates src - added to <paramref name="trackForDisposal"/> so the
    /// caller can dispose every intermediate crop once it's done with the final result.</summary>
    private static ImgSharpImage? CropImage(ImgSharpImage src, int left, int top, int width, int height, List<ImgSharpImage> trackForDisposal)
    {
        if (src.Width <= 0 || src.Height <= 0)
        {
            return null;
        }

        left = Math.Clamp(left, 0, src.Width - 1);
        top = Math.Clamp(top, 0, src.Height - 1);
        width = Math.Clamp(width, 1, src.Width - left);
        height = Math.Clamp(height, 1, src.Height - top);

        var dest = new ImgSharpImage(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                dest[x, y] = src[left + x, top + y];
            }
        }

        trackForDisposal.Add(dest);
        return dest;
    }

    private ImgSharpImage? DecodeUiSourceImage(string imageProp)
    {
        try
        {
            var trimmed = imageProp.Trim('{', '}');
            var parts = trimmed.Split(',');
            if (parts.Length != 2)
            {
                return null;
            }

            var group = EntryClipboard.ParseHex(parts[0]);
            var instance = EntryClipboard.ParseHex(parts[1]);

            var imageEntry = Entries.FirstOrDefault(e =>
                e.Entry.TGI.TypeID == 0x856DDBAC && e.Entry.TGI.GroupID == group && e.Entry.TGI.InstanceID == instance);

            if (imageEntry?.Entry is not null)
            {
                return DecodePng(imageEntry.Entry);
            }

            // Not in this file's own entries - UI dialogs very commonly reference shared
            // chrome/background graphics (rounded borders, button skins, ...) that live in
            // the base game's own package files rather than being duplicated into every
            // single UI-definition .dat that uses them. Ilive Reader itself would show the
            // exact same plain-fillcolor fallback box if opening this one file in isolation
            // without the rest of the game's assets available - falling back to a search of
            // the configured SC4 installation folder (see Options) is this app's own
            // addition on top of that, not something the original ever did.
            //
            // PRIORITY INSTRUCTION: .Clone() here is required, not optional. UiElementIndex
            // is one shared, app-wide service (every tab/document uses the same instance -
            // see App.axaml.cs) that keeps its OWN cache of decoded shared images
            // (UiElementIndexService._sharedImageCache) so the same background/button-skin
            // graphic isn't re-decoded for every dialog that references it. Without cloning,
            // this method would hand back that exact cached object by reference - which then
            // gets stored in THIS document's own _uiPreviewSourceImageCache and disposed the
            // next time LoadUiEditorForSelectedEntry runs (switching entries, even to an
            // unrelated one). That silently corrupts the SHARED cache entry too, since it's
            // the same object - the next lookup for that image, from this document or any
            // other tab, returns an already-disposed image that fails to decode/9-slice and
            // silently falls back to no image at all. That's exactly the "images work once,
            // then vanish on the next selection - even after switching away and back" bug
            // this fixes: each document needs its own independently-disposable copy.
            return UiElementIndex.ResolveSharedImage(0x856DDBAC, group, instance)?.Clone();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// PRIORITY INSTRUCTION: never call DBPFEntryPNG.Decode() directly on a possibly-
    /// compressed entry. Confirmed against csDBPF's own source (Entries/DBPFEntryPNG.cs):
    /// unlike DBPFEntryUI/DBPFEntryLTEXT/DBPFEntryFSH/DBPFEntryEXMP, its Decode() calls
    /// `Image.Load(ByteData)` directly with NO IsCompressed check/QFS.Decompress call first
    /// - a real gap in that library, not something in this app's own pipeline. Most base-
    /// game/shared UI chrome images ARE QFS-compressed, so Decode() was being handed
    /// compressed bytes: ImageSharp's format-sniffing occasionally parsed that "successfully"
    /// into a non-null Image with essentially garbage width/height derived from misread
    /// compressed data instead of throwing outright - exactly why a dialog's chrome kept
    /// rendering at some unrelated size, visibly detached from its own content, even after
    /// the coordinate math and the "always fits its own area" sizing guarantees were both
    /// independently verified correct. ByteData's setter is `protected` (DBPFEntry.cs), so
    /// there's no way to hand a fixed-up entry back into csDBPF's own Decode() - decoding
    /// directly via ImageSharp after this app's own RawEntryBytes.GetDecompressed (already
    /// used everywhere else non-PNG in this codebase) sidesteps the gap entirely, and
    /// doubles as the BMP/JPEG-under-the-same-shared-TypeID fallback in one step, since
    /// Image.Load auto-detects the real format instead of assuming PNG specifically.
    /// </summary>
    private static ImgSharpImage? DecodePng(DBPFEntry entry)
    {
        try
        {
            var bytes = RawEntryBytes.GetDecompressed(entry);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            using var image = SixLabors.ImageSharp.Image.Load(bytes);
            // .CloneAs<Rgba32>() guarantees a concrete Image<Rgba32> regardless of the
            // decoded image's own exact pixel format (indexed/greyscale/etc PNGs all decode
            // to something other than Rgba32 directly) - required anyway since
            // BuildEdgeImage's per-pixel indexer below needs a known concrete pixel type to
            // compile against.
            return image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Port of Ilive Reader's <c>EdgeCxImage</c> (ui_common.cpp): 9-slice-scales
    /// <paramref name="src"/> to exactly <paramref name="destWidth"/>x<paramref name="destHeight"/>.
    /// The source is treated as a 3x3 grid of equal thirds - the 4 corner thirds are copied
    /// unstretched to the 4 corners of the result; the remaining middle third of each edge,
    /// and the very center third, are repeated (tiled) to fill however much space is left,
    /// rather than being stretched/blurred. This is what keeps a dialog's rounded corner/
    /// border artwork sharp at any dialog size.
    ///
    /// PRIORITY INSTRUCTION: this ALWAYS returns an image sized exactly destWidth x
    /// destHeight (never null except for a genuinely invalid destination size) - an earlier
    /// version returned null as a "already the right size, caller can just use src as-is"
    /// shortcut, but the caller (ResolveUiImage) can't actually verify that assumption still
    /// holds once other crop steps (button-state/imagerect) have already run first, and a
    /// shared chrome/background image is routinely reused at several different sizes across
    /// different dialogs anyway. Falling back to src's own native size when it DIDN'T
    /// actually match the target is exactly what made a dialog's chrome render bigger than
    /// its own area and visibly detached from its content - this always produces a result
    /// that fits, with a plain copy standing in for the 9-slice math only in the genuine
    /// exact-match case.
    /// </summary>
    private static ImgSharpImage? BuildEdgeImage(ImgSharpImage src, int destWidth, int destHeight)
    {
        if (destWidth <= 0 || destHeight <= 0)
        {
            return null;
        }

        var sw = src.Width;
        var sh = src.Height;
        if (sw == destWidth && sh == destHeight)
        {
            return src.Clone();
        }

        var thirdW = Math.Max(1, sw / 3);
        var thirdH = Math.Max(1, sh / 3);

        var dest = new ImgSharpImage(destWidth, destHeight);

        for (var dy = 0; dy < destHeight; dy++)
        {
            int sy;
            if (dy < thirdH)
            {
                sy = dy;
            }
            else if (dy >= destHeight - thirdH)
            {
                sy = sh - (destHeight - dy);
            }
            else
            {
                sy = thirdH + (dy - thirdH) % thirdH;
            }

            for (var dx = 0; dx < destWidth; dx++)
            {
                int sx;
                if (dx < thirdW)
                {
                    sx = dx;
                }
                else if (dx >= destWidth - thirdW)
                {
                    sx = sw - (destWidth - dx);
                }
                else
                {
                    sx = thirdW + (dx - thirdW) % thirdW;
                }

                dest[dx, dy] = src[sx, sy];
            }
        }

        return dest;
    }

    /// <summary>Parses Ilive Reader's "(left,top,right,bottom)" rect text (ui_common.cpp's TextToCRect) into an Avalonia-friendly X/Y/Width/Height rectangle.</summary>
    private static Avalonia.PixelRect ParseRect(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new Avalonia.PixelRect(0, 0, 0, 0);
        }

        var parts = text.Trim('(', ')').Split(',');
        if (parts.Length != 4)
        {
            return new Avalonia.PixelRect(0, 0, 0, 0);
        }

        if (!int.TryParse(parts[0], out var l) || !int.TryParse(parts[1], out var t) ||
            !int.TryParse(parts[2], out var r) || !int.TryParse(parts[3], out var b))
        {
            return new Avalonia.PixelRect(0, 0, 0, 0);
        }

        return new Avalonia.PixelRect(l, t, r - l, b - t);
    }

    /// <summary>Parses Ilive Reader's "(r,g,b)" color text (ui_common.cpp's TextToColor).</summary>
    private static Avalonia.Media.Color? ParseColor(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var parts = text.Trim('(', ')').Split(',');
        if (parts.Length != 3 ||
            !byte.TryParse(parts[0], out var r) || !byte.TryParse(parts[1], out var g) || !byte.TryParse(parts[2], out var b))
        {
            return null;
        }

        return new Avalonia.Media.Color(255, r, g, b);
    }

    /// <summary>Called from UiPreviewControl when a box is dragged - writes the new position straight back into that node's "area" prop, live (Ilive Reader's own preview is read-only; this adds real drag-to-reposition on top).</summary>
    public void MoveUiNode(UiLegacyNode node, int newLeft, int newTop)
    {
        var area = ParseRect(node.GetProp("area"));
        var width = area.Width;
        var height = area.Height;
        var newArea = $"({newLeft},{newTop},{newLeft + width},{newTop + height})";

        var existing = node.Properties.FirstOrDefault(p => p.Key == "area");
        if (existing is not null)
        {
            existing.Value = newArea;
        }
        else
        {
            node.Properties.Add(new UiLegacyProp { Key = "area", Value = newArea });
        }

        if (SelectedUiNode?.Node == node)
        {
            RefreshUiProperties();
        }

        RefreshUiPreview();
    }

    /// <summary>Re-encodes the whole tree (Ilive Reader's CUIParse::Encode) and writes it back into the selected entry - the UI Editor's Save().</summary>
    private void SaveUiEditor()
    {
        if (SelectedEntry is null || _uiRoot is null)
        {
            return;
        }

        try
        {
            var text = UiLegacyParser.Encode(_uiRoot);
            var bytes = Encoding.UTF8.GetBytes(text);
            var oldVm = SelectedEntry;
            var index = Entries.IndexOf(oldVm);

            var newEntry = _service.ReplaceEntryBytes(oldVm.Entry, bytes);
            try
            {
                _service.SetEntryCompression(newEntry, false);
            }
            catch
            {
                // Not fatal - see SaveLuaScriptToSelectedEntry's identical note.
            }

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
            UiEditorStatusMessage = "UI entry saved (remember to save the package).";
        }
        catch (Exception ex)
        {
            UiEditorStatusMessage = $"Error saving UI entry: {ex.Message}";
        }
    }

    /// <summary>
    /// Batch-exports the given entries' decoded (decompressed) bytes into <paramref name="folder"/>
    /// (Ilive Reader's "Save decoded file", ID_MENU_SAVEDECODEDFILE / <c>ExtractSelectedFiles(TRUE)</c>,
    /// but multi-select instead of one file-save-dialog per entry) - same underlying export
    /// (<see cref="EntryExporter.ExportAll"/>, with the matching ".TGI" companion file) as
    /// <see cref="ExportAllEntries"/>, just scoped to a chosen set of entries instead of the
    /// whole package.
    /// </summary>
    public void ExportSelectedEntriesDecoded(IReadOnlyCollection<DBPFEntry> entries, string folder)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            var (succeeded, failed, warnings) = EntryExporter.ExportAll(entries, folder);
            StatusMessage = (failed, warnings) switch
            {
                (0, 0) => $"{succeeded} decoded entr(y/ies) exported to: {folder}",
                (0, > 0) => $"{succeeded} decoded entr(y/ies) exported to: {folder} — {warnings} with a validation warning",
                _ => $"{succeeded} decoded entr(y/ies) exported ({warnings} with a warning), {failed} skipped (error), to: {folder}",
            };
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while exporting: {ex.Message}";
        }
    }

    /// <summary>
    /// "EXPORT READABLE (TXT)...": writes each selected entry's content in genuinely
    /// human-readable form - one .txt file per entry, same TTTTTTTT-GGGGGGGG-IIIIIIII
    /// naming as every other export in this app - instead of raw bytes. For Exemplar/
    /// Cohort entries in particular (the common complaint: opening the ".exmp"/".cohort"
    /// binary export in Notepad shows garbage, because that binary "EQZB" property-table
    /// layout was never meant to be read as text) this decodes every property to its name
    /// (via <see cref="PropertyRegistry"/>) and formatted value/array - exactly the same
    /// text <see cref="EntryDescriber.Describe"/> already builds for the Details panel, just
    /// written to a file instead of shown inline. LTEXT/UI/FSH and everything else
    /// EntryDescriber knows how to summarize come along the same way; entries it has no
    /// structured reader for still get *something* useful (TGI, type, size) rather than a
    /// blank/garbled file.
    /// </summary>
    public void ExportSelectedEntriesReadable(IReadOnlyCollection<DBPFEntry> entries, string folder)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            var succeeded = 0;
            var failed = 0;

            foreach (var entry in entries)
            {
                try
                {
                    var text = EntryDescriber.Describe(entry, PropertyRegistry);
                    var fileName = EntryExporter.BaseFileName(entry) + ".readable.txt";
                    File.WriteAllText(Path.Combine(folder, fileName), text);
                    succeeded++;
                }
                catch
                {
                    failed++;
                }
            }

            StatusMessage = failed == 0
                ? $"{succeeded} entr(y/ies) exported as readable text to: {folder}"
                : $"{succeeded} entr(y/ies) exported as readable text ({failed} skipped due to an error), to: {folder}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while exporting readable text: {ex.Message}";
        }
    }

    /// <summary>
    /// Ilive Reader's "Save header as text" (ID_MENU_SAVEHEADER_TXT) wrote each selected
    /// entry's raw 20-byte on-disk index record as a hex byte dump to its own auto-named
    /// .hdr file. csDBPF doesn't expose that raw on-disk index record, so this writes the
    /// same information in a readable form instead - one line per entry (Type/Group/
    /// Instance/size in hex) into a single chosen file, covering every currently selected
    /// entry in one go rather than one file-save-dialog per entry.
    /// </summary>
    public void SaveSelectedEntryHeaders(IReadOnlyCollection<DBPFEntry> entries, string filePath)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                var tgi = entry.TGI;
                var size = entry.ByteData?.Length ?? 0;
                sb.AppendLine($"Type=0x{tgi.TypeID:X8} Group=0x{tgi.GroupID:X8} Instance=0x{tgi.InstanceID:X8} Size=0x{size:X8}");
            }

            File.WriteAllText(filePath, sb.ToString());
            StatusMessage = $"{entries.Count} entry header(s) saved to: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while saving headers: {ex.Message}";
        }
    }

}
