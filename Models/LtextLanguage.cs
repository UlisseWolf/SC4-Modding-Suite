using System.Collections.Generic;

namespace SC4ModdingSuite.Models;

/// <summary>
/// One entry in SC4's "Multilingual Group ID Offset" table for LTEXT files (Type ID
/// always 0x2026960B): a language-family's LTEXT translation is not addressed by its own
/// TGI - it shares the base LTEXT's Type and Instance ID, and is found by adding
/// <see cref="Offset"/> to the base entry's Group ID. Documented by RippleJet in
/// "LTEXT - Language Text Files (How to make multilingual buildings and queries)":
/// <see href="https://www.sc4devotion.com/forums/index.php?topic=532.0"/>.
/// </summary>
public sealed record LtextLanguage(byte Offset, string Name)
{
    /// <summary>"Italian (offset 0x05)" - used as the ComboBox display text.</summary>
    public string Label => $"{Name} (offset 0x{Offset:X2})";

    public override string ToString() => Label;
}

/// <summary>
/// The full offset table from the SC4Devotion tutorial above, in the order given there.
/// Offset 0x00 ("Default Language") is what the game falls back to if no LTEXT file exists
/// at a specific language's offset - it is not itself tied to any one language, and is
/// commonly what a single-language source file's own TGI already sits at before any
/// translations are added.
/// </summary>
public static class LtextLanguages
{
    public static readonly IReadOnlyList<LtextLanguage> All = new List<LtextLanguage>
    {
        new(0x00, "Default"),
        new(0x01, "US English"),
        new(0x02, "UK English"),
        new(0x03, "French"),
        new(0x04, "German"),
        new(0x05, "Italian"),
        new(0x06, "Spanish"),
        new(0x07, "Dutch"),
        new(0x08, "Danish"),
        new(0x09, "Swedish"),
        new(0x0A, "Norwegian"),
        new(0x0B, "Finnish"),
        new(0x0F, "Japanese"),
        new(0x10, "Polish"),
        new(0x11, "Simplified Chinese"),
        new(0x12, "Traditional Chinese"),
        new(0x13, "Thai"),
        new(0x14, "Korean"),
        new(0x23, "Portuguese (Brazilian)"),
    };

    /// <summary>"Default" (offset 0x00) - used as the initial/fallback selection for both source and target language pickers.</summary>
    public static LtextLanguage Default => All[0];
}
