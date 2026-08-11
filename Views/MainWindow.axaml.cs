using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>
/// MDI shell window: Navigator tree + one tab per open document (see
/// ViewModels/MainWindowShellViewModel and Views/DbpfWorkspaceView, the former single-
/// window MainWindow content). Also hosts the multi-file tools that don't belong to any
/// one tab - Compare, Merge, Directory sync - opened here the same way every dialog in
/// this app is opened, from code-behind against the active tab (<see cref="MainWindowShellViewModel.SelectedDocument"/>).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowShellViewModel ViewModel => (MainWindowShellViewModel)DataContext!;

    private static readonly FilePickerFileType Sc4PackageFileType = new("File SC4 (DBPF)")
    {
        Patterns = new[] { "*.dat", "*.sc4lot", "*.sc4desc", "*.sc4model" },
    };

    private void OnNewClick(object? sender, RoutedEventArgs e) => ViewModel.NewDocument();

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Open SC4 (DBPF) file(s)",
            AllowMultiple = true, // MDI: opening several at once puts each into its own tab
            FileTypeFilter = new List<FilePickerFileType> { Sc4PackageFileType, FilePickerFileTypes.All },
        };

        var startFolder = ViewModel.SelectedDocument?.AppOptions.PluginsFolder;
        if (string.IsNullOrWhiteSpace(startFolder))
        {
            startFolder = ViewModel.SelectedDocument?.AppOptions.Sc4InstallFolder;
        }

        if (!string.IsNullOrWhiteSpace(startFolder))
        {
            try
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(startFolder);
            }
            catch
            {
                // Folder may no longer exist; just fall back to the picker's own default.
            }
        }

        var files = await StorageProvider.OpenFilePickerAsync(options);
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is not null)
            {
                ViewModel.OpenDocument(path);
            }
        }
    }

    private void OnCloseTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MainWindowViewModel doc })
        {
            ViewModel.CloseDocument(doc);
        }
    }

    /// <summary>Double-clicking a package file in the Navigator opens it into a new tab.</summary>
    private void OnNavigatorDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TreeView { SelectedItem: NavigatorNodeViewModel { IsSc4Package: true, Path: { } path } })
        {
            ViewModel.OpenDocument(path);
        }
    }

    private async void OnCompareClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new DatCompareDialog(new DatCompareDialogViewModel());
        await dialog.ShowDialog(this);
    }

    private async void OnMergeClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new MergeDialog(new MergeDialogViewModel(ViewModel.SelectedDocument));
        await dialog.ShowDialog(this);
    }

    private async void OnDirectorySyncClick(object? sender, RoutedEventArgs e)
    {
        var document = ViewModel.SelectedDocument;
        if (document is null || !document.HasOpenFile)
        {
            return;
        }

        var dialog = new DirectoryDialog(new DirectoryDialogViewModel(document));
        await dialog.ShowDialog(this);
    }

    // ---------------------------------------------------------------
    // Save/Save As/Export/Import/Options - act on SelectedDocument (the active tab), same as
    // "New"/"Open" above. Used to be duplicated per-tab in Views/DbpfWorkspaceView's own
    // toolbar; consolidated up here so every file-operation button lives in one single place
    // regardless of which tab is active.
    // ---------------------------------------------------------------

    private void OnSaveClick(object? sender, RoutedEventArgs e) => ViewModel.SelectedDocument?.SaveInPlace();

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        var document = ViewModel.SelectedDocument;
        if (document is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save SC4 (DBPF) file as",
            SuggestedFileName = "new_package.dat",
            FileTypeChoices = new List<FilePickerFileType> { Sc4PackageFileType, FilePickerFileTypes.All },
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        document.SaveToPath(path);
    }

    private async void OnExportSelectedClick(object? sender, RoutedEventArgs e)
    {
        var document = ViewModel.SelectedDocument;
        if (document?.SelectedEntry is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export entry as...",
            SuggestedFileName = document.SuggestedExportFileName,
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        document.ExportSelectedEntry(path);
    }

    private async void OnImportIntoSelectedClick(object? sender, RoutedEventArgs e)
    {
        var document = ViewModel.SelectedDocument;
        if (document?.SelectedEntry is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import file into selected entry",
            AllowMultiple = false,
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        document.ImportIntoSelectedEntry(path);
    }

    private async void OnExportAllClick(object? sender, RoutedEventArgs e)
    {
        var document = ViewModel.SelectedDocument;
        if (document is null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the folder to export all entries to",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        document.ExportAllEntries(path);
    }

    private async void OnOpenOptionsClick(object? sender, RoutedEventArgs e)
    {
        var document = ViewModel.SelectedDocument;
        if (document is null)
        {
            return;
        }

        var dialogVm = new OptionsDialogViewModel(
            document.AppOptionsService,
            document.AppOptions,
            document.ThemeService,
            document.LocalizationService);

        var dialog = new OptionsDialog(
            dialogVm,
            document.PropertySourceService,
            registry => document.SetPropertyRegistry(registry),
            document.UiElementIndex,
            document.AppOptions.Sc4InstallFolder,
            document.AppOptions.PluginsFolder);

        await dialog.ShowDialog(this);

        // Options may have just changed the Plugins and/or SC4 installation folder paths -
        // rescan the Navigator so the new/corrected paths show up without needing a restart.
        ViewModel.RefreshNavigatorRoots();

        // Same for the automatic UI Elements index (see App.axaml.cs/UiElementIndexService) -
        // otherwise it would stay empty (or stale) until the app is restarted if this is the
        // first time either folder gets set. Its own on-disk cache means files that haven't
        // actually changed since the last scan are skipped, not fully reparsed.
        _ = document.UiElementIndex.RefreshAsync(new[] { document.AppOptions.Sc4InstallFolder, document.AppOptions.PluginsFolder });
    }
}
