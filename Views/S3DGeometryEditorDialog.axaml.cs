using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class S3DGeometryEditorDialog : Window
{
    public S3DGeometryEditorDialog()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    // "Delete Sel." reads the DataGrid's own SelectedItems (multi-select via Ctrl/Shift-click)
    // and hands the row indices to the ViewModel - matches "Add" living as a plain Command
    // binding (no selection needed) while "Delete" needs code-behind to reach the grid's
    // current selection. Moved here unchanged from MainWindow.axaml.cs.

    private void OnDeleteS3DVertexRowsClick(object? sender, RoutedEventArgs e)
    {
        var indices = VertexGrid.SelectedItems.Cast<S3DVertexRowViewModel>().Select(r => r.Index).ToList();
        ViewModel.DeleteS3DVertexPoints(indices);
    }

    private void OnDeleteS3DIndexRowsClick(object? sender, RoutedEventArgs e)
    {
        var indices = IndexGrid.SelectedItems.Cast<S3DIndexRowViewModel>().Select(r => r.RowIndex).ToList();
        ViewModel.DeleteS3DIndexTriangles(indices);
    }

    private void OnDeleteS3DPrimRowsClick(object? sender, RoutedEventArgs e)
    {
        var indices = PrimGrid.SelectedItems.Cast<S3DPrimRowViewModel>().Select(r => r.RowIndex).ToList();
        ViewModel.DeleteS3DPrimRows(indices);
    }
}
