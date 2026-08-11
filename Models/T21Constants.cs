using System.Collections.Generic;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Property IDs and small lookup tables for the "T21 Editor" (network-lot / base-texture
/// Prop&amp;Flora exemplars, Type 0x6534284A, Group 0x89AC5643 - see
/// <c>MainWindowViewModel.T21GroupId</c>). Every constant here is a straight, verified port
/// of the equivalent <c>public static final long</c> field at the bottom of Jondor's own
/// <c>T21EditWindow.java</c> (see the bundled "jondor-t21-editor-main" source), which this
/// editor is a from-scratch re-implementation of on top of this app's own Avalonia UI and
/// csDBPF property model instead of Jondor's Swing/jDBPF stack.
/// </summary>
public static class T21Constants
{
    /// <summary>ExemplarType (0x21 = "Network Lot Configurations Prop&amp;Flora" for a well-formed T21).</summary>
    public const uint ExemplarTypeProp = 0x00000010;

    /// <summary>ExemplarName (string).</summary>
    public const uint ExemplarName = 0x00000020;

    /// <summary>ExemplarID - mirrors the exemplar's own TGI Instance ID (Jondor writes both).</summary>
    public const uint ExemplarId = 0x00000021;

    /// <summary>T21 "version" byte Jondor always writes as 2.</summary>
    public const uint Version = 0x88EDC789;

    /// <summary>Cross-reference to the base texture/tile exemplar this T21 is bound to.</summary>
    public const uint TileIid = 0xC9A5A1BE;

    public const uint MinSlope = 0xAA120972;
    public const uint MaxSlope = 0xAA120973;

    /// <summary>3 (diagonal-only network tiles) or 4 (every other tile) possible orientations.</summary>
    public const uint PatternSize = 0xCA81B8D4;

    /// <summary>4 UINT8 values, each a 4-bit mask selecting which of up to 4 orientations this T21 applies to.</summary>
    public const uint Pattern = 0x49D55951;

    /// <summary>Variable-length UINT8 array of zone type codes this T21 applies to (see <see cref="ZoneNames"/>).</summary>
    public const uint Zones = 0x88EDC793;

    /// <summary>Variable-length UINT8 array of wealth type codes (0-3) this T21 applies to.</summary>
    public const uint Wealths = 0x88EDC795;

    /// <summary>Single UINT8: 0 = Both, 1 = Flipped Only, 2 = Non-flipped Only.</summary>
    public const uint Flips = 0xCC3E4755;

    /// <summary>Single UINT8 bitmask: bit0=North, bit1=East, bit2=South, bit3=West.</summary>
    public const uint Rots = 0xEC3BD470;

    /// <summary>
    /// First of a run of consecutive property IDs (0x88EDC900, 0x88EDC901, ...), one per
    /// prop/flora placed on the lot - Jondor's own <c>OBJECTS</c> constant/loop.
    /// </summary>
    public const uint ObjectsBase = 0x88EDC900;

    /// <summary>Zone type codes 0-15, in order - matches Jondor's <c>zonesCheck[0..15]</c> labels exactly.</summary>
    /// <summary>Group ID shared by every T21 Exemplar - "Network Lots"/"Base Texture" Prop&amp;Flora placement exemplars (same value as <c>MainWindowViewModel.T21GroupId</c>, kept as its own public constant here so Model-layer code like <see cref="T21LhdConverter"/> doesn't need a reference to the ViewModel).</summary>
    public const uint T21GroupId = 0x89AC5643;

    public static readonly IReadOnlyList<string> ZoneNames = new[]
    {
        "None",                  // 0
        "Residential $",         // 1
        "Residential $$",        // 2
        "Residential $$$",       // 3
        "Commercial $",          // 4
        "Commercial $$",         // 5
        "Commercial $$$",        // 6
        "Industrial $ (Low)",    // 7
        "Industrial $$ (Med)",   // 8
        "Industrial $$$ (High)", // 9
        "Military",              // 10
        "Airport",               // 11
        "Seaport",               // 12
        "Spaceport",             // 13
        "Landfill",              // 14
        "Plopped Building",      // 15
    };

    /// <summary>Wealth codes 0-3.</summary>
    public static readonly IReadOnlyList<string> WealthNames = new[] { "None", "Low ($)", "Medium ($$)", "High ($$$)" };

    /// <summary>Flips combo options, index = stored value.</summary>
    public static readonly IReadOnlyList<string> FlipOptions = new[] { "Both", "Flipped Only", "Non-flipped Only" };

    /// <summary>Rotation combo options for a single placed object, index = stored value (Jondor's rotCombo).</summary>
    public static readonly IReadOnlyList<string> RotationOptions = new[] { "South (0)", "West (1)", "North (2)", "East (3)" };

    /// <summary>Object type options for a placed lot object - T21 only ever places Props or Flora (Jondor's typeEnum).</summary>
    public static readonly IReadOnlyList<string> ObjectTypeOptions = new[] { "Prop", "Flora" };

    public static long ObjectTypeCode(string name) => name == "Flora" ? 0x4L : 0x1L;

    public static string ObjectTypeName(long code) => code == 0x4L ? "Flora" : "Prop";

    /// <summary>LOD options for a placed lot object - Jondor's lodEnum, stored packed into the top nibble of value 1.</summary>
    public static readonly IReadOnlyList<string> LodOptions = new[] { "All", "Med or High", "High Only" };

    public static long LodCode(string name) => name switch
    {
        "Med or High" => 0x10L,
        "High Only" => 0x20L,
        _ => 0x0L,
    };

    public static string LodName(long code) => (code & 0xF0L) switch
    {
        0x10L => "Med or High",
        0x20L => "High Only",
        _ => "All",
    };
}
