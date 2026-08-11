using System;
using System.Collections.Generic;
using System.Linq;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Converts a T21 (network lot / base-texture Prop&amp;Flora placement) Exemplar from
/// right-hand-drive (RHD) to left-hand-drive (LHD) by mirroring every geometry-related
/// property along the tile's X axis - the same axis a network's own textures/props are
/// mirrored across when a mod is republished for LHD play:
///
/// <list type="bullet">
/// <item><description>Every placed lot object's (<see cref="T21Constants.ObjectsBase"/>+N)
/// X position and X bounding box are mirrored around the tile center
/// (<c>x' = TileWidth - x</c>, in the file's 16.16 fixed-point encoding - <see cref="TileWidth"/>
/// is one full SC4 tile, 16 meters, not 1 meter: X/XMin/XMax values in a real T21 range
/// roughly 0-16, e.g. a value like <c>720896</c> is <c>720896 / 0x10000 = 11.0</c> meters
/// into the tile, not 11 tiles), and its rotation is swapped between West(1)/East(3)
/// (South(0)/North(2) are on the mirror axis itself and stay put) - this is Jondor's own
/// rotation encoding, see <see cref="T21Constants.RotationOptions"/>.</description></item>
/// <item><description>The top-level "allowed rotations" bitmask
/// (<see cref="T21Constants.Rots"/>) has its East/West bits swapped the same way.</description></item>
/// <item><description>The top-level "Flips" property (<see cref="T21Constants.Flips"/>) has
/// "Flipped Only" and "Non-flipped Only" swapped - mirroring the whole tile turns what used
/// to need an extra flip into what now must never be flipped, and vice versa. "Both" is
/// left as "Both".</description></item>
/// </list>
///
/// Every other property (Name, IID, Version, Tile IID, slopes, Pattern, Zones, Wealths, and
/// any custom/unrecognized property a real-world file might carry) is copied through byte-
/// for-byte unchanged - this is deliberately a narrow, surgical mirror of just the
/// left/right-sensitive geometry, not a full property rebuild (unlike the T21 Editor's own
/// SAVE button), so nothing about a real file this app doesn't otherwise understand gets
/// silently dropped.
/// </summary>
public static class T21LhdConverter
{
    /// <summary>
    /// 16.16 fixed-point encoding of one full SC4 tile (16 meters) - mirroring a coordinate
    /// is <c>TileWidth - x</c>. <b>Not</b> <c>0x10000</c> (that is 1.0 meter, i.e. 1/16th of
    /// a tile): a placed lot object's X/XMin/XMax sit anywhere from 0 to ~16 meters into the
    /// tile it's on (see <see cref="T21ObjectRowViewModel"/>'s own <c>/ 0x10000</c> "meters"
    /// conversion, used identically for display in the T21 Editor), so mirroring around
    /// <c>0x10000</c> instead of a full 16-meter tile (<c>16 * 0x10000</c>) would have moved
    /// every object roughly 15 tiles off to the side instead of to its correct mirrored spot
    /// on the *same* tile.
    /// </summary>
    private const long TileWidth = 16L * 0x10000;


    public static bool IsT21Exemplar(DBPFEntry entry) =>
        entry is DBPFEntryEXMP && entry.TGI.GroupID == T21Constants.T21GroupId;

    /// <summary>
    /// Builds a brand-new <see cref="DBPFEntryEXMP"/> - same TGI as <paramref name="source"/>,
    /// same compression state - with every LHD-relevant property mirrored. Does not modify
    /// <paramref name="source"/> in any way. Returns <see langword="null"/> if
    /// <paramref name="source"/> doesn't parse as a well-formed T21 (ExemplarType 0x21); the
    /// caller should fall back to copying <paramref name="source"/> through unchanged in
    /// that case, same as every other non-T21 entry in the package.
    /// </summary>
    public static DBPFEntryEXMP? MirrorToLhd(DBPFEntryEXMP source)
    {
        var rawBytes = RawEntryBytes.GetDecompressed(source);
        var parsed = ExemplarBinaryParser.Parse(rawBytes);
        if (!parsed.IsWellFormed)
        {
            return null;
        }

        var typeProp = parsed.Properties.FirstOrDefault(p => p.Id == T21Constants.ExemplarTypeProp);
        if (typeProp is null || Convert.ToInt64(typeProp.Values.FirstOrDefault() ?? 0L) != 0x21L)
        {
            return null;
        }

        // A fresh entry built from the *original* raw bytes (untouched) via csDBPF's own
        // "read an existing entry" constructor, then immediately re-decoded and its whole
        // property list replaced with the mirrored one - the exact same "clean slate"
        // approach as DbpfService.ReplaceAllProperties, just applied to an independent
        // clone instead of the live, currently-open entry.
        var mirrored = new DBPFEntryEXMP(source.TGI, 0u, (uint)(source.ByteData?.Length ?? 0), 0u, source.ByteData ?? Array.Empty<byte>());
        mirrored.Decode();
        mirrored.RemoveAllProperties();

        foreach (var property in parsed.Properties)
        {
            DBPFProperty mirroredProperty = property.Id switch
            {
                T21Constants.Flips => MirrorFlips(property),
                T21Constants.Rots => MirrorRots(property),
                _ when property.Id >= T21Constants.ObjectsBase => MirrorLotObject(property),
                _ => ExemplarBinaryParser.ToDbpfProperty(property),
            };

            mirrored.AddOrUpdateProperty(mirroredProperty);
        }

        // See ExemplarEncodeFix: without this, csDBPF's Encode() silently no-ops (its
        // internal _isDecoded flag is never set true by Decode()), so every property
        // change made just above would be discarded and the "mirrored" entry written to
        // disk would come out byte-for-byte identical to the RHD source - exactly the bug
        // this fixes.
        ExemplarEncodeFix.EnsureEncodable(mirrored);
        mirrored.Encode(source.IsCompressed);
        return mirrored;
    }

    private static DBPFProperty MirrorFlips(ParsedExemplarProperty property)
    {
        var value = Convert.ToInt64(property.Values.FirstOrDefault() ?? 0L);
        var mirroredValue = value switch
        {
            1L => 2L, // Flipped Only -> Non-flipped Only
            2L => 1L, // Non-flipped Only -> Flipped Only
            _ => value, // Both -> Both
        };

        return new DBPFPropertyLong(property.DataType, new[] { mirroredValue }, DBPF.Encoding.Binary) { ID = property.Id };
    }

    private static DBPFProperty MirrorRots(ParsedExemplarProperty property)
    {
        var mask = Convert.ToInt64(property.Values.FirstOrDefault() ?? 0L);
        var east = (mask >> 1) & 1L;
        var west = (mask >> 3) & 1L;
        var mirroredMask = (mask & ~0b1010L) | (east << 3) | (west << 1);

        return new DBPFPropertyLong(property.DataType, new[] { mirroredMask }, DBPF.Encoding.Binary) { ID = property.Id };
    }

    /// <summary>
    /// Mirrors one placed lot object (raw layout: [0]=type [1]=lod+flag [2]=rotation
    /// [3..5]=X/Y/Z [6..9]=XMin/ZMin/XMax/ZMax [10]=reserved [11]=object key [12..]=IID(s),
    /// matching the layout <c>T21ObjectRowViewModel.ToRawValues</c> in the ViewModels
    /// project writes) - X position and X bounds mirror around
    /// the tile center, rotation swaps West(1)/East(3), and every other value (including all
    /// IIDs - this mirrors *placement*, not *which* prop/flora is placed) is left untouched.
    /// Rows that don't actually look like a lot object (wrong type code / too few values -
    /// e.g. some other, unrelated property that happens to share this ID range) are copied
    /// through unchanged instead of guessed at.
    /// </summary>
    private static DBPFProperty MirrorLotObject(ParsedExemplarProperty property)
    {
        var values = property.Values.Select(Convert.ToInt64).ToArray();
        if (values.Length < 12 || (values[0] != 0x1L && values[0] != 0x4L))
        {
            return ExemplarBinaryParser.ToDbpfProperty(property);
        }

        var mirrored = (long[])values.Clone();

        mirrored[2] = values[2] switch
        {
            1L => 3L, // West -> East
            3L => 1L, // East -> West
            _ => values[2], // South/North are on the mirror axis - unchanged
        };

        mirrored[3] = TileWidth - values[3]; // X
        mirrored[6] = TileWidth - values[8]; // new XMin = mirror(old XMax)
        mirrored[8] = TileWidth - values[6]; // new XMax = mirror(old XMin)

        return new DBPFPropertyLong(property.DataType, mirrored, DBPF.Encoding.Binary) { ID = property.Id };
    }
}
