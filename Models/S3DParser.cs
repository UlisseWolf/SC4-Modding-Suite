using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>One VERT block: vertex positions (and, when the format includes them, UV0/UV1/color) sharing the same vertex format.</summary>
public sealed class S3DVertexBlock
{
    /// <summary>Unused on-disk flag word, kept only to round-trip byte-for-byte on re-encode.</summary>
    public ushort Flag { get; set; }

    /// <summary>V3F_* format constant (see <see cref="S3DParser"/>) - which optional fields (color, UV0, UV1) this block's vertices carry.</summary>
    public uint Format { get; set; } = S3DParser.FormatT2F;

    public List<Vector3> Positions { get; } = new();

    /// <summary>UV0 texture coordinates, index-aligned with <see cref="Positions"/>; empty if this block's vertex format has no UV data.</summary>
    public List<Vector2> Uvs { get; } = new();

    /// <summary>UV1 (second texture coordinate set), index-aligned with <see cref="Positions"/>; only present for the 2T2F/C4Ub2T2F formats.</summary>
    public List<Vector2> Uv1s { get; } = new();

    /// <summary>Per-vertex diffuse color (B,G,R,A, one byte per channel), index-aligned with <see cref="Positions"/>; only present for the C4Ub/C4UbT2F/C4Ub2T2F formats.</summary>
    public List<(byte B, byte G, byte R, byte A)> Colors { get; } = new();

    public bool HasUvs => Uvs.Count == Positions.Count && Uvs.Count > 0;
}

/// <summary>One INDX block: a group of 16-bit vertex indices, local to the matching VERT block.</summary>
public sealed class S3DIndexBlock
{
    /// <summary>Unused on-disk flag word, kept only to round-trip byte-for-byte on re-encode.</summary>
    public ushort Flag { get; set; }

    /// <summary>Always 2 (bytes per index) in every real S3D file - kept only to round-trip byte-for-byte on re-encode.</summary>
    public ushort Stride { get; set; } = 2;

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

/// <summary>One texture reference within a material (<see cref="S3DMaterial"/>).</summary>
public sealed class S3DMaterialTexture
{
    public uint TextureId { get; set; }
    public byte WrapModeS { get; set; }
    public byte WrapModeT { get; set; }
    public byte MagFilter { get; set; }
    public byte MinFilter { get; set; }
    public ushort AnimRate { get; set; }
    public ushort AnimMode { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>One material within the MATS chunk (render state flags + up to several texture references).</summary>
public sealed class S3DMaterial
{
    public uint Flag { get; set; }
    public byte AlphaFunc { get; set; }
    public byte DepthFunc { get; set; }
    public byte SrcBlendFactor { get; set; }
    public byte DstBlendFactor { get; set; }
    public ushort AlphaThreshold { get; set; }
    public uint MaterialClass { get; set; }
    public byte Reserved { get; set; }
    public List<S3DMaterialTexture> Textures { get; } = new();
}

/// <summary>One animation frame: which VERT/INDX/PRIM/material block index this mesh uses at this frame.</summary>
public sealed class S3DAnimFrame
{
    public ushort VertBlock { get; set; }
    public ushort IndexBlock { get; set; }
    public ushort PrimBlock { get; set; }
    public ushort MaterialBlock { get; set; }
}

/// <summary>One named mesh within the ANIM chunk - a sequence of frames (one per animation keyframe; a single frame for a static/non-animated mesh group).</summary>
public sealed class S3DAnimMesh
{
    public byte Flags { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<S3DAnimFrame> Frames { get; } = new();
}

/// <summary>The ANIM chunk: overall animation timing plus the per-mesh frame list that maps each named mesh to its VERT/INDX/PRIM/material blocks.</summary>
public sealed class S3DAnimation
{
    public ushort FrameCount { get; set; }
    public ushort FrameRate { get; set; }
    public ushort AnimMode { get; set; }
    public uint Flag { get; set; }
    public float Displacement { get; set; }
    public List<S3DAnimMesh> Meshes { get; } = new();
}

/// <summary>One entry in the PROP chunk: an arbitrary key/value string pair attached to a mesh/frame.</summary>
public sealed class S3DPropBlock
{
    public ushort MeshIndex { get; set; }
    public ushort FrameNumber { get; set; }
    public string KeyName { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
}

/// <summary>One transform (translation + quaternion orientation) within a REGP registration-point block.</summary>
public sealed class S3DRegPointTransform
{
    public Vector3 Translation { get; set; }
    public float[] Orientation { get; set; } = new float[4];
}

/// <summary>One named registration point in the REGP chunk (e.g. an attachment point), with one transform per animation frame.</summary>
public sealed class S3DRegPointBlock
{
    public string Name { get; set; } = string.Empty;
    public List<S3DRegPointTransform> Transforms { get; } = new();
}

/// <summary>
/// A parsed S3D model (SimCity 4's building/prop 3D mesh format). Captures every chunk
/// (HEAD/VERT/INDX/PRIM/MATS/ANIM/PROP/REGP) with full fidelity - not just enough for a
/// wireframe viewer - so a model can be edited (merged, texture data inspected) and then
/// re-encoded byte-for-byte via <see cref="S3DWriter"/> without losing information.
/// </summary>
public sealed class S3DModel
{
    public ushort MajorRevision { get; set; }
    public ushort MinorRevision { get; set; }
    public List<S3DVertexBlock> VertexBlocks { get; } = new();
    public List<S3DIndexBlock> IndexBlocks { get; } = new();
    public List<S3DPrimBlock> PrimBlocks { get; } = new();
    public List<S3DMaterial> Materials { get; } = new();
    public S3DAnimation Animation { get; } = new();
    public List<S3DPropBlock> Props { get; } = new();
    public List<S3DRegPointBlock> RegPoints { get; } = new();

    public int MaterialCount => Materials.Count;
    public bool HasAnimation => Animation.Meshes.Count > 0;

    /// <summary>
    /// The first texture reference found across all materials (a material's own
    /// <c>textureID</c>, per Ilive Reader's <c>_s3dmat::Decode</c>). By SC4 modding
    /// convention this ID is the Instance ID of an FSH texture entry sharing the model's
    /// own Group ID within the same package - used to resolve a texture bitmap for the
    /// "Solid" render mode. Null if the model has no materials/textures at all.
    /// </summary>
    public uint? PrimaryTextureId
    {
        get
        {
            var id = Materials.SelectMany(m => m.Textures).Select(t => t.TextureId).FirstOrDefault(id => id != 0);
            return id == 0 ? null : id;
        }
    }

    public int TotalVertexCount => VertexBlocks.Sum(b => b.Positions.Count);

    /// <summary>
    /// Resolves which entry in <see cref="Materials"/> mesh/group <paramref name="group"/>
    /// uses at animation keyframe <paramref name="frame"/> - "group" has the same meaning as
    /// the <c>Group</c> yielded by <see cref="EnumerateTriangles"/> (a VertBlock index for
    /// animated models with an ANIM chunk, a plain 0-based VERT/INDX/PRIM/MATS block index
    /// otherwise - the four block lists are parallel for static, non-animated models, the
    /// same convention <see cref="SC4ModdingSuite.ViewModels.S3DMaterialRowViewModel.GroupLabel"/> already
    /// assumes for the Material Editor's own "Group" column).
    ///
    /// Needed by the viewer to pick each group's own texture for the "Solid" render instead
    /// of resolving a single <see cref="PrimaryTextureId"/> and applying it to the whole
    /// model - multi-material models (most animated building/prop meshes; see MATS/ANIM)
    /// otherwise show every group's UVs sampled against a texture meant for a different
    /// group, producing a scrambled/misaligned result.
    /// </summary>
    public int? GetMaterialIndex(int frame, int group)
    {
        if (Animation.Meshes.Count > 0)
        {
            foreach (var mesh in Animation.Meshes)
            {
                if (mesh.Frames.Count == 0)
                {
                    continue;
                }

                var f = ((frame % mesh.Frames.Count) + mesh.Frames.Count) % mesh.Frames.Count;
                var frameRef = mesh.Frames[f];
                if (frameRef.VertBlock != group)
                {
                    continue;
                }

                return frameRef.MaterialBlock < Materials.Count ? frameRef.MaterialBlock : null;
            }

            return null;
        }

        return group < Materials.Count ? group : null;
    }

    /// <summary>
    /// Enumerates every triangle across all mesh groups as (group index, local vertex
    /// index A, B, C), expanding triangle strips/fans/quads into plain triangles. "Local"
    /// index is relative to that group's own VERT block, matching how INDX values are
    /// always scoped to their paired VERT block in the S3D format.
    /// </summary>
    /// <summary>
    /// <paramref name="frame"/> selects which animation keyframe to draw when this model has
    /// an ANIM chunk (see <see cref="Animation"/>) - each named mesh independently maps its
    /// own frame number to a VERT/INDX/PRIM block triple (wrapping if <paramref name="frame"/>
    /// exceeds that mesh's own frame count), matching how Ilive Reader's own renderer walks
    /// <c>anim.meshes[...].frames[iFrame]</c> (<c>GlViewS3D::DrawGLScene</c>) instead of just
    /// VERT/INDX/PRIM blocks by parallel position. Non-animated models (no ANIM chunk, or a
    /// single frame per mesh - the overwhelming majority of SC4 building/prop models) render
    /// identically regardless of <paramref name="frame"/>. <paramref name="hiddenGroups"/>
    /// skips entire mesh groups (indices into <see cref="Animation"/>.Meshes when present,
    /// otherwise into <see cref="VertexBlocks"/>/<see cref="IndexBlocks"/>/<see cref="PrimBlocks"/>
    /// directly) - the S3D Editor's per-group visibility toggles.
    /// </summary>
    public IEnumerable<(int Group, int A, int B, int C)> EnumerateTriangles(int frame = 0, IReadOnlySet<int>? hiddenGroups = null)
    {
        if (Animation.Meshes.Count > 0)
        {
            for (var meshIndex = 0; meshIndex < Animation.Meshes.Count; meshIndex++)
            {
                if (hiddenGroups?.Contains(meshIndex) == true)
                {
                    continue;
                }

                var mesh = Animation.Meshes[meshIndex];
                if (mesh.Frames.Count == 0)
                {
                    continue;
                }

                var f = ((frame % mesh.Frames.Count) + mesh.Frames.Count) % mesh.Frames.Count;
                var frameRef = mesh.Frames[f];

                if (frameRef.VertBlock >= VertexBlocks.Count || frameRef.IndexBlock >= IndexBlocks.Count || frameRef.PrimBlock >= PrimBlocks.Count)
                {
                    continue;
                }

                // The yielded "Group" is the actual VERT block index (not the mesh index) -
                // that's what the viewer needs to resolve local vertex indices against the
                // right flattened vertex block, since an animated mesh's VertBlock can differ
                // from its own position in the Meshes list.
                foreach (var t in ExpandPrimitives(frameRef.VertBlock, IndexBlocks[frameRef.IndexBlock].Indices, PrimBlocks[frameRef.PrimBlock].Primitives))
                {
                    yield return t;
                }
            }

            yield break;
        }

        var groupCount = Math.Min(IndexBlocks.Count, PrimBlocks.Count);
        for (var g = 0; g < groupCount; g++)
        {
            if (hiddenGroups?.Contains(g) == true)
            {
                continue;
            }

            foreach (var t in ExpandPrimitives(g, IndexBlocks[g].Indices, PrimBlocks[g].Primitives))
            {
                yield return t;
            }
        }
    }

    private static IEnumerable<(int Group, int A, int B, int C)> ExpandPrimitives(int group, List<ushort> indices, List<S3DPrimitive> primitives)
    {
        foreach (var prim in primitives)
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

                        yield return (group, indices[first + i], indices[first + i + 1], indices[first + i + 2]);
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
                            ? (group, indices[first + i], indices[first + i + 1], indices[first + i + 2])
                            : (group, indices[first + i + 1], indices[first + i], indices[first + i + 2]);
                    }

                    break;

                case 2: // triangle fan
                    for (var i = 1; i + 1 < count; i++)
                    {
                        if (first + i + 1 >= indices.Count)
                        {
                            break;
                        }

                        yield return (group, indices[first], indices[first + i], indices[first + i + 1]);
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
                        yield return (group, a, b, c);
                        yield return (group, a, c, d);
                    }

                    break;

                // Type 7 (quad strip) is rare in SC4 building models and intentionally
                // not expanded here, matching the "viewer only" scope requested.
            }
        }
    }
}

/// <summary>
/// Parses the S3D format (SimCity 4 building/prop 3D models). Ported byte-for-byte from
/// Ilive Reader's s3d module (<c>s3d/s3d_main.cpp</c>, <c>s3d/struct.h</c>): a flat
/// top-level container of 4-byte-tagged chunks (HEAD/VERT/INDX/PRIM/MATS/ANIM/PROP/REGP)
/// located by linear scan, each with its own binary sub-format.
///
/// <para>
/// Every chunk is now decoded (not just HEAD/VERT/INDX/PRIM) so a model can be re-encoded
/// via <see cref="S3DWriter"/> without losing materials/animation/props/registration
/// points - needed for real editing (merge, save) rather than a read-only viewer.
/// </para>
/// </summary>
public static class S3DParser
{
    // Every chunk's local buffer starts with its own 4-byte tag + 4-byte declared size;
    // COMMON_HEAD is Ilive Reader's name for that 8-byte prefix to skip past it.
    internal const int CommonHeadSize = 8;

    // Vertex format codes from struct.h / s3d_main.cpp (V3F_* constants).
    internal const uint FormatC4Ub = 0x80000101; // position + color
    internal const uint FormatT2F = 0x80004001; // position + 1 UV set
    internal const uint Format2T2F = 0x80008001; // position + 2 UV sets
    internal const uint FormatC4UbT2F = 0x80004101; // position + color + 1 UV set
    internal const uint FormatC4Ub2T2F = 0x80008101; // position + color + 2 UV sets

    public static S3DModel? Parse(byte[] data)
    {
        if (data.Length < 16)
        {
            return null;
        }

        // Faithful port of Ilive Reader's DecodeS3D(): a single linear scan overwriting
        // each chunk's start offset every time its tag is (re-)found, so the LAST
        // occurrence of each tag wins - exactly like the original.
        int iVert = 0, iIndx = 0, iPrim = 0, iMats = 0, iAnim = 0, iProp = 0, iRegp = 0;
        for (var i = 8; i <= data.Length - 4; i++)
        {
            if (Matches(data, i, "VERT")) iVert = i;
            else if (Matches(data, i, "INDX")) iIndx = i;
            else if (Matches(data, i, "PRIM")) iPrim = i;
            else if (Matches(data, i, "MATS")) iMats = i;
            else if (Matches(data, i, "ANIM")) iAnim = i;
            else if (Matches(data, i, "PROP")) iProp = i;
            else if (Matches(data, i, "REGP")) iRegp = i;
        }

        const int iHead = 8;

        ushort major = 0, minor = 0;
        if (iVert > 0 && iHead + CommonHeadSize + 4 <= data.Length)
        {
            major = BitConverter.ToUInt16(data, iHead + CommonHeadSize);
            minor = BitConverter.ToUInt16(data, iHead + CommonHeadSize + 2);
        }

        var model = new S3DModel { MajorRevision = major, MinorRevision = minor };

        // Each chunk's end is the next chunk's start (whichever comes next and is actually
        // present), or the end of the file for the very last chunk present - same cascading
        // "iX && iY" checks as DecodeS3D, just expressed as explicit end offsets.
        int VertEnd() => iIndx > iVert ? iIndx : (iPrim > iVert ? iPrim : (iMats > iVert ? iMats : (iAnim > iVert ? iAnim : (iProp > iVert ? iProp : (iRegp > iVert ? iRegp : data.Length)))));
        int IndxEnd() => iPrim > iIndx ? iPrim : (iMats > iIndx ? iMats : (iAnim > iIndx ? iAnim : (iProp > iIndx ? iProp : (iRegp > iIndx ? iRegp : data.Length))));
        int PrimEnd() => iMats > iPrim ? iMats : (iAnim > iPrim ? iAnim : (iProp > iPrim ? iProp : (iRegp > iPrim ? iRegp : data.Length)));
        int MatsEnd() => iAnim > iMats ? iAnim : (iProp > iMats ? iProp : (iRegp > iMats ? iRegp : data.Length));
        int AnimEnd() => iProp > iAnim ? iProp : (iRegp > iAnim ? iRegp : data.Length);
        int PropEnd() => iRegp > iProp ? iRegp : data.Length;

        if (iVert > 0 && iIndx > iVert)
        {
            ParseVert(data, iVert, VertEnd(), major, minor, model.VertexBlocks);
        }

        if (iIndx > 0 && iPrim > iIndx)
        {
            ParseIndx(data, iIndx, IndxEnd(), model.IndexBlocks);
        }

        if (iPrim > 0)
        {
            ParsePrim(data, iPrim, iMats > iPrim ? iMats : PrimEnd(), model.PrimBlocks);
        }

        if (iMats > 0)
        {
            ParseMats(data, iMats, iAnim > iMats ? iAnim : MatsEnd(), model.Materials);
        }

        if (iAnim > 0)
        {
            ParseAnim(data, iAnim, iProp > iAnim ? iProp : AnimEnd(), model.Animation);
        }

        if (iProp > 0)
        {
            ParseProp(data, iProp, iRegp > iProp ? iRegp : PropEnd(), model.Props);
        }

        if (iRegp > 0)
        {
            ParseRegp(data, iRegp, data.Length, model.RegPoints);
        }

        return model;
    }

    /// <summary>
    /// Locates every top-level chunk (HEAD/VERT/INDX/PRIM/MATS/ANIM/PROP/REGP) and returns
    /// each one's raw bytes (tag+size header included) - for the S3D Hex Editor (Ilive
    /// Reader's Tab3DMHex/Tab3DMHExCont: one hex-dump tab per chunk, via GetBufferS3D).
    /// Same tag scan as <see cref="Parse"/>, generalized into one reusable pass instead of
    /// duplicating it; a chunk's end is the closest following chunk's start (or EOF for
    /// whichever chunk is physically last) - equivalent to <see cref="Parse"/>'s own
    /// cascading End() checks for any well-formed (canonically-ordered) file, and more
    /// robust than that fixed-priority cascade for an out-of-order one.
    /// </summary>
    public static IReadOnlyList<(string Tag, byte[] Bytes)> LocateChunks(byte[] data)
    {
        var result = new List<(string, byte[])>();
        if (data.Length < 16)
        {
            return result;
        }

        const int iHead = 8;
        int iVert = 0, iIndx = 0, iPrim = 0, iMats = 0, iAnim = 0, iProp = 0, iRegp = 0;
        for (var i = 8; i <= data.Length - 4; i++)
        {
            if (Matches(data, i, "VERT")) iVert = i;
            else if (Matches(data, i, "INDX")) iIndx = i;
            else if (Matches(data, i, "PRIM")) iPrim = i;
            else if (Matches(data, i, "MATS")) iMats = i;
            else if (Matches(data, i, "ANIM")) iAnim = i;
            else if (Matches(data, i, "PROP")) iProp = i;
            else if (Matches(data, i, "REGP")) iRegp = i;
        }

        var starts = new[] { iVert, iIndx, iPrim, iMats, iAnim, iProp, iRegp };

        int EndAfter(int start)
        {
            var end = data.Length;
            foreach (var s in starts)
            {
                if (s > start && s < end)
                {
                    end = s;
                }
            }

            return end;
        }

        void Add(string tag, int start)
        {
            if (start <= 0 || start >= data.Length)
            {
                return;
            }

            var length = Math.Max(0, EndAfter(start) - start);
            result.Add((tag, data.AsSpan(start, length).ToArray()));
        }

        Add("HEAD", iHead);
        Add("VERT", iVert);
        Add("INDX", iIndx);
        Add("PRIM", iPrim);
        Add("MATS", iMats);
        Add("ANIM", iAnim);
        Add("PROP", iProp);
        Add("REGP", iRegp);

        return result;
    }

    private static bool Matches(byte[] data, int offset, string tag) =>
        data.AsSpan(offset, tag.Length).SequenceEqual(Encoding.ASCII.GetBytes(tag));

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

            var flag = BitConverter.ToUInt16(data, index);
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
                    _ => FormatT2F,
                };
            }

            block.Flag = flag;
            block.Format = format;
            index += 8;

            for (var v = 0; v < vertexCount && index + 12 <= chunkEnd; v++)
            {
                var x = BitConverter.ToSingle(data, index);
                var y = BitConverter.ToSingle(data, index + 4);
                var z = BitConverter.ToSingle(data, index + 8);
                block.Positions.Add(new Vector3(x, y, z));
                index += 12;

                // Color (four separate BYTES, one per channel - matching Ilive Reader's own
                // _s3dvert::Decode exactly: "vertex.b = *(BYTE*)(buffer+iIndex+12)" etc.,
                // 4 bytes total) comes first when the format includes one, then UV0, then
                // UV1. NOTE: Ilive Reader's own _s3dvert_vertex::Encode writes b/g/r/a as
                // 16-bit words (8 bytes total) instead - a latent bug/inconsistency in the
                // original app (its own Encode doesn't round-trip its own Decode) that was
                // mistakenly "fixed" here in an earlier pass by copying Encode's byte width
                // into Decode too, corrupting every subsequent field's alignment for any
                // colored-vertex-format model. Real SC4 files (built by the game's own
                // export tooling, not by Ilive Reader's Encode) match Decode's 4-byte
                // layout, which is what's implemented here again.
                var hasColor = format is FormatC4Ub or FormatC4UbT2F or FormatC4Ub2T2F;
                var hasUv0 = format is FormatT2F or Format2T2F or FormatC4UbT2F or FormatC4Ub2T2F;
                var hasUv1 = format is Format2T2F or FormatC4Ub2T2F;

                if (hasColor && index + 4 <= chunkEnd)
                {
                    block.Colors.Add((data[index], data[index + 1], data[index + 2], data[index + 3]));
                    index += 4;
                }

                if (hasUv0 && index + 8 <= chunkEnd)
                {
                    var u0 = BitConverter.ToSingle(data, index);
                    var v0 = BitConverter.ToSingle(data, index + 4);
                    block.Uvs.Add(new Vector2(u0, v0));
                    index += 8;
                }

                if (hasUv1 && index + 8 <= chunkEnd)
                {
                    var u1 = BitConverter.ToSingle(data, index);
                    var v1 = BitConverter.ToSingle(data, index + 4);
                    block.Uv1s.Add(new Vector2(u1, v1));
                    index += 8;
                }
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
            var block = new S3DIndexBlock
            {
                Flag = BitConverter.ToUInt16(data, index),
                Stride = BitConverter.ToUInt16(data, index + 2),
            };

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

    private static void ParseMats(byte[] data, int chunkStart, int chunkEnd, List<S3DMaterial> output)
    {
        var index = chunkStart + CommonHeadSize;
        if (index + 4 > chunkEnd)
        {
            return;
        }

        var materialCount = BitConverter.ToUInt32(data, index);
        index += 4;

        for (uint m = 0; m < materialCount && index + 16 <= chunkEnd; m++)
        {
            var mat = new S3DMaterial
            {
                Flag = BitConverter.ToUInt32(data, index),
                AlphaFunc = data[index + 4],
                DepthFunc = data[index + 5],
                SrcBlendFactor = data[index + 6],
                DstBlendFactor = data[index + 7],
                AlphaThreshold = BitConverter.ToUInt16(data, index + 8),
                MaterialClass = BitConverter.ToUInt32(data, index + 10),
                Reserved = data[index + 14],
            };
            var textureCount = data[index + 15];
            index += 16;

            for (var t = 0; t < textureCount && index + 13 <= chunkEnd; t++)
            {
                var textureId = BitConverter.ToUInt32(data, index);
                var wrapS = data[index + 4];
                var wrapT = data[index + 5];
                var magFilter = data[index + 6];
                var minFilter = data[index + 7];
                var animRate = BitConverter.ToUInt16(data, index + 8);
                var animMode = BitConverter.ToUInt16(data, index + 10);
                var nameLen = data[index + 12];
                var nameEnd = Math.Min(index + 13 + nameLen, chunkEnd);
                var name = nameEnd > index + 13 ? Encoding.ASCII.GetString(data, index + 13, nameEnd - (index + 13)).TrimEnd('\0') : string.Empty;
                index += 13 + nameLen;

                mat.Textures.Add(new S3DMaterialTexture
                {
                    TextureId = textureId,
                    WrapModeS = wrapS,
                    WrapModeT = wrapT,
                    MagFilter = magFilter,
                    MinFilter = minFilter,
                    AnimRate = animRate,
                    AnimMode = animMode,
                    Name = name,
                });
            }

            output.Add(mat);
        }
    }

    private static void ParseAnim(byte[] data, int chunkStart, int chunkEnd, S3DAnimation output)
    {
        var headerStart = chunkStart + CommonHeadSize;
        if (headerStart + 16 > chunkEnd)
        {
            return;
        }

        output.FrameCount = BitConverter.ToUInt16(data, headerStart);
        output.FrameRate = BitConverter.ToUInt16(data, headerStart + 2);
        output.AnimMode = BitConverter.ToUInt16(data, headerStart + 4);
        output.Flag = BitConverter.ToUInt32(data, headerStart + 6);
        output.Displacement = BitConverter.ToSingle(data, headerStart + 10);
        var meshCount = BitConverter.ToUInt16(data, headerStart + 14);

        var index = headerStart + 16;
        for (var m = 0; m < meshCount && index + 2 <= chunkEnd; m++)
        {
            var nameLen = data[index];
            var flags = data[index + 1];
            var nameEnd = Math.Min(index + 2 + nameLen, chunkEnd);
            var name = nameEnd > index + 2 ? Encoding.ASCII.GetString(data, index + 2, nameEnd - (index + 2)).TrimEnd('\0') : string.Empty;
            index += 2 + nameLen;

            var mesh = new S3DAnimMesh { Flags = flags, Name = name };

            for (var f = 0; f < output.FrameCount && index + 8 <= chunkEnd; f++)
            {
                mesh.Frames.Add(new S3DAnimFrame
                {
                    VertBlock = BitConverter.ToUInt16(data, index),
                    IndexBlock = BitConverter.ToUInt16(data, index + 2),
                    PrimBlock = BitConverter.ToUInt16(data, index + 4),
                    MaterialBlock = BitConverter.ToUInt16(data, index + 6),
                });
                index += 8;
            }

            output.Meshes.Add(mesh);
        }
    }

    private static void ParseProp(byte[] data, int chunkStart, int chunkEnd, List<S3DPropBlock> output)
    {
        var index = chunkStart + CommonHeadSize;
        if (index + 4 > chunkEnd)
        {
            return;
        }

        var count = BitConverter.ToUInt32(data, index);
        index += 4;

        for (uint b = 0; b < count && index + 6 <= chunkEnd; b++)
        {
            var meshIndex = BitConverter.ToUInt16(data, index);
            var frameNumber = BitConverter.ToUInt16(data, index + 2);
            var keyLen = data[index + 4];
            var keyEnd = Math.Min(index + 5 + keyLen, chunkEnd);
            var keyName = keyEnd > index + 5 ? Encoding.ASCII.GetString(data, index + 5, keyEnd - (index + 5)).TrimEnd('\0') : string.Empty;

            var valueLenPos = index + 5 + keyLen;
            if (valueLenPos >= chunkEnd)
            {
                break;
            }

            var valueLen = data[valueLenPos];
            var valueEnd = Math.Min(valueLenPos + 1 + valueLen, chunkEnd);
            var valueName = valueEnd > valueLenPos + 1 ? Encoding.ASCII.GetString(data, valueLenPos + 1, valueEnd - (valueLenPos + 1)).TrimEnd('\0') : string.Empty;

            output.Add(new S3DPropBlock
            {
                MeshIndex = meshIndex,
                FrameNumber = frameNumber,
                KeyName = keyName,
                ValueName = valueName,
            });

            index = valueLenPos + 1 + valueLen;
        }
    }

    private static void ParseRegp(byte[] data, int chunkStart, int chunkEnd, List<S3DRegPointBlock> output)
    {
        var index = chunkStart + CommonHeadSize;
        if (index + 4 > chunkEnd)
        {
            return;
        }

        var count = BitConverter.ToUInt32(data, index);
        index += 4;

        for (uint b = 0; b < count && index + 3 <= chunkEnd; b++)
        {
            var nameLen = data[index];
            var nameEnd = Math.Min(index + 1 + nameLen, chunkEnd);
            var name = nameEnd > index + 1 ? Encoding.ASCII.GetString(data, index + 1, nameEnd - (index + 1)).TrimEnd('\0') : string.Empty;
            var transformCount = BitConverter.ToUInt16(data, index + 1 + nameLen);
            index += 3 + nameLen;

            var block = new S3DRegPointBlock { Name = name };

            for (var t = 0; t < transformCount && index + 28 <= chunkEnd; t++)
            {
                block.Transforms.Add(new S3DRegPointTransform
                {
                    Translation = new Vector3(
                        BitConverter.ToSingle(data, index),
                        BitConverter.ToSingle(data, index + 4),
                        BitConverter.ToSingle(data, index + 8)),
                    Orientation = new[]
                    {
                        BitConverter.ToSingle(data, index + 12),
                        BitConverter.ToSingle(data, index + 16),
                        BitConverter.ToSingle(data, index + 20),
                        BitConverter.ToSingle(data, index + 24),
                    },
                });
                index += 28;
            }

            output.Add(block);
        }
    }
}
