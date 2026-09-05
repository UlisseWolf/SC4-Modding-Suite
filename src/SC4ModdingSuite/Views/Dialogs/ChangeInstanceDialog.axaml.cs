using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgChangeInstance - see ChangeInstanceDialogViewModel for the conflict-checked apply logic.</summary>
public partial class ChangeInstanceDialog : Window
{
    public ChangeInstanceDialog()
    {
        InitializeComponent();
    }

    public ChangeInstanceDialog(ChangeInstanceDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Closed += (_, _) => Close();
    }
}
