using System;
using System.Collections.ObjectModel;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// MDI shell: owns the set of currently open documents (each a full <see cref="MainWindowViewModel"/>,
/// unchanged from the pre-MDI single-window app - see Views/DbpfWorkspaceView) shown as tabs in
/// Views/MainWindow, plus the "Navigator" tree (Plugins/SC4 Install folders, Ilive Reader's own
/// Workspace bar) used to open files into new tabs. Multi-file tools that need more than one
/// document at once - Compare, Merge, Directory sync - are opened from MainWindow's code-behind
/// against <see cref="SelectedDocument"/>, the same way every other dialog in this app is opened.
/// </summary>
public sealed class MainWindowShellViewModel : ViewModelBase
{
    private readonly Func<MainWindowViewModel> _documentFactory;
    private readonly AppOptions _appOptions;

    public MainWindowShellViewModel(Func<MainWindowViewModel> documentFactory, AppOptions appOptions, LocalizationService localizationService)
    {
        _documentFactory = documentFactory;
        _appOptions = appOptions;
        LocalizationService = localizationService;

        PluginsRoot = new NavigatorNodeViewModel("PLUGINS");
        InstallRoot = new NavigatorNodeViewModel("SC4 INSTALL");
        NavigatorRoots = new ObservableCollection<NavigatorNodeViewModel> { PluginsRoot, InstallRoot };
        RefreshNavigatorRoots();

        NewDocumentCommand = new RelayCommand(_ => NewDocument());
        CloseDocumentCommand = new RelayCommand(doc => CloseDocument(doc as MainWindowViewModel));

        NewDocument();
    }

    public LocalizationService LocalizationService { get; }

    /// <summary>Every currently open document (one per MDI tab).</summary>
    public ObservableCollection<MainWindowViewModel> Documents { get; } = new();

    private MainWindowViewModel? _selectedDocument;

    /// <summary>The document behind the active tab - the target for Directory sync, and the
    /// default "merge into" target offered by the Merge dialog.</summary>
    public MainWindowViewModel? SelectedDocument
    {
        get => _selectedDocument;
        set => SetField(ref _selectedDocument, value);
    }

    /// <summary>Root of the "Plugins" branch of the Navigator tree (from Options' configured Plugins folder).</summary>
    public NavigatorNodeViewModel PluginsRoot { get; }

    /// <summary>Root of the "SC4 Install" branch of the Navigator tree (from Options' configured SC4
    /// installation folder) - where the base game's own package files live (SimCity_1.dat...
    /// SimCity_5.dat, SimCityLocale.DAT), as opposed to <see cref="PluginsRoot"/>'s user mods.</summary>
    public NavigatorNodeViewModel InstallRoot { get; }

    /// <summary>Both roots together, bound directly to the Navigator TreeView's ItemsSource.</summary>
    public ObservableCollection<NavigatorNodeViewModel> NavigatorRoots { get; }

    public RelayCommand NewDocumentCommand { get; }
    public RelayCommand CloseDocumentCommand { get; }

    /// <summary>
    /// (Re)points the Navigator's root folders at the currently configured Plugins folder and
    /// the configured SC4 installation folder - called at startup and again after Options is
    /// closed, in case either path just changed.
    /// </summary>
    public void RefreshNavigatorRoots()
    {
        if (!string.IsNullOrWhiteSpace(_appOptions.PluginsFolder))
        {
            PluginsRoot.AddRootFolder(_appOptions.PluginsFolder!);
        }

        if (!string.IsNullOrWhiteSpace(_appOptions.Sc4InstallFolder))
        {
            InstallRoot.AddRootFolder(_appOptions.Sc4InstallFolder!);
        }
    }

    /// <summary>Opens a brand-new empty tab (toolbar "New").</summary>
    public MainWindowViewModel NewDocument()
    {
        var doc = _documentFactory();
        doc.CreateNewPackage();
        Documents.Add(doc);
        SelectedDocument = doc;
        return doc;
    }

    /// <summary>
    /// Opens <paramref name="path"/> into a brand-new tab - always a new tab (never reuses/replaces
    /// an existing one), which is exactly what "MDI, several .dat open together" means: the point
    /// is to have them all open side by side, not to keep replacing a single document like the
    /// app did before this feature.
    /// </summary>
    public MainWindowViewModel OpenDocument(string path)
    {
        var doc = _documentFactory();
        doc.OpenFile(path);
        Documents.Add(doc);
        SelectedDocument = doc;
        return doc;
    }

    /// <summary>Closes a tab (its "x" button). Never leaves zero tabs open - a fresh empty one
    /// takes its place, matching the app's previous behavior of always having *a* document.</summary>
    public void CloseDocument(MainWindowViewModel? doc)
    {
        if (doc is null || !Documents.Contains(doc))
        {
            return;
        }

        var wasSelected = SelectedDocument == doc;
        Documents.Remove(doc);

        if (Documents.Count == 0)
        {
            NewDocument();
        }
        else if (wasSelected)
        {
            SelectedDocument = Documents[^1];
        }
    }
}
