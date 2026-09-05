using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgDirectory - see DirectoryDialogViewModel for the decode/cross-check logic.</summary>
public partial class DirectoryDialog : Window
{
    public DirectoryDialog()
    {
        InitializeComponent();
    }

    public DirectoryDialog(DirectoryDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private DirectoryDialogViewModel ViewModel => (DirectoryDialogViewModel)DataContext!;

    private void OnSelectInListClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectCommand.CanExecute(null))
        {
            ViewModel.SelectCommand.Execute(null);
        }
    }
}
