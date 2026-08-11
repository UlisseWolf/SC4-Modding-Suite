namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One labeled on/off toggle - backs the T21 Editor's Pattern (16), Zones (16), Wealth
/// Types (4) and Allowed Rotations (4) checkbox grids, each of which is just a small,
/// fixed-size set of "which of these bit positions are set" checkboxes in Jondor's own
/// T21 Editor (<c>patternButton[]</c>/<c>zonesCheck[]</c>/<c>wealthNone..High</c>/
/// <c>rotsNorth..West</c>). Kept as one reusable, data-bound list per group instead of 16
/// individually-named boolean properties.
/// </summary>
public sealed class T21ToggleViewModel : ViewModelBase
{
    public T21ToggleViewModel(int code, string label)
    {
        Code = code;
        Label = label;
    }

    /// <summary>The stored bit position / enum code this toggle represents (0-15 for Pattern/Zones, 0-3 for Wealth/Rotations).</summary>
    public int Code { get; }

    public string Label { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
