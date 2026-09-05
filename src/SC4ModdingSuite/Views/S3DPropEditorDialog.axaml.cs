using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class S3DPropEditorDialog : Window
{
    public S3DPropEditorDialog()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void OnDeletePropsClick(object? sender, RoutedEventArgs e)
    {
        var selected = PropGrid.SelectedItems.Cast<S3DPropRowViewModel>().ToList();
        var indices = selected.Select(row => ViewModel.S3DPropRows.IndexOf(row)).Where(i => i >= 0).ToList();
        ViewModel.DeleteS3DProps(indices);
    }
}
