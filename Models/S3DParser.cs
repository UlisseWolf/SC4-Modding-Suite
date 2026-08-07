using System;
using System.Collections.Generic;
using System.Numerics;

namespace SC4ModdingSuite.Models;

/// <summary>One VERT block: vertex positions (and, when the format includes them, UV0 texture coordinates) sharing the same vertex format.</summary>
public sealed class S3DVertexBlock
{
    public List<Vector3> Positions { get; } = new();

    /// <summary>UV0 texture coordinates, index-aligned with <see cref="Positions"/>; empty if this block's vertex format has no UV data.</summary>
    public List<Vector2> Uvs { get; } = new();

    public bool HasUvs => Uvs.Count == Positions.Count && Uvs.Count > 0;
}

/// <summary>One INDX block: a group of 16-bit vertex indices, local to the matching VERT block.</summary>
public sealed class S3DIndexBlock
{
    public List<ushort> Indices { get; } = new();
}

/// <summary>One drawable primitive within a PRIM block (a run of indices interpreted as triangles/strip/fan/quads).</summary>
public readonly struct S3DPrimitive
{
    public uint Type { get; init; }
    public uint First { get; init; }
    public uint Count { get; init; }
}

/// <summary>One PRIM block: a group of primitives, matching one VERT/INDX block pair.</summary>
public sealed class S3DPrimBlock
{
    public List<S3DPrimitive> Primitives { get; } = new();
}

/// <summary>
/// A parsed S3D model (SimCity 4's building/prop 3D mesh format), sufficient for a basic
/// wireframe viewer. VERT/INDX/PRIM blocks at the same list position belong to the same
/// mesh group, exactly as Ilive Reader's own s3d module treats them.
/// </summary>
public sealed class S3DModel
{
    public ushort MajorRevision { get; set; }
    public ushort MinorRevision { get; set; }
    public List<S3DVertexBlock> VertexBlocks { get; } = new();
    public List<S3DIndexBlock> IndexBlocks { get; } = new();
    public List<S3DPrimBlock> PrimBlocks { get; } = new();
    public int MaterialCount { get; set; }
    public bool HasAnimation { get; set; }

    /// <summary>
    /// The first texture reference found across all materials (a material's own
    /// <c>textureID</c>, per Ilive Reader's <c>_s3dmat::Decode</c>). By SC4 modding
    /// convention this ID is the Instance ID of an FSH texture entry sharing the model's
    /// own Group ID within the same package - used to resolve a texture bitmap for the
    /// "Solid" render mode. Null if the model has no materials/textures at all.
    /// </summary>
    public uint? PrimaryTextureId { get; set; }

    public int TotalVertexCount
    {
        get
        {
            var total = 0;
            foreach (var block in VertexBlocks)
            {
                total += block.Positions.Count;
            }

            return total;
        }
    }

    /// <summary>
    /// Enumerates every triangle across all mesh groups as (group index, local vertex
    /// index A, B, C), expanding triangle strips/fans/quads into plain triangles. "Local"
    /// index is relative to that group's own VERT block, matching how INDX values are
    /// always scoped to their paired VERT block in the S3D format.
    /// </summary>
    public IEnumerable<(int Group, int A, int B, int C)> EnumerateTriangles()
    {
        var groupCount = Math.Min(IndexBlocks.Count, PrimBlocks.Count);
        for (var g = 0; g < groupCount; g++)
        {
            var indices = IndexBlocks[g].Indices;
            foreach (var prim in PrimBlocks[g].Primitives)
            {
                var first = (int)prim.First;
                var count = (int)prim.Count;

                switch (prim.Type)
                {
                    case 0: // triangles
                        for (var i = 0; i + 2 < count; i += 3)
                        {
                            if (first + i + 2 >= indices.Count)
                            {
                                break;
                            }

                            yield return (g, indices[first + i], indices[first + i + 1], indices[first + i + 2]);
                        }

                        break;

                    case 1: // triangle strip
                        for (var i = 0; i + 2 < count; i++)
                        {
                            if (first + i + 2 >= indices.Count)
                            {
                                break;
                            }

                            yield return i % 2 == 0
                                ? (g, indices[first + i], indices[first + i + 1], indices[first + i + 2])
                                : (g, indices[first + i + 1], indices[first + i], indices[first + i + 2]);
                        }

                        break;

                    case 2: // triangle fan
                        for (var i = 1; i + 1 < count; i++)
                        {
                            if (first + i + 1 >= indices.Count)
                            {
                                break;
                            }

                            yield return (g, indices[first], indices[first + i], indices[first + i + 1]);
                        }

                        break;

                    case 6: // quads -> 2 triangles each
                        for (var i = 0; i + 3 < count; i += 4)
                        {
                            if (first + i + 3 >= indices.Count)
                            {
                                break;
                            }

                            var a = indices[first + i];
                            var b = indices[first + i + 1];
                            var c = indices[first + i + 2];
                            var d = indices[first + i + 3];
                            yield return (g, a, b, c);
                            yield return (g, a, c, d);
                        }

                        break;

                    // Type 7 (quad strip) is rare in SC4 building models and intentionally
                    // not expanded here, matching the "viewer only" scope requested.
                }
            }
        }
    }
}

/// <summary>
/// Parses the S3D format (SimCity 4 building/prop 3D models). Ported byte-for-byte from
/// Ilive Reader's s3d module (<c>s3d/s3d_main.cpp</c>, <c>s3d/struct.h</c>): a flat
/// top-level container of 4-byte-tagged chunks (HEAD/VERT/INDX/PRIM/MATS/ANIM/PROP/REGP)
/// located by linear scan, each with its own binary sub-format. Only HEAD/VERT/INDX/PRIM
/// are fully parsed here - everything needed to render a wireframe - matching the "viewer
/// only" scope requested; MATS/ANIM/PROP/REGP are only detected for the info panel.
/// </summary>
public static class S3DParser
{
    // Every chunk's local buffer starts with its own 4-byte tag + 4-byte declared size;
    // COMMON_HEAD is Ilive Reader's name for that 8-byte prefix to skip past it.
    private const int CommonHeadSize = 8;

    // Vertex format codes from struct.h / s3d_main.cpp (V3F_* constants).
    private const uint FormatC4Ub = 0x80000101; // position + color
    private const uint FormatT2F = 0x80004001; // position + 1 UV set
    private const uint Format2T2F = 0x80008001; // position + 2 UV sets
    private const uint FormatC4UbT2F = 0x80004101; // position + color + 1 UV set
    private const uint FormatC4Ub2T2F = 0x80008101; // position + color + 2 UV sets

    public static S3DModel? Parse(byte[] data)
    {
        if (data.Length < 16)
        {
            return null;
        }

        // Faithful port of Ilive Reader's DecodeS3D(): a single linear scan overwriting
        // each chunk's start offset every time its tag is (re-)found, so the LAST
        // occurrence of each tag wins - exactly like the original.
        int iVert = 0, iIndx = 0, iPrim = 0, iMats = 0, iAnim = 0;
        for (var i = 8; i <= data.Length - 4; i++)
        {
            if (Matches(data, i, "VERT")) iVert = i;
            else if (Matches(data, i, "INDX")) iIndx = i;
            else if (Matches(data, i, "PRIM")) iPrim = i;
            else if (Matches(data, i, "MATS")) iMats = i;
            else if (Matches(data, i, "ANIM")) iAnim = i;
        }

        const int iHead = 8;

        ushort major = 0, minor = 0;
        if (iVert > 0 && iHead + CommonHeadSize + 4 <= data.Length)
        {
            major = BitConverter.ToUInt16(data, iHead + CommonHeadSize);
            minor = BitConverter.ToUInt16(data, iHead + CommonHeadSize + 2);
        }

        var model = new S3DModel { MajorRevision = major, MinorRevision = minor };

        if (iVert > 0 && iIndx > iVert)
        {
            ParseVert(data, iVert, iIndx, major, minor, model.VertexBlocks);
        }

        if (iIndx > 0 && iPrim > iIndx)
        {
            ParseIndx(data, iIndx, iPrim, model.IndexBlocks);
        }

        if (iPrim > 0)
        {
            var primEnd = iMats > iPrim ? iMats : data.Length;
            ParsePrim(data, iPrim, primEnd, model.PrimBlocks);
        }

        if (iMats > 0)
        {
            var matsEnd = iAnim > iMats ? iAnim : data.Length;
            model.MaterialCount = TryReadCount(data, iMats, matsEnd);
            model.PrimaryTextureId = TryFindPrimaryTextureId(data, iMats, matsEnd);
        }

        model.HasAnimation = iAnim > 0;

        return model;
    }

    /// <summary>
    /// Finds the first texture reference in the MATS chunk, ported from Ilive Reader's
    /// <c>_s3dmat::Decode</c> (<c>s3d/s3d_main.cpp</c>): each material has a 16-byte header
    /// ending in a texture count, followed by that many texture entries (each starting
    /// with a 4-byte texture ID, then wrap/filter/anim bytes, then a name length + name).
    /// Only the very first texture ID found is used - good enough to resolve the model's
    /// main diffuse texture for the "Solid" render mode without needing a full
    /// material/multi-texture system, matching the "viewer only" scope requested.
    /// </summary>
    private static uint? TryFindPrimaryTextureId(byte[] data, int chunkStart, int chunkEnd)
    {
        var index = chunkStart + CommonHeadSize;
        if (index + 4 > chunkEnd)
        {
            return null;
        }

        var materialCount = BitConverter.ToUInt32(data, index);
        index += 4;

        for (uint m = 0; m < materialCount && index + 16 <= chunkEnd; m++)
        {
            var textureCount = data[index + 15];
            index += 16;

            for (uint t = 0; t < textureCount && index + 13 <= chunkEnd; t++)
            {
                var textureId = BitConverter.ToUInt32(data, index);
                var nameLen = data[index + 12];
                index += 13 + nameLen;

                if (textureId != 0)
                {
                    return textureId;
                }
            }
        }

        return null;
    }

    private static bool Matches(byte[] data, int offset, string tag)
    {
        for (var i = 0; i < 4; i++)
        {
            if (data[offset + i] != (byte)tag[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int TryReadCount(byte[] data, int chunkStart, int chunkEnd)
    {
        var countOffset = chunkStart + CommonHeadSize;
        return countOffset + 4 <= chunkEnd && countOffset + 4 <= data.Length
            ? (int)BitConverter.ToUInt32(data, countOffset)
            : 0;
    }

    private static void ParseVert(
        byte[] data, int chunkStart, int chunkEnd, ushort major, ushort minor, List<S3DVertexBlock> output)
    {
        var index = chunkStart + CommonHeadSize;
        if (index + 4 > chunkEnd)
        {
            return;
        }

        var blockCount = BitConverter.ToUInt32(data, index);
        index += 4;

        for (uint b = 0; b < blockCount && index + 8 <= chunkEnd; b++)
        {
            var block = new S3DVertexBlock();

            // flag (WORD, unused) at index, vertex count (WORD) at index+2.
            var vertexCount = BitConverter.ToUInt16(data, index + 2);
            uint format;

            if (major >= 1 && minor >= 4)
            {
                format = BitConverter.ToUInt32(data, index + 4);
            }
            else
            {
                var type = BitConverter.ToUInt16(data, index + 4);
                format = type switch
                {
                    1 => FormatC4Ub,
                    2 => FormatT2F,
                    3 => Format2T2F,
                    10 => FormatC4UbT2F,
                    11 => FormatC4Ub2T2F,
                    _ => 0,
                };
            }

            index += 8;

            for (var v = 0; v < vertexCount && index + 12 <= chunkEnd; v++)
            {
                var x = BitConverter.ToSingle(data, index);
                var y = BitConverter.ToSingle(data, index + 4);
                var z = BitConverter.ToSingle(data, index + 8);
                block.Positions.Add(new Vector3(x, y, z));

                // UV0 sits right after the position, skipping over the color bytes (4)
                // when the format includes one - matches the byte layout used below to
                // advance `index` past the whole vertex.
                var uvOffset = format is FormatC4UbT2F or FormatC4Ub2T2F ? index + 16 : index + 12;
                var hasUv = format is FormatT2F or Format2T2F or FormatC4UbT2F or FormatC4Ub2T2F;
                if (hasUv && uvOffset + 8 <= chunkEnd)
                {
                    var u = BitConverter.ToSingle(data, uvOffset);
                    var uvV = BitConverter.ToSingle(data, uvOffset + 4);
                    block.Uvs.Add(new Vector2(u, uvV));
                }

                index += 12 + format switch
                {
                    FormatC4Ub => 4,
                    FormatT2F => 8,
                    Format2T2F => 16,
                    FormatC4UbT2F => 12,
                    FormatC4Ub2T2F => 20,
                    _ => 0,
                };
            }

            output.Add(block);
        }
    }

    private static void ParseIndx(byte[] data, int chunkStart, int chunkEnd, List<S3DIndexBlock> output)
    {
        var index = chunkStart + CommonHeadSize;
        if (index + 4 > chunkEnd)
        {
            return;
        }

        var blockCount = BitConverter.ToUInt32(data, index);
        index += 4;

        for (uint b = 0; b < blockCount && index + 6 <= chunkEnd; b++)
        {
            var block = new S3DIndexBlock();

            // flag (WORD) + stride (WORD) at index/index+2, index count (WORD) at index+4.
            var indexCount = BitConverter.ToUInt16(data, index + 4);
            index += 6;

            for (var i = 0; i < indexCount && index + 2 <= chunkEnd; i++)
            {
                block.Indices.Add(BitConverter.ToUInt16(data, index));
                index += 2;
            }

            output.Add(block);
        }
    }

    private static void ParsePrim(byte[] data, int chunkStart, int chunkEnd, List<S3DPrimBlock> output)
    {
        var index = chunkStart + CommonHeadSize;
        if (index + 4 > chunkEnd)
        {
            return;
        }

        var blockCount = BitConverter.ToUInt32(data, index);
        index += 4;

        for (uint b = 0; b < blockCount && index + 2 <= chunkEnd; b++)
        {
            var block = new S3DPrimBlock();

            var primCount = BitConverter.ToUInt16(data, index);
            index += 2;

            for (var p = 0; p < primCount && index + 12 <= chunkEnd; p++)
            {
                block.Primitives.Add(new S3DPrimitive
                {
                    Type = BitConverter.ToUInt32(data, index),
                    First = BitConverter.ToUInt32(data, index + 4),
                    Count = BitConverter.ToUInt32(data, index + 8),
                });
                index += 12;
            }

            output.Add(block);
        }
    }
}
