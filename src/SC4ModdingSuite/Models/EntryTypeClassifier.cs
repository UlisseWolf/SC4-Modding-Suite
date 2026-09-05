using System;
using System.Text;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Small helpers for entry classification edge cases around the TGI Type ID
/// <c>0x2026960B</c>, which - confirmed directly in Ilive Reader's own constants
/// (<c>or_dat/sim015.h</c>: <c>ENT_XA</c>, <c>ENT_LTEXT</c>, <c>ENT_WAV</c> are all
/// <c>0x2026960B</c>) and in csDBPF's docs (<c>DBPFTGI.WAV</c> = "(0x2026960b,
/// 0xaa4d1933, #)", <c>DBPFTGI.LTEXT</c> = "(0x2026960b, #, #)") - is shared between
/// **three** different formats: WAV, LTEXT, and the rarer compressed EA audio format
/// "XA". Ilive Reader tells them apart by sniffing the actual bytes (its
/// <c>_entry::SetFlag</c>, <c>or_dat/cl_entry.cpp</c>), not by trusting Group ID alone -
/// the same technique is used here:
///
/// <list type="bullet">
/// <item>bytes starting with the RIFF container magic → WAV;</item>
/// <item>otherwise → LTEXT (the overwhelmingly common case for this Type ID in real SC4
/// packages; XA is intentionally not specifically detected here, out of scope for a
/// "viewer" - it would just show as unreadable text, same as Ilive Reader falling back to
/// FLG_UNKNOWN for content it can't otherwise identify).</item>
/// </list>
///
/// Important: Type ID <c>0x00000000</c> is <b>not</b> "blank/unrecognized" - it is the
/// legitimate, documented Type ID for SC4 "UI" resource entries (<c>ENT_UI</c> in Ilive
/// Reader, <c>DBPFTGI.UI</c> in csDBPF, which already has a dedicated
/// <see cref="DBPFEntryUI"/> class for it) - so this class does **not** special-case it
/// to "Unknown" the way an earlier version of this app incorrectly did.
/// </summary>
public static class EntryTypeClassifier
{
    private const uint LtextWavXaTypeId = 0x2026960B;

    /// <summary>True if this entry's Type ID is the one shared between WAV/LTEXT/XA.</summary>
    public static bool IsLtextWavXaType(TGI tgi) => tgi.TypeID == LtextWavXaTypeId;

    /// <summary>True if <paramref name="data"/> starts with the RIFF container magic every WAV file has.</summary>
    public static bool LooksLikeRiffWav(byte[]? data) =>
        data is { Length: >= 4 } &&
        data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F';

    /// <summary>
    /// Best-effort decode of the LTEXT binary layout (a little-endian UTF-16 character
    /// count, a 0x1000 marker, then the UTF-16LE string itself) for entries under the
    /// shared WAV/LTEXT/XA Type ID that csDBPF didn't itself construct as a
    /// <see cref="DBPFEntryLTEXT"/> - i.e. the "special"/non-standard-group LTEXT entries
    /// the app was previously mislabeling as "Unknown". Returns null if the bytes don't
    /// look like this layout at all (e.g. they are actually XA audio).
    /// </summary>
    public static string? TryDecodeAsLtext(byte[]? data)
    {
        if (data is not { Length: >= 4 })
        {
            return null;
        }

        var declaredChars = data[0] | (data[1] << 8);
        var marker = data[2] | (data[3] << 8);
        if (marker != 0x1000)
        {
            return null;
        }

        var maxChars = (data.Length - 4) / 2;
        var charCount = Math.Clamp(declaredChars, 0, maxChars);
        return Encoding.Unicode.GetString(data, 4, charCount * 2);
    }

    /// <summary>
    /// Checks whether raw bytes look like plain readable text rather than binary data -
    /// used as a generic fallback preview for entry types Ilive Reader recognizes as
    /// script/text formats but csDBPF has no structured decoder for at all, notably Lua
    /// scripts (<c>ENT_LUA</c>, <c>or_dat/sim015.h</c>) and network intersection rule
    /// files (<c>ENT_RUL</c>). Rather than hardcoding every such Type ID from Ilive
    /// Reader's long constant list (most of which are savegame-internal subsystem data out
    /// of scope for a building/prop modding tool), this sniffs content directly: if at
    /// least 95% of the bytes are printable ASCII or common whitespace, it's shown as text
    /// instead of an unhelpful hex dump - covering LUA/RUL and any other unlisted
    /// text-based format the same way.
    /// </summary>
    public static bool LooksLikePlainText(byte[]? data)
    {
        if (data is not { Length: > 0 })
        {
            return false;
        }

        var sampleLength = Math.Min(data.Length, 4096);
        var printable = 0;

        for (var i = 0; i < sampleLength; i++)
        {
            var b = data[i];
            if (b is 0x09 or 0x0A or 0x0D || b is >= 0x20 and < 0x7F)
            {
                printable++;
            }
        }

        return printable >= sampleLength * 0.95;
    }
}
