using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// One prop=value pair on a &lt;LEGACY&gt; node. Implements <see cref="INotifyPropertyChanged"/>
/// so the type-aware editors in UiNodePropertiesDialog (checkbox/color/rect/xy fields -
/// see <see cref="Kind"/>) can bind straight to <see cref="Value"/>'s parsed sub-fields and
/// have edits through any of them (a checkbox, a color swatch's R field, ...) immediately
/// update <see cref="Value"/>'s own raw text too, and vice versa.
/// </summary>
public sealed class UiLegacyProp : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _key = string.Empty;
    public string Key
    {
        get => _key;
        set
        {
            if (_key == value)
            {
                return;
            }

            _key = value;
            OnChanged();
            // The specialized editor shown for this row (see UiNodePropertiesDialog.axaml's
            // DataGridTemplateColumn) depends on the property's *name* - renaming a row (a
            // rare edit, but the Key column is still plain, freely-editable text) can change
            // which editor should show for it.
            OnChanged(nameof(Kind));
            OnChanged(nameof(IsBool));
            OnChanged(nameof(IsColor));
            OnChanged(nameof(IsRect));
            OnChanged(nameof(IsXy));
            OnChanged(nameof(IsPlainText));
        }
    }

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnChanged();
            OnChanged(nameof(BoolValue));
            OnChanged(nameof(RectLeft));
            OnChanged(nameof(RectTop));
            OnChanged(nameof(RectRight));
            OnChanged(nameof(RectBottom));
            OnChanged(nameof(XyX));
            OnChanged(nameof(XyY));
            OnChanged(nameof(ColorR));
            OnChanged(nameof(ColorG));
            OnChanged(nameof(ColorB));
            OnChanged(nameof(ColorPreview));
        }
    }

    /// <summary>Which specialized editor this property's row should show - see <see cref="UiPropertyTypeCatalog"/>.</summary>
    public UiPropertyKind Kind => UiPropertyTypeCatalog.GetKind(Key);

    public bool IsBool => Kind == UiPropertyKind.Bool;
    public bool IsColor => Kind == UiPropertyKind.Color;
    public bool IsRect => Kind == UiPropertyKind.Rect;
    public bool IsXy => Kind == UiPropertyKind.Xy;
    public bool IsPlainText => Kind == UiPropertyKind.String;

    public bool BoolValue
    {
        get => UiValueFormat.ParseBool(Value);
        set => Value = UiValueFormat.FormatBool(value);
    }

    // decimal, not int/byte: NumericUpDown.Value (bound to these in
    // UiNodePropertiesDialog.axaml's type-aware editors) is itself a decimal - keeping
    // these the same type avoids needing a value converter for every single field.

    public decimal RectLeft
    {
        get => UiValueFormat.ParseRect(Value).Left;
        set { var r = UiValueFormat.ParseRect(Value); Value = UiValueFormat.FormatRect((int)value, r.Top, r.Right, r.Bottom); }
    }

    public decimal RectTop
    {
        get => UiValueFormat.ParseRect(Value).Top;
        set { var r = UiValueFormat.ParseRect(Value); Value = UiValueFormat.FormatRect(r.Left, (int)value, r.Right, r.Bottom); }
    }

    public decimal RectRight
    {
        get => UiValueFormat.ParseRect(Value).Right;
        set { var r = UiValueFormat.ParseRect(Value); Value = UiValueFormat.FormatRect(r.Left, r.Top, (int)value, r.Bottom); }
    }

    public decimal RectBottom
    {
        get => UiValueFormat.ParseRect(Value).Bottom;
        set { var r = UiValueFormat.ParseRect(Value); Value = UiValueFormat.FormatRect(r.Left, r.Top, r.Right, (int)value); }
    }

    public decimal XyX
    {
        get => UiValueFormat.ParseXy(Value).X;
        set { var p = UiValueFormat.ParseXy(Value); Value = UiValueFormat.FormatXy((int)value, p.Y); }
    }

    public decimal XyY
    {
        get => UiValueFormat.ParseXy(Value).Y;
        set { var p = UiValueFormat.ParseXy(Value); Value = UiValueFormat.FormatXy(p.X, (int)value); }
    }

    public decimal ColorR
    {
        get => UiValueFormat.ParseColor(Value).R;
        set { var c = UiValueFormat.ParseColor(Value); Value = UiValueFormat.FormatColor(ClampByte(value), c.G, c.B); }
    }

    public decimal ColorG
    {
        get => UiValueFormat.ParseColor(Value).G;
        set { var c = UiValueFormat.ParseColor(Value); Value = UiValueFormat.FormatColor(c.R, ClampByte(value), c.B); }
    }

    public decimal ColorB
    {
        get => UiValueFormat.ParseColor(Value).B;
        set { var c = UiValueFormat.ParseColor(Value); Value = UiValueFormat.FormatColor(c.R, c.G, ClampByte(value)); }
    }

    private static byte ClampByte(decimal value) => (byte)System.Math.Clamp(value, 0, 255);

    /// <summary>Swatch preview brush for the color-kind editor row.</summary>
    public Avalonia.Media.IBrush ColorPreview
    {
        get
        {
            var c = UiValueFormat.ParseColor(Value);
            return new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(255, c.R, c.G, c.B));
        }
    }
}

/// <summary>
/// One node of a UI entry's element tree (Ilive Reader's <c>_ui_legacy</c>/<c>CUIParse</c>).
/// UI entries (TGI TypeID 0x00000000, per Ilive Reader's <c>cl_entry.cpp</c> classification)
/// are plain text, not binary - a nested <c>&lt;LEGACY key=val ...&gt;</c> / <c>&lt;CHILDREN&gt;...&lt;/CHILDREN&gt;</c>
/// tag format, one node per in-game UI element (button, caption, background, ...).
/// </summary>
public sealed class UiLegacyNode
{
    public bool IsRoot { get; set; }
    public List<UiLegacyProp> Properties { get; } = new();
    public List<UiLegacyNode> Children { get; } = new();
    public UiLegacyNode? Parent { get; set; }

    public string? GetProp(string key)
    {
        foreach (var p in Properties)
        {
            if (p.Key == key)
            {
                return p.Value;
            }
        }

        return null;
    }

    /// <summary>Sets an existing prop's value, or adds a new one if this node doesn't have <paramref name="key"/> yet - used by the "ALL ELEMENTS" grid (UiElementsGridDialog) to edit several nodes' common properties inline, spreadsheet-style, matching Ilive Reader's own FormUI grid.</summary>
    public void SetProp(string key, string value)
    {
        foreach (var p in Properties)
        {
            if (p.Key == key)
            {
                p.Value = value;
                return;
            }
        }

        Properties.Add(new UiLegacyProp { Key = key, Value = value });
    }
}

/// <summary>
/// Port of Ilive Reader's <c>CUIParse::Parse</c>/<c>Encode</c> (<c>or_dat/sim019.cpp</c>) -
/// same tag grammar, same quote-aware tokenizing (<c>FindWord</c>: a delimiter inside
/// <c>"..."</c> doesn't split), so files round-trip byte-for-byte compatible.
/// </summary>
public static class UiLegacyParser
{
    /// <summary>Finds the next unquoted occurrence of <paramref name="delimiter"/> in <paramref name="text"/> starting at <paramref name="start"/>, or text.Length if none (mirrors Ilive Reader's FindWord, which toggles an "inside quotes" flag on each '"' and ignores the delimiter while inside).</summary>
    private static int FindWord(string text, char delimiter, int start)
    {
        var inQuotes = false;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (text[i] == delimiter && !inQuotes)
            {
                return i;
            }
        }

        return text.Length;
    }

    public static UiLegacyNode Parse(string text)
    {
        var root = new UiLegacyNode { IsRoot = true };
        var pos = 0;
        ParseLevel(text, ref pos, root);
        return root;
    }

    private static void ParseLevel(string text, ref int pos, UiLegacyNode parent)
    {
        UiLegacyNode? active = null;

        while (pos < text.Length)
        {
            var open = text.IndexOf('<', pos);
            if (open == -1)
            {
                break;
            }

            var close = FindWord(text, '>', open);
            if (close >= text.Length)
            {
                break;
            }

            var tag = text.Substring(open, close - open + 1);
            var name = ExtractName(tag);
            pos = close + 1;

            if (name == "CHILDREN")
            {
                if (active is not null)
                {
                    ParseLevel(text, ref pos, active);
                }
            }
            else if (name == "LEGACY")
            {
                var inner = tag.Trim();
                inner = inner.Substring(0, inner.Length - 1); // drop trailing '>'
                inner = inner.Substring("<LEGACY".Length); // drop leading "<LEGACY"

                var node = new UiLegacyNode { Parent = parent };
                ExtractLine(inner, node);
                parent.Children.Add(node);
                active = node;
            }
            else if (name == "/CHILDREN")
            {
                return;
            }
        }
    }

    private static string ExtractName(string tag)
    {
        for (var i = 0; i < tag.Length; i++)
        {
            if (tag[i] == ' ')
            {
                return tag.Substring(1, i - 1);
            }
        }

        return tag.Length >= 2 ? tag.Substring(1, tag.Length - 2) : tag;
    }

    private static void ExtractLine(string text, UiLegacyNode node)
    {
        var pos = 0;
        while (pos < text.Length)
        {
            var spacePos = FindWord(text, ' ', pos);
            var token = text.Substring(pos, spacePos - pos);
            pos = spacePos + 1;

            if (token.Length == 0)
            {
                continue;
            }

            var eq = token.IndexOf('=');
            if (eq != -1)
            {
                node.Properties.Add(new UiLegacyProp { Key = token.Substring(0, eq), Value = token.Substring(eq + 1) });
            }
            else
            {
                node.Properties.Add(new UiLegacyProp { Key = token, Value = "Y" });
            }
        }
    }

    public static string Encode(UiLegacyNode root)
    {
        var sb = new StringBuilder("# Generated by SC4ModdingSuite UI Editor\r\n");
        EncodeNode(root, sb, -1);
        return sb.ToString();
    }

    private static void EncodeNode(UiLegacyNode node, StringBuilder sb, int level)
    {
        var indent = new string(' ', System.Math.Max(0, level) * 3);

        if (node.Properties.Count > 0)
        {
            sb.Append(indent).Append("<LEGACY ");
            foreach (var prop in node.Properties)
            {
                if (prop.Key.Length == 0)
                {
                    continue;
                }

                sb.Append(prop.Key).Append('=').Append(prop.Value).Append(' ');
            }

            sb.Append(">\r\n");
        }

        var hasChildren = !node.IsRoot && node.Children.Count > 0;
        if (hasChildren)
        {
            sb.Append(indent).Append("<CHILDREN>\r\n");
        }

        foreach (var child in node.Children)
        {
            EncodeNode(child, sb, level + 1);
        }

        if (hasChildren)
        {
            sb.Append(indent).Append("</CHILDREN>\r\n");
        }
    }
}
