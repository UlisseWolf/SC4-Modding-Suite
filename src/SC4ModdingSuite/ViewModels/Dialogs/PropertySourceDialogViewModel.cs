using System;
using System.Threading.Tasks;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the dialog shown at every program startup (and reopenable at any time from the
/// toolbar) that lets the user pick which new_properties.xml source to use: NAM Team,
/// UlisseWolf's CAM-oriented patches, or a developer's local override file if present.
/// </summary>
public sealed class PropertySourceDialogViewModel : ViewModelBase
{
    private readonly PropertySourceService _service;
    private readonly UiElementIndexService _uiElementIndex;
    private readonly string? _sc4InstallFolder;
    private readonly string? _pluginsFolder;

    public PropertySourceDialogViewModel(
        PropertySourceService service,
        PropertySource lastUsedSource,
        UiElementIndexService uiElementIndex,
        string? sc4InstallFolder,
        string? pluginsFolder)
    {
        _service = service;
        _uiElementIndex = uiElementIndex;
        _sc4InstallFolder = sc4InstallFolder;
        _pluginsFolder = pluginsFolder;
        HasLocalOverride = service.HasLocalOverride;
        LocalOverridePath = service.LocalOverridePath;

        if (HasLocalOverride)
        {
            _useLocalOverride = true;
        }
        else if (lastUsedSource == PropertySource.UlisseWolfPatches)
        {
            _ulisseWolfSelected = true;
        }
        else
        {
            _namTeamSelected = true;
        }

        ContinueCommand = new AsyncRelayCommand(ContinueAsync);
    }

    public bool HasLocalOverride { get; }
    public string LocalOverridePath { get; }

    private bool _namTeamSelected;
    public bool NamTeamSelected
    {
        get => _namTeamSelected;
        set
        {
            if (SetField(ref _namTeamSelected, value) && value)
            {
                UlisseWolfSelected = false;
                UseLocalOverride = false;
            }
        }
    }

    private bool _ulisseWolfSelected;
    public bool UlisseWolfSelected
    {
        get => _ulisseWolfSelected;
        set
        {
            if (SetField(ref _ulisseWolfSelected, value) && value)
            {
                NamTeamSelected = false;
                UseLocalOverride = false;
            }
        }
    }

    private bool _useLocalOverride;
    public bool UseLocalOverride
    {
        get => _useLocalOverride;
        set
        {
            if (SetField(ref _useLocalOverride, value) && value)
            {
                NamTeamSelected = false;
                UlisseWolfSelected = false;
            }
        }
    }

    private bool _checkForUpdates = true;
    public bool CheckForUpdates
    {
        get => _checkForUpdates;
        set => SetField(ref _checkForUpdates, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public AsyncRelayCommand ContinueCommand { get; }

    /// <summary>Populated once <see cref="ContinueCommand"/> completes successfully.</summary>
    public PropertyDefinitionsRegistry? Result { get; private set; }

    /// <summary>The source actually used to build <see cref="Result"/> (for persisting as "last used").</summary>
    public PropertySource? ResultSource { get; private set; }

    /// <summary>Raised when the dialog should close - true if a registry was successfully loaded.</summary>
    public event EventHandler<bool>? CloseRequested;

    private async Task ContinueAsync()
    {
        IsBusy = true;
        try
        {
            var registry = new PropertyDefinitionsRegistry();

            if (UseLocalOverride && HasLocalOverride)
            {
                StatusMessage = "Loading the custom local file...";
                await Task.Run(() => registry.Load(_service.LocalOverridePath, "Custom local file (developer)"));
                Result = registry;
                ResultSource = null;
                await IndexUiElementsAsync();
                CloseRequested?.Invoke(this, true);
                return;
            }

            var source = UlisseWolfSelected ? PropertySource.UlisseWolfPatches : PropertySource.NamTeam;

            if (CheckForUpdates)
            {
                StatusMessage = $"Checking for updates for {PropertySourceService.DisplayName(source)}...";
                StatusMessage = await _service.CheckForUpdateAsync(source);
            }

            var path = _service.ResolveActivePath(source);
            if (path is null)
            {
                StatusMessage = "No copy available: an Internet connection is required on first run.";
                return;
            }

            StatusMessage = $"Loading {PropertySourceService.DisplayName(source)}...";
            await Task.Run(() => registry.Load(path, PropertySourceService.DisplayName(source)));
            Result = registry;
            ResultSource = source;
            _service.SaveLastUsedSource(source);
            await IndexUiElementsAsync();
            CloseRequested?.Invoke(this, true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Indexes every UI/image entry (see UiElementIndexService) across BOTH the SC4
    /// installation folder and the Plugins folder before this "please wait" screen closes,
    /// so the app is fully ready - shared UI chrome images resolvable, "UI Elements" results
    /// populated - the moment the main window appears, instead of racing a background scan
    /// against whatever the person does first. Same idea as SC4 PIM-X finishing its own data
    /// loading before revealing its main window. The Plugins folder can be considerably
    /// bigger than the installation folder (thousands of files vs. a handful) - the
    /// tradeoff is a longer wait here in exchange for the UI Editor never needing to fall
    /// back to an incomplete index once the person actually gets to it.
    /// </summary>
    private async Task IndexUiElementsAsync()
    {
        var folders = new[] { _sc4InstallFolder, _pluginsFolder };
        if (Array.TrueForAll(folders, string.IsNullOrWhiteSpace))
        {
            return;
        }

        StatusMessage = "Indexing UI and image entries in Plugins and the SC4 installation folder...";
        await _uiElementIndex.RefreshAsync(folders);
    }
}
