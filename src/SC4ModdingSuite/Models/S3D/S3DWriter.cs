using System;
using System.IO;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Encodes an <see cref="S3DModel"/> back to raw S3D bytes, and merges two models together -
/// direct ports of Ilive Reader's <c>_s3d::EncodeS3D()</c> and <c>_s3d::MergeS3D()</c>
/// (<c>s3d/s3d_main.cpp</c>). This is what makes the S3D Editor an actual editor instead of
/// a read-only viewer: <see cref="Encode"/> is used by "Apply/Save" (write the edited model
/// back into its package entry) and after <see cref="Merge"/> (combine two models' geometry).
///
/// <para>
/// Every chunk's "size" field is the total byte length of that chunk including its own
/// 4-byte tag and 4-byte size field - written as a placeholder, then back-patched once the
/// chunk's actual content has been written, exactly like the original's
/// "SeekToEnd/Write(size)/Seek(back)/Write(actual size)/SeekToEnd" pattern.
/// </para>
/// </summary>
public static class S3DWriter
{
    /// <summary>Encodes <paramref name="model"/> to a complete S3D byte buffer ("3DMD" wrapper + HEAD/VERT/INDX/PRIM/MATS/ANIM/PROP/REGP chunks).</summary>
    public static byte[] Encode(S3DModel model)
    {
        using var stream = new MemoryStream();

        stream.Write("3DMD"u8);
        WriteUInt32(stream, 0); // total size placeholder, patched below

        WriteHead(stream, model);
        WriteVert(stream, model);
        WriteIndx(stream, model);
        WritePrim(stream, model);
        WriteMats(stream, model);
        WriteAnim(stream, model.Animation);
        WriteProp(stream, model);
        WriteRegp(stream, model);

        var bytes = stream.ToArray();
        var totalSize = BitConverter.GetBytes(bytes.Length);
        Array.Copy(totalSize, 0, bytes, 4, 4);
        return bytes;
    }

    /// <summary>
    /// Appends every VERT/INDX/PRIM/material/animation-mesh block from <paramref name="source"/>
    /// onto <paramref name="target"/> (mutated in place), offsetting the appended animation
    /// frames' block indices so they still point at the right (now-shifted) blocks - same
    /// approach as <c>_s3d::MergeS3D</c>. PROP/REGP are intentionally left untouched (the
    /// original doesn't merge them either - they reference mesh/frame indices by position,
    /// which merging would need to remap with no clear "correct" answer for arbitrary files).
    /// </summary>
    public static void Merge(S3DModel target, S3DModel source)
    {
        var vertOffset = target.VertexBlocks.Count;
        var indxOffset = target.IndexBlocks.Count;
        var primOffset = target.PrimBlocks.Count;
        var matsOffset = target.Materials.Count;

        foreach (var mesh in source.Animation.Meshes)
        {
            var copy = new S3DAnimMesh { Flags = mesh.Flags, Name = mesh.Name };
            foreach (var frame in mesh.Frames)
            {
                copy.Frames.Add(new S3DAnimFrame
                {
                    VertBlock = (ushort)(frame.VertBlock + vertOffset),
                    IndexBlock = (ushort)(frame.IndexBlock + indxOffset),
                    PrimBlock = (ushort)(frame.PrimBlock + primOffset),
                    MaterialBlock = (ushort)(frame.MaterialBlock + matsOffset),
                });
            }

            target.Animation.Meshes.Add(copy);
        }

        target.Materials.AddRange(source.Materials);
        target.PrimBlocks.AddRange(source.PrimBlocks);
        target.IndexBlocks.AddRange(source.IndexBlocks);
        target.VertexBlocks.AddRange(source.VertexBlocks);
    }

    private static void WriteHead(MemoryStream stream, S3DModel model)
    {
        stream.Write("HEAD"u8);
        WriteUInt32(stream, 12);
        WriteUInt16(stream, model.MajorRevision);
        WriteUInt16(stream, model.MinorRevision);
    }

    private static void WriteVert(MemoryStream stream, S3DModel model)
    {
        stream.Write("VERT"u8);
        var sizePos = stream.Position;
        WriteUInt32(stream, 0);
        WriteUInt32(stream, (uint)model.VertexBlocks.Count);

        foreach (var block in model.VertexBlocks)
        {
            WriteUInt16(stream, block.Flag);
            WriteUInt16(stream, (ushort)block.Positions.Count);

            // v1.4+ always used (matches Import3DS/new content, which always sets
            // majorRevision=1/minorRevision>=4) - the legacy pre-1.4 8-bit "type" field is
            // read (S3DParser) but never written back, since every real SC4 file already
            // in this project's supported range is >= 1.4.
            WriteUInt32(stream, block.Format);

            var hasColor = block.Format is S3DParser.FormatC4Ub or S3DParser.FormatC4UbT2F or S3DParser.FormatC4Ub2T2F;
            var hasUv0 = block.Format is S3DParser.FormatT2F or S3DParser.Format2T2F or S3DParser.FormatC4UbT2F or S3DParser.FormatC4Ub2T2F;
            var hasUv1 = block.Format is S3DParser.Format2T2F or S3DParser.FormatC4Ub2T2F;

            for (var v = 0; v < block.Positions.Count; v++)
            {
                var p = block.Positions[v];
                WriteSingle(stream, p.X);
                WriteSingle(stream, p.Y);
                WriteSingle(stream, p.Z);

                if (hasColor && v < block.Colors.Count)
                {
                    // One byte per channel - matches S3DParser's ParseVert (and the real
                    // on-disk format/Ilive Reader's own Decode), NOT Ilive Reader's own
                    // Encode (which writes 16-bit words here - a bug in the original app
                    // that would corrupt every subsequent field's alignment if copied).
                    var (b, g, r, a) = block.Colors[v];
                    stream.WriteByte(b);
                    stream.WriteByte(g);
                    stream.WriteByte(r);
                    stream.WriteByte(a);
                }

                if (hasUv0 && v < block.Uvs.Count)
                {
                    WriteSingle(stream, block.Uvs[v].X);
                    WriteSingle(stream, block.Uvs[v].Y);
                }

                if (hasUv1 && v < block.Uv1s.Count)
                {
                    WriteSingle(stream, block.Uv1s[v].X);
                    WriteSingle(stream, block.Uv1s[v].Y);
                }
            }
        }

        PatchChunkSize(stream, sizePos);
    }

    private static void WriteIndx(MemoryStream stream, S3DModel model)
    {
        stream.Write("INDX"u8);
        var sizePos = stream.Position;
        WriteUInt32(stream, 0);
        WriteUInt32(stream, (uint)model.IndexBlocks.Count);

        foreach (var block in model.IndexBlocks)
        {
            WriteUInt16(stream, block.Flag);
            WriteUInt16(stream, block.Stride);
            WriteUInt16(stream, (ushort)block.Indices.Count);

            foreach (var i in block.Indices)
            {
                WriteUInt16(stream, i);
            }
        }

        PatchChunkSize(stream, sizePos);
    }

    private static void WritePrim(MemoryStream stream, S3DModel model)
    {
        stream.Write("PRIM"u8);
        var sizePos = stream.Position;
        WriteUInt32(stream, 0);
        WriteUInt32(stream, (uint)model.PrimBlocks.Count);

        foreach (var block in model.PrimBlocks)
        {
            WriteUInt16(stream, (ushort)block.Primitives.Count);

            foreach (var prim in block.Primitives)
            {
                WriteUInt32(stream, prim.Type);
                WriteUInt32(stream, prim.First);
                WriteUInt32(stream, prim.Count);
            }
        }

        PatchChunkSize(stream, sizePos);
    }

    private static void WriteMats(MemoryStream stream, S3DModel model)
    {
        stream.Write("MATS"u8);
        var sizePos = stream.Position;
        WriteUInt32(stream, 0);
        WriteUInt32(stream, (uint)model.Materials.Count);

        foreach (var mat in model.Materials)
        {
            WriteUInt32(stream, mat.Flag);
            stream.WriteByte(mat.AlphaFunc);
            stream.WriteByte(mat.DepthFunc);
            stream.WriteByte(mat.SrcBlendFactor);
            stream.WriteByte(mat.DstBlendFactor);
            WriteUInt16(stream, mat.AlphaThreshold);
            WriteUInt32(stream, mat.MaterialClass);
            stream.WriteByte(mat.Reserved);
            stream.WriteByte((byte)mat.Textures.Count);

            foreach (var tex in mat.Textures)
            {
                WriteUInt32(stream, tex.TextureId);
                stream.WriteByte(tex.WrapModeS);
                stream.WriteByte(tex.WrapModeT);
                stream.WriteByte(tex.MagFilter);
                stream.WriteByte(tex.MinFilter);
                WriteUInt16(stream, tex.AnimRate);
                WriteUInt16(stream, tex.AnimMode);
                var nameBytes = System.Text.Encoding.ASCII.GetBytes(tex.Name ?? string.Empty);
                stream.WriteByte((byte)nameBytes.Length);
                stream.Write(nameBytes);
            }
        }

        PatchChunkSize(stream, sizePos);
    }

    private static void WriteAnim(MemoryStream stream, S3DAnimation anim)
    {
        stream.Write("ANIM"u8);
        var sizePos = stream.Position;
        WriteUInt32(stream, 0);
        WriteUInt16(stream, anim.FrameCount);
        WriteUInt16(stream, anim.FrameRate);
        WriteUInt16(stream, anim.AnimMode);
        WriteUInt32(stream, anim.Flag);
        WriteSingle(stream, anim.Displacement);
        WriteUInt16(stream, (ushort)anim.Meshes.Count);

        foreach (var mesh in anim.Meshes)
        {
            var nameBytes = System.Text.Encoding.ASCII.GetBytes(mesh.Name ?? string.Empty);
            stream.WriteByte((byte)nameBytes.Length);
            stream.WriteByte(mesh.Flags);
            stream.Write(nameBytes);

            foreach (var frame in mesh.Frames)
            {
                WriteUInt16(stream, frame.VertBlock);
                WriteUInt16(stream, frame.IndexBlock);
                WriteUInt16(stream, frame.PrimBlock);
                WriteUInt16(stream, frame.MaterialBlock);
            }
        }

        PatchChunkSize(stream, sizePos);
    }

    private static void WriteProp(MemoryStream stream, S3DModel model)
    {
        stream.Write("PROP"u8);
        var sizePos = stream.Position;
        WriteUInt32(stream, 0);
        WriteUInt32(stream, (uint)model.Props.Count);

        foreach (var prop in model.Props)
        {
            WriteUInt16(stream, prop.MeshIndex);
            WriteUInt16(stream, prop.FrameNumber);

            var keyBytes = System.Text.Encoding.ASCII.GetBytes(prop.KeyName ?? string.Empty);
            stream.WriteByte((byte)keyBytes.Length);
            stream.Write(keyBytes);

            var valueBytes = System.Text.Encoding.ASCII.GetBytes(prop.ValueName ?? string.Empty);
            stream.WriteByte((byte)valueBytes.Length);
            stream.Write(valueBytes);
        }

        PatchChunkSize(stream, sizePos);
    }

    private static void WriteRegp(MemoryStream stream, S3DModel model)
    {
        stream.Write("REGP"u8);
        var sizePos = stream.Position;
        WriteUInt32(stream, 0);
        WriteUInt32(stream, (uint)model.RegPoints.Count);

        foreach (var block in model.RegPoints)
        {
            var nameBytes = System.Text.Encoding.ASCII.GetBytes(block.Name ?? string.Empty);
            stream.WriteByte((byte)nameBytes.Length);
            stream.Write(nameBytes);
            WriteUInt16(stream, (ushort)block.Transforms.Count);

            foreach (var t in block.Transforms)
            {
                WriteSingle(stream, t.Translation.X);
                WriteSingle(stream, t.Translation.Y);
                WriteSingle(stream, t.Translation.Z);
                WriteSingle(stream, t.Orientation.Length > 0 ? t.Orientation[0] : 0f);
                WriteSingle(stream, t.Orientation.Length > 1 ? t.Orientation[1] : 0f);
                WriteSingle(stream, t.Orientation.Length > 2 ? t.Orientation[2] : 0f);
                WriteSingle(stream, t.Orientation.Length > 3 ? t.Orientation[3] : 0f);
            }
        }

        PatchChunkSize(stream, sizePos);
    }

    /// <summary>Back-patches a chunk's 4-byte size field (at <paramref name="sizePos"/>) with the actual byte length written since the chunk's own tag started (4 bytes before <paramref name="sizePos"/>).</summary>
    private static void PatchChunkSize(MemoryStream stream, long sizePos)
    {
        var chunkSize = (uint)(stream.Position - (sizePos - 4));
        var endPos = stream.Position;
        stream.Position = sizePos;
        WriteUInt32(stream, chunkSize);
        stream.Position = endPos;
    }

    private static void WriteUInt32(MemoryStream stream, uint value) => stream.Write(BitConverter.GetBytes(value));
    private static void WriteUInt16(MemoryStream stream, ushort value) => stream.Write(BitConverter.GetBytes(value));
    private static void WriteSingle(MemoryStream stream, float value) => stream.Write(BitConverter.GetBytes(value));
}
