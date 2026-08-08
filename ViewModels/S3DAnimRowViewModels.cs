using System;
using System.Collections.Generic;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>AnimMode option list for the Animation Editor - values/labels ported from Ilive Reader's Tab3DMAnim::OnInitDialog (TAB3DANIM_MSG3/4/5). Reuses <see cref="S3DMaterialOption"/> (Value+Label) rather than a near-identical type just for this one combo.</summary>
public static class S3DAnimModeOptions
{
    public static IReadOnlyList<S3DMaterialOption> Values { get; } = new[]
    {
        new S3DMaterialOption(1, "ping-pong"),
        new S3DMaterialOption(2, "one-shot"),
        new S3DMaterialOption(3, "loop"),
    };
}

/// <summary>
/// One mesh in the Animation Editor's mesh list (Ilive Reader's Tab3DMAnim grid's top-level
/// rows). Name/Flags edit straight through to the underlying <see cref="S3DAnimMesh"/> - no
/// buffering, same immediate-commit convention as the Geometry/Material editors.
/// </summary>
public sealed class S3DAnimMeshRowViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    public S3DAnimMeshRowViewModel(S3DAnimMesh mesh, int index, Action onChanged)
    {
        Mesh = mesh;
        Index = index;
        _onChanged = onChanged;
    }

    public S3DAnimMesh Mesh { get; }
    public int Index { get; }

    public string Name
    {
        get => Mesh.Name;
        set { Mesh.Name = value ?? string.Empty; OnPropertyChanged(); _onChanged(); }
    }

    /// <summary>Hex text for the mesh's on-disk Flags byte (Ilive's grid "Flags" column, e.g. "0x01") - a byte has no useful individual bit meaning documented anywhere, so this is exposed as a raw hex value rather than checkboxes.</summary>
    public string FlagsHex
    {
        get => $"0x{Mesh.Flags:X2}";
        set
        {
            if (byte.TryParse(value?.Trim().TrimStart('0', 'x', 'X'), System.Globalization.NumberStyles.HexNumber, null, out var parsed))
            {
                Mesh.Flags = parsed;
                OnPropertyChanged();
                _onChanged();
            }
        }
    }

    public int FrameCount => Mesh.Frames.Count;
}

/// <summary>One animation frame row for the currently selected mesh - VertBlock/IndexBlock/PrimBlock/MaterialBlock (Ilive's per-frame grid sub-rows), which VERT/INDX/PRIM/MATS block index this mesh uses at this keyframe.</summary>
public sealed class S3DAnimFrameRowViewModel : ViewModelBase
{
    private readonly S3DAnimMesh _mesh;
    private readonly Action _onChanged;

    public S3DAnimFrameRowViewModel(S3DAnimMesh mesh, int frameIndex, Action onChanged)
    {
        _mesh = mesh;
        FrameIndex = frameIndex;
        _onChanged = onChanged;
    }

    public int FrameIndex { get; }

    private S3DAnimFrame Frame => _mesh.Frames[FrameIndex];

    public int VertBlock
    {
        get => Frame.VertBlock;
        set { Frame.VertBlock = (ushort)Math.Max(0, value); OnPropertyChanged(); _onChanged(); }
    }

    public int IndexBlock
    {
        get => Frame.IndexBlock;
        set { Frame.IndexBlock = (ushort)Math.Max(0, value); OnPropertyChanged(); _onChanged(); }
    }

    public int PrimBlock
    {
        get => Frame.PrimBlock;
        set { Frame.PrimBlock = (ushort)Math.Max(0, value); OnPropertyChanged(); _onChanged(); }
    }

    public int MaterialBlock
    {
        get => Frame.MaterialBlock;
        set { Frame.MaterialBlock = (ushort)Math.Max(0, value); OnPropertyChanged(); _onChanged(); }
    }
}
