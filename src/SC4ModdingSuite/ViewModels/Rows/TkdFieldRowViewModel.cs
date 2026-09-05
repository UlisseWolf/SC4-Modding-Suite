namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One editable row of the TRK field grid (see MainWindowViewModel.TrkFieldRows) - Label is
/// fixed (either "Field N" or one of the three special names - XA/TLO/HLS instance), Value
/// is the actual field text, edited in place and read back by MainWindowViewModel.SaveTrk
/// when SAVE TRK is pressed.
/// </summary>
public sealed class TkdFieldRowViewModel : ViewModelBase
{
    public TkdFieldRowViewModel(string label, string value, bool isReadOnly = false)
    {
        Label = label;
        _value = value;
        IsReadOnly = isReadOnly;
    }

    public string Label { get; }
    public bool IsReadOnly { get; }

    private string _value;
    public string Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }
}
