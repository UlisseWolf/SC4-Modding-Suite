using System;
using System.Collections.Generic;
using System.Linq;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Opzioni" dialog: paths to SC4's protected data files (for reference/quick
/// access - actual save-blocking is enforced independently by
/// <see cref="Models.ProtectedFileNames"/> regardless of what's configured here), paths to
/// four external SC4 modding tools, and the property-database/language/theme settings.
/// Theme and language changes apply immediately (live preview); path fields are only
/// written back to <see cref="AppOptions"/> when "Salva" is pressed.
/// </summary>
public sealed class OptionsDialogViewModel : ViewModelBase
{
    private readonly AppOptionsService _optionsService;
    private readonly AppOptions _options;
    private readonly ThemeService _themeService;
    private readonly LocalizationService _localizationService;

    /// <summary>Exposed for XAML bindings (e.g. <c>{Binding LocalizationService.OptionsTitle}</c>).</summary>
    public LocalizationService LocalizationService => _localizationService;

    public OptionsDialogViewModel(
        AppOptionsService optionsService,
        AppOptions options,
        ThemeService themeService,
        LocalizationService localizationService)
    {
        _optionsService = optionsService;
        _options = options;
        _themeService = themeService;
        _localizationService = localizationService;

        _simCityLocalePath = options.SimCityLocalePath ?? string.Empty;
        _sc4InstallFolder = options.Sc4InstallFolder ?? string.Empty;
        _pluginsFolder = options.PluginsFolder ?? string.Empty;
        _pimXPath = options.PimXPath ?? string.Empty;
        _dataNodePath = options.DataNodePath ?? string.Empty;
        _mapperPath = options.MapperPath ?? string.Empty;
        _terraformerPath = options.TerraformerPath ?? string.Empty;
        _sc4PacEditorPath = options.Sc4PacEditorPath ?? string.Empty;
        _namDevelopmentSuitePath = options.NamDevelopmentSuitePath ?? string.Empty;

        IsNamDevelopmentSuiteEnabled = DevFeatureFlags.IsNamDevelopmentSuiteEnabled();

        AvailableThemes = themeService.AvailableThemes();
        AvailableLanguages = localizationService.AvailableLanguages();

        _selectedTheme = AvailableThemes.FirstOrDefault(t => t.Key == options.Theme) ?? AvailableThemes[0];
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == options.Language)
            ?? AvailableLanguages.FirstOrDefault()
            ?? new LocalizationEntry { Code = "it", DisplayName = "Italiano" };

        SaveCommand = new RelayCommand(_ => Save());
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, EventArgs.Empty));
        CleanTempFilesCommand = new RelayCommand(_ => CleanTempFiles());
    }

    // --- Protected SC4 data file paths (reference only) ---

    private string _simCityLocalePath;
    public string SimCityLocalePath
    {
        get => _simCityLocalePath;
        set => SetField(ref _simCityLocalePath, value);
    }

    private string _sc4InstallFolder;
    public string Sc4InstallFolder
    {
        get => _sc4InstallFolder;
        set => SetField(ref _sc4InstallFolder, value);
    }

    private string _pluginsFolder;
    public string PluginsFolder
    {
        get => _pluginsFolder;
        set => SetField(ref _pluginsFolder, value);
    }

    // --- External tool paths ---

    private string _pimXPath;
    public string PimXPath
    {
        get => _pimXPath;
        set => SetField(ref _pimXPath, value);
    }

    private string _dataNodePath;
    public string DataNodePath
    {
        get => _dataNodePath;
        set => SetField(ref _dataNodePath, value);
    }

    private string _mapperPath;
    public string MapperPath
    {
        get => _mapperPath;
        set => SetField(ref _mapperPath, value);
    }

    private string _terraformerPath;
    public string TerraformerPath
    {
        get => _terraformerPath;
        set => SetField(ref _terraformerPath, value);
    }

    private string _sc4PacEditorPath;
    public string Sc4PacEditorPath
    {
        get => _sc4PacEditorPath;
        set => SetField(ref _sc4PacEditorPath, value);
    }

    /// <summary>
    /// Only meaningful/shown in the UI when <see cref="IsNamDevelopmentSuiteEnabled"/> is
    /// true - see <see cref="Models.DevFeatureFlags"/> for how that hidden flag works.
    /// </summary>
    private string _namDevelopmentSuitePath;
    public string NamDevelopmentSuitePath
    {
        get => _namDevelopmentSuitePath;
        set => SetField(ref _namDevelopmentSuitePath, value);
    }

    /// <summary>True only if the hidden, unshipped developer flag file is present and set.</summary>
    public bool IsNamDevelopmentSuiteEnabled { get; }

    // --- Property database (reuses the existing startup dialog) ---

    public event EventHandler? ChangePropertySourceRequested;

    public RelayCommand ChangePropertySourceCommand => new(_ => ChangePropertySourceRequested?.Invoke(this, EventArgs.Empty));

    // --- Language / theme (apply immediately) ---

    public List<ThemeChoice> AvailableThemes { get; }
    public List<LocalizationEntry> AvailableLanguages { get; }

    private ThemeChoice _selectedTheme;
    public ThemeChoice SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetField(ref _selectedTheme, value))
            {
                _themeService.Apply(value.Key);
                _options.Theme = value.Key;
                _optionsService.Save(_options);
            }
        }
    }

    private LocalizationEntry _selectedLanguage;
    public LocalizationEntry SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetField(ref _selectedLanguage, value))
            {
                _localizationService.SetLanguage(value.Code);
                _options.Language = value.Code;
                _optionsService.Save(_options);
            }
        }
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CloseCommand { get; }

    public event EventHandler? CloseRequested;

    // --- Temp file cleanup (Ilive Reader's DlgOption::OnBnClickedclean) ---

    private string _cleanTempFilesStatus = string.Empty;
    public string CleanTempFilesStatus
    {
        get => _cleanTempFilesStatus;
        private set => SetField(ref _cleanTempFilesStatus, value);
    }

    public RelayCommand CleanTempFilesCommand { get; }

    private void CleanTempFiles()
    {
        var (count, bytes) = TempFileCleaner.Clean();
        CleanTempFilesStatus = count == 0
            ? "No leftover temporary files found."
            : $"Deleted {count} temporary file(s) ({bytes / 1024.0:N1} KB).";
    }

    private void Save()
    {
        _options.SimCityLocalePath = NullIfEmpty(SimCityLocalePath);
        _options.Sc4InstallFolder = NullIfEmpty(Sc4InstallFolder);
        _options.PluginsFolder = NullIfEmpty(PluginsFolder);
        _options.PimXPath = NullIfEmpty(PimXPath);
        _options.DataNodePath = NullIfEmpty(DataNodePath);
        _options.MapperPath = NullIfEmpty(MapperPath);
        _options.TerraformerPath = NullIfEmpty(TerraformerPath);
        _options.Sc4PacEditorPath = NullIfEmpty(Sc4PacEditorPath);

        if (IsNamDevelopmentSuiteEnabled)
        {
            _options.NamDevelopmentSuitePath = NullIfEmpty(NamDevelopmentSuitePath);
        }

        _optionsService.Save(_options);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
