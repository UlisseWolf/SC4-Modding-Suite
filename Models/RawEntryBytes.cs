using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Gets the plain, QFS-decompressed raw bytes of an entry regardless of its compression
/// state. Used for entry types csDBPF has no structured decoder for (S3D models, WAV
/// audio) where we work with the raw payload directly instead of going through
/// <c>Decode()</c>/a type-specific data object.
/// </summary>
public static class RawEntryBytes
{
    public static byte[]? GetDecompressed(DBPFEntry entry)
    {
        var bytes = entry.ByteData;
        if (bytes is null || bytes.Length == 0)
        {
            return bytes;
        }

        return entry.IsCompressed ? QFS.Decompress(bytes) : bytes;
    }
}
