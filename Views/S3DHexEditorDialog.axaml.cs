using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.Views;

public partial class S3DHexEditorDialog : Window
{
    public S3DHexEditorDialog()
    {
        InitializeComponent();
    }

    public S3DHexEditorDialog(IReadOnlyList<(string Tag, byte[] Bytes)> chunks) : this()
    {
        foreach (var (tag, bytes) in chunks)
        {
            var textBox = new TextBox
            {
                Text = $"{bytes.Length:N0} bytes\n\n{HexDump.Format(bytes)}",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas,Menlo,monospace"),
                FontSize = 12,
                BorderThickness = new Avalonia.Thickness(0),
                Background = Brushes.Transparent,
                Margin = new Avalonia.Thickness(6),
            };

            var tab = new TabItem
            {
                Header = tag,
                Content = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = textBox,
                },
            };

            ChunkTabs.Items.Add(tab);
        }

        if (chunks.Count == 0)
        {
            ChunkTabs.Items.Add(new TabItem
            {
                Header = "(none)",
                Content = new TextBlock { Text = "No recognizable S3D chunks found in this entry.", Margin = new Avalonia.Thickness(10) },
            });
        }
    }
}
