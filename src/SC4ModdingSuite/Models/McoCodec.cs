using System;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Port of Ilive Reader's <c>_mco::DecodeMCO</c> (<c>or_dat/cl_mco.cpp</c>, ENT_MCO
/// 0x29A5D1EC) - a fixed 48-byte record, 12 DWORD fields, with no "unknown"/unexplained
/// fields at all (unlike F1B below): a texture reference (FSH TGI) and an animated-viewpoint
/// reference (F1B type/group, plus one Instance ID per zoom level 1-5) for a building's
/// menu/mayor's-camera preview. Ilive Reader itself never implements an <c>EncodeMCO</c> -
/// but because every field here is a plain, unambiguous DWORD at a fixed offset (no
/// variable-length or "meaning unclear" data the way F1B/HLS have), writing one back is not
/// a guess: this is the same "pack the same 12 DWORDs back in the same order" every other
/// fixed-layout format in this app already does.
/// </summary>
public static class McoCodec
{
    public sealed record McoData(
        uint McoType, uint FshType, uint FshGroup, uint FshInstance,
        uint F1BType, uint F1BGroup,
        uint F1BInstanceZoom1, uint F1BInstanceZoom2, uint F1BInstanceZoom3, uint F1BInstanceZoom4, uint F1BInstanceZoom5,
        uint FrameCount);

    public const int Size = 48;

    public static bool TryDecode(byte[] input, out McoData data)
    {
        data = new McoData(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (input.Length < Size)
        {
            return false;
        }

        data = new McoData(
            BitConverter.ToUInt32(input, 0),
            BitConverter.ToUInt32(input, 4),
            BitConverter.ToUInt32(input, 8),
            BitConverter.ToUInt32(input, 12),
            BitConverter.ToUInt32(input, 16),
            BitConverter.ToUInt32(input, 20),
            BitConverter.ToUInt32(input, 24),
            BitConverter.ToUInt32(input, 28),
            BitConverter.ToUInt32(input, 32),
            BitConverter.ToUInt32(input, 36),
            BitConverter.ToUInt32(input, 40),
            BitConverter.ToUInt32(input, 44));
        return true;
    }

    public static byte[] Encode(McoData data)
    {
        var result = new byte[Size];
        BitConverter.GetBytes(data.McoType).CopyTo(result, 0);
        BitConverter.GetBytes(data.FshType).CopyTo(result, 4);
        BitConverter.GetBytes(data.FshGroup).CopyTo(result, 8);
        BitConverter.GetBytes(data.FshInstance).CopyTo(result, 12);
        BitConverter.GetBytes(data.F1BType).CopyTo(result, 16);
        BitConverter.GetBytes(data.F1BGroup).CopyTo(result, 20);
        BitConverter.GetBytes(data.F1BInstanceZoom1).CopyTo(result, 24);
        BitConverter.GetBytes(data.F1BInstanceZoom2).CopyTo(result, 28);
        BitConverter.GetBytes(data.F1BInstanceZoom3).CopyTo(result, 32);
        BitConverter.GetBytes(data.F1BInstanceZoom4).CopyTo(result, 36);
        BitConverter.GetBytes(data.F1BInstanceZoom5).CopyTo(result, 40);
        BitConverter.GetBytes(data.FrameCount).CopyTo(result, 44);
        return result;
    }
}
