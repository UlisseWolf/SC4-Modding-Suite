using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SC4ModdingSuite.Models;
using AvColor = Avalonia.Media.Color;
using AvPoint = Avalonia.Point;
using AvBitmap = Avalonia.Media.Imaging.Bitmap;
using ImgImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using Vector2 = System.Numerics.Vector2;

namespace SC4ModdingSuite.Views;

/// <summary>
/// Interactive 2D UV editor (Ilive Reader's Tab3DMUV equivalent): draws the resolved
/// diffuse texture as a background, overlays the editing group's UV points connected by
/// their triangle wireframe (same visual as Tab3DMUV::Paint - white edges, blue point
/// markers, white highlight for the selected point), and lets you drag a point to edit its
/// U/V directly. Sized to the (zoomed) texture and meant to sit inside a ScrollViewer for
/// panning - native scrollbars instead of porting Tab3DMUV's own manual SCROLLINFO
/// handling. Zoom is driven externally (the dialog's +/- buttons, same as Ilive's
/// BT_zoomp/BT_zoomm) rather than scroll-to-zoom, matching the original.
/// </summary>
public sealed class S3DUVEditorControl : Control
{
    public static readonly StyledProperty<S3DVertexBlock?> VertexBlockProperty =
        AvaloniaProperty.Register<S3DUVEditorControl, S3DVertexBlock?>(nameof(VertexBlock));

    public S3DVertexBlock? VertexBlock
    {
        get => GetValue(VertexBlockProperty);
        set => SetValue(VertexBlockProperty, value);
    }

    public static readonly StyledProperty<S3DIndexBlock?> IndexBlockProperty =
        AvaloniaProperty.Register<S3DUVEditorControl, S3DIndexBlock?>(nameof(IndexBlock));

    public S3DIndexBlock? IndexBlock
    {
        get => GetValue(IndexBlockProperty);
        set => SetValue(IndexBlockProperty, value);
    }

    /// <summary>Resolved diffuse texture (same resolution MainWindowViewModel already does for the "Solid" 3D preview) - background image, and what UV=1 maps to in pixels.</summary>
    public static readonly StyledProperty<ImgImage?> TextureProperty =
        AvaloniaProperty.Register<S3DUVEditorControl, ImgImage?>(nameof(Texture));

    public ImgImage? Texture
    {
        get => GetValue(TextureProperty);
        set => SetValue(TextureProperty, value);
    }

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<S3DUVEditorControl, double>(nameof(Zoom), 1.0);

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<S3DUVEditorControl, int>(nameof(SelectedIndex), -1);

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Invoked (no parameter) after a drag-move finishes changing a point's U/V - lets the ViewModel refresh the Geometry Editor grid / 3D preview / status line.</summary>
    public static readonly StyledProperty<ICommand?> ChangedCommandProperty =
        AvaloniaProperty.Register<S3DUVEditorControl, ICommand?>(nameof(ChangedCommand));

    public ICommand? ChangedCommand
    {
        get => GetValue(ChangedCommandProperty);
        set => SetValue(ChangedCommandProperty, value);
    }

    private const int DefaultCanvasSize = 256;
    private const double HitRadius = 7;

    private AvBitmap? _bitmapCache;
    private ImgImage? _bitmapCacheSource;

    /// <summary>Multi-selection (Ilive's m_aSelected) - rubber-band or Ctrl+click adds to it; a plain click on an unselected point replaces it with just that point. Dragging any point in the set moves the whole group together, matching Tab3DMUV::OnMouseMove.</summary>
    private readonly HashSet<int> _selected = new();
    private bool _draggingPoints;
    private AvPoint? _lastDragPos;
    private AvPoint? _rubberBandStart;
    private AvPoint? _rubberBandCurrent;

    private static readonly IPen EdgePen = new Pen(new SolidColorBrush(AvColor.Parse("#FFFFFF")), 1);
    private static readonly IBrush PointBrush = new SolidColorBrush(AvColor.Parse("#3355FF"));
    private static readonly IBrush SelectedPointBrush = new SolidColorBrush(AvColor.Parse("#FFFFFF"));
    private static readonly IPen RubberBandPen = new Pen(new SolidColorBrush(AvColor.Parse("#FFCC00")), 1);

    static S3DUVEditorControl()
    {
        AffectsRender<S3DUVEditorControl>(VertexBlockProperty, IndexBlockProperty, TextureProperty, ZoomProperty, SelectedIndexProperty);
        AffectsMeasure<S3DUVEditorControl>(TextureProperty, ZoomProperty);
    }

    public S3DUVEditorControl()
    {
        Focusable = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VertexBlockProperty)
        {
            // Switched editing group - indices from the old block are meaningless here.
            _selected.Clear();
            _draggingPoints = false;
            _rubberBandStart = null;
            _rubberBandCurrent = null;
        }
    }

    /// <summary>(pixel width, pixel height) of the UV canvas at Zoom=1 - the texture's own size, or a fixed default square if no texture resolved (points still editable/visible, just against a blank backdrop).</summary>
    private (double W, double H) BaseSize => Texture is { } tex ? (tex.Width, tex.Height) : (DefaultCanvasSize, DefaultCanvasSize);

    protected override Size MeasureOverride(Size availableSize)
    {
        var (w, h) = BaseSize;
        return new Size(w * Zoom, h * Zoom);
    }

    private AvPoint PointFor(System.Numerics.Vector2 uv)
    {
        var (w, h) = BaseSize;
        return new AvPoint(uv.X * w * Zoom, uv.Y * h * Zoom);
    }

    private int HitTestPoint(AvPoint p)
    {
        var block = VertexBlock;
        if (block is null || !block.HasUvs)
        {
            return -1;
        }

        var best = -1;
        var bestDist = HitRadius;
        for (var i = 0; i < block.Uvs.Count; i++)
        {
            var pt = PointFor(block.Uvs[i]);
            var dist = Math.Sqrt(Math.Pow(pt.X - p.X, 2) + Math.Pow(pt.Y - p.Y, 2));
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var hit = HitTestPoint(pos);
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (hit >= 0)
        {
            if (ctrl)
            {
                if (!_selected.Remove(hit))
                {
                    _selected.Add(hit);
                }
            }
            else if (!_selected.Contains(hit))
            {
                // Clicking a point outside the current multi-selection replaces it; clicking
                // one already inside the selection keeps the whole group for a group-drag.
                _selected.Clear();
                _selected.Add(hit);
            }

            SelectedIndex = hit;
            _draggingPoints = true;
            _lastDragPos = pos;
            e.Pointer.Capture(this);
            Focus();
        }
        else
        {
            // Empty space: start a rubber-band. Plain click clears the existing selection
            // first (so a click-release with no drag just deselects); Ctrl+drag adds to it.
            if (!ctrl)
            {
                _selected.Clear();
                SelectedIndex = -1;
            }

            _rubberBandStart = pos;
            _rubberBandCurrent = pos;
            e.Pointer.Capture(this);
        }

        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);

        if (_draggingPoints)
        {
            if (VertexBlock is not { } block || _lastDragPos is not { } last)
            {
                return;
            }

            var (w, h) = BaseSize;
            if (w <= 0 || h <= 0 || Zoom <= 0)
            {
                return;
            }

            var du = (float)((pos.X - last.X) / (w * Zoom));
            var dv = (float)((pos.Y - last.Y) / (h * Zoom));
            _lastDragPos = pos;

            foreach (var i in _selected)
            {
                if (i >= 0 && i < block.Uvs.Count)
                {
                    block.Uvs[i] += new Vector2(du, dv);
                }
            }

            InvalidateVisual();
        }
        else if (_rubberBandStart is not null)
        {
            _rubberBandCurrent = pos;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_rubberBandStart is { } start && _rubberBandCurrent is { } current && VertexBlock is { } block)
        {
            var rect = new Rect(start, current);
            for (var i = 0; i < block.Uvs.Count; i++)
            {
                if (rect.Contains(PointFor(block.Uvs[i])))
                {
                    _selected.Add(i);
                }
            }

            if (SelectedIndex < 0 && _selected.Count > 0)
            {
                foreach (var i in _selected)
                {
                    SelectedIndex = i;
                    break;
                }
            }
        }

        if (_draggingPoints && ChangedCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }

        _draggingPoints = false;
        _lastDragPos = null;
        _rubberBandStart = null;
        _rubberBandCurrent = null;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    /// <summary>Arrow-key nudge (Ilive's Tab3DMUV::PreTranslateMessage: ±0.002 per press, applied to every point in <see cref="_selected"/>) - a precise alternative to dragging with the mouse.</summary>
    private const float NudgeStep = 0.002f;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_selected.Count == 0 || VertexBlock is not { } block)
        {
            return;
        }

        var (du, dv) = e.Key switch
        {
            Key.Up => (0f, -NudgeStep),
            Key.Down => (0f, NudgeStep),
            Key.Left => (-NudgeStep, 0f),
            Key.Right => (NudgeStep, 0f),
            _ => (0f, 0f),
        };

        if (du == 0f && dv == 0f)
        {
            return;
        }

        foreach (var i in _selected)
        {
            if (i >= 0 && i < block.Uvs.Count)
            {
                block.Uvs[i] += new Vector2(du, dv);
            }
        }

        e.Handled = true;
        InvalidateVisual();
        if (ChangedCommand is { } command && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(AvColor.Parse("#0A0A0A")), new Rect(bounds.Size));

        if (Texture is { } tex)
        {
            if (_bitmapCacheSource != tex)
            {
                _bitmapCache?.Dispose();
                _bitmapCache = ImageConversion.ToAvaloniaBitmap(tex);
                _bitmapCacheSource = tex;
            }

            if (_bitmapCache is { } bmp)
            {
                context.DrawImage(bmp, new Rect(bmp.Size), new Rect(bounds.Size));
            }
        }

        var block = VertexBlock;
        if (block is null || !block.HasUvs)
        {
            return;
        }

        if (IndexBlock is { } indices)
        {
            for (var i = 0; i + 2 < indices.Indices.Count; i += 3)
            {
                var t1 = indices.Indices[i];
                var t2 = indices.Indices[i + 1];
                var t3 = indices.Indices[i + 2];
                if (t1 >= block.Uvs.Count || t2 >= block.Uvs.Count || t3 >= block.Uvs.Count)
                {
                    continue;
                }

                var p1 = PointFor(block.Uvs[t1]);
                var p2 = PointFor(block.Uvs[t2]);
                var p3 = PointFor(block.Uvs[t3]);
                context.DrawLine(EdgePen, p1, p2);
                context.DrawLine(EdgePen, p2, p3);
                context.DrawLine(EdgePen, p3, p1);
            }
        }

        const double half = 4;
        for (var i = 0; i < block.Uvs.Count; i++)
        {
            var p = PointFor(block.Uvs[i]);
            var brush = i == SelectedIndex || _selected.Contains(i) ? SelectedPointBrush : PointBrush;
            context.FillRectangle(brush, new Rect(p.X - half, p.Y - half, half * 2, half * 2));
        }

        if (_rubberBandStart is { } start && _rubberBandCurrent is { } current)
        {
            var r = new Rect(start, current);
            var tl = r.TopLeft;
            var tr = r.TopRight;
            var br = r.BottomRight;
            var bl = r.BottomLeft;
            context.DrawLine(RubberBandPen, tl, tr);
            context.DrawLine(RubberBandPen, tr, br);
            context.DrawLine(RubberBandPen, br, bl);
            context.DrawLine(RubberBandPen, bl, tl);
        }
    }
}
