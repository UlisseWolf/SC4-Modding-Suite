using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgGroupPatch - see GroupPatchDialogViewModel for scope notes and apply logic.</summary>
public partial class GroupPatchDialog : Window
{
    public GroupPatchDialog()
    {
        InitializeComponent();
    }

    public GroupPatchDialog(GroupPatchDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
