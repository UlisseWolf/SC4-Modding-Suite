using System.Collections.Generic;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.Views;

namespace SC4ModdingSuite.ViewModels;

/// <summary>One selectable entry in AddUiElementDialog's template list - the source node, a human-readable label, and its own rendered preview (see MainWindowViewModel.BuildSingleElementPreview).</summary>
public sealed class UiElementTemplateRowViewModel
{
    public UiElementTemplateRowViewModel(UiLegacyNode node, string label, IReadOnlyList<UiPreviewControl.PreviewBox> preview)
    {
        Node = node;
        Label = label;
        Preview = preview;
    }

    public UiLegacyNode Node { get; }
    public string Label { get; }
    public IReadOnlyList<UiPreviewControl.PreviewBox> Preview { get; }
}
