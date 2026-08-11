using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgMerge - see MergeDialogViewModel for the actual merge logic.</summary>
public partial class MergeDialog : Window
{
    public MergeDialog()
    {
        InitializeComponent();
    }

    public MergeDialog(MergeDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private MergeDialogViewModel ViewModel => (MergeDialogViewModel)DataContext!;

    private static readonly FilePickerFileType Sc4PackageFileType = new("File SC4 (DBPF)")
    {
        Patterns = new[] { "*.dat", "*.sc4lot", "*.sc4desc", "*.sc4model" },
    };

    private async void OnAddFilesClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose .dat files to merge",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType> { Sc4PackageFileType, FilePickerFileTypes.All },
        });

        var paths = files.Select(f => f.TryGetLocalPath()).Where(p => p is not null).Select(p => p!);
        ViewModel.AddSourceFiles(paths);
    }

    private async void OnBrowseOutputClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save merged package as",
            SuggestedFileName = "merged.dat",
            FileTypeChoices = new List<FilePickerFileType> { Sc4PackageFileType, FilePickerFileTypes.All },
        });

        var path = file?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel.OutputPath = path;
        }
    }
}
