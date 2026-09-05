using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgTemplate - see TemplateDialogViewModel for the insert logic.</summary>
public partial class TemplateDialog : Window
{
    public TemplateDialog()
    {
        InitializeComponent();
    }

    public TemplateDialog(TemplateDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.Closed += (_, _) => Close();
    }

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = (TemplateDialogViewModel)DataContext!;
        if (viewModel.InsertCommand.CanExecute(null))
        {
            viewModel.InsertCommand.Execute(null);
        }
    }
}
