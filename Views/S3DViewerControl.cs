using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

    /// <summary>
    /// Every material's own resolved texture (material index -&gt; bitmap), day/night aware
    /// like <see cref="Texture"/>. Multi-material models sample each triangle's own group
    /// against its own material's texture here (see <see cref="S3DModel.GetMaterialIndex"/>)
    /// instead of the single <see cref="Texture"/> being applied to every group - which is
    /// what previously made multi-material models look scrambled/misaligned in Solid mode,
    /// since each group's UVs are only meaningful against the texture they were authored for.
    /// <see cref="Texture"/> (the primary material's texture) is kept as the fallback for any
    /// group whose material can't be resolved here, so nothing regresses for simpler,
    /// single-material models.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyDictionary<int, ImgImage>?> MaterialTexturesProperty =
        AvaloniaProperty.Register<S3DViewerControl, IReadOnlyDictionary<int, ImgImage>?>(nameof(MaterialTextures));

    public IReadOnlyDictionary<int, ImgImage>? MaterialTextures
    {
        get => GetValue(MaterialTexturesProperty);
        set => SetValue(MaterialTexturesProperty, value);
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

    /// <summary>Which animation keyframe to draw (see <see cref="S3DModel.EnumerateTriangles"/>) - ignored for models with no animation/a single frame per mesh, which is the overwhelming majority.</summary>
    public static readonly StyledProperty<int> CurrentFrameProperty =
        AvaloniaProperty.Register<S3DViewerControl, int>(nameof(CurrentFrame));

    public int CurrentFrame
    {
        get => GetValue(CurrentFrameProperty);
        set => SetValue(CurrentFrameProperty, value);
    }

    /// <summary>Mesh/group indices to skip entirely - the S3D Editor's per-group visibility toggles.</summary>
    public static readonly StyledProperty<IReadOnlySet<int>?> HiddenGroupsProperty =
        AvaloniaProperty.Register<S3DViewerControl, IReadOnlySet<int>?>(nameof(HiddenGroups));

    public IReadOnlySet<int>? HiddenGroups
    {
        get => GetValue(HiddenGroupsProperty);
        set => SetValue(HiddenGroupsProperty, value);
    }

    /// <summary>
    /// The currently selected/highlighted triangle - (Group, A, B, C) local vertex indices
    /// within that group's own VERT block, same tuple shape <see cref="S3DModel.EnumerateTriangles"/>
    /// yields. Set from the ViewModel (a grid row selection in the S3D Editor's Indices
    /// grid) to draw a highlight outline here; also read back after a click-to-pick (see
    /// <see cref="PickCommand"/>) so the two stay in sync regardless of which side changed it.
    /// </summary>
    public static readonly StyledProperty<(int Group, int A, int B, int C)?> HighlightTriangleProperty =
        AvaloniaProperty.Register<S3DViewerControl, (int Group, int A, int B, int C)?>(nameof(HighlightTriangle));

    public (int Group, int A, int B, int C)? HighlightTriangle
    {
        get => GetValue(HighlightTriangleProperty);
        set => SetValue(HighlightTriangleProperty, value);
    }

    /// <summary>
    /// Invoked on a plain click (press+release with no drag in between - a drag still only
    /// orbits the view, as before) with a (Group, A, B, C)? parameter: the picked triangle's
    /// local vertex indices, or null if the click didn't hit any triangle. This is the
    /// viewer-&gt;grid half of the S3D Editor's bidirectional row&lt;-&gt;triangle selection link.
    /// </summary>
    public static readonly StyledProperty<ICommand?> PickCommandProperty =
        AvaloniaProperty.Register<S3DViewerControl, ICommand?>(nameof(PickCommand));

    public ICommand? PickCommand
    {
        get => GetValue(PickCommandProperty);
        set => SetValue(PickCommandProperty, value);
    }

    private double _yaw = 35;
    private double _pitch = -20;
    private double _zoom = 1.0;
    private AvPoint? _lastPointerPosition;
    private AvPoint? _pressPosition;
    private bool _dragged;

    private static readonly IPen WirePenDay = new Pen(new SolidColorBrush(AvColor.Parse("#FF9900")), 1);
    private static readonly IPen WirePenNight = new Pen(new SolidColorBrush(AvColor.Parse("#3355AA")), 1);
    private static readonly IPen HighlightPen = new Pen(new SolidColorBrush(AvColor.Parse("#FF33FF")), 2.5);

    static S3DViewerControl()
    {
        AffectsRender<S3DViewerControl>(ModelProperty, BackgroundProperty, TextureProperty, MaterialTexturesProperty, SolidProperty, NightProperty, CurrentFrameProperty, HiddenGroupsProperty, HighlightTriangleProperty);
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
        _pressPosition = _lastPointerPosition;
        _dragged = false;
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

        if (Math.Abs(delta.X) > 0.5 || Math.Abs(delta.Y) > 0.5)
        {
            _dragged = true;
        }

        _yaw += delta.X * 0.5;
        _pitch = Math.Clamp(_pitch - delta.Y * 0.5, -89, 89);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        // A press+release with no drag in between is a "click" - used to pick a triangle
        // (see PickCommand) instead of just ending an orbit-drag.
        if (!_dragged && _pressPosition is { } pressPos && PickCommand is { } command)
        {
            var picked = PickTriangle(pressPos);
            if (command.CanExecute(picked))
            {
                command.Execute(picked);
            }
        }

        _lastPointerPosition = null;
        _pressPosition = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _zoom = Math.Clamp(_zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), 0.1, 20);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>Camera-space geometry shared by <see cref="Render"/> and <see cref="PickTriangle"/> - computed once per call site rather than duplicated, since both need the same rotate/project pipeline.</summary>
    private readonly record struct Geometry3D(int[] BlockOffsets, Vector3[] RotatedPositions, double Cx, double Cy, double Scale);

    private Geometry3D? BuildGeometry(S3DModel model, Rect bounds)
    {
        if (model.VertexBlocks.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        // Flatten all vertex blocks into one indexable array, tracking each group's start
        // offset so triangle indices (which are local to their own VERT block) resolve
        // correctly against the flattened array.
        var blockOffsets = new int[model.VertexBlocks.Count];
        var allPositions = new List<Vector3>();
        for (var i = 0; i < model.VertexBlocks.Count; i++)
        {
            var block = model.VertexBlocks[i];
            blockOffsets[i] = allPositions.Count;
            allPositions.AddRange(block.Positions);
        }

        if (allPositions.Count == 0)
        {
            return null;
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

        return new Geometry3D(blockOffsets, rotatedPositions, cx, cy, scale);
    }

    private static AvPoint Project(in Geometry3D g, Vector3 rotated) =>
        new(g.Cx + rotated.X * g.Scale, g.Cy - rotated.Y * g.Scale);

    /// <summary>
    /// Hit-tests a click position (control-local coordinates) against every currently
    /// visible triangle (same set <see cref="Render"/> draws - respects CurrentFrame and
    /// HiddenGroups), returning the nearest (smallest view-space depth) hit, or null if
    /// nothing was hit. This is what drives the grid-&gt;viewer/viewer-&gt;grid selection link
    /// together with <see cref="PickCommand"/>.
    /// </summary>
    private (int Group, int A, int B, int C)? PickTriangle(AvPoint clickPoint)
    {
        var model = Model;
        if (model is null)
        {
            return null;
        }

        if (BuildGeometry(model, Bounds) is not { } g)
        {
            return null;
        }

        (int Group, int A, int B, int C)? best = null;
        var bestDepth = float.MaxValue;

        foreach (var (group, a, b, c) in model.EnumerateTriangles(CurrentFrame, HiddenGroups))
        {
            if (!TryResolve(group, a, b, c, g.BlockOffsets, g.RotatedPositions.Length, out var ia, out var ib, out var ic))
            {
                continue;
            }

            var pa = Project(g, g.RotatedPositions[ia]);
            var pb = Project(g, g.RotatedPositions[ib]);
            var pc = Project(g, g.RotatedPositions[ic]);
            if (!PointInTriangle(clickPoint, pa, pb, pc))
            {
                continue;
            }

            var depth = (g.RotatedPositions[ia].Z + g.RotatedPositions[ib].Z + g.RotatedPositions[ic].Z) / 3f;
            if (depth < bestDepth)
            {
                bestDepth = depth;
                best = (group, a, b, c);
            }
        }

        return best;
    }

    private static bool PointInTriangle(AvPoint p, AvPoint a, AvPoint b, AvPoint c)
    {
        var area = Edge(a, b, c.X, c.Y);
        if (Math.Abs(area) < 1e-6)
        {
            return false;
        }

        var w0 = Edge(b, c, p.X, p.Y) / area;
        var w1 = Edge(c, a, p.X, p.Y) / area;
        var w2 = Edge(a, b, p.X, p.Y) / area;
        return w0 >= 0 && w1 >= 0 && w2 >= 0;
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
        if (model is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (BuildGeometry(model, bounds) is not { } g)
        {
            return;
        }

        var blockOffsets = g.BlockOffsets;
        var rotatedPositions = g.RotatedPositions;

        void DrawHighlight(IPen pen)
        {
            if (HighlightTriangle is not { } h)
            {
                return;
            }

            if (!TryResolve(h.Group, h.A, h.B, h.C, blockOffsets, rotatedPositions.Length, out var ia, out var ib, out var ic))
            {
                return;
            }

            var pa = Project(g, rotatedPositions[ia]);
            var pb = Project(g, rotatedPositions[ib]);
            var pc = Project(g, rotatedPositions[ic]);
            context.DrawLine(pen, pa, pb);
            context.DrawLine(pen, pb, pc);
            context.DrawLine(pen, pc, pa);
        }

        var solid = Solid;
        var night = Night;
        var wirePen = night ? WirePenNight : WirePenDay;
        var texture = solid ? Texture : null;

        if (!solid)
        {
            // Wireframe mode: simple, no sorting/shading needed.
            foreach (var (group, a, b, c) in model.EnumerateTriangles(CurrentFrame, HiddenGroups))
            {
                if (!TryResolve(group, a, b, c, blockOffsets, rotatedPositions.Length, out var ia, out var ib, out var ic))
                {
                    continue;
                }

                var pa = Project(g, rotatedPositions[ia]);
                var pb = Project(g, rotatedPositions[ib]);
                var pc = Project(g, rotatedPositions[ic]);

                context.DrawLine(wirePen, pa, pb);
                context.DrawLine(wirePen, pb, pc);
                context.DrawLine(wirePen, pc, pa);
            }

            DrawHighlight(HighlightPen);
            return;
        }

        // Solid mode: collect triangles with their depth for back-to-front painting, and -
        // critically - the texture that group's own material actually uses (falling back to
        // the single "primary" Texture only when that group's material can't be resolved),
        // instead of sampling every group's UVs against one texture for the whole model.
        var materialTextures = MaterialTextures;
        var triangles = new List<(int A, int B, int C, float Depth, ImgImage? Texture)>();
        foreach (var (group, a, b, c) in model.EnumerateTriangles(CurrentFrame, HiddenGroups))
        {
            if (!TryResolve(group, a, b, c, blockOffsets, rotatedPositions.Length, out var ia, out var ib, out var ic))
            {
                continue;
            }

            var groupTexture = texture;
            if (materialTextures is not null
                && model.GetMaterialIndex(CurrentFrame, group) is { } materialIndex
                && materialTextures.TryGetValue(materialIndex, out var resolved))
            {
                groupTexture = resolved;
            }

            var depth = (rotatedPositions[ia].Z + rotatedPositions[ib].Z + rotatedPositions[ic].Z) / 3f;
            triangles.Add((ia, ib, ic, depth, groupTexture));
        }

        triangles.Sort((t1, t2) => t1.Depth.CompareTo(t2.Depth));

        // UVs, flattened the same way positions were in BuildGeometry, aligned 1:1 with
        // rotatedPositions when that vertex's block format actually has them - only needed
        // for solid/textured mode, so computed here rather than in the shared geometry.
        var allUvs = new List<Vector2?>();
        foreach (var block in model.VertexBlocks)
        {
            for (var v = 0; v < block.Positions.Count; v++)
            {
                allUvs.Add(block.HasUvs ? block.Uvs[v] : null);
            }
        }

        // Real per-pixel rasterization into an offscreen bitmap - NOT one flat color per
        // triangle (that older approach made an actual FSH texture image invisible as
        // detail on the low-poly models typical of SC4 props/buildings: a handful of
        // triangles just showed a handful of solid colors). UVs are interpolated linearly
        // across each triangle in screen space, which is exact (not merely an
        // approximation) here because the projection used above is an orthographic-style
        // scale/rotate, not a perspective one - no perspective-correct division needed.
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        var pixels = new byte[pixelWidth * pixelHeight * 4]; // BGRA8888, starts fully transparent

        foreach (var (ia, ib, ic, _, triTexture) in triangles)
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

            RasterizeTriangle(
                pixels, pixelWidth, pixelHeight,
                Project(g, va), Project(g, vb), Project(g, vc),
                allUvs[ia], allUvs[ib], allUvs[ic],
                triTexture, brightness, night);
        }

        using var bitmap = new WriteableBitmap(
            new PixelSize(pixelWidth, pixelHeight), new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = bitmap.Lock())
        {
            var rowBytes = pixelWidth * 4;
            for (var y = 0; y < pixelHeight; y++)
            {
                Marshal.Copy(pixels, y * rowBytes, IntPtr.Add(fb.Address, y * fb.RowBytes), rowBytes);
            }
        }

        context.DrawImage(bitmap, new Rect(bitmap.PixelSize.ToSize(1)), new Rect(bounds.Size));
        DrawHighlight(HighlightPen);
    }

    /// <summary>
    /// Fills the on-screen triangle (pa,pb,pc) into <paramref name="pixels"/> (a BGRA8888
    /// buffer, <paramref name="width"/>x<paramref name="height"/>), sampling
    /// <paramref name="texture"/> at each pixel's barycentric-interpolated UV when all three
    /// UVs are available, or falling back to a flat neutral color otherwise (no texture
    /// resolved, or this vertex format has no UVs) - same fallback color as before.
    /// </summary>
    private static void RasterizeTriangle(
        byte[] pixels, int width, int height,
        AvPoint pa, AvPoint pb, AvPoint pc,
        Vector2? uvA, Vector2? uvB, Vector2? uvC,
        ImgImage? texture, float brightness, bool night)
    {
        var minX = Math.Max(0, (int)Math.Floor(Math.Min(pa.X, Math.Min(pb.X, pc.X))));
        var maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(pa.X, Math.Max(pb.X, pc.X))));
        var minY = Math.Max(0, (int)Math.Floor(Math.Min(pa.Y, Math.Min(pb.Y, pc.Y))));
        var maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(pa.Y, Math.Max(pb.Y, pc.Y))));
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        var area = Edge(pa, pb, pc.X, pc.Y);
        if (Math.Abs(area) < 1e-6)
        {
            return;
        }

        var hasUv = texture is not null && uvA is not null && uvB is not null && uvC is not null;
        var flat = hasUv ? new AvColor() : Shade((190, 190, 190), brightness, night);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                double px = x + 0.5, py = y + 0.5;
                var w0 = Edge(pb, pc, px, py) / area;
                var w1 = Edge(pc, pa, px, py) / area;
                var w2 = Edge(pa, pb, px, py) / area;
                if (w0 < 0 || w1 < 0 || w2 < 0)
                {
                    continue;
                }

                AvColor shaded;
                if (hasUv)
                {
                    var u = (float)(w0 * uvA!.Value.X + w1 * uvB!.Value.X + w2 * uvC!.Value.X);
                    var v = (float)(w0 * uvA.Value.Y + w1 * uvB.Value.Y + w2 * uvC.Value.Y);
                    u -= MathF.Floor(u);
                    v -= MathF.Floor(v);

                    var tx = Math.Clamp((int)(u * texture!.Width), 0, texture.Width - 1);
                    var ty = Math.Clamp((int)(v * texture.Height), 0, texture.Height - 1);
                    var pixel = texture[tx, ty];
                    shaded = Shade((pixel.R, pixel.G, pixel.B), brightness, night);
                }
                else
                {
                    shaded = flat;
                }

                var idx = (y * width + x) * 4;
                pixels[idx] = shaded.B;
                pixels[idx + 1] = shaded.G;
                pixels[idx + 2] = shaded.R;
                pixels[idx + 3] = 255;
            }
        }
    }

    private static double Edge(AvPoint p0, AvPoint p1, double px, double py) =>
        (p1.X - p0.X) * (py - p0.Y) - (p1.Y - p0.Y) * (px - p0.X);

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

    private static AvColor Shade((byte R, byte G, byte B) baseColor, float brightness, bool night)
    {
        // Warm tint for day, cool blue tint for night.
        var (tintR, tintG, tintB) = night ? (0.55f, 0.65f, 0.95f) : (1.05f, 1.0f, 0.92f);

        byte Apply(byte channel, float tint) =>
            (byte)Math.Clamp(channel * brightness * tint, 0, 255);

        return new AvColor(255, Apply(baseColor.R, tintR), Apply(baseColor.G, tintG), Apply(baseColor.B, tintB));
    }
}
