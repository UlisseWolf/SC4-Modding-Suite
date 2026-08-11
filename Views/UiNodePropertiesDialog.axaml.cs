using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Prop/Value editor for one UI element - see UiNodePropertiesDialogViewModel.</summary>
public partial class UiNodePropertiesDialog : Window
{
    public UiNodePropertiesDialog()
    {
        InitializeComponent();
    }

    public UiNodePropertiesDialog(UiNodePropertiesDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private UiNodePropertiesDialogViewModel ViewModel => (UiNodePropertiesDialogViewModel)DataContext!;

    private void OnPropertyCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        // Editing a Prop/Value cell directly (e.g. changing "area" or "caption") used to
        // only show up in the preview after manually pressing REFRESH PREVIEW - now it
        // updates as soon as the cell is committed, matching every other edit here
        // (add/remove node, add/remove property) already refreshing automatically.
        if (ViewModel.RefreshPreviewCommand.CanExecute(null))
        {
            ViewModel.RefreshPreviewCommand.Execute(null);
        }
    }
}
