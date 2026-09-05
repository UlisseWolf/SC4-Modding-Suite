using System.Collections.Generic;
using System.Numerics;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Row-level mutation helpers for the S3D Editor's VERT/INDX/PRIM grids - the geometry
/// editing operations Ilive Reader offers across its three separate tabs (Tab3DMVert,
/// Tab3DMIndx, Tab3DMPrim), generalized here into one consistent Add/Delete pattern for
/// all three block types instead of replicating Ilive's own inconsistencies (e.g. Ilive's
/// Tab3DMPrim only allows whole-group add/delete, never individual rows; there is no
/// reason to carry that limitation forward here).
///
/// All list mutations are index/count-based on the plain <see cref="S3DVertexBlock"/>/
/// <see cref="S3DIndexBlock"/>/<see cref="S3DPrimBlock"/> data - no UI/ViewModel
/// dependency, so these are trivially unit-testable and reusable from anywhere that holds
/// a parsed <see cref="S3DModel"/> (currently just MainWindowViewModel's S3D Editor).
/// </summary>
public static class S3DEditOps
{
    /// <summary>Appends <paramref name="count"/> zero-initialized vertices. All four parallel lists (Positions/Uvs/Uv1s/Colors) are kept the same length regardless of the block's Format - S3DWriter only ever emits the fields Format says are present, so extra unused entries in the others are harmless and this avoids having to special-case every format combination here.</summary>
    public static void AddVertexPoints(S3DVertexBlock block, int count)
    {
        for (var i = 0; i < count; i++)
        {
            block.Positions.Add(Vector3.Zero);
            block.Uvs.Add(Vector2.Zero);
            block.Uv1s.Add(Vector2.Zero);
            block.Colors.Add((0, 0, 0, 255));
        }
    }

    /// <summary>Removes the given local vertex indices (single or multiple - "N" is just the count of <paramref name="indices"/>) from every parallel list.</summary>
    public static void RemoveVertexPoints(S3DVertexBlock block, IReadOnlyList<int> indices)
    {
        foreach (var i in SortedDescending(indices))
        {
            if (i < 0 || i >= block.Positions.Count)
            {
                continue;
            }

            block.Positions.RemoveAt(i);
            if (i < block.Uvs.Count) block.Uvs.RemoveAt(i);
            if (i < block.Uv1s.Count) block.Uv1s.RemoveAt(i);
            if (i < block.Colors.Count) block.Colors.RemoveAt(i);
        }
    }

    /// <summary>Appends <paramref name="count"/> zero-index triangle rows (3 indices each).</summary>
    public static void AddIndexTriangles(S3DIndexBlock block, int count)
    {
        for (var i = 0; i < count; i++)
        {
            block.Indices.Add(0);
            block.Indices.Add(0);
            block.Indices.Add(0);
        }
    }

    /// <summary>Removes the given triangle rows (each row = 3 consecutive WORDs in the flat index array).</summary>
    public static void RemoveIndexTriangles(S3DIndexBlock block, IReadOnlyList<int> rowIndices)
    {
        foreach (var row in SortedDescending(rowIndices))
        {
            var start = row * 3;
            if (start < 0 || start + 2 >= block.Indices.Count)
            {
                continue;
            }

            block.Indices.RemoveAt(start + 2);
            block.Indices.RemoveAt(start + 1);
            block.Indices.RemoveAt(start);
        }
    }

    /// <summary>Appends one default primitive row (type 4 = GL_TRIANGLES, matching Ilive Reader's own primitive type constants; first/count both 0, edit them in the grid).</summary>
    public static void AddPrimRow(S3DPrimBlock block) =>
        block.Primitives.Add(new S3DPrimitive { Type = 4, First = 0, Count = 0 });

    public static void RemovePrimRows(S3DPrimBlock block, IReadOnlyList<int> rowIndices)
    {
        foreach (var row in SortedDescending(rowIndices))
        {
            if (row < 0 || row >= block.Primitives.Count)
            {
                continue;
            }

            block.Primitives.RemoveAt(row);
        }
    }

    /// <summary>Adds one fully-empty group: a VERT + INDX + PRIM block together, keeping the three arrays parallel (matches how <see cref="S3DModel.EnumerateTriangles"/> indexes them for non-animated models).</summary>
    public static void AddGroup(S3DModel model)
    {
        model.VertexBlocks.Add(new S3DVertexBlock { Format = S3DParser.FormatT2F });
        model.IndexBlocks.Add(new S3DIndexBlock());
        model.PrimBlocks.Add(new S3DPrimBlock());
    }

    /// <summary>
    /// Removes group <paramref name="index"/> from VERT/INDX/PRIM. Does NOT renumber any
    /// Animation frame block references - safe for the common non-animated case; for an
    /// animated model this can leave stale frame indices pointing past the shrunk arrays
    /// (the caller surfaces that as a StatusMessage warning rather than silently
    /// attempting to rewrite the ANIM chunk's frame mapping, which would be a much larger
    /// and riskier change for a rarely-hit case).
    /// </summary>
    public static void RemoveGroup(S3DModel model, int index)
    {
        if (index >= 0 && index < model.VertexBlocks.Count) model.VertexBlocks.RemoveAt(index);
        if (index >= 0 && index < model.IndexBlocks.Count) model.IndexBlocks.RemoveAt(index);
        if (index >= 0 && index < model.PrimBlocks.Count) model.PrimBlocks.RemoveAt(index);
    }

    /// <summary>Axis swap across every vertex of every group - ported from Ilive Reader's Tab3DMVert::OnMenuFlipXY/XZ/YZ exactly: a coordinate swap (x&lt;-&gt;y etc.), not a negation/mirror.</summary>
    public static void FlipAxes(S3DModel model, char axisA, char axisB)
    {
        foreach (var block in model.VertexBlocks)
        {
            for (var i = 0; i < block.Positions.Count; i++)
            {
                block.Positions[i] = Swap(block.Positions[i], axisA, axisB);
            }
        }
    }

    private static Vector3 Swap(Vector3 p, char axisA, char axisB)
    {
        var a = Get(p, axisA);
        var b = Get(p, axisB);
        Set(ref p, axisA, b);
        Set(ref p, axisB, a);
        return p;
    }

    private static float Get(Vector3 p, char axis) => axis switch { 'x' => p.X, 'y' => p.Y, _ => p.Z };

    private static void Set(ref Vector3 p, char axis, float value)
    {
        switch (axis)
        {
            case 'x': p.X = value; break;
            case 'y': p.Y = value; break;
            default: p.Z = value; break;
        }
    }

    /// <summary>
    /// "Remap Indices" - clamps every index in this INDX block into the valid
    /// [0, vertexCount) range for its matching VERT group, fixing stale/out-of-range
    /// references left over after point deletions or manual grid edits. (Ilive Reader has
    /// no real equivalent of this despite the name - its own "Remap" button in Tab3DMVert
    /// is actually a UV-wrap/normalize tool, unrelated to indices; this is the tool the
    /// name in the feature request implies.) Returns how many indices were out of range
    /// and got fixed.
    /// </summary>
    public static int RemapIndices(S3DIndexBlock indexBlock, int vertexCount)
    {
        if (vertexCount <= 0)
        {
            return 0;
        }

        var fixedCount = 0;
        for (var i = 0; i < indexBlock.Indices.Count; i++)
        {
            if (indexBlock.Indices[i] >= vertexCount)
            {
                indexBlock.Indices[i] = (ushort)(vertexCount - 1);
                fixedCount++;
            }
        }

        return fixedCount;
    }

    private static List<int> SortedDescending(IReadOnlyList<int> indices)
    {
        var list = new List<int>(indices);
        list.Sort();
        list.Reverse();
        return list;
    }
}
