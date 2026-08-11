using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>
/// One open document's whole editor UI (entry list, TGI editor, previews, S3D/property
/// panels) - hosted once per tab in the MDI shell (<see cref="MainWindow"/>'s TabControl).
/// This is the former MainWindow content, extracted verbatim into a UserControl so several
/// can exist side by side, each bound to its own MainWindowViewModel instance (see
/// ViewModels/MainWindowShellViewModel.OpenDocument/NewDocument).
/// </summary>
public partial class DbpfWorkspaceView : UserControl
{
    public DbpfWorkspaceView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    // UserControl has no Window-only members (StorageProvider, ShowDialog owner) of its own -
    // both are reached via TopLevel.GetTopLevel(this), same as the Clipboard access further
    // down in this file already did even before the MDI split.
    private IStorageProvider StorageProvider => TopLevel.GetTopLevel(this)!.StorageProvider;
    private Window OwnerWindow => (Window)TopLevel.GetTopLevel(this)!;

    // "New"/"Open" used to live here too (pre-MDI leftover), duplicating the MDI shell's own
    // New/Open (Views/MainWindow.axaml.cs) with the same button labels but different behavior
    // (replacing this tab's content instead of opening a new tab). Removed - MainWindow's
    // toolbar is now the single place to create or open documents. ViewModel.CreateNewPackage()
    // and ViewModel.OpenFile(path) are still used by MainWindowShellViewModel for that.

    // Save/Save As/Export/Import/Options moved to the MDI shell's own toolbar
    // (Views/MainWindow.axaml.cs), operating on MainWindowShellViewModel.SelectedDocument -
    // see OnSaveClick and friends there. "New"/"Open" made the same move earlier (see the
    // comment where they used to be, just above).

    private static readonly FilePickerFileType PoFileType = new("Gettext PO/POT (.po, .pot)")
    {
        Patterns = new[] { "*.po", "*.pot" },
    };

    private async void OnExportLtextPoeditClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export LTEXT strings as Poedit .po/.pot",
            SuggestedFileName = $"{ViewModel.LtextTargetLanguage.Name}.po",
            FileTypeChoices = new List<FilePickerFileType> { PoFileType, FilePickerFileTypes.All },
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ExportLtextPoedit(path);
    }

    private async void OnImportLtextPoeditClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Poedit .po/.pot translations",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { PoFileType, FilePickerFileTypes.All },
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ImportLtextPoedit(path);
    }

    private async void OnAddPropertyClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedExemplar is null)
        {
            return;
        }

        var dialogVm = new PropertyEditDialogViewModel(ViewModel.PropertyRegistry, existing: null);
        var dialog = new PropertyEditDialog(dialogVm);

        var accepted = await dialog.ShowDialog<bool>(OwnerWindow);
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

        var accepted = await dialog.ShowDialog<bool>(OwnerWindow);
        if (accepted && dialogVm.Result is not null)
        {
            ViewModel.AddOrUpdateProperty(dialogVm.Result);
        }
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
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnUiPreviewNodeClicked(object? sender, UiLegacyNode node)
    {
        // SelectCommand (bound alongside NodeClicked on UiPreviewControl) runs first and
        // synchronously, so ViewModel.SelectedUiNode/UiProperties already reflect this node
        // by the time this handler runs - see UiPreviewControl.OnPointerReleased.
        var dialog = new UiNodePropertiesDialog(new UiNodePropertiesDialogViewModel(ViewModel));
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnOpenS3DMaterialEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DMaterialEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnOpenS3DAnimationEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DAnimationEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnOpenS3DPropEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DPropEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnOpenS3DUVEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DUVEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(OwnerWindow);
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
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnOpenS3DRegPointEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new S3DRegPointEditorDialog { DataContext = ViewModel };
        await dialog.ShowDialog(OwnerWindow);
    }

    // ---------------------------------------------------------------
    // Copy/paste (full entries and TGI-only). Multi-select on EntriesListBox works out of
    // the box from SelectionMode="Multiple" (click = select one, Ctrl+click = toggle
    // add/remove, Shift+click = select a contiguous range) - standard Avalonia ListBox
    // behavior, no extra code needed for that part.
    // ---------------------------------------------------------------

    /// <summary>
    /// PRIORITY INSTRUCTION: EntriesListBox's own SelectedItem="{Binding SelectedEntry}"
    /// TwoWay binding is NOT reliable enough on its own to drive SelectedEntry (and so the
    /// UI Editor preview/every other single-entry panel) once SelectionMode="Multiple" is
    /// on - a plain click can move the ListBox's own visual highlight without the bound
    /// SelectedEntry actually changing, a well-known Avalonia/WPF ListBox quirk once
    /// multi-select is enabled. That left the UI Editor preview (and everything else keyed
    /// off SelectedEntry) showing whatever entry was selected before, sometimes for an
    /// entry that no longer matched the highlighted row at all. SelectionChanged, unlike
    /// the property binding, is a hard guarantee - it fires for every user selection change
    /// regardless of mode - so this explicitly re-syncs SelectedEntry from the ListBox's own
    /// authoritative SelectedItem every time, instead of trusting the binding alone.
    /// GetSelectedEntries() below (multi-select for Copy/Paste) is unaffected - it already
    /// reads SelectedItems directly from the control rather than through this property.
    /// </summary>
    private void OnEntriesListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // PRIORITY INSTRUCTION: DataContext can already be null/gone by the time this fires -
        // closing a tab removes this view from the tree, which resets the ListBox's own
        // selection model and raises SelectionChanged as part of that teardown, AFTER
        // DataContext has already been cleared. Skipping when there's nothing to update into
        // isn't optional here: the crash log for the previous version of this handler
        // (unconditional "ViewModel.SelectedEntry = ...", where ViewModel force-casts
        // DataContext with "!") shows exactly this - closing a tab while it held the UI
        // Editor open threw a NullReferenceException here and crashed the whole app, since
        // this fires from deep inside Avalonia's own routed-event dispatch with nothing
        // upstream to catch it.
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SelectedEntry = EntriesListBox.SelectedItem as EntryItemViewModel;
    }

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

    // ---------------------------------------------------------------
    // SC4 Editor-only batch/insert tools (Ilive Reader's DlgClone/DlgTGIEditor/
    // DlgChangeInstance/DlgGroupPatch/DlgInsertBatch/DlgTemplate/ID_MENU_REINDEX/
    // WorkspaceConvert) - see the "SC4 TOOLS" toolbar in this view's .axaml, only visible
    // in IsSc4EditorMode. Opened the same way every other dialog in this app is: a
    // ViewModel built here in code-behind, shown with ShowDialog against OwnerWindow.
    // ---------------------------------------------------------------

    private async void OnCloneClick(object? sender, RoutedEventArgs e)
    {
        var sourceEntries = GetSelectedEntries().Select(vm => vm.Entry).ToList();
        if (sourceEntries.Count == 0)
        {
            return;
        }

        var dialog = new CloneDialog(new CloneDialogViewModel(ViewModel, sourceEntries));
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnTgiEditorClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new TgiEditorDialog(new TgiEditorDialogViewModel(ViewModel));
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnChangeInstanceClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
        {
            return;
        }

        var dialog = new ChangeInstanceDialog(new ChangeInstanceDialogViewModel(ViewModel, ViewModel.SelectedEntry.Entry));
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnGroupPatchClick(object? sender, RoutedEventArgs e)
    {
        var targetEntries = GetSelectedEntries().Select(vm => vm.Entry).ToList();
        if (targetEntries.Count == 0)
        {
            return;
        }

        var dialog = new GroupPatchDialog(new GroupPatchDialogViewModel(ViewModel, targetEntries));
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnInsertBatchClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new InsertBatchDialog(new InsertBatchDialogViewModel(ViewModel));
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnInsertTemplateClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new TemplateDialog(new TemplateDialogViewModel(ViewModel));
        await dialog.ShowDialog(OwnerWindow);
    }

    private void OnReindexClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasOpenFile)
        {
            return;
        }

        var removed = ViewModel.Service.Reindex();
        ViewModel.ReloadEntries();
        ViewModel.SetStatusMessage(removed == 0
            ? "Reindex: nothing to do (no stray Directory entry found)."
            : $"Reindex: removed {removed} stray Directory entry(ies) - Save/Save As will rebuild it fresh.");
    }

    private async void OnConvertFormatClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasOpenFile)
        {
            return;
        }

        var dialog = new ConvertFormatDialog(new ConvertFormatDialogViewModel(ViewModel));
        await dialog.ShowDialog(OwnerWindow);
    }

    /// <summary>
    /// "RHD → LHD..." (T21 Editor panel): writes a brand-new file containing every entry
    /// from this document, with every T21 Exemplar mirrored for left-hand-drive traffic
    /// (see <see cref="T21LhdConverter"/>) - the currently open package itself is left
    /// untouched, same "pick a new path, write a separate file" shape as CONVERT FORMAT
    /// above.
    /// </summary>
    private async void OnRhdToLhdClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasOpenFile)
        {
            return;
        }

        var suggestedName = System.IO.Path.GetFileNameWithoutExtension(ViewModel.DocumentTitle);
        var suggestedExtension = System.IO.Path.GetExtension(ViewModel.Service.CurrentPath) is { Length: > 0 } ext ? ext : ".dat";

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "RHD → LHD: save converted file as...",
            SuggestedFileName = $"{suggestedName}_LHD{suggestedExtension}",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("File SC4 (DBPF)") { Patterns = new[] { "*.dat", "*.sc4lot", "*.sc4desc", "*.sc4model" } },
                FilePickerFileTypes.All,
            },
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        try
        {
            var result = ViewModel.Service.ExportRhdToLhd(path);
            ViewModel.SetStatusMessage(
                $"RHD → LHD: mirrored {result.Mirrored} T21 exemplar(s), copied {result.CopiedUnchanged} other entr(y/ies) unchanged, into \"{System.IO.Path.GetFileName(path)}\".");
        }
        catch (System.Exception ex)
        {
            ViewModel.SetStatusMessage($"RHD → LHD failed: {ex.Message}");
        }
    }

    private async void OnSaveDecodedClick(object? sender, RoutedEventArgs e)
    {
        var entries = GetSelectedEntries().Select(vm => vm.Entry).ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the folder to save decoded entries to",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ExportSelectedEntriesDecoded(entries, path);
    }

    private async void OnExportReadableClick(object? sender, RoutedEventArgs e)
    {
        var entries = GetSelectedEntries().Select(vm => vm.Entry).ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the folder to save readable (.txt) entries to",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ExportSelectedEntriesReadable(entries, path);
    }

    private async void OnSaveHeaderTxtClick(object? sender, RoutedEventArgs e)
    {
        var entries = GetSelectedEntries().Select(vm => vm.Entry).ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save entry header(s) as text",
            SuggestedFileName = "headers.txt",
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.SaveSelectedEntryHeaders(entries, path);
    }

    private async void OnXmlGenClick(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasOpenFile)
        {
            return;
        }

        var dialog = new XmlGenDialog(new XmlGenDialogViewModel(ViewModel));
        await dialog.ShowDialog(OwnerWindow);
    }

    private async void OnRecorderClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new RecorderDialog(new RecorderDialogViewModel(ViewModel));
        await dialog.ShowDialog(OwnerWindow);
    }

    // ---------------------------------------------------------------
    // Analysis mode (Find/Index Analyser, Plugins Analyser, Exemplar/Cohort Analyser,
    // Property Find/Count, Property Manager) - the actual scanning/reporting logic lives on
    // MainWindowViewModel (see its "Analysis mode" region); this is just folder-picking and
    // "double-click a result row to jump to it" plumbing, same pattern as everything else here.
    // ---------------------------------------------------------------

    private async void OnBrowseAnalysisFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a Plugins (or any) folder to scan",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel.AnalysisFolder = path;
        }
    }

    private void OnFindResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: AnalysisResultRowViewModel row })
        {
            ViewModel.SelectAnalysisResult(row);
        }
    }

    private void OnPropertyFindResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: AnalysisResultRowViewModel row })
        {
            ViewModel.SelectAnalysisResult(row);
        }
    }

    private void OnUiElementResultDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid { SelectedItem: AnalysisResultRowViewModel row })
        {
            ViewModel.SelectUiFinderResult(row);
        }
    }

    private void OnRescanUiElementsClick(object? sender, RoutedEventArgs e)
    {
        _ = ViewModel.UiElementIndex.RefreshAsync(new[] { ViewModel.AppOptions.Sc4InstallFolder, ViewModel.AppOptions.PluginsFolder });
    }
}
