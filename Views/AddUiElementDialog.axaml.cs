using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>See AddUiElementDialog.axaml - template picker for MainWindowViewModel.AddUiChildNode.</summary>
public partial class AddUiElementDialog : Window
{
    public UiLegacyNode? SelectedTemplate { get; private set; }
    public bool Confirmed { get; private set; }

    public AddUiElementDialog()
    {
        InitializeComponent();
    }

    /// <param name="templates">Every candidate template (see MainWindowViewModel.BuildUiElementTemplates) - there is no "blank element" fallback shown here; if this is empty, ADD is disabled (nothing to start from) rather than silently offering a placeholder.</param>
    /// <param name="buildPreview">MainWindowViewModel.BuildSingleElementPreview, so each row can render its own actual preview instead of just a type name.</param>
    public AddUiElementDialog(IReadOnlyList<UiLegacyNode> templates, System.Func<UiLegacyNode, IReadOnlyList<UiPreviewControl.PreviewBox>> buildPreview) : this()
    {
        var rows = templates
            .Select(node => new UiElementTemplateRowViewModel(node, Describe(node), buildPreview(node)))
            .ToList();

        TemplateList.ItemsSource = rows;
        TemplateList.SelectedIndex = rows.Count > 0 ? 0 : -1;

        if (rows.Count == 0)
        {
            AddButton.IsEnabled = false;
            ToolTip.SetTip(AddButton, "No other UI elements are open in this package to start from - open/select a file with at least one UI entry first.");
        }
    }

    private static string Describe(UiLegacyNode node)
    {
        var iid = node.GetProp("iid") ?? "(no iid)";
        var captionRaw = node.GetProp("caption");
        var caption = captionRaw is { Length: >= 2 } && captionRaw[0] == '"' && captionRaw[^1] == '"'
            ? captionRaw[1..^1]
            : captionRaw;

        var label = string.IsNullOrEmpty(caption) ? iid : $"{iid} - \"{caption}\"";
        return $"{label}\n{node.Properties.Count} properties";
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (TemplateList.SelectedItem is UiElementTemplateRowViewModel row)
        {
            SelectedTemplate = row.Node;
        }

        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
