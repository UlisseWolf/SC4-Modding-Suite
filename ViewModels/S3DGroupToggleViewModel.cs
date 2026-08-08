using System;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One checkbox row in the S3D Editor's per-group visibility list. "Group" here means
/// either an animation mesh (if the model has one) or a raw VERT/INDX/PRIM group index
/// (if it doesn't) - whichever S3DModel.EnumerateTriangles is iterating over.
/// </summary>
public sealed class S3DGroupToggleViewModel : ViewModelBase
{
    private bool _isVisible = true;

    public S3DGroupToggleViewModel(int index, string name, Action onChanged)
    {
        Index = index;
        Name = name;
        _onChanged = onChanged;
    }

    private readonly Action _onChanged;

    public int Index { get; }

    public string Name { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetField(ref _isVisible, value))
            {
                _onChanged();
            }
        }
    }
}
