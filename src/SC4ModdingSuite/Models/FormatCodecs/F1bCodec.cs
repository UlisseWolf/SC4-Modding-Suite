using System;
using System.Collections.Generic;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Port of Ilive Reader's <c>_f1b::DecodeF1B</c> (<c>or_dat/cl_f1b.cpp</c>, ENT_F1B
/// 0x09ADCD75) - Animation Viewpoints for a building's animated menu/mayor's-camera
/// preview (referenced by <see cref="McoCodec"/>'s F1B fields): a 36-byte header (type,
/// entry count, two fields the original author themselves only ever named "unknown1"/
/// "unknown2", 16 more unexplained bytes, and a second entry count), then that many 8-byte
/// per-frame records (plane/unknown/file position/width/height/two position bytes).
///
/// Read-only by design: three of this format's own header fields are literally named
/// "unknown" in Ilive Reader's own source, and there is no <c>EncodeF1B</c> - the reference
/// implementation doesn't fully understand this format well enough to write it back out.
/// This only reproduces what's confirmed, not the unnamed fields (see
/// <see cref="KnownFormats"/>'s note on TLO/LDAT/EFFDIR/MAD).
/// </summary>
public static class F1bCodec
{
    public sealed record F1bFrame(byte Plane, byte Unknown1, ushort FilePosition, byte Width, byte Height, byte Pos1, byte Pos2);

    public sealed record F1bData(uint F1BType, uint Entries, uint Unknown1, uint Unknown2, byte[] Unknown3, uint Entries2, IReadOnlyList<F1bFrame> Frames);

    public static bool TryDecode(byte[] input, out F1bData data)
    {
        data = new F1bData(0, 0, 0, 0, Array.Empty<byte>(), 0, Array.Empty<F1bFrame>());
        if (input.Length < 36)
        {
            return false;
        }

        var f1bType = BitConverter.ToUInt32(input, 0);
        var entries = BitConverter.ToUInt32(input, 4);
        var unknown1 = BitConverter.ToUInt32(input, 8);
        var unknown2 = BitConverter.ToUInt32(input, 12);
        var unknown3 = new byte[16];
        Array.Copy(input, 16, unknown3, 0, 16);
        var entries2 = BitConverter.ToUInt32(input, 32);

        var maxFrames = (uint)((input.Length - 36) / 8);
        var actualFrames = Math.Min(entries2, maxFrames);

        var frames = new F1bFrame[actualFrames];
        var offset = 36;
        for (var i = 0; i < actualFrames; i++)
        {
            frames[i] = new F1bFrame(
                input[offset], input[offset + 1],
                BitConverter.ToUInt16(input, offset + 2),
                input[offset + 4], input[offset + 5], input[offset + 6], input[offset + 7]);
            offset += 8;
        }

        data = new F1bData(f1bType, entries, unknown1, unknown2, unknown3, entries2, frames);
        return true;
    }
}
