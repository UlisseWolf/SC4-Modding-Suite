using System.Numerics;

namespace SC4ModdingSuite.Models;

/// <summary>
/// RHD -> LHD conversion for the S3D models of a 20-view set (5 zoom levels x 4 rotations,
/// as exported by BAT). Derived from comparing a real RHD/LHD pair of the same model: the
/// LHD version is the RHD one moved by a single rigid horizontal offset (identical Instance
/// IDs, vertex order, indices, UVs), with only the name text changing from "RHD" to "LHD".
/// Because every view is rendered from a different side, the same world-space offset shows
/// up rotated in each rotation slot - the Instance ID's second hex nibble (0x?0, 0x?10,
/// 0x?20, 0x?30) gives the slot, and is identical at every zoom level.
///
/// <para>
/// The offset is expressed for rotation slot 0 (South); the other slots receive it rotated
/// by 90 degrees per slot. S3D's up axis is Y, so the offset acts on X and Z.
/// </para>
/// </summary>
public static class S3DRhdLhdConverter
{
    /// <summary>Rotation slot (0 South, 1 East, 2 North, 3 West) encoded in an S3D Instance ID of a 20-view set, or null if the ID doesn't follow that layout.</summary>
    public static int? RotationSlot(uint instanceId)
    {
        var nibble = (int)((instanceId >> 4) & 0xF);
        var low = instanceId & 0xF;
        return nibble <= 3 && low == 0 ? nibble : null;
    }

    /// <summary>Rotates the slot-0 offset (<paramref name="dx"/>, <paramref name="dz"/>) into the given slot's view.</summary>
    public static (float X, float Z) OffsetForSlot(float dx, float dz, int slot) => slot switch
    {
        0 => (dx, dz),
        1 => (-dz, dx),
        2 => (-dx, -dz),
        _ => (dz, -dx),
    };

    /// <summary>Moves every vertex by the slot-adjusted offset and, if requested, renames RHD to LHD in texture and animation-mesh names. Returns how many names were changed.</summary>
    public static int Convert(S3DModel model, float dx, float dz, int slot, bool renameRhdToLhd)
    {
        var (ox, oz) = OffsetForSlot(dx, dz, slot);
        var offset = new Vector3(ox, 0f, oz);
        foreach (var block in model.VertexBlocks)
        {
            for (var i = 0; i < block.Positions.Count; i++)
            {
                block.Positions[i] += offset;
            }
        }

        if (!renameRhdToLhd)
        {
            return 0;
        }

        var renamed = 0;
        foreach (var material in model.Materials)
        {
            foreach (var texture in material.Textures)
            {
                renamed += Rename(texture.Name, v => texture.Name = v);
            }
        }

        foreach (var mesh in model.Animation.Meshes)
        {
            renamed += Rename(mesh.Name, v => mesh.Name = v);
        }

        return renamed;
    }

    private static int Rename(string? name, System.Action<string> set)
    {
        if (string.IsNullOrEmpty(name) || !name.Contains("RHD", System.StringComparison.Ordinal))
        {
            return 0;
        }

        set(name.Replace("RHD", "LHD", System.StringComparison.Ordinal));
        return 1;
    }
}
