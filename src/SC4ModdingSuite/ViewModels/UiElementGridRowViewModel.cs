using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One row of the "ALL ELEMENTS" grid (UiElementsGridDialog) - Ilive Reader's own
/// <c>CFormUI</c> shows the entire UI tree as one big spreadsheet, every node a row and
/// every known property a column, editable directly in the grid cells. This app's
/// equivalent is intentionally narrower - the handful of properties every element is most
/// likely to actually have (Id/Caption/Area/FillColor) rather than ~90 columns nearly all
/// of which would be empty for any given row - as a quick bulk-editing overview alongside
/// (not a replacement for) the richer per-node UiNodePropertiesDialog.
/// </summary>
public sealed class UiElementGridRowViewModel : ViewModelBase
{
    public UiElementGridRowViewModel(UiLegacyNode node, int depth)
    {
        Node = node;
        Depth = depth;
    }

    public UiLegacyNode Node { get; }
    public int Depth { get; }

    /// <summary>Indented IID (or a placeholder for a node with none) - the row's own "which element is this" label, indentation standing in for Ilive Reader's tree column embedded directly in its own grid.</summary>
    public string Label => new string(' ', Depth * 3) + (string.IsNullOrEmpty(Iid) ? "(no iid)" : Iid);

    public string Iid => Node.GetProp("iid") ?? string.Empty;

    public string Id
    {
        get => Node.GetProp("id") ?? string.Empty;
        set
        {
            Node.SetProp("id", value);
            OnPropertyChanged();
        }
    }

    public string Caption
    {
        get => Node.GetProp("caption") ?? string.Empty;
        set
        {
            Node.SetProp("caption", value);
            OnPropertyChanged();
        }
    }

    public string Area
    {
        get => Node.GetProp("area") ?? string.Empty;
        set
        {
            Node.SetProp("area", value);
            OnPropertyChanged();
        }
    }

    public string FillColor
    {
        get => Node.GetProp("fillcolor") ?? string.Empty;
        set
        {
            Node.SetProp("fillcolor", value);
            OnPropertyChanged();
        }
    }
}
