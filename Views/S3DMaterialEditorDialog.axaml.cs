using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class S3DMaterialEditorDialog : Window
{
    public S3DMaterialEditorDialog()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private static readonly FilePickerFileType FshFileType = new("FSH texture (.fsh)")
    {
        Patterns = new[] { "*.fsh" },
    };

    private async void OnReplaceTextureClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Replace texture with .fsh file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { FshFileType, FilePickerFileTypes.All },
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ReplaceS3DMaterialTexture(path);
    }

    private async void OnAddTextureFromFileClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add texture from .fsh file",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { FshFileType, FilePickerFileTypes.All },
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.AddS3DMaterialTextureFromFile(path);
    }
}
