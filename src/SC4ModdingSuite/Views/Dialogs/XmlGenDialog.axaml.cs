using Avalonia.Controls;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgXmlGen - see XmlGenDialogViewModel for the SC4PLUGINDESC generation logic.</summary>
public partial class XmlGenDialog : Window
{
    public XmlGenDialog()
    {
        InitializeComponent();
    }

    public XmlGenDialog(XmlGenDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
}
