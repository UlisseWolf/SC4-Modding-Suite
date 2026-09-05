using System;
using System.Collections.ObjectModel;
using System.Linq;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Tree row wrapping one <see cref="UiLegacyNode"/> - Ilive Reader's UI element tree
/// (WorkspaceUILegacy.cpp's tree, FormUI's grid selection). <see cref="Children"/> makes
/// this directly bindable to a TreeView's HierarchicalDataTemplate, matching Ilive Reader's
/// own tree, which - unlike this app's 2D preview alone - lets every node be found and
/// selected regardless of whether it currently has a visible box at all (zero size, fully
/// hidden under a sibling, or off the visible canvas).
/// </summary>
public sealed class UiLegacyNodeViewModel : ViewModelBase
{
    private readonly Action? _onDisplayedChanged;

    /// <param name="onDisplayedChanged">
    /// Called whenever <see cref="IsDisplayed"/> actually changes - MainWindowViewModel
    /// passes its own RefreshUiPreview here so toggling a node's checkbox in the tree
    /// (see <see cref="IsDisplayed"/>) immediately shows/hides that node's box in the 2D
    /// preview, the same way Ilive Reader's own tree checkbox drives
    /// <c>_preview_ui::displayed</c>. Threaded through the recursive Children construction
    /// below so every descendant gets the same callback, not just the root being constructed.
    /// </param>
    public UiLegacyNodeViewModel(UiLegacyNode node, Action? onDisplayedChanged = null)
    {
        Node = node;
        _onDisplayedChanged = onDisplayedChanged;
        Children = new ObservableCollection<UiLegacyNodeViewModel>(node.Children.Select(c => new UiLegacyNodeViewModel(c, onDisplayedChanged)));
    }

    public UiLegacyNode Node { get; }
    public ObservableCollection<UiLegacyNodeViewModel> Children { get; }

    /// <summary>Display label: caption if present (quotes stripped, same as Ilive Reader's BuildPreviewUI), else clsid, else "LEGACY".</summary>
    public string Name
    {
        get
        {
            var caption = Node.GetProp("caption");
            if (!string.IsNullOrEmpty(caption))
            {
                return caption.Length >= 2 && caption[0] == '"' && caption[^1] == '"'
                    ? caption.Substring(1, caption.Length - 2)
                    : caption;
            }

            return Node.GetProp("clsid") ?? (Node.IsRoot ? "(root)" : "LEGACY");
        }
    }

    /// <summary>The "iid" prop, e.g. "IGZWinBtn" - shown as its own tree column, matching Ilive Reader's tree (IID/Id/Caption columns).</summary>
    public string Iid => Node.GetProp("iid") ?? string.Empty;

    /// <summary>The "id" prop (the numeric control ID a Notify handler dispatches on) - the tree's second column in Ilive Reader.</summary>
    public string Id => Node.GetProp("id") ?? string.Empty;

    /// <summary>
    /// Whether this node's own box (if it draws one at all - see MainWindowViewModel.
    /// CollectPreviewBoxes) currently shows in the 2D preview - bound to the tree's own
    /// checkbox per row, same as Ilive Reader's <c>TVS_CHECKBOXES</c> tree
    /// (WorkspaceUILegacy.cpp's <c>pPreviewUI-&gt;displayed</c>). Purely a preview-time
    /// toggle - does not touch the node's own data/props, so it's never part of what SAVE
    /// writes back into the entry, and unchecking a group node does not hide its children
    /// (each node's checkbox is independent, exactly like Ilive Reader's own per-item
    /// checkboxes).
    /// </summary>
    private bool _isDisplayed = true;
    public bool IsDisplayed
    {
        get => _isDisplayed;
        set
        {
            if (SetField(ref _isDisplayed, value))
            {
                _onDisplayedChanged?.Invoke();
            }
        }
    }

    public void RefreshName() => OnPropertyChanged(nameof(Name));
}
