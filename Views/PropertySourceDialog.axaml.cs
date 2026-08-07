using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class PropertySourceDialog : Window
{
    public PropertySourceDialog()
    {
        InitializeComponent();
    }

    public PropertySourceDialog(PropertySourceDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, success) => Close(success);
    }
}
