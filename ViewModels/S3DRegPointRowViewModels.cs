using System;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One registration point in the REGP Editor's list (e.g. an attachment point - a named
/// point with one transform per animation frame). Neither Ilive Reader nor SC4ModdingSuite
/// had a working editor for this chunk before now (Ilive's own Tab3DMRegp.cpp is a dead,
/// never-wired-up stub - see the class remarks below); this is a fresh UI over the
/// already-correct <see cref="S3DRegPointBlock"/> read/write support, not a port.
/// </summary>
public sealed class S3DRegPointRowViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    public S3DRegPointRowViewModel(S3DRegPointBlock block, Action onChanged)
    {
        Block = block;
        _onChanged = onChanged;
    }

    public S3DRegPointBlock Block { get; }

    public string Name
    {
        get => Block.Name;
        set { Block.Name = value ?? string.Empty; OnPropertyChanged(); _onChanged(); }
    }

    public int TransformCount => Block.Transforms.Count;
}

/// <summary>One transform (translation + quaternion orientation) for the selected registration point, at a given animation frame index.</summary>
public sealed class S3DRegPointTransformRowViewModel : ViewModelBase
{
    private readonly S3DRegPointBlock _block;
    private readonly Action _onChanged;

    public S3DRegPointTransformRowViewModel(S3DRegPointBlock block, int frameIndex, Action onChanged)
    {
        _block = block;
        FrameIndex = frameIndex;
        _onChanged = onChanged;
    }

    public int FrameIndex { get; }

    private S3DRegPointTransform Transform => _block.Transforms[FrameIndex];

    public float X
    {
        get => Transform.Translation.X;
        set { var t = Transform; var v = t.Translation; v.X = value; t.Translation = v; OnPropertyChanged(); _onChanged(); }
    }

    public float Y
    {
        get => Transform.Translation.Y;
        set { var t = Transform; var v = t.Translation; v.Y = value; t.Translation = v; OnPropertyChanged(); _onChanged(); }
    }

    public float Z
    {
        get => Transform.Translation.Z;
        set { var t = Transform; var v = t.Translation; v.Z = value; t.Translation = v; OnPropertyChanged(); _onChanged(); }
    }

    public float QX
    {
        get => Transform.Orientation[0];
        set { Transform.Orientation[0] = value; OnPropertyChanged(); _onChanged(); }
    }

    public float QY
    {
        get => Transform.Orientation[1];
        set { Transform.Orientation[1] = value; OnPropertyChanged(); _onChanged(); }
    }

    public float QZ
    {
        get => Transform.Orientation[2];
        set { Transform.Orientation[2] = value; OnPropertyChanged(); _onChanged(); }
    }

    public float QW
    {
        get => Transform.Orientation[3];
        set { Transform.Orientation[3] = value; OnPropertyChanged(); _onChanged(); }
    }
}
