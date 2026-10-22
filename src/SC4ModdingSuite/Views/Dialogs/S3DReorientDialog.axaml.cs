using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Clean-room "Reorient" dialog - see S3DReorientDialogViewModel for the actual logic.</summary>
public partial class S3DReorientDialog : Window
{
    public S3DReorientDialog()
    {
        InitializeComponent();
    }

    public S3DReorientDialog(S3DReorientDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
