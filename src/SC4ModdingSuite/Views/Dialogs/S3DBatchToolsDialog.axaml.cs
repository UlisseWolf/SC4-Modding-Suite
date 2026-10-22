using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Clean-room S3D batch tools dialog - see S3DBatchToolsDialogViewModel for the actual operations.</summary>
public partial class S3DBatchToolsDialog : Window
{
    public S3DBatchToolsDialog()
    {
        InitializeComponent();
    }

    public S3DBatchToolsDialog(S3DBatchToolsDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
