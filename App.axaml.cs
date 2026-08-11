using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.ViewModels;
using SC4ModdingSuite.Views;

namespace SC4ModdingSuite;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // General app options (external tool paths, protected-file reference paths,
            // language, theme) - loaded once here and shared by reference with
            // MainWindowViewModel/OptionsDialogViewModel so a change from the Options
            // dialog is immediately visible everywhere.
            var appOptionsService = new AppOptionsService();
            var appOptions = appOptionsService.Load();

            var themeService = new ThemeService();
            themeService.Apply(appOptions.Theme);

            var localizationService = new LocalizationService();
            localizationService.SetLanguage(appOptions.Language);

            // Automatically indexes every UI element and image entry across the SC4
            // installation folder and the Plugins folder - see UiElementIndexService for the
            // scan/on-disk-cache logic. One shared instance for the whole app (like the
            // services above). Both folders are scanned up front, awaited as part of the
            // startup/property-source screen below, before the main window ever appears -
            // same idea as SC4 PIM-X finishing its own data loading before revealing its main
            // window - so the UI Editor never races an incomplete background scan (see
            // UiElementIndexService's own "PRIORITY INSTRUCTION" doc comment on
            // ResolveSharedImage for why that matters). The Plugins folder can be
            // considerably bigger than the installation folder, so this does mean a longer
            // wait at startup than scanning only the installation folder would - see that
            // same "please wait" screen for the tradeoff this makes on purpose.
            var uiElementIndex = new UiElementIndexService();

            // Per requirements, the property-database source dialog is shown at every
            // program startup (not just the first run) before the main window appears.
            // It is briefly used as the classic-desktop-lifetime's "main window" so
            // Avalonia shows it automatically as part of startup; once the person picks
            // a source (or closes the dialog outright), it is swapped for the real
            // MainWindow. Its own "please wait" screen (progress bar + status text) now also
            // covers the UI/image indexing above, not just loading new_properties.xml - see
            // PropertySourceDialogViewModel.ContinueAsync/IndexUiElementsAsync.
            var propertySourceService = new PropertySourceService();
            var lastUsedSource = propertySourceService.LoadLastUsedSource();
            var sourceDialogViewModel = new PropertySourceDialogViewModel(
                propertySourceService, lastUsedSource, uiElementIndex, appOptions.Sc4InstallFolder, appOptions.PluginsFolder);
            var sourceDialog = new PropertySourceDialog(sourceDialogViewModel);

            sourceDialog.Closed += (_, _) =>
            {
                // If the person closed the dialog without picking a source (e.g. via the
                // window's own close button), fall back to an empty registry rather than
                // blocking the app entirely - property names just won't resolve until
                // "Options..." is used from the shell's own toolbar to pick a source.
                var registry = sourceDialogViewModel.Result ?? new PropertyDefinitionsRegistry();

                // MDI: every open tab is its own MainWindowViewModel (unchanged from the
                // pre-MDI single-window app), all sharing the same registry/services by
                // reference - so e.g. a theme/language change from any one tab's Options
                // dialog is still immediately visible everywhere, exactly as before.
                // MainWindowShellViewModel calls this once per new/opened tab.
                //
                // shellViewModel is assigned right after MainWindowShellViewModel finishes
                // constructing. Its own constructor calls NewDocument() -> CreateDocument()
                // synchronously (before that assignment runs), so the very first document is
                // created while shellViewModel is still null - but the callback below is a
                // closure over the *local variable*, not its value at that instant: it only
                // actually reads shellViewModel when OpenDocumentInNewTab is later invoked (a
                // "whole folder" scan hit from another file), by which point construction has
                // long finished and shellViewModel is set - so every document, including the
                // first, ends up correctly wired to the real shell instance.
                MainWindowShellViewModel? shellViewModel = null;

                MainWindowViewModel CreateDocument() => new(
                    registry,
                    propertySourceService,
                    appOptionsService,
                    appOptions,
                    themeService,
                    localizationService,
                    uiElementIndex,
                    openDocumentInNewTab: path => shellViewModel!.OpenDocument(path));

                shellViewModel = new MainWindowShellViewModel(CreateDocument, appOptions, localizationService);

                var mainWindow = new MainWindow
                {
                    DataContext = shellViewModel,
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
            };

            desktop.MainWindow = sourceDialog;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
