using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>RHD to LHD conversion of 20-view S3D sets - see S3DRhdLhdDialogViewModel.</summary>
public partial class S3DRhdLhdDialog : Window
{
    public S3DRhdLhdDialog()
    {
        InitializeComponent();
    }

    public S3DRhdLhdDialog(S3DRhdLhdDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
