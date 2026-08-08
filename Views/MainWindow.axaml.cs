using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private static readonly FilePickerFileType Sc4PackageFileType = new("File SC4 (DBPF)")
    {
        Patterns = new[] { "*.dat", "*.sc4lot", "*.sc4desc", "*.sc4model" },
    };

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.CreateNewPackage();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Open SC4 (DBPF) file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { Sc4PackageFileType, FilePickerFileTypes.All },
        };

        // Nice-to-have: start the picker in the configured Plugins folder (Opzioni), if any
        // - that's where someone using this app actually keeps the mod files they want to
        // open, as opposed to the base game's own install folder. Falls back to the SC4
        // installation folder if no Plugins folder is configured, so existing setups that
        // only ever set that one still get a sensible starting location. Purely a
        // convenience - it has no bearing on which files end up protected against in-place
        // saving (see ProtectedFileNames).
        var startFolder = ViewModel.AppOptions.PluginsFolder;
        if (string.IsNullOrWhiteSpace(startFolder))
        {
            startFolder = ViewModel.AppOptions.Sc4InstallFolder;
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

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.OpenFile(path);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.SaveInPlace();
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
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

        ViewModel.SaveToPath(path);
    }

    private async void OnAddPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedExemplar is null)
        {
            return;
        }

        var dialogVm = new PropertyEditDialogViewModel(ViewModel.PropertyRegistry, existing: null);
        var dialog = new PropertyEditDialog(dialogVm);

        var accepted = await dialog.ShowDialog<bool>(this);
        if (accepted && dialogVm.Result is not null)
        {
            ViewModel.AddOrUpdateProperty(dialogVm.Result);
        }
    }

    private async void OnEditPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedExemplar is null || ViewModel.SelectedProperty is null)
        {
            return;
        }

        var dialogVm = new PropertyEditDialogViewModel(ViewModel.PropertyRegistry, ViewModel.SelectedProperty.Property);
        var dialog = new PropertyEditDialog(dialogVm);

        var accepted = await dialog.ShowDialog<bool>(this);
        if (accepted && dialogVm.Result is not null)
        {
            ViewModel.AddOrUpdateProperty(dialogVm.Result);
        }
    }

    private async void OnExportSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export entry as...",
            SuggestedFileName = ViewModel.SuggestedExportFileName,
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ExportSelectedEntry(path);
    }

    private async void OnImportIntoSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
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

        ViewModel.ImportIntoSelectedEntry(path);
    }

    private async void OnExportAllClick(object? sender, RoutedEventArgs e)
    {
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

        ViewModel.ExportAllEntries(path);
    }

    private static readonly FilePickerFileType ThreeDsFileType = new("3D Studio mesh (.3ds)")
    {
        Patterns = new[] { "*.3ds" },
    };

    private async void OnImportS3DClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import .3ds file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { ThreeDsFileType, FilePickerFileTypes.All },
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ImportS3DFrom3ds(path);
    }

    private async void OnExportS3DClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export model as .3ds",
            SuggestedFileName = "model.3ds",
            FileTypeChoices = new List<FilePickerFileType> { ThreeDsFileType },
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ExportS3DTo3ds(path);
    }

    private async void OnExportS3DGroupClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export editing group as .3ds",
            SuggestedFileName = $"group{ViewModel.S3DEditGroupIndex}.3ds",
            FileTypeChoices = new List<FilePickerFileType> { ThreeDsFileType },
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ExportS3DGroupTo3ds(path);
    }

    // ---------------------------------------------------------------
    // S3D Editor: geometry editor. Opens in its own window (S3DGeometryEditorDialog),
    // sharing this window's MainWindowViewModel as DataContext - no separate dialog
    // view model needed, the grids/commands it binds to already live on MainWindowViewModel.
    // ---------------------------------------------------------------

    private async void OnOpenS3DGeometryEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DGeometryEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(this);
    }

    private async void OnOpenS3DMaterialEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DMaterialEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(this);
    }

    private async void OnOpenS3DAnimationEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DAnimationEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(this);
    }

    private async void OnOpenS3DPropEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DPropEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(this);
    }

    private async void OnOpenS3DUVEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DUVEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(this);
    }

    private async void OnOpenS3DHexEditorClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
        {
            return;
        }

        var bytes = RawEntryBytes.GetDecompressed(ViewModel.SelectedEntry.Entry);
        var chunks = bytes is null ? System.Array.Empty<(string, byte[])>() : S3DParser.LocateChunks(bytes);

        var dialog = new S3DHexEditorDialog(chunks);
        await dialog.ShowDialog(this);
    }

    private async void OnOpenS3DRegPointEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DRegPointEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(this);
    }

    private async void OnOpenOptionsClick(object? sender, RoutedEventArgs e)
    {
        var dialogVm = new OptionsDialogViewModel(
            ViewModel.AppOptionsService,
            ViewModel.AppOptions,
            ViewModel.ThemeService,
            ViewModel.LocalizationService);

        var dialog = new OptionsDialog(
            dialogVm,
            ViewModel.PropertySourceService,
            registry => ViewModel.SetPropertyRegistry(registry));

        await dialog.ShowDialog(this);
    }

    // ---------------------------------------------------------------
    // Copy/paste (full entries and TGI-only). Multi-select on EntriesListBox works out of
    // the box from SelectionMode="Multiple" (click = select one, Ctrl+click = toggle
    // add/remove, Shift+click = select a contiguous range) - standard Avalonia ListBox
    // behavior, no extra code needed for that part.
    // ---------------------------------------------------------------

    private IReadOnlyCollection<EntryItemViewModel> GetSelectedEntries() =>
        EntriesListBox.SelectedItems?.Cast<EntryItemViewModel>().ToList() ?? new List<EntryItemViewModel>();

    private async void OnCopyEntriesClick(object? sender, RoutedEventArgs e) => await CopyEntriesAsync();

    private async void OnPasteEntriesClick(object? sender, RoutedEventArgs e) => await PasteEntriesAsync();

    private async void OnCopyTgiClick(object? sender, RoutedEventArgs e) => await CopyTgiAsync();

    private async void OnPasteTgiClick(object? sender, RoutedEventArgs e) => await PasteTgiAsync();

    private async void OnWindowKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        var ctrl = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift);

        if (!ctrl)
        {
            return;
        }

        switch (e.Key)
        {
            case Avalonia.Input.Key.C when shift:
                await CopyTgiAsync();
                e.Handled = true;
                break;
            case Avalonia.Input.Key.C:
                await CopyEntriesAsync();
                e.Handled = true;
                break;
            case Avalonia.Input.Key.V when shift:
                await PasteTgiAsync();
                e.Handled = true;
                break;
            case Avalonia.Input.Key.V:
                await PasteEntriesAsync();
                e.Handled = true;
                break;
        }
    }

    private async System.Threading.Tasks.Task CopyEntriesAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var text = ViewModel.BuildEntriesClipboardText(GetSelectedEntries());
        if (clipboard is null || text is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }

    private async System.Threading.Tasks.Task PasteEntriesAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var text = await clipboard.GetTextAsync();
        ViewModel.PasteEntriesFromClipboardText(text);
    }

    private async System.Threading.Tasks.Task CopyTgiAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        var text = ViewModel.BuildTgiClipboardText(GetSelectedEntries());
        if (clipboard is null || text is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }

    private async System.Threading.Tasks.Task PasteTgiAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var text = await clipboard.GetTextAsync();
        ViewModel.PasteTgiFromClipboardText(text);
    }
}
