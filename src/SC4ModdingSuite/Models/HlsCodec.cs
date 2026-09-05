using System;
using System.Collections.Generic;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Port of Ilive Reader's <c>_hls::DecodeHLS</c> (<c>or_dat/cl_hls.cpp</c>, ENT_HLS
/// 0x7B1ACFCD) - a "Hitlist" (playlist): a 4-byte header DWORD, a 4-byte count, then that
/// many 4-byte Instance ID references (to XA audio clips - Type ID 0x2026960B, the same
/// shared one WAV/LTEXT/XA use).
///
/// Read-only by design: Ilive Reader's own <c>_hls</c> class has no <c>EncodeHLS</c>, only
/// decode. Since the reference implementation doesn't write this format back out either,
/// this app doesn't fabricate an encoder (see <see cref="KnownFormats"/>'s note on
/// TLO/LDAT/EFFDIR/MAD).
/// </summary>
public static class HlsCodec
{
    public sealed record HlsData(uint Header, IReadOnlyList<uint> Instances);

    public static bool TryDecode(byte[] input, out HlsData data)
    {
        data = new HlsData(0, Array.Empty<uint>());
        if (input.Length < 8)
        {
            return false;
        }

        var header = BitConverter.ToUInt32(input, 0);
        var count = BitConverter.ToUInt32(input, 4);

        // Defensive: a corrupt/truncated file could claim a count that doesn't fit -
        // Ilive Reader's own C++ has no such guard and would simply read past the buffer,
        // so this stops short instead of throwing.
        var maxCount = (uint)((input.Length - 8) / 4);
        var actualCount = Math.Min(count, maxCount);

        var instances = new uint[actualCount];
        for (var i = 0; i < actualCount; i++)
        {
            instances[i] = BitConverter.ToUInt32(input, 8 + i * 4);
        }

        data = new HlsData(header, instances);
        return true;
    }
}
