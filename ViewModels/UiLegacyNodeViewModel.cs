using System.Collections.ObjectModel;
using System.Linq;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>Tree row wrapping one <see cref="UiLegacyNode"/> - Ilive Reader's UI element tree (WorkspaceUILegacy.cpp's tree, FormUI's grid selection).</summary>
public sealed class UiLegacyNodeViewModel : ViewModelBase
{
    public UiLegacyNodeViewModel(UiLegacyNode node)
    {
        Node = node;
        Children = new ObservableCollection<UiLegacyNodeViewModel>(node.Children.Select(c => new UiLegacyNodeViewModel(c)));
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

    public void RefreshName() => OnPropertyChanged(nameof(Name));
}
