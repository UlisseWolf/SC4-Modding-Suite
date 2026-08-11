using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SC4ModdingSuite.Models;
using AvColor = Avalonia.Media.Color;
using AvPoint = Avalonia.Point;

namespace SC4ModdingSuite.Views;

/// <summary>
/// Live 2D preview for the UI Editor (Ilive Reader's FormUIPrev, plus drag-to-reposition
/// that Ilive Reader's own read-only preview never had). Draws each <see cref="PreviewBox"/>
/// as a filled/outlined rect with its caption; click selects the node (same node the Lot
/// Grid Editor pattern uses - selection drives the Prop/Value grid), drag moves it and
/// writes the new position straight back into that node's "area" prop via MoveCommand.
/// </summary>
public sealed class UiPreviewControl : Control
{
    public sealed record PreviewBox(UiLegacyNode Node, PixelRect Area, string Caption, string Iid, AvColor? FillColor, AvColor? TextColor,
        string BltType, Avalonia.Media.Imaging.Bitmap? Image, bool IsRadioCheckStyle);

    public static readonly StyledProperty<IReadOnlyList<PreviewBox>?> BoxesProperty =
        AvaloniaProperty.Register<UiPreviewControl, IReadOnlyList<PreviewBox>?>(nameof(Boxes));

    public IReadOnlyList<PreviewBox>? Boxes
    {
        get => GetValue(BoxesProperty);
        set => SetValue(BoxesProperty, value);
    }

    /// <summary>Executed with the clicked node (or null if empty space was clicked) on a plain click.</summary>
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<UiPreviewControl, ICommand?>(nameof(SelectCommand));

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    /// <summary>Fired whenever an actual box (not empty space) is clicked, in addition to
    /// SelectCommand above - unlike a ViewModel property change, this fires every time,
    /// even if the same node was already selected, which is what the owning view needs to
    /// reliably open that node's Properties dialog on every click.</summary>
    public event EventHandler<UiLegacyNode>? NodeClicked;

    /// <summary>Executed with (UiLegacyNode Node, int Left, int Top) once a drag ends.</summary>
    public static readonly StyledProperty<ICommand?> MoveCommandProperty =
        AvaloniaProperty.Register<UiPreviewControl, ICommand?>(nameof(MoveCommand));

    public ICommand? MoveCommand
    {
        get => GetValue(MoveCommandProperty);
        set => SetValue(MoveCommandProperty, value);
    }

    private PreviewBox? _dragging;
    private AvPoint _dragStartPointer;
    private PixelRect _dragStartArea;
    private bool _dragged;

    static UiPreviewControl()
    {
        // ponytail: AffectsRender only repaints when the *reference* assigned to Boxes
        // changes - it does NOT watch for the bound ObservableCollection's own contents
        // changing in place (Clear()+Add(), which is exactly what
        // MainWindowViewModel.RefreshUiPreview does every time - same collection instance,
        // refilled). Left registered anyway (harmless, covers the "a brand new list gets
        // bound" case too) - OnPropertyChanged below does the actual work by subscribing to
        // INotifyCollectionChanged directly, which is what makes this control genuinely
        // reactive to switching UI entries, adding/removing nodes, or editing "area"/
        // "caption"/"fillcolor" props, instead of only ever painting whatever was loaded
        // the first time.
        AffectsRender<UiPreviewControl>(BoxesProperty);
    }

    public UiPreviewControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>
    /// PRIORITY INSTRUCTION: the control's own size must always come from THIS - the exact
    /// same Boxes collection Render() below paints from - never from a separately-bound
    /// Width/Height. An earlier version relied on the ViewModel computing the dialog's
    /// extent (RefreshUiPreview) and binding that to this control's own Width/Height
    /// properties in XAML - two independent AvaloniaProperty bindings (Boxes, and Width/
    /// Height) updated together in the same C# method call, but with no guarantee Avalonia's
    /// layout/render pipeline applies both atomically in the same pass. That's the most
    /// likely explanation for a dialog's chrome (the outer/child GZWinGen background,
    /// 9-sliced to fit its own area) rendering at a different apparent size/position than
    /// its content (text/sliders/icons) even though CollectPreviewBoxes computes coordinates
    /// for all of them consistently - the two bound properties could legitimately reach the
    /// screen a frame apart. Measuring intrinsically from Boxes removes that risk entirely:
    /// there is only one source of truth for both what gets drawn and how big the control is.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var boxes = Boxes;
        double maxRight = 1;
        double maxBottom = 1;
        if (boxes is not null)
        {
            foreach (var box in boxes)
            {
                maxRight = Math.Max(maxRight, box.Area.X + box.Area.Width);
                maxBottom = Math.Max(maxBottom, box.Area.Y + box.Area.Height);
            }
        }

        return new Size(maxRight, maxBottom);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoxesProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldObservable)
            {
                oldObservable.CollectionChanged -= OnBoxesCollectionChanged;
            }

            if (change.NewValue is INotifyCollectionChanged newObservable)
            {
                newObservable.CollectionChanged += OnBoxesCollectionChanged;
            }

            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void OnBoxesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private PreviewBox? HitTest(AvPoint p)
    {
        var boxes = Boxes;
        if (boxes is null)
        {
            return null;
        }

        // Later entries are drawn on top (children after their parent - see
        // MainWindowViewModel.CollectPreviewBoxes), so hit-test from the end for the
        // topmost box under the pointer.
        for (var i = boxes.Count - 1; i >= 0; i--)
        {
            var box = boxes[i];
            if (p.X >= box.Area.X && p.X <= box.Area.X + box.Area.Width &&
                p.Y >= box.Area.Y && p.Y <= box.Area.Y + box.Area.Height)
            {
                return box;
            }
        }

        return null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        _dragging = HitTest(pos);
        _dragStartPointer = pos;
        _dragStartArea = _dragging?.Area ?? default;
        _dragged = false;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging is null)
        {
            return;
        }

        var pos = e.GetPosition(this);
        var delta = pos - _dragStartPointer;
        if (Math.Abs(delta.X) > 1 || Math.Abs(delta.Y) > 1)
        {
            _dragged = true;
        }

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragging is { } box)
        {
            if (_dragged)
            {
                var pos = e.GetPosition(this);
                var delta = pos - _dragStartPointer;
                var newLeft = _dragStartArea.X + (int)delta.X;
                var newTop = _dragStartArea.Y + (int)delta.Y;
                var parameter = (box.Node, newLeft, newTop);
                if (MoveCommand?.CanExecute(parameter) == true)
                {
                    MoveCommand.Execute(parameter);
                }
            }
            else if (SelectCommand?.CanExecute(box.Node) == true)
            {
                SelectCommand.Execute(box.Node);
                NodeClicked?.Invoke(this, box.Node);
            }
        }
        else if (!_dragged && SelectCommand?.CanExecute(null) == true)
        {
            SelectCommand.Execute(null);
        }

        _dragging = null;
        _dragged = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        context.FillRectangle(new SolidColorBrush(AvColor.Parse("#2B2B2B")), new Rect(bounds.Size));

        var boxes = Boxes;
        if (boxes is null)
        {
            return;
        }

        // Faithful port of Ilive Reader's ui_common.cpp::Preview() drawing logic - crucially,
        // WHICH parts of a node get drawn depends entirely on its "iid" (interface ID,
        // effectively the control's visual kind) AND whether it has a real decoded image:
        //  - A node WITH an image draws that image instead of any flat-color fallback,
        //    exactly like the original ("pData->iid == "IGZWinGen" && !pData->img" - the
        //    flat fill only ever applies when there's no image). Drawn at the image's own
        //    natural pixel size positioned at the box's top-left (matching CxImage::Draw's
        //    -1,-1 "natural size" call), or tiled across the whole box for blttype="tiled".
        //  - IGZWinGen/IGZWinTreeView/IGZWinCombo/IGZWinListBox/IGZWinGrid with no image:
        //    a flat-filled rectangle (list/tree/combo/grid controls also get a small
        //    centered label naming their own kind, same as the original, since there's
        //    nothing else to show for a control whose real content is only known at runtime).
        //  - IGZWinFlatRect: a thin border outline only, never filled.
        //  - IGZWinText/IGZWinBtn/IGZWinTextEdit: caption text only, no rectangle at all.
        //  - anything else (unrecognized iid, or none): caption text only if present.
        foreach (var box in boxes)
        {
            var area = box.Area;
            var rect = new Rect(area.X, area.Y, Math.Max(1, area.Width), Math.Max(1, area.Height));
            var isDragged = box == _dragging;

            if (box.Image is { } image)
            {
                // Always draw using the image's own raw PixelSize, never its (DIP,
                // DPI-metadata-dependent) Size property. Bitmap.Size can differ from the
                // actual pixel dimensions if the re-encoded PNG carries embedded DPI
                // metadata other than 96 (common for older game art assets) - since
                // ResolveUiImage already 9-slices an "edge" image to exactly this box's
                // own Area.Width/Height in raw pixels (BuildEdgeImage), trusting Size
                // instead of PixelSize here could silently draw it at the wrong scale,
                // which is exactly the kind of "some elements line up, others don't"
                // mismatch a per-image DPI difference would produce.
                var pixelSize = image.PixelSize;
                var imageSize = new Size(pixelSize.Width, pixelSize.Height);

                // Faithful port of Ilive Reader's own 5-way priority order for drawing an
                // image (ui_common.cpp::Preview, right where it checks pData->img) - which
                // branch applies depends on blttype/style/iid, in this exact order:
                if (box.BltType is "normal" or "edge")
                {
                    // 1) "normal"/"edge" (or edgeimage="yes", not modeled separately here):
                    // native pixel size, not stretched. An "edge" image was already
                    // 9-sliced to exactly this box's own area by ResolveUiImage/
                    // BuildEdgeImage beforehand, so "native size" ends up filling the area
                    // exactly either way.
                    context.DrawImage(image, new Rect(imageSize), new Rect(area.X, area.Y, imageSize.Width, imageSize.Height));
                }
                else if (box.BltType == "tiled")
                {
                    // 2) Explicit blttype="tiled": CxImage::Tile-equivalent, filling the
                    // whole area with repeated copies via an ImageBrush in Tile mode.
                    var brush = new ImageBrush(image)
                    {
                        TileMode = TileMode.Tile,
                        Stretch = Stretch.None,
                        SourceRect = new RelativeRect(0, 0, pixelSize.Width, pixelSize.Height, RelativeUnit.Absolute),
                        DestinationRect = new RelativeRect(0, 0, pixelSize.Width, pixelSize.Height, RelativeUnit.Absolute),
                    };
                    context.FillRectangle(brush, rect);
                }
                else if (box.IsRadioCheckStyle)
                {
                    // 3) A radiocheck-styled control that ISN'T blttype normal/edge/tiled:
                    // native size, once - same as case 1, just reached via a different
                    // condition in the original (this is a narrow fallback case; buttons
                    // normally hit case 1 or 4 instead, and already have their own state
                    // frame cropped out by ResolveUiImage before reaching here regardless).
                    context.DrawImage(image, new Rect(imageSize), new Rect(area.X, area.Y, imageSize.Width, imageSize.Height));
                }
                else if (box.Iid == "IGZWinBMP")
                {
                    // 4) PRIORITY INSTRUCTION: this is the missing piece that made a
                    // "rating" icon (e.g. a school's Grade, or Police's Effectiveness) show
                    // just one icon - sometimes with an unwanted second frame from its own
                    // imagerect bleeding through underneath - instead of a repeated row of
                    // N icons. Ilive Reader draws every IGZWinBMP by tiling the (already
                    // imagerect-cropped, if any) image repeatedly across the control's own
                    // *area* - not the image's own size - stepping by the image's own
                    // native width/height each time, clipped to that area. A 120-wide area
                    // with a 24-wide icon naturally yields exactly 5 repeats; a 22-tall area
                    // with a 44-tall (2-frame) source image naturally clips each repeat down
                    // to just its own top half, without needing any separate "which state"
                    // logic - the area's own height does that for free.
                    using (context.PushClip(rect))
                    {
                        var stepX = Math.Max(1, pixelSize.Width);
                        var stepY = Math.Max(1, pixelSize.Height);
                        for (var x = area.X; x < area.X + area.Width; x += stepX)
                        {
                            for (var y = area.Y; y < area.Y + area.Height; y += stepY)
                            {
                                context.DrawImage(image, new Rect(imageSize), new Rect(x, y, imageSize.Width, imageSize.Height));
                            }
                        }
                    }
                }
                else
                {
                    // 5) Generic fallback (anything else, no matching blttype/style/iid
                    // case above): stretched to fill the area exactly, unlike case 1's
                    // native size.
                    context.DrawImage(image, new Rect(imageSize), rect);
                }

                if (isDragged)
                {
                    context.DrawRectangle(new Pen(Brushes.White, 2), rect);
                }
            }
            else
            {
                switch (box.Iid)
                {
                    case "IGZWinGen":
                    {
                        var fill = box.FillColor is { } c ? new SolidColorBrush(c) : new SolidColorBrush(AvColor.Parse("#4472C4"), 0.35);
                        context.FillRectangle(fill, rect);
                        context.DrawRectangle(new Pen(Brushes.White, isDragged ? 2 : 1), rect);
                        break;
                    }

                    case "IGZWinTreeView":
                    case "IGZWinCombo":
                    case "IGZWinListBox":
                    case "IGZWinGrid":
                    {
                        var fill = box.FillColor is { } c ? new SolidColorBrush(c) : new SolidColorBrush(AvColor.Parse("#4472C4"), 0.35);
                        context.FillRectangle(fill, rect);
                        context.DrawRectangle(new Pen(Brushes.White, isDragged ? 2 : 1), rect);

                        var label = new FormattedText(box.Iid, System.Globalization.CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight, Typeface.Default, 11, Brushes.White);
                        context.DrawText(label, new AvPoint(
                            rect.X + Math.Max(0, (rect.Width - label.Width) / 2),
                            rect.Y + Math.Max(0, (rect.Height - label.Height) / 2)));
                        break;
                    }

                    case "IGZWinFlatRect":
                    {
                        var lineColor = box.FillColor ?? AvColor.Parse("#808080");
                        context.DrawRectangle(new Pen(new SolidColorBrush(lineColor), isDragged ? 2 : 1), rect);
                        break;
                    }

                    default:
                    {
                        // IGZWinText/IGZWinBtn/IGZWinTextEdit, and any other/unknown kind: no
                        // rectangle - only a thin outline while actually being dragged, so
                        // there's still some visual feedback for what's being moved.
                        if (isDragged)
                        {
                            context.DrawRectangle(new Pen(Brushes.White, 1), rect);
                        }

                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(box.Caption) &&
                box.Iid is "IGZWinText" or "IGZWinBtn" or "IGZWinTextEdit")
            {
                var formatted = new FormattedText(
                    box.Caption,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    11,
                    new SolidColorBrush(box.TextColor ?? Colors.White))
                {
                    MaxTextWidth = Math.Max(1, rect.Width - 4),
                };
                context.DrawText(formatted, new AvPoint(rect.X + 3, rect.Y + 2));
            }
        }
    }
}
