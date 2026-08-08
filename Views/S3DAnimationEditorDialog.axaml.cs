using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class S3DAnimationEditorDialog : Window
{
    public S3DAnimationEditorDialog()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void OnDeleteAnimFramesClick(object? sender, RoutedEventArgs e)
    {
        var indices = FrameGrid.SelectedItems.Cast<S3DAnimFrameRowViewModel>().Select(r => r.FrameIndex).ToList();
        ViewModel.DeleteS3DAnimFrames(indices);
    }
}
