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

            // Per requirements, the property-database source dialog is shown at every
            // program startup (not just the first run) before the main window appears.
            // It is briefly used as the classic-desktop-lifetime's "main window" so
            // Avalonia shows it automatically as part of startup; once the person picks
            // a source (or closes the dialog outright), it is swapped for the real
            // MainWindow.
            var propertySourceService = new PropertySourceService();
            var lastUsedSource = propertySourceService.LoadLastUsedSource();
            var sourceDialogViewModel = new PropertySourceDialogViewModel(propertySourceService, lastUsedSource);
            var sourceDialog = new PropertySourceDialog(sourceDialogViewModel);

            sourceDialog.Closed += (_, _) =>
            {
                // If the person closed the dialog without picking a source (e.g. via the
                // window's own close button), fall back to an empty registry rather than
                // blocking the app entirely - property names just won't resolve until
                // "Options..." is used from the main window's toolbar to pick a source.
                var registry = sourceDialogViewModel.Result ?? new PropertyDefinitionsRegistry();

                var mainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(
                        registry,
                        propertySourceService,
                        appOptionsService,
                        appOptions,
                        themeService,
                        localizationService),
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
            };

            desktop.MainWindow = sourceDialog;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
