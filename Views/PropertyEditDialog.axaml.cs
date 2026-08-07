using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class PropertyEditDialog : Window
{
    public PropertyEditDialog()
    {
        InitializeComponent();
    }

    public PropertyEditDialog(PropertyEditDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => Close(accepted);
    }
}
