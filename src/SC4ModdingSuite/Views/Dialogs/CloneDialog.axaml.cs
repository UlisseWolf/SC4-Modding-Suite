using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgClone - see CloneDialogViewModel for the actual clone logic.</summary>
public partial class CloneDialog : Window
{
    public CloneDialog()
    {
        InitializeComponent();
    }

    public CloneDialog(CloneDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
