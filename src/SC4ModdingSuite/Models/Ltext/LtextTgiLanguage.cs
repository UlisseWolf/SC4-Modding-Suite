using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Implements the one piece of LTEXT localization that isn't just "edit some text": SC4
/// picks a multilingual LTEXT file's language purely from its <b>Group ID</b>, by adding a
/// fixed per-language offset (<see cref="LtextLanguage.Offset"/>) to the pointer's base
/// Group ID - Type ID and Instance ID never change between languages of the same string.
/// See RippleJet's "LTEXT - Language Text Files" tutorial for the full explanation and the
/// Brandenburg Gate / Local Limousine Funding worked examples this offset table is taken
/// from: <see href="https://www.sc4devotion.com/forums/index.php?topic=532.0"/>.
///
/// <para>
/// Every LTEXT operation in this app that deals with more than one language (Save file for
/// language, Export/Import Poedit) goes through <see cref="TgiForLanguage"/> below, so the
/// Group ID math only lives in one place.
/// </para>
/// </summary>
public static class LtextTgiLanguage
{
    /// <summary>
    /// Computes the TGI of <paramref name="sourceTgi"/>'s sibling entry in
    /// <paramref name="targetLanguage"/>, given that <paramref name="sourceTgi"/> itself
    /// is understood to already sit at <paramref name="sourceLanguage"/>'s offset (this
    /// is almost always <see cref="LtextLanguages.Default"/>/offset 0x00 - the common
    /// convention, per the tutorial, of keeping the "root" pointer TGI unshifted and only
    /// offsetting the per-language siblings around it). Type ID and Instance ID are always
    /// carried over unchanged; only the Group ID shifts, by
    /// <c>targetLanguage.Offset - sourceLanguage.Offset</c>.
    /// </summary>
    public static TGI TgiForLanguage(TGI sourceTgi, LtextLanguage sourceLanguage, LtextLanguage targetLanguage)
    {
        unchecked
        {
            var shiftedGroup = sourceTgi.GroupID - (uint)sourceLanguage.Offset + (uint)targetLanguage.Offset;
            return new TGI(sourceTgi.TypeID, shiftedGroup, sourceTgi.InstanceID);
        }
    }

    /// <summary>
    /// The inverse: given an entry already believed to sit at <paramref name="knownLanguage"/>'s
    /// offset, returns what its Group ID would be at <see cref="LtextLanguages.Default"/>
    /// (offset 0x00) - i.e. the "base"/root Group ID used to key a Poedit msgctxt, so
    /// re-importing a translation lands back on the same family regardless of which
    /// language happened to be open when it was exported.
    /// </summary>
    public static TGI ToBaseTgi(TGI tgi, LtextLanguage knownLanguage) =>
        TgiForLanguage(tgi, knownLanguage, LtextLanguages.Default);
}
