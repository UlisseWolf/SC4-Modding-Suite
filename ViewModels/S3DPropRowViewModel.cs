using System;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>One row in the PROP Editor's grid (Ilive Reader's Tab3DMProp, TAB3DPROP_COL0..4: row #/mesh index/frame number/key/value) - a single arbitrary key/value string pair attached to a mesh+frame. Edits commit straight through to the underlying <see cref="S3DPropBlock"/>, same immediate-commit convention as the other S3D Editor grids.</summary>
public sealed class S3DPropRowViewModel : ViewModelBase
{
    private readonly S3DPropBlock _block;
    private readonly Action _onChanged;

    public S3DPropRowViewModel(S3DPropBlock block, Action onChanged)
    {
        _block = block;
        _onChanged = onChanged;
    }

    public int MeshIndex
    {
        get => _block.MeshIndex;
        set { _block.MeshIndex = (ushort)Math.Max(0, value); OnPropertyChanged(); _onChanged(); }
    }

    public int FrameNumber
    {
        get => _block.FrameNumber;
        set { _block.FrameNumber = (ushort)Math.Max(0, value); OnPropertyChanged(); _onChanged(); }
    }

    public string KeyName
    {
        get => _block.KeyName;
        set { _block.KeyName = value ?? string.Empty; OnPropertyChanged(); _onChanged(); }
    }

    public string ValueName
    {
        get => _block.ValueName;
        set { _block.ValueName = value ?? string.Empty; OnPropertyChanged(); _onChanged(); }
    }
}
