using System;
using System.Collections.Generic;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Port of Ilive Reader's <c>_ld::DecodeLD</c>/<c>EncodeLD</c> (<c>or_dat/cl_ld.cpp</c>,
/// ENT_LD 0x6BE74C60, "LDAT - Lot Data") - a fixed 40-byte header, a variable-length lot
/// name, then a list of required-plugin entries (each its own length-prefixed name).
/// </summary>
public static class LdCodec
{
    public sealed record LdData(
        uint Version, uint Compatibility, uint Wealth, uint Subtype, uint Type, uint StageLot,
        ushort NewLot, ushort LotWidth, ushort LotDepth, ushort NumberProp,
        string LotName, IReadOnlyList<string> Plugins);

    private static readonly Encoding Latin1 = Encoding.Latin1;

    public static bool TryDecode(byte[] input, out LdData data)
    {
        data = new LdData(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, string.Empty, Array.Empty<string>());
        if (input.Length < 40)
        {
            return false;
        }

        var version = BitConverter.ToUInt32(input, 0);
        var compatibility = BitConverter.ToUInt32(input, 4);
        var wealth = BitConverter.ToUInt32(input, 8);
        var subtype = BitConverter.ToUInt32(input, 12);
        var type = BitConverter.ToUInt32(input, 16);
        var stageLot = BitConverter.ToUInt32(input, 20);
        var newLot = BitConverter.ToUInt16(input, 24);
        var lotWidth = BitConverter.ToUInt16(input, 26);
        var lotDepth = BitConverter.ToUInt16(input, 28);
        var numberProp = BitConverter.ToUInt16(input, 30);
        var pluginsCount = BitConverter.ToUInt32(input, 32);
        var nameLength = BitConverter.ToUInt32(input, 36);

        if (nameLength > 255 || input.Length < 40 + nameLength)
        {
            return false;
        }

        var lotName = Latin1.GetString(input, 40, (int)nameLength);

        var offset = (int)(40 + nameLength);
        var plugins = new List<string>();
        for (var i = 0; i < pluginsCount; i++)
        {
            if (offset + 4 > input.Length)
            {
                break;
            }

            var itemLength = BitConverter.ToUInt32(input, offset);
            offset += 4;
            if (itemLength > 255 || offset + itemLength > input.Length)
            {
                break;
            }

            plugins.Add(Latin1.GetString(input, offset, (int)itemLength));
            offset += (int)itemLength;
        }

        data = new LdData(version, compatibility, wealth, subtype, type, stageLot,
            newLot, lotWidth, lotDepth, numberProp, lotName, plugins);
        return true;
    }

    public static byte[] Encode(LdData data)
    {
        var nameBytes = Latin1.GetBytes(data.LotName);
        if (nameBytes.Length > 255)
        {
            Array.Resize(ref nameBytes, 255);
        }

        var pluginBytesList = new List<byte[]>();
        foreach (var plugin in data.Plugins)
        {
            var bytes = Latin1.GetBytes(plugin);
            if (bytes.Length > 255)
            {
                Array.Resize(ref bytes, 255);
            }

            pluginBytesList.Add(bytes);
        }

        var totalSize = 40 + nameBytes.Length;
        foreach (var bytes in pluginBytesList)
        {
            totalSize += 4 + bytes.Length;
        }

        var result = new byte[totalSize];
        BitConverter.GetBytes(data.Version).CopyTo(result, 0);
        BitConverter.GetBytes(data.Compatibility).CopyTo(result, 4);
        BitConverter.GetBytes(data.Wealth).CopyTo(result, 8);
        BitConverter.GetBytes(data.Subtype).CopyTo(result, 12);
        BitConverter.GetBytes(data.Type).CopyTo(result, 16);
        BitConverter.GetBytes(data.StageLot).CopyTo(result, 20);
        BitConverter.GetBytes(data.NewLot).CopyTo(result, 24);
        BitConverter.GetBytes(data.LotWidth).CopyTo(result, 26);
        BitConverter.GetBytes(data.LotDepth).CopyTo(result, 28);
        BitConverter.GetBytes(data.NumberProp).CopyTo(result, 30);
        BitConverter.GetBytes((uint)pluginBytesList.Count).CopyTo(result, 32);
        BitConverter.GetBytes((uint)nameBytes.Length).CopyTo(result, 36);
        nameBytes.CopyTo(result, 40);

        var offset = 40 + nameBytes.Length;
        foreach (var bytes in pluginBytesList)
        {
            BitConverter.GetBytes((uint)bytes.Length).CopyTo(result, offset);
            offset += 4;
            bytes.CopyTo(result, offset);
            offset += bytes.Length;
        }

        return result;
    }
}
