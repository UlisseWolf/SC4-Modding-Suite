using System.Collections.ObjectModel;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs Views/UiNodePropertiesDialog.axaml: opened by DbpfWorkspaceView.axaml.cs whenever
/// UiPreviewControl.NodeClicked fires (a box in the UI preview was clicked). A thin shell
/// around MainWindowViewModel's own SelectedUiNode/UiProperties/AddUiPropertyCommand/etc -
/// same "dialog wraps the owning document's own state" pattern DirectoryDialogViewModel and
/// UiElementFinderDialogViewModel (now removed) used, just for one node's Prop/Value list
/// instead of always having its own side panel next to the preview (which left no room for
/// the preview itself - see the "UI Editor" region of DbpfWorkspaceView.axaml).
/// </summary>
public sealed class UiNodePropertiesDialogViewModel
{
    private readonly MainWindowViewModel _document;

    public UiNodePropertiesDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
    }

    public string NodeName => _document.SelectedUiNode?.Name ?? "(node)";

    public ObservableCollection<UiLegacyProp> Properties => _document.UiProperties;

    public UiLegacyProp? SelectedProperty
    {
        get => _document.SelectedUiProperty;
        set => _document.SelectedUiProperty = value;
    }

    public RelayCommand AddPropertyCommand => _document.AddUiPropertyCommand;
    public RelayCommand RemovePropertyCommand => _document.RemoveUiPropertyCommand;
    public RelayCommand AddChildCommand => _document.AddUiChildNodeCommand;
    public RelayCommand RemoveNodeCommand => _document.RemoveUiNodeCommand;
    public RelayCommand RefreshPreviewCommand => _document.RefreshUiPreviewCommand;
}
