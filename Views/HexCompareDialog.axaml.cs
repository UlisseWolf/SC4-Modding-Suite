using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SC4ModdingSuite.Views;

/// <summary>
/// Ilive Reader's DlgHexCmp: two byte buffers, shown as side-by-side hex+ASCII dumps with
/// differing bytes highlighted, synced scrolling, and Previous/Next to jump between diff
/// runs. Built entirely from the constructor argument (like S3DHexEditorDialog) - a pure
/// snapshot view, no viewmodel needed.
/// </summary>
public partial class HexCompareDialog : Window
{
    private const int BytesPerRow = 16;
    private const double RowHeight = 18;

    // One row index per contiguous run of differing bytes (only the run's first row is
    // recorded, mirroring DlgHexCmp::OnNext/OnPrevious skipping over consecutive
    // highlighted offsets instead of stopping on every single one).
    private readonly List<int> _diffRunRowIndices = new();
    private int _diffCursor = -1;
    private bool _syncing;

    public HexCompareDialog()
    {
        InitializeComponent();
    }

    public HexCompareDialog(byte[] dataA, byte[] dataB) : this()
    {
        var rowCount = (int)Math.Ceiling(Math.Max(dataA.Length, dataB.Length) / (double)BytesPerRow);
        var diffMask = new bool[Math.Max(dataA.Length, dataB.Length)];
        var maxCommon = Math.Min(dataA.Length, dataB.Length);
        for (var i = 0; i < maxCommon; i++)
        {
            diffMask[i] = dataA[i] != dataB[i];
        }
        for (var i = maxCommon; i < diffMask.Length; i++)
        {
            diffMask[i] = true; // tail bytes that only exist on one side count as "differing"
        }

        var previousRowHadDiff = false;
        for (var row = 0; row < rowCount; row++)
        {
            var offset = row * BytesPerRow;
            LeftPanel.Children.Add(BuildRow(dataA, offset, diffMask));
            RightPanel.Children.Add(BuildRow(dataB, offset, diffMask));

            var rowHasDiff = false;
            for (var i = 0; i < BytesPerRow && offset + i < diffMask.Length; i++)
            {
                if (diffMask[offset + i])
                {
                    rowHasDiff = true;
                    break;
                }
            }

            if (rowHasDiff && !previousRowHadDiff)
            {
                _diffRunRowIndices.Add(row);
            }
            previousRowHadDiff = rowHasDiff;
        }

        var byteDiffs = 0;
        foreach (var d in diffMask)
        {
            if (d) byteDiffs++;
        }

        DiffCountText.Text = dataA.Length == dataB.Length
            ? $"{byteDiffs:N0} differing byte(s) out of {dataA.Length:N0}."
            : $"Sizes differ: {dataA.Length:N0} vs {dataB.Length:N0} bytes ({byteDiffs:N0} byte position(s) flagged).";
    }

    private static Control BuildRow(byte[] data, int offset, bool[] diffMask)
    {
        var text = new TextBlock
        {
            FontFamily = new FontFamily("Consolas,Menlo,monospace"),
            FontSize = 12,
            Height = RowHeight,
        };

        text.Inlines ??= new InlineCollection();
        text.Inlines.Add(new Run($"{offset:X8}  ") { Foreground = Brushes.Gray });

        var count = Math.Min(BytesPerRow, Math.Max(0, data.Length - offset));

        for (var i = 0; i < BytesPerRow; i++)
        {
            if (i < count)
            {
                var idx = offset + i;
                var differs = idx < diffMask.Length && diffMask[idx];
                text.Inlines.Add(new Run($"{data[idx]:X2} ") { Foreground = differs ? Brushes.OrangeRed : null });
            }
            else
            {
                text.Inlines.Add(new Run("   "));
            }

            if (i == 7)
            {
                text.Inlines.Add(new Run(" "));
            }
        }

        text.Inlines.Add(new Run(" "));
        for (var i = 0; i < count; i++)
        {
            var idx = offset + i;
            var b = data[idx];
            var ch = b is >= 0x20 and < 0x7F ? (char)b : '.';
            var differs = idx < diffMask.Length && diffMask[idx];
            text.Inlines.Add(new Run(ch.ToString()) { Foreground = differs ? Brushes.OrangeRed : null });
        }

        return text;
    }

    // Keep both panes scrolled to the same vertical position - same purpose as
    // DlgHexCmp::OnUserVScroll, with the same "ignore the echo" reentrancy guard.
    private void OnLeftScrollChanged(object? sender, ScrollChangedEventArgs e) => SyncScroll(LeftScroll, RightScroll);
    private void OnRightScrollChanged(object? sender, ScrollChangedEventArgs e) => SyncScroll(RightScroll, LeftScroll);

    private void SyncScroll(ScrollViewer from, ScrollViewer to)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        to.Offset = new Vector(to.Offset.X, from.Offset.Y);
        _syncing = false;
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_diffRunRowIndices.Count == 0)
        {
            return;
        }

        _diffCursor = (_diffCursor + 1) % _diffRunRowIndices.Count;
        ScrollToRow(_diffRunRowIndices[_diffCursor]);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        if (_diffRunRowIndices.Count == 0)
        {
            return;
        }

        _diffCursor = _diffCursor <= 0 ? _diffRunRowIndices.Count - 1 : _diffCursor - 1;
        ScrollToRow(_diffRunRowIndices[_diffCursor]);
    }

    private void ScrollToRow(int row)
    {
        var y = Math.Max(0, row * RowHeight - RowHeight * 3); // a little context above the target row
        LeftScroll.Offset = new Vector(LeftScroll.Offset.X, y);
        RightScroll.Offset = new Vector(RightScroll.Offset.X, y);
    }
}
