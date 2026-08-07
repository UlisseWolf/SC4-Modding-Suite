using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SC4ModdingSuite.Models;
using AvColor = Avalonia.Media.Color;
using AvPoint = Avalonia.Point;
using ImgImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace SC4ModdingSuite.Views;

/// <summary>
/// Interactive viewer for parsed S3D models: drag to orbit (yaw/pitch), scroll to zoom.
/// Draws directly via <see cref="DrawingContext"/> for performance (avoids creating
/// thousands of individual XAML shape elements for higher-poly building models) rather
/// than composing a tree of Avalonia shapes.
///
/// Two independent toggles (bound to pairs of RadioButtons - "dot buttons" - in
/// MainWindow.axaml) control the render:
/// <list type="bullet">
/// <item><see cref="Solid"/>: Wireframe (edges only) vs. Solid (filled, textured triangles).</item>
/// <item><see cref="Night"/>: Day vs. night lighting.</item>
/// </list>
///
/// Solid mode is a flat-shaded/flat-textured approximation, not a full per-pixel
/// rasterizer: each triangle is filled with a single color sampled from the resolved
/// texture (see <see cref="Texture"/>) at that triangle's average UV coordinate, then
/// darkened/tinted by a simple directional-light dot product (recomputed per triangle
/// from its rotated face normal) depending on <see cref="Night"/>. Triangles are drawn
/// back-to-front (painter's algorithm, sorted by rotated depth) for correct-looking
/// occlusion without a full depth buffer. This is deliberately a lightweight
/// approximation appropriate for a "viewer", not a game-quality renderer.
/// </summary>
public sealed class S3DViewerControl : Control
{
    public static readonly StyledProperty<S3DModel?> ModelProperty =
        AvaloniaProperty.Register<S3DViewerControl, S3DModel?>(nameof(Model));

    public S3DModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Resolved diffuse texture for Solid mode (from the model's primary material), or null if none/unavailable.</summary>
    public static readonly StyledProperty<ImgImage?> TextureProperty =
        AvaloniaProperty.Register<S3DViewerControl, ImgImage?>(nameof(Texture));

    public ImgImage? Texture
    {
        get => GetValue(TextureProperty);
        set => SetValue(TextureProperty, value);
    }

    /// <summary>False = wireframe, true = solid/textured.</summary>
    public static readonly StyledProperty<bool> SolidProperty =
        AvaloniaProperty.Register<S3DViewerControl, bool>(nameof(Solid));

    public bool Solid
    {
        get => GetValue(SolidProperty);
        set => SetValue(SolidProperty, value);
    }

    /// <summary>False = day lighting, true = night lighting.</summary>
    public static readonly StyledProperty<bool> NightProperty =
        AvaloniaProperty.Register<S3DViewerControl, bool>(nameof(Night));

    public bool Night
    {
        get => GetValue(NightProperty);
        set => SetValue(NightProperty, value);
    }

    // Control (unlike TemplatedControl) has no Background property of its own, so this
    // viewer declares one and paints it manually in Render() below.
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<S3DViewerControl, IBrush?>(nameof(Background));

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    private double _yaw = 35;
    private double _pitch = -20;
    private double _zoom = 1.0;
    private AvPoint? _lastPointerPosition;

    private static readonly IPen WirePenDay = new Pen(new SolidColorBrush(AvColor.Parse("#FF9900")), 1);
    private static readonly IPen WirePenNight = new Pen(new SolidColorBrush(AvColor.Parse("#3355AA")), 1);
    private static readonly IPen SolidEdgePen = new Pen(new SolidColorBrush(AvColor.Parse("#00000080")), 0.5);

    static S3DViewerControl()
    {
        AffectsRender<S3DViewerControl>(ModelProperty, BackgroundProperty, TextureProperty, SolidProperty, NightProperty);
    }

    public S3DViewerControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>Resets the view to the default orbit/zoom - called when a new model is loaded.</summary>
    public void ResetView()
    {
        _yaw = 35;
        _pitch = -20;
        _zoom = 1.0;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _lastPointerPosition = e.GetPosition(this);
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointerPosition is not { } last)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - last;
        _lastPointerPosition = current;

        _yaw += delta.X * 0.5;
        _pitch = Math.Clamp(_pitch - delta.Y * 0.5, -89, 89);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lastPointerPosition = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), 0.1, 20);
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (Background is { } background && bounds.Width > 0 && bounds.Height > 0)
        {
            context.FillRectangle(background, new Rect(bounds.Size));
        }

        var model = Model;
        if (model is null || model.VertexBlocks.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // Flatten all vertex blocks into one indexable array, tracking each group's start
        // offset so triangle indices (which are local to their own VERT block) resolve
        // correctly against the flattened array. UVs are flattened the same way, aligned
        // 1:1 with positions when that block's format actually has them.
        var blockOffsets = new int[model.VertexBlocks.Count];
        var allPositions = new List<Vector3>();
        var allUvs = new List<Vector2?>();
        for (var i = 0; i < model.VertexBlocks.Count; i++)
        {
            var block = model.VertexBlocks[i];
            blockOffsets[i] = allPositions.Count;
            allPositions.AddRange(block.Positions);
            for (var v = 0; v < block.Positions.Count; v++)
            {
                allUvs.Add(block.HasUvs ? block.Uvs[v] : null);
            }
        }

        if (allPositions.Count == 0)
        {
            return;
        }

        var min = allPositions[0];
        var max = allPositions[0];
        foreach (var p in allPositions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        var center = (min + max) / 2f;
        var extent = max - min;
        var radius = MathF.Max(0.001f, MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z)));

        var yawRad = (float)(_yaw * Math.PI / 180.0);
        var pitchRad = (float)(_pitch * Math.PI / 180.0);
        var rotation = Matrix4x4.CreateRotationY(yawRad) * Matrix4x4.CreateRotationX(pitchRad);

        var scale = Math.Min(bounds.Width, bounds.Height) * 0.4 / radius * _zoom;
        var cx = bounds.Width / 2.0;
        var cy = bounds.Height / 2.0;

        // Pre-compute rotated (view-space) positions once - reused for projection, face
        // normals, and depth sorting.
        var rotatedPositions = new Vector3[allPositions.Count];
        for (var i = 0; i < allPositions.Count; i++)
        {
            rotatedPositions[i] = Vector3.Transform(allPositions[i] - center, rotation);
        }

        AvPoint ProjectRotated(Vector3 rotated) => new(cx + rotated.X * scale, cy - rotated.Y * scale);

        var solid = Solid;
        var night = Night;
        var wirePen = night ? WirePenNight : WirePenDay;
        var texture = solid ? Texture : null;

        if (!solid)
        {
            // Wireframe mode: simple, no sorting/shading needed.
            foreach (var (group, a, b, c) in model.EnumerateTriangles())
            {
                if (!TryResolve(group, a, b, c, blockOffsets, allPositions.Count, out var ia, out var ib, out var ic))
                {
                    continue;
                }

                var pa = ProjectRotated(rotatedPositions[ia]);
                var pb = ProjectRotated(rotatedPositions[ib]);
                var pc = ProjectRotated(rotatedPositions[ic]);

                context.DrawLine(wirePen, pa, pb);
                context.DrawLine(wirePen, pb, pc);
                context.DrawLine(wirePen, pc, pa);
            }

            return;
        }

        // Solid mode: collect triangles with their depth for back-to-front painting.
        var triangles = new List<(int A, int B, int C, float Depth)>();
        foreach (var (group, a, b, c) in model.EnumerateTriangles())
        {
            if (!TryResolve(group, a, b, c, blockOffsets, allPositions.Count, out var ia, out var ib, out var ic))
            {
                continue;
            }

            var depth = (rotatedPositions[ia].Z + rotatedPositions[ib].Z + rotatedPositions[ic].Z) / 3f;
            triangles.Add((ia, ib, ic, depth));
        }

        triangles.Sort((t1, t2) => t1.Depth.CompareTo(t2.Depth));

        foreach (var (ia, ib, ic, _) in triangles)
        {
            var va = rotatedPositions[ia];
            var vb = rotatedPositions[ib];
            var vc = rotatedPositions[ic];

            var normal = Vector3.Cross(vb - va, vc - va);
            if (normal.LengthSquared() > 0)
            {
                normal = Vector3.Normalize(normal);
            }

            var brightness = ComputeLight(normal, night);
            var baseColor = SampleTriangleColor(texture, allUvs[ia], allUvs[ib], allUvs[ic]);
            var shaded = Shade(baseColor, brightness, night);

            var pa = ProjectRotated(va);
            var pb = ProjectRotated(vb);
            var pc = ProjectRotated(vc);

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(pa, isFilled: true);
                ctx.LineTo(pb);
                ctx.LineTo(pc);
                ctx.EndFigure(true);
            }

            context.DrawGeometry(new SolidColorBrush(shaded), SolidEdgePen, geometry);
        }
    }

    private static bool TryResolve(int group, int a, int b, int c, int[] blockOffsets, int totalCount, out int ia, out int ib, out int ic)
    {
        ia = ib = ic = 0;
        if (group >= blockOffsets.Length)
        {
            return false;
        }

        var offset = blockOffsets[group];
        ia = offset + a;
        ib = offset + b;
        ic = offset + c;
        return ia < totalCount && ib < totalCount && ic < totalCount;
    }

    /// <summary>
    /// Simple directional "headlamp" light fixed relative to the camera (not the model),
    /// so lighting stays consistent while orbiting - a reasonable simplification for a
    /// model viewer rather than a fixed world-space light.
    /// </summary>
    private static float ComputeLight(Vector3 normalViewSpace, bool night)
    {
        var lightDir = night
            ? Vector3.Normalize(new Vector3(-0.3f, 0.4f, 0.9f))
            : Vector3.Normalize(new Vector3(0.4f, 0.8f, 0.6f));

        var nDotL = Math.Max(0f, Vector3.Dot(normalViewSpace, lightDir));
        var ambient = night ? 0.12f : 0.35f;
        var diffuse = night ? 0.30f : 0.75f;
        return ambient + diffuse * nDotL;
    }

    private static (byte R, byte G, byte B) SampleTriangleColor(ImgImage? texture, Vector2? uvA, Vector2? uvB, Vector2? uvC)
    {
        if (texture is null || uvA is null || uvB is null || uvC is null)
        {
            // No texture (or this vertex format has no UVs) - a neutral mid-grey so
            // shading is still visible instead of defaulting to flat white/black.
            return (190, 190, 190);
        }

        var u = (uvA.Value.X + uvB.Value.X + uvC.Value.X) / 3f;
        var v = (uvA.Value.Y + uvB.Value.Y + uvC.Value.Y) / 3f;

        // Wrap to [0,1) - S3D UVs commonly tile beyond that range.
        u -= MathF.Floor(u);
        v -= MathF.Floor(v);

        var x = Math.Clamp((int)(u * texture.Width), 0, texture.Width - 1);
        var y = Math.Clamp((int)(v * texture.Height), 0, texture.Height - 1);

        var pixel = texture[x, y];
        return (pixel.R, pixel.G, pixel.B);
    }

    private static AvColor Shade((byte R, byte G, byte B) baseColor, float brightness, bool night)
    {
        // Warm tint for day, cool blue tint for night.
        var (tintR, tintG, tintB) = night ? (0.55f, 0.65f, 0.95f) : (1.05f, 1.0f, 0.92f);

        byte Apply(byte channel, float tint) =>
            (byte)Math.Clamp(channel * brightness * tint, 0, 255);

        return new AvColor(255, Apply(baseColor.R, tintR), Apply(baseColor.G, tintG), Apply(baseColor.B, tintB));
    }
}
