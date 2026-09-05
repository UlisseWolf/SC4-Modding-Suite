using System.Collections.Generic;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Friendly names for TGI Type IDs that Ilive Reader recognizes (its <c>ENT_*</c>
/// constants in <c>or_dat/sim015.h</c>) - used to label the fallback preview
/// (<see cref="MainWindowViewModel.LoadSimplePreview"/>) with the correct format name
/// instead of a plain "RAW DATA (HEX)"/"TEXT", for every Type ID that doesn't get its own
/// fully separate preview panel elsewhere in this app (PNG/FSH/S3D/WAV do).
///
/// Several of these have a structured decoder ported from Ilive Reader's own
/// <c>or_dat/cl_*.cpp</c> source:
/// <list type="bullet">
/// <item><description><b>TRK</b> (<see cref="TkdCodec"/>), <b>MCO</b>/<b>LDAT</b>
/// (<see cref="McoCodec"/>/<see cref="LdCodec"/>), and <b>LEV</b>
/// (<see cref="LevCodec"/> - shares this Type ID with LTEXT/WAV/XA, see
/// <see cref="EntryTypeClassifier.IsLtextWavXaType"/>) are fully editable: the reference
/// implementation has a complete Decode+Encode pair for all four.</description></item>
/// <item><description><b>HLS</b>, <b>AVP</b>, and <b>SC4Path</b>
/// (<see cref="HlsCodec"/>/<see cref="F1bCodec"/>/<see cref="Sc4PathCodec"/>) are decoded
/// but read-only: Ilive Reader's own <c>_hls</c>/<c>_f1b</c>/<c>_path</c> classes only
/// implement Decode, never Encode.</description></item>
/// </list>
///
/// The rest - <b>TLO</b>, <b>EFFDIR</b>, <b>MAD</b> - are listed as "not fully decoded" or
/// "properties not yet defined" by the community's own official SC4 file format reference
/// (Ilive Reader's own source has no <c>cl_*.cpp</c> for them either), so a structural
/// parser here would be guesswork rather than a documented decoder. EFFDIR already has a
/// dedicated external editor elsewhere in the SC4 modding community, so a read-only hex
/// view is the deliberate end state for it here. TLO/MAD fall back the same way: the
/// generic hex dump, or for genuinely text-shaped undecoded formats like RUL/SC4Path/
/// KEYCFG, the generic editable text box (matching Ilive Reader's own default
/// <c>CFormRichEdit</c> fallback - see <c>FrameTabBase.cpp</c>'s switch statement).
/// </summary>
public static class KnownFormats
{
    private static readonly Dictionary<uint, string> Names = new()
    {
        [0x5D73A611] = "TRK - Track Definition",
        [0x0B8D821A] = "TRK - Track Definition (secondary)",
        [0x9D796DB4] = "TLO - Track Logic Object",
        [0x7B1ACFCD] = "HLS - Hitlist Playlist",
        [0x09ADCD75] = "AVP - Animation Viewpoints",
        [0x296678F7] = "SC4Path - Network Path",
        [0x0A5BCF4B] = "RUL - Network Rules",
        [0xCA63E2A3] = "LUA - Lua Script",
        [0x0A8B0E70] = "MAD - EA MAD Video",
        [0xA2E3D533] = "KEYCFG/TAB - Keyboard Accelerator Table",
        [0x6BE74C60] = "LDAT - Lot Data",
        [0x29A5D1EC] = "MCO - Building Menu/Camera Preview",
        [0xEA5118B0] = "EFFDIR - Effect Resource Tree",
        [0xEA5118B1] = "EFFDIR - Effect Resource Tree",
        [0x6A5B7BF5] = "DBPF - Nested Package",
    };

    public static string? TryGetName(uint typeId) => Names.GetValueOrDefault(typeId);
}
