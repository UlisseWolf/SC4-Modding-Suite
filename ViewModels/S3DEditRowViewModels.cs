using System;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One row in the S3D Editor's Vertices grid (Tab3DMVert equivalent). Reads/writes
/// straight through to the underlying <see cref="S3DVertexBlock"/> at a fixed index - no
/// buffering/apply step, matching Ilive Reader's own immediate-commit grid editing.
/// Position (X/Y/Z) and UV0 (U0/V0) are exposed; UV1 and vertex color exist on the
/// underlying block (see <see cref="S3DEditOps.AddVertexPoints"/>) but aren't surfaced as
/// grid columns - a deliberate scope cut, since the vast majority of SC4 prop/building
/// models only use a single UV set and no per-vertex color.
/// </summary>
public sealed class S3DVertexRowViewModel : ViewModelBase
{
    private readonly S3DVertexBlock _block;
    private readonly Action _onChanged;

    public S3DVertexRowViewModel(S3DVertexBlock block, int index, Action onChanged)
    {
        _block = block;
        Index = index;
        _onChanged = onChanged;
    }

    public int Index { get; }

    public float X
    {
        get => _block.Positions[Index].X;
        set { var p = _block.Positions[Index]; p.X = value; _block.Positions[Index] = p; OnPropertyChanged(); _onChanged(); }
    }

    public float Y
    {
        get => _block.Positions[Index].Y;
        set { var p = _block.Positions[Index]; p.Y = value; _block.Positions[Index] = p; OnPropertyChanged(); _onChanged(); }
    }

    public float Z
    {
        get => _block.Positions[Index].Z;
        set { var p = _block.Positions[Index]; p.Z = value; _block.Positions[Index] = p; OnPropertyChanged(); _onChanged(); }
    }

    public float U0
    {
        get => Index < _block.Uvs.Count ? _block.Uvs[Index].X : 0f;
        set { if (Index < _block.Uvs.Count) { var uv = _block.Uvs[Index]; uv.X = value; _block.Uvs[Index] = uv; OnPropertyChanged(); _onChanged(); } }
    }

    public float V0
    {
        get => Index < _block.Uvs.Count ? _block.Uvs[Index].Y : 0f;
        set { if (Index < _block.Uvs.Count) { var uv = _block.Uvs[Index]; uv.Y = value; _block.Uvs[Index] = uv; OnPropertyChanged(); _onChanged(); } }
    }
}

/// <summary>
/// One row in the S3D Editor's Indices grid (Tab3DMIndx equivalent) - one triangle, i.e.
/// three consecutive WORDs in the flat <see cref="S3DIndexBlock.Indices"/> array. RowIndex
/// is the triangle's position among all triangle-rows in this INDX block (raw grouping by
/// 3, same simplification Ilive Reader's own Tab3DMIndx uses - it does not attempt to
/// interpret PRIM primitive types like strip/fan when building rows).
/// </summary>
public sealed class S3DIndexRowViewModel : ViewModelBase
{
    private readonly S3DIndexBlock _block;
    private readonly Action _onChanged;

    public S3DIndexRowViewModel(S3DIndexBlock block, int rowIndex, Action onChanged)
    {
        _block = block;
        RowIndex = rowIndex;
        _onChanged = onChanged;
    }

    public int RowIndex { get; }

    private int Base => RowIndex * 3;

    public ushort T1
    {
        get => _block.Indices[Base];
        set { _block.Indices[Base] = value; OnPropertyChanged(); _onChanged(); }
    }

    public ushort T2
    {
        get => _block.Indices[Base + 1];
        set { _block.Indices[Base + 1] = value; OnPropertyChanged(); _onChanged(); }
    }

    public ushort T3
    {
        get => _block.Indices[Base + 2];
        set { _block.Indices[Base + 2] = value; OnPropertyChanged(); _onChanged(); }
    }
}

/// <summary>One row in the S3D Editor's Primitives grid (Tab3DMPrim equivalent) - Type/First/Count of one <see cref="S3DPrimitive"/>.</summary>
public sealed class S3DPrimRowViewModel : ViewModelBase
{
    private readonly S3DPrimBlock _block;
    private readonly Action _onChanged;

    public S3DPrimRowViewModel(S3DPrimBlock block, int rowIndex, Action onChanged)
    {
        _block = block;
        RowIndex = rowIndex;
        _onChanged = onChanged;
    }

    public int RowIndex { get; }

    public uint Type
    {
        get => _block.Primitives[RowIndex].Type;
        set { _block.Primitives[RowIndex] = _block.Primitives[RowIndex] with { Type = value }; OnPropertyChanged(); _onChanged(); }
    }

    public uint First
    {
        get => _block.Primitives[RowIndex].First;
        set { _block.Primitives[RowIndex] = _block.Primitives[RowIndex] with { First = value }; OnPropertyChanged(); _onChanged(); }
    }

    public uint Count
    {
        get => _block.Primitives[RowIndex].Count;
        set { _block.Primitives[RowIndex] = _block.Primitives[RowIndex] with { Count = value }; OnPropertyChanged(); _onChanged(); }
    }
}
