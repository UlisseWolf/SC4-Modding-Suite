using System;
using System.Collections.Generic;
using System.Linq;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>One selectable value in a Material Editor ComboBox (render state enums use small, fixed, non-contiguous byte codes - not a plain 0..N index - so each option carries its own on-disk value alongside its label).</summary>
public sealed record S3DMaterialOption(byte Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The fixed option lists for the Material Editor's render-state ComboBoxes - labels and
/// byte values ported directly from Ilive Reader's Dlg3DMMat::OnInitDialog (reader/Dlg3DMMat.cpp)
/// and its string table (DLG3DMAT_MSG4..MSG29 in res/res.rc).
/// </summary>
public static class S3DMaterialOptions
{
    public static IReadOnlyList<S3DMaterialOption> CompareFuncs { get; } = new[]
    {
        new S3DMaterialOption(0, "never"),
        new S3DMaterialOption(1, "less than"),
        new S3DMaterialOption(2, "equal"),
        new S3DMaterialOption(3, "less than or equal"),
        new S3DMaterialOption(4, "greater"),
        new S3DMaterialOption(5, "not equal"),
        new S3DMaterialOption(6, "greater than or equal"),
        new S3DMaterialOption(7, "always"),
    };

    public static IReadOnlyList<S3DMaterialOption> BlendFactors { get; } = new[]
    {
        new S3DMaterialOption(0, "zero"),
        new S3DMaterialOption(1, "one"),
        new S3DMaterialOption(2, "source color (dest only)"),
        new S3DMaterialOption(3, "one minus source color (dest only)"),
        new S3DMaterialOption(4, "source alpha"),
        new S3DMaterialOption(5, "one minus source alpha"),
        new S3DMaterialOption(8, "destination color (src only)"),
        new S3DMaterialOption(9, "one minus destination color (src only)"),
    };

    public static IReadOnlyList<S3DMaterialOption> WrapModes { get; } = new[]
    {
        new S3DMaterialOption(2, "clamp"),
        new S3DMaterialOption(3, "repeat"),
    };

    public static IReadOnlyList<S3DMaterialOption> MagFilters { get; } = new[]
    {
        new S3DMaterialOption(0, "nearest"),
        new S3DMaterialOption(1, "bilinear"),
    };

    public static IReadOnlyList<S3DMaterialOption> MinFilters { get; } = new[]
    {
        new S3DMaterialOption(0, "nearest"),
        new S3DMaterialOption(1, "bilinear"),
        new S3DMaterialOption(2, "nearest mipmap nearest"),
        new S3DMaterialOption(3, "linear mipmap nearest (bilinear with mipmapping)"),
        new S3DMaterialOption(4, "nearest mipmap linear"),
        new S3DMaterialOption(5, "linear mipmap linear (trilinear)"),
    };
}

/// <summary>
/// One row in the Material Editor's grid: one texture reference within one material
/// (Ilive Reader's Dlg3DMMat flattens the same way - one grid row per _s3d_material_elem,
/// "Group" column = the material's own index). Render state (flags/alpha+depth
/// func/blend factors/alpha threshold) belongs to the whole <see cref="Material"/> and is
/// shared by every row of that material; wrap mode/filtering belongs to this row's own
/// <see cref="Texture"/> - exactly Ilive's own split (see Dlg3DMMat::OnChanged, which writes
/// both pBlock->material_block.* and pTexture->* from the same control set in one go).
/// All edits commit immediately into the shared <see cref="S3DModel"/> object graph (same
/// "no buffering" convention as <see cref="S3DVertexRowViewModel"/>) - "APPLY/SAVE" on the
/// model is what persists it into the entry, same as the Geometry Editor.
/// </summary>
public sealed class S3DMaterialRowViewModel : ViewModelBase
{
    private readonly Action _onChanged;

    public S3DMaterialRowViewModel(S3DMaterial material, int materialIndex, S3DMaterialTexture texture, Action onChanged)
    {
        Material = material;
        MaterialIndex = materialIndex;
        Texture = texture;
        _onChanged = onChanged;
    }

    public S3DMaterial Material { get; }
    public S3DMaterialTexture Texture { get; }
    public int MaterialIndex { get; }

    public string GroupLabel => MaterialIndex.ToString();
    public string TextureIdHex => $"0x{Texture.TextureId:X8}";

    public string TextureName
    {
        get => Texture.Name;
        set { Texture.Name = value ?? string.Empty; OnPropertyChanged(); _onChanged(); }
    }

    // ---- Render state flags (Material.Flag bits - Dlg3DMMat::OnChanged) ----

    public bool AlphaTest { get => GetFlag(0x01); set => SetFlag(0x01, value); }
    public bool DepthTest { get => GetFlag(0x02); set => SetFlag(0x02, value); }
    public bool BackfaceCulling { get => GetFlag(0x08); set => SetFlag(0x08, value); }
    public bool FramebufferBlend { get => GetFlag(0x10); set => SetFlag(0x10, value); }
    public bool Texturing { get => GetFlag(0x20); set => SetFlag(0x20, value); }

    private bool GetFlag(uint bit) => (Material.Flag & bit) != 0;

    private void SetFlag(uint bit, bool value)
    {
        Material.Flag = value ? (Material.Flag | bit) : (Material.Flag & ~bit);
        OnPropertyChanged();
        _onChanged();
    }

    // ---- Alpha/depth func, blend factors, alpha threshold (Material-level) ----

    public S3DMaterialOption AlphaFunc
    {
        get => Find(S3DMaterialOptions.CompareFuncs, Material.AlphaFunc);
        set { Material.AlphaFunc = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    public S3DMaterialOption DepthFunc
    {
        get => Find(S3DMaterialOptions.CompareFuncs, Material.DepthFunc);
        set { Material.DepthFunc = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    public S3DMaterialOption SrcBlend
    {
        get => Find(S3DMaterialOptions.BlendFactors, Material.SrcBlendFactor);
        set { Material.SrcBlendFactor = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    public S3DMaterialOption DstBlend
    {
        get => Find(S3DMaterialOptions.BlendFactors, Material.DstBlendFactor);
        set { Material.DstBlendFactor = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    /// <summary>0-255 (on-disk field is a WORD, but Ilive's own edit control treats it as a byte-range threshold in practice).</summary>
    public int AlphaThreshold
    {
        get => Material.AlphaThreshold;
        set { Material.AlphaThreshold = (ushort)Math.Clamp(value, 0, 255); OnPropertyChanged(); _onChanged(); }
    }

    // ---- Wrap mode / filtering (this texture reference only) ----

    public S3DMaterialOption WrapModeS
    {
        get => Find(S3DMaterialOptions.WrapModes, Texture.WrapModeS);
        set { Texture.WrapModeS = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    public S3DMaterialOption WrapModeT
    {
        get => Find(S3DMaterialOptions.WrapModes, Texture.WrapModeT);
        set { Texture.WrapModeT = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    public S3DMaterialOption MagFilter
    {
        get => Find(S3DMaterialOptions.MagFilters, Texture.MagFilter);
        set { Texture.MagFilter = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    public S3DMaterialOption MinFilter
    {
        get => Find(S3DMaterialOptions.MinFilters, Texture.MinFilter);
        set { Texture.MinFilter = value.Value; OnPropertyChanged(); _onChanged(); }
    }

    private static S3DMaterialOption Find(IReadOnlyList<S3DMaterialOption> options, byte value) =>
        options.FirstOrDefault(o => o.Value == value) ?? options[0];
}
