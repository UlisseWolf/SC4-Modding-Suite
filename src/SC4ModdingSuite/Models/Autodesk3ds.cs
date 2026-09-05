using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Minimal reader/writer for the Autodesk .3DS chunk format's static-mesh subset
/// (vertices, triangle faces, UV mapping - no materials/smoothing groups/animation).
///
/// <para>
/// <b>Not a port</b>: Ilive Reader's own 3DS import/export (<c>_s3d::Import3DS</c> /
/// <c>_s3d::ExportAs3DS</c>, <c>s3d/s3d_main.cpp</c>) is built on a licensed third-party
/// "3D Studio File Toolkit" (<c>3dsftk.h</c>) that isn't available to port from. This is a
/// self-contained reimplementation of the well-documented, open .3DS chunk IDs
/// (0x4D4D/0x3D3D/0x4000/0x4100/0x4110/0x4120/0x4140) instead - readable/writable by
/// Blender, 3ds Max, and any other standard 3DS-aware tool, which is the actual point of
/// this feature (get S3D geometry into a real modeling tool and back).
/// </para>
/// </summary>
public static class Autodesk3ds
{
    private const ushort MainChunk = 0x4D4D;
    private const ushort Edit3DChunk = 0x3D3D;
    private const ushort ObjectChunk = 0x4000;
    private const ushort TriMeshChunk = 0x4100;
    private const ushort VertexListChunk = 0x4110;
    private const ushort FaceListChunk = 0x4120;
    private const ushort MappingCoordsChunk = 0x4140;

    public sealed class Mesh3ds
    {
        public string Name { get; set; } = string.Empty;
        public List<Vector3> Vertices { get; } = new();
        public List<Vector2> Uvs { get; } = new();
        public List<(ushort A, ushort B, ushort C)> Faces { get; } = new();
    }

    /// <summary>Writes every group of <paramref name="model"/> as one named 3DS mesh object each ("Group0", "Group1", ...).</summary>
    public static void Export(S3DModel model, string path)
    {
        var groupCount = Math.Min(model.VertexBlocks.Count, Math.Min(model.IndexBlocks.Count, model.PrimBlocks.Count));
        var meshChunks = new List<byte[]>();

        for (var g = 0; g < groupCount; g++)
        {
            if (BuildGroupObjectChunk(model, g) is { } chunk)
            {
                meshChunks.Add(chunk);
            }
        }

        WriteMainChunk(meshChunks, path);
    }

    /// <summary>
    /// "Export as 3DS (Group)" - writes only <paramref name="group"/> (the S3D Editor's
    /// current "editing group", S3DEditGroupIndex) as a single-object 3DS file, instead of
    /// the whole model - Ilive Reader's own separate group-only export command
    /// (<c>_s3d::ExportAs3DS</c> called with <c>GetSelectedGroup()</c> rather than -1/"all").
    /// </summary>
    /// <exception cref="InvalidOperationException">The group is empty or out of range - nothing to export.</exception>
    public static void ExportGroup(S3DModel model, int group, string path)
    {
        if (BuildGroupObjectChunk(model, group) is not { } chunk)
        {
            throw new InvalidOperationException($"Group {group} has no geometry to export.");
        }

        WriteMainChunk(new List<byte[]> { chunk }, path);
    }

    private static void WriteMainChunk(List<byte[]> meshChunks, string path)
    {
        var edit3D = WrapChunk(Edit3DChunk, Concat(meshChunks));
        var main = WrapChunk(MainChunk, edit3D);
        File.WriteAllBytes(path, main);
    }

    /// <summary>Builds one group's 3DS object chunk, or null if the group is out of range / has no vertices / has no triangles (nothing meaningful to export).</summary>
    private static byte[]? BuildGroupObjectChunk(S3DModel model, int g)
    {
        if (g < 0 || g >= model.VertexBlocks.Count)
        {
            return null;
        }

        var vertBlock = model.VertexBlocks[g];
        if (vertBlock.Positions.Count == 0)
        {
            return null;
        }

        var faces = new List<(ushort, ushort, ushort)>();
        foreach (var (group, a, b, c) in model.EnumerateTriangles())
        {
            if (group == g)
            {
                faces.Add(((ushort)a, (ushort)b, (ushort)c));
            }
        }

        return faces.Count == 0
            ? null
            : BuildObjectChunk($"Group{g}", vertBlock.Positions, vertBlock.HasUvs ? vertBlock.Uvs : null, faces);
    }

    private static byte[] BuildObjectChunk(string name, List<Vector3> vertices, List<Vector2>? uvs, List<(ushort A, ushort B, ushort C)> faces)
    {
        using var vertPayload = new MemoryStream();
        WriteUInt16(vertPayload, (ushort)vertices.Count);
        foreach (var v in vertices)
        {
            WriteSingle(vertPayload, v.X);
            WriteSingle(vertPayload, v.Y);
            WriteSingle(vertPayload, v.Z);
        }

        var vertChunk = WrapChunk(VertexListChunk, vertPayload.ToArray());

        using var facePayload = new MemoryStream();
        WriteUInt16(facePayload, (ushort)faces.Count);
        foreach (var (a, b, c) in faces)
        {
            WriteUInt16(facePayload, a);
            WriteUInt16(facePayload, b);
            WriteUInt16(facePayload, c);
            WriteUInt16(facePayload, 0); // face flags
        }

        var faceChunk = WrapChunk(FaceListChunk, facePayload.ToArray());

        byte[] mapChunk = Array.Empty<byte>();
        if (uvs is not null && uvs.Count == vertices.Count)
        {
            using var mapPayload = new MemoryStream();
            WriteUInt16(mapPayload, (ushort)uvs.Count);
            foreach (var uv in uvs)
            {
                WriteSingle(mapPayload, uv.X);
                WriteSingle(mapPayload, -uv.Y); // 3DS V axis is flipped relative to S3D's, matching Ilive Reader's own Import3DS/ExportAs3DS convention
            }

            mapChunk = WrapChunk(MappingCoordsChunk, mapPayload.ToArray());
        }

        var triMesh = WrapChunk(TriMeshChunk, Concat(new[] { vertChunk, faceChunk, mapChunk }));

        using var objectPayload = new MemoryStream();
        var nameBytes = Encoding.ASCII.GetBytes(name);
        objectPayload.Write(nameBytes);
        objectPayload.WriteByte(0);
        objectPayload.Write(triMesh);

        return WrapChunk(ObjectChunk, objectPayload.ToArray());
    }

    /// <summary>
    /// Replaces <paramref name="model"/>'s geometry (VERT/INDX/PRIM/ANIM) with one group per
    /// imported mesh object - same behavior as Ilive Reader's own <c>_s3d::Import3DS</c>
    /// (which also wholesale-replaces rather than appends: it clears vert/indx/prim/anim
    /// first, and bumps the header to v1.5). MATS/PROP/REGP are left untouched, matching the
    /// original as well.
    /// </summary>
    public static void ApplyToModel(IReadOnlyList<Mesh3ds> meshes, S3DModel model)
    {
        model.VertexBlocks.Clear();
        model.IndexBlocks.Clear();
        model.PrimBlocks.Clear();
        model.Animation.Meshes.Clear();

        foreach (var mesh in meshes)
        {
            if (mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
            {
                continue;
            }

            var hasUvs = mesh.Uvs.Count == mesh.Vertices.Count;
            var vertBlock = new S3DVertexBlock { Format = S3DParser.FormatT2F };
            vertBlock.Positions.AddRange(mesh.Vertices);
            if (hasUvs)
            {
                vertBlock.Uvs.AddRange(mesh.Uvs);
            }

            var indexBlock = new S3DIndexBlock();
            foreach (var (a, b, c) in mesh.Faces)
            {
                indexBlock.Indices.Add(a);
                indexBlock.Indices.Add(b);
                indexBlock.Indices.Add(c);
            }

            var primBlock = new S3DPrimBlock();
            primBlock.Primitives.Add(new S3DPrimitive { Type = 0, First = 0, Count = (uint)indexBlock.Indices.Count });

            var groupIndex = model.VertexBlocks.Count;
            model.VertexBlocks.Add(vertBlock);
            model.IndexBlocks.Add(indexBlock);
            model.PrimBlocks.Add(primBlock);

            var animMesh = new S3DAnimMesh { Name = mesh.Name };
            animMesh.Frames.Add(new S3DAnimFrame
            {
                VertBlock = (ushort)groupIndex,
                IndexBlock = (ushort)groupIndex,
                PrimBlock = (ushort)groupIndex,
                MaterialBlock = 0,
            });
            model.Animation.Meshes.Add(animMesh);
        }

        model.MajorRevision = 1;
        model.MinorRevision = 5;
    }

    /// <summary>Reads every named mesh object in <paramref name="path"/> as a separate <see cref="Mesh3ds"/>.</summary>
    public static List<Mesh3ds> Import(string path)
    {
        var data = File.ReadAllBytes(path);
        var meshes = new List<Mesh3ds>();

        var (id, _, payloadStart, payloadEnd) = ReadChunkHeader(data, 0);
        if (id != MainChunk)
        {
            throw new InvalidDataException("Not a .3ds file (missing MAIN_CHUNK 0x4D4D).");
        }

        WalkForMain(data, payloadStart, payloadEnd, meshes);
        return meshes;
    }

    private static void WalkForMain(byte[] data, int start, int end, List<Mesh3ds> meshes)
    {
        var pos = start;
        while (pos + 6 <= end)
        {
            var (id, size, childStart, childEnd) = ReadChunkHeader(data, pos);
            if (id == Edit3DChunk)
            {
                WalkEdit3D(data, childStart, childEnd, meshes);
            }

            pos += (int)size;
        }
    }

    private static void WalkEdit3D(byte[] data, int start, int end, List<Mesh3ds> meshes)
    {
        var pos = start;
        while (pos + 6 <= end)
        {
            var (id, size, childStart, childEnd) = ReadChunkHeader(data, pos);
            if (id == ObjectChunk)
            {
                ReadObjectChunk(data, childStart, childEnd, meshes);
            }

            pos += (int)size;
        }
    }

    private static void ReadObjectChunk(byte[] data, int start, int end, List<Mesh3ds> meshes)
    {
        // Object chunk payload starts with a null-terminated name, then sub-chunks.
        var nameEnd = start;
        while (nameEnd < end && data[nameEnd] != 0)
        {
            nameEnd++;
        }

        var name = Encoding.ASCII.GetString(data, start, nameEnd - start);
        var pos = nameEnd + 1;

        while (pos + 6 <= end)
        {
            var (id, size, childStart, childEnd) = ReadChunkHeader(data, pos);
            if (id == TriMeshChunk)
            {
                meshes.Add(ReadTriMeshChunk(data, name, childStart, childEnd));
            }

            pos += (int)size;
        }
    }

    private static Mesh3ds ReadTriMeshChunk(byte[] data, string name, int start, int end)
    {
        var mesh = new Mesh3ds { Name = name };
        var pos = start;

        while (pos + 6 <= end)
        {
            var (id, size, payloadStart, _) = ReadChunkHeader(data, pos);

            if (id == VertexListChunk)
            {
                var count = BitConverter.ToUInt16(data, payloadStart);
                var p = payloadStart + 2;
                for (var i = 0; i < count && p + 12 <= end; i++)
                {
                    mesh.Vertices.Add(new Vector3(
                        BitConverter.ToSingle(data, p),
                        BitConverter.ToSingle(data, p + 4),
                        BitConverter.ToSingle(data, p + 8)));
                    p += 12;
                }
            }
            else if (id == FaceListChunk)
            {
                var count = BitConverter.ToUInt16(data, payloadStart);
                var p = payloadStart + 2;
                for (var i = 0; i < count && p + 8 <= end; i++)
                {
                    mesh.Faces.Add((
                        BitConverter.ToUInt16(data, p),
                        BitConverter.ToUInt16(data, p + 2),
                        BitConverter.ToUInt16(data, p + 4)));
                    p += 8; // 3 indices + face flags
                }
            }
            else if (id == MappingCoordsChunk)
            {
                var count = BitConverter.ToUInt16(data, payloadStart);
                var p = payloadStart + 2;
                for (var i = 0; i < count && p + 8 <= end; i++)
                {
                    var u = BitConverter.ToSingle(data, p);
                    var v = BitConverter.ToSingle(data, p + 4);
                    mesh.Uvs.Add(new Vector2(u, -v)); // undo the V flip applied on export
                    p += 8;
                }
            }

            pos += (int)size;
        }

        return mesh;
    }

    /// <summary>Reads a chunk header (2-byte ID + 4-byte total length, length includes the 6-byte header itself) at <paramref name="offset"/>.</summary>
    private static (ushort Id, uint Size, int PayloadStart, int PayloadEnd) ReadChunkHeader(byte[] data, int offset)
    {
        var id = BitConverter.ToUInt16(data, offset);
        var size = BitConverter.ToUInt32(data, offset + 2);
        var payloadStart = offset + 6;
        var payloadEnd = offset + (int)Math.Max(size, 6);
        return (id, Math.Max(size, 6), payloadStart, payloadEnd);
    }

    private static byte[] WrapChunk(ushort id, byte[] payload)
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, id);
        WriteUInt32(stream, (uint)(payload.Length + 6));
        stream.Write(payload);
        return stream.ToArray();
    }

    private static byte[] Concat(IReadOnlyList<byte[]> chunks)
    {
        var total = 0;
        foreach (var c in chunks)
        {
            total += c.Length;
        }

        var result = new byte[total];
        var offset = 0;
        foreach (var c in chunks)
        {
            Buffer.BlockCopy(c, 0, result, offset, c.Length);
            offset += c.Length;
        }

        return result;
    }

    private static void WriteUInt16(Stream stream, ushort value) => stream.Write(BitConverter.GetBytes(value));
    private static void WriteUInt32(Stream stream, uint value) => stream.Write(BitConverter.GetBytes(value));
    private static void WriteSingle(Stream stream, float value) => stream.Write(BitConverter.GetBytes(value));
}
