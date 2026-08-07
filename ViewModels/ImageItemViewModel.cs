using Avalonia.Media.Imaging;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One decoded, displayable image belonging to the selected entry: either the single image
/// of a PNG entry, or one named sub-entry of a multi-image FSH file (mirroring the "CB_img"
/// sub-image selector in Ilive Reader's FormImg).
/// </summary>
public sealed class ImageItemViewModel
{
    public required string Label { get; init; }
    public required Bitmap Bitmap { get; init; }

    public int Width => (int)Bitmap.Size.Width;
    public int Height => (int)Bitmap.Size.Height;
}
