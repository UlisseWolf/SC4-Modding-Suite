using System.IO;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Converts SixLabors.ImageSharp images (used by csDBPF for FSH/PNG decoding) into Avalonia
/// <see cref="Bitmap"/> objects that Avalonia can actually render, since Avalonia has no
/// native understanding of ImageSharp's <c>Image</c> type. The bridge is a simple re-encode
/// to PNG in memory, then a re-decode via Avalonia's own <c>Bitmap(Stream)</c> constructor
/// (backed by Skia on desktop, which natively understands PNG).
/// </summary>
public static class ImageConversion
{
    public static Bitmap? ToAvaloniaBitmap(Image? image)
    {
        if (image is null)
        {
            return null;
        }

        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;
        return new Bitmap(stream);
    }

    /// <summary>
    /// Decodes raw bytes as an image of whatever format ImageSharp can auto-detect from
    /// the file's own magic bytes (PNG, BMP, JPEG, GIF, ...). Used as a fallback for the
    /// DBPF TGI Type ID 0x856DDBAC, which - confirmed in Ilive Reader's own constants
    /// (<c>ENT_PNG</c>/<c>ENT_BMP</c>/<c>ENT_JPEG</c> are all 0x856DDBAC) - is shared
    /// between PNG, BMP, and JPEG: csDBPF always builds a <c>DBPFEntryPNG</c> for it and
    /// tries to decode as PNG specifically, which throws for a genuine BMP/JPEG under this
    /// type. Returns null if the bytes don't look like any image format ImageSharp knows.
    /// </summary>
    public static Bitmap? TryDecodeAnyFormat(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        try
        {
            using var image = Image.Load(data);
            return ToAvaloniaBitmap(image);
        }
        catch
        {
            return null;
        }
    }
}
