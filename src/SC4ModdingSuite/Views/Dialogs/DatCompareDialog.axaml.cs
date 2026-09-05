using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgCompare - see DatCompareDialogViewModel for the matching/diff logic.</summary>
public partial class DatCompareDialog : Window
{
    public DatCompareDialog()
    {
        InitializeComponent();
    }

    public DatCompareDialog(DatCompareDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private DatCompareDialogViewModel ViewModel => (DatCompareDialogViewModel)DataContext!;

    private static readonly FilePickerFileType Sc4PackageFileType = new("File SC4 (DBPF)")
    {
        Patterns = new[] { "*.dat", "*.sc4lot", "*.sc4desc", "*.sc4model" },
    };

    private async void OnBrowseFile1Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync();
        if (path is not null)
        {
            ViewModel.FilePath1 = path;
        }
    }

    private async void OnBrowseFile2Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync();
        if (path is not null)
        {
            ViewModel.FilePath2 = path;
        }
    }

    private async System.Threading.Tasks.Task<string?> PickFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a .dat file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { Sc4PackageFileType, FilePickerFileTypes.All },
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    /// <summary>DlgCompare::OnGridDblClick/OnViewDiff - opens the byte-level hex diff for the selected matched row.</summary>
    private async void OnViewDiffClick(object? sender, RoutedEventArgs e)
    {
        var row = ViewModel.SelectedRow;
        if (row is null || row.EntryA is null || row.EntryB is null)
        {
            return;
        }

        var bytesA = RawEntryBytes.GetDecompressed(row.EntryA) ?? System.Array.Empty<byte>();
        var bytesB = RawEntryBytes.GetDecompressed(row.EntryB) ?? System.Array.Empty<byte>();

        var dialog = new HexCompareDialog(bytesA, bytesB);
        await dialog.ShowDialog(this);
    }
}
