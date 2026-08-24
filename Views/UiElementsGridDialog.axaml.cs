using Avalonia.Controls;
using Avalonia.Input;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Spreadsheet-style "all elements at once" view of the UI tree - see UiElementsGridDialog.axaml.</summary>
public partial class UiElementsGridDialog : Window
{
    public UiElementsGridDialog()
    {
        InitializeComponent();
    }

    public UiElementsGridDialog(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    /// <summary>Same reasoning as UiNodePropertiesDialog's own OnPropertyCellEditEnded: an edit here (Id/Caption/Area/FillColor) should show up in the preview immediately, not only after switching tabs.</summary>
    private void OnGridCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
    {
        if (ViewModel.RefreshUiPreviewCommand.CanExecute(null))
        {
            ViewModel.RefreshUiPreviewCommand.Execute(null);
        }
    }
    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not DataGrid { SelectedItem: UiElementGridRowViewModel row })
        {
            return;
        }

        ViewModel.SelectUiNodeCommand.Execute(row.Node);
        var dialog = new UiNodePropertiesDialog(new UiNodePropertiesDialogViewModel(ViewModel));
        dialog.ShowDialog(this);
    }

    private void OnRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.RefreshUiElementsGrid();

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
