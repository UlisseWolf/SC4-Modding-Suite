using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class S3DRegPointEditorDialog : Window
{
    public S3DRegPointEditorDialog()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private void OnDeleteTransformsClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var grid = this.FindControl<DataGrid>("TransformGrid");
        var indices = grid?.SelectedItems.Cast<S3DRegPointTransformRowViewModel>()
            .Select(r => r.FrameIndex).ToList() ?? new System.Collections.Generic.List<int>();

        ViewModel.DeleteS3DRegPointTransforms(indices);
    }
}
