using System;
using System.Collections.Generic;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Which kind of value a UI element property holds - drives which specialized editor
/// <c>UiNodePropertiesDialog</c> shows for it (checkbox/RGB fields/rect fields/xy fields)
/// instead of always plain text. Mirrors Ilive Reader's own <c>WorkspaceUIProp.cpp</c>
/// (<c>g_aUIPropType[]</c>: 0=bool, 1=str, 2=rect, 3=color, 4=xy, 5=list).
/// </summary>
public enum UiPropertyKind
{
    /// <summary>Plain text - also used for Ilive Reader's "list" kind (font/align/blttype/direction): those are genuinely enum-like, but this app doesn't have a verified, complete list of every legal value for them, and an incomplete/wrong dropdown would be worse than free text.</summary>
    String,
    Bool,
    Rect,
    Color,
    Xy,
}

/// <summary>
/// Maps a property name to its <see cref="UiPropertyKind"/> - the same association Ilive
/// Reader's own property grid (<c>CWorkspaceUIProp</c>) uses to pick checkbox/color-picker/
/// rect-fields instead of a plain text box. Built directly from
/// <c>WorkspaceUIProp.cpp</c>'s <c>g_aUIPropType[]</c> table (both "winflag_enable" and
/// "winflag_enabled" are included - Ilive Reader's own two property tables,
/// <c>g_aUIPropType[]</c> here and <c>ColProp[]</c> in <c>FormUI.cpp</c>, actually disagree
/// with each other on which spelling is correct).
/// </summary>
public static class UiPropertyTypeCatalog
{
    private static readonly Dictionary<string, UiPropertyKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["area"] = UiPropertyKind.Rect,
        ["imagerect"] = UiPropertyKind.Rect,
        ["combodownarrowrect"] = UiPropertyKind.Rect,

        ["gutters"] = UiPropertyKind.Xy,
        ["scrollbargutters"] = UiPropertyKind.Xy,
        ["textoffsets"] = UiPropertyKind.Xy,
        ["minmaxvalue"] = UiPropertyKind.Xy,
        ["tipoffsets"] = UiPropertyKind.Xy,
        ["minmax"] = UiPropertyKind.Xy,

        ["fillcolor"] = UiPropertyKind.Color,
        ["forecolor"] = UiPropertyKind.Color,
        ["bkgcolor"] = UiPropertyKind.Color,
        ["colorfontnormal"] = UiPropertyKind.Color,
        ["colorfontnormalbkg"] = UiPropertyKind.Color,
        ["colorfontdisabled"] = UiPropertyKind.Color,
        ["colorfontdisabledbkg"] = UiPropertyKind.Color,
        ["colorfonthilited"] = UiPropertyKind.Color,
        ["colorfonthilitedbkg"] = UiPropertyKind.Color,
        ["combodowncolor"] = UiPropertyKind.Color,
        ["caretcolor"] = UiPropertyKind.Color,
        ["highlightcolor"] = UiPropertyKind.Color,
        ["outlinecolor"] = UiPropertyKind.Color,
        ["colorleft"] = UiPropertyKind.Color,
        ["colorright"] = UiPropertyKind.Color,
        ["colortop"] = UiPropertyKind.Color,
        ["colorbottom"] = UiPropertyKind.Color,
        ["coloroutlinel"] = UiPropertyKind.Color,
        ["coloroutliner"] = UiPropertyKind.Color,
        ["coloroutlinet"] = UiPropertyKind.Color,
        ["coloroutlineb"] = UiPropertyKind.Color,

        ["winflag_visible"] = UiPropertyKind.Bool,
        ["winflag_enable"] = UiPropertyKind.Bool,
        ["winflag_enabled"] = UiPropertyKind.Bool,
        ["winflag_moveable"] = UiPropertyKind.Bool,
        ["winflag_sizeable"] = UiPropertyKind.Bool,
        ["winflag_sortable"] = UiPropertyKind.Bool,
        ["winflag_pbuff"] = UiPropertyKind.Bool,
        ["winflag_pbufftrans"] = UiPropertyKind.Bool,
        ["winflag_pbufferase"] = UiPropertyKind.Bool,
        ["winflag_pbuffvid"] = UiPropertyKind.Bool,
        ["winflag_alphablend"] = UiPropertyKind.Bool,
        ["winflag_acceptfocus"] = UiPropertyKind.Bool,
        ["winflag_mousetrans"] = UiPropertyKind.Bool,
        ["winflag_ignoremouse"] = UiPropertyKind.Bool,
        ["alphablend"] = UiPropertyKind.Bool,
        ["transparentbkg"] = UiPropertyKind.Bool,
        ["transparent"] = UiPropertyKind.Bool,
        ["edgeimage"] = UiPropertyKind.Bool,
        ["wrapped"] = UiPropertyKind.Bool,
        ["editable"] = UiPropertyKind.Bool,
        ["outline"] = UiPropertyKind.Bool,
        ["sunken"] = UiPropertyKind.Bool,
        ["sort"] = UiPropertyKind.Bool,
        ["sortable"] = UiPropertyKind.Bool,
        ["drop"] = UiPropertyKind.Bool,
        ["fill"] = UiPropertyKind.Bool,
        ["titlebar"] = UiPropertyKind.Bool,
        ["moveable"] = UiPropertyKind.Bool,
        ["sizeable"] = UiPropertyKind.Bool,
        ["sidebar"] = UiPropertyKind.Bool,
        ["paint"] = UiPropertyKind.Bool,
        ["defaultkeys"] = UiPropertyKind.Bool,
        ["closevisible"] = UiPropertyKind.Bool,
        ["gobackvisible"] = UiPropertyKind.Bool,
        ["minmaxvisible"] = UiPropertyKind.Bool,
        ["closedisabled"] = UiPropertyKind.Bool,
        ["gobackdisabled"] = UiPropertyKind.Bool,
        ["minmaxdisabled"] = UiPropertyKind.Bool,
        ["hscrollbar"] = UiPropertyKind.Bool,
        ["vscrollbar"] = UiPropertyKind.Bool,
        ["singleline"] = UiPropertyKind.Bool,
        ["caretvisible"] = UiPropertyKind.Bool,
        ["allowundo"] = UiPropertyKind.Bool,
        ["tips"] = UiPropertyKind.Bool,
        ["autosize"] = UiPropertyKind.Bool,
        ["showcaption"] = UiPropertyKind.Bool,
        ["wrapcaption"] = UiPropertyKind.Bool,
        ["shiftcaption"] = UiPropertyKind.Bool,
        ["autonumber"] = UiPropertyKind.Bool,
        ["autonumbercomma"] = UiPropertyKind.Bool,
        ["autonumbercurrency"] = UiPropertyKind.Bool,
        ["toggle"] = UiPropertyKind.Bool,
        ["triggerondown"] = UiPropertyKind.Bool,
    };

    public static UiPropertyKind GetKind(string? propertyName) =>
        !string.IsNullOrEmpty(propertyName) && Kinds.TryGetValue(propertyName, out var kind) ? kind : UiPropertyKind.String;
}
