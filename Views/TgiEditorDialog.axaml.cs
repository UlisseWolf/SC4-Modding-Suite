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
        ViewModel.SelectedEntries = EntryGrid.SelectedItems.Cast<EntryItemViewModel>().ToList();
    }
}
