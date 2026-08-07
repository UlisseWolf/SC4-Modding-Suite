using System.Collections.Generic;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Friendly names for TGI Type IDs that Ilive Reader recognizes (its <c>ENT_*</c>
/// constants in <c>or_dat/sim015.h</c>) but that don't have their own dedicated,
/// structured preview panel in this app - used only to label the generic text/hex
/// fallback preview (<see cref="MainWindowViewModel.LoadSimplePreview"/>) with the
/// correct format name instead of a plain "RAW DATA (HEX)". The content itself is still
/// shown as the best generic form available (readable text if it looks like text,
/// hex+ASCII otherwise): several of these formats - TRK, TLO, LDAT, EFFDIR, MAD - are
/// themselves listed as "not fully decoded" or "properties not yet defined" by the
/// community's own official SC4 file format reference, so writing a bespoke structural
/// parser for them here would be guesswork rather than a documented decoder, out of scope
/// for a display-only viewer.
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
        [0xEA5118B0] = "EFFDIR - Effect Resource Tree",
        [0xEA5118B1] = "EFFDIR - Effect Resource Tree",
        [0x6A5B7BF5] = "DBPF - Nested Package",
    };

    public static string? TryGetName(uint typeId) => Names.GetValueOrDefault(typeId);
}
