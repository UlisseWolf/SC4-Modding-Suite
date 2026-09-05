using System.Linq;
using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgTGIEditor - see TgiEditorDialogViewModel for the mask/apply logic.</summary>
public partial class TgiEditorDialog : Window
{
    public TgiEditorDialog()
    {
        InitializeComponent();
    }

    public TgiEditorDialog(TgiEditorDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private TgiEditorDialogViewModel ViewModel => (TgiEditorDialogViewModel)DataContext!;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = EntryGrid.SelectedItems.Cast<EntryItemViewModel>().ToList();
        ViewModel.SelectedEntries = selected;

        // Keep the single-entry editor section (bound to Document.SelectedEntry, which
        // also auto-populates NewTypeText/NewGroupText/NewInstanceText - see
        // MainWindowViewModel's own OnSelectedEntryChanged) in sync with this grid's own
        // selection, so picking a row here shows and edits *that* entry, not whatever was
        // selected in the main workspace before this dialog opened.
        if (selected.Count == 1)
        {
            ViewModel.Document.SelectedEntry = selected[0];
        }
    }
}
