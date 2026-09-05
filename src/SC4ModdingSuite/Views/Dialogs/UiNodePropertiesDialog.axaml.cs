using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Prop/Value editor for one UI element - see UiNodePropertiesDialogViewModel.</summary>
public partial class UiNodePropertiesDialog : Window
{
    public UiNodePropertiesDialog()
    {
        InitializeComponent();
    }

    public UiNodePropertiesDialog(UiNodePropertiesDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private UiNodePropertiesDialogViewModel ViewModel => (UiNodePropertiesDialogViewModel)DataContext!;

    private void OnPropertyCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e) => RefreshPreview();

    /// <summary>
    /// Same purpose as OnPropertyCellEditEnded above, for the type-aware editors (checkbox/
    /// color/rect/xy fields - see UiNodePropertiesDialog.axaml's DataGridTemplateColumn):
    /// those commit through their own two-way bindings on UiLegacyProp immediately (no
    /// DataGrid cell-edit lifecycle involved), so the preview needs its own nudge here too.
    /// </summary>
    private void OnTypedPropertyEdited(object? sender, RoutedEventArgs e) => RefreshPreview();

    private void OnTypedPropertyEdited(object? sender, NumericUpDownValueChangedEventArgs e) => RefreshPreview();

    /// <summary>This dialog's own copy of the main toolbar's "ADD CHILD" button (see DbpfWorkspaceView.axaml.cs.OnAddUiChildClick) - opens the same template picker, then adds under whichever node this dialog is currently showing.</summary>
    private async void OnAddChildClick(object? sender, RoutedEventArgs e)
    {
        var templates = ViewModel.Document.BuildUiElementTemplates();
        var dialog = new AddUiElementDialog(templates, ViewModel.Document.BuildSingleElementPreview);
        await dialog.ShowDialog(this);
        if (dialog.Confirmed)
        {
            ViewModel.Document.AddUiChildNode(dialog.SelectedTemplate);
        }
    }

    private void RefreshPreview()
    {
        if (ViewModel.RefreshPreviewCommand.CanExecute(null))
        {
            ViewModel.RefreshPreviewCommand.Execute(null);
        }
    }
}
