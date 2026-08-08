using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class LuaEditorDialog : Window
{
    public LuaEditorDialog()
    {
        InitializeComponent();
    }

    public LuaEditorDialog(LuaEditorDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => Close(accepted);
    }
}
