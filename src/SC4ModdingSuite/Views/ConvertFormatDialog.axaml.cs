using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>See ConvertFormatDialogViewModel for what "Convert Format" actually does (and why WorkspaceConvert wasn't a real port).</summary>
public partial class ConvertFormatDialog : Window
{
    public ConvertFormatDialog()
    {
        InitializeComponent();
    }

    public ConvertFormatDialog(ConvertFormatDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private ConvertFormatDialogViewModel ViewModel => (ConvertFormatDialogViewModel)DataContext!;

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Convert to...",
            SuggestedFileName = "converted" + ViewModel.SelectedExtension,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("File SC4 (DBPF)") { Patterns = new[] { "*.dat", "*.sc4lot", "*.sc4desc", "*.sc4model" } },
                FilePickerFileTypes.All,
            },
        });

        var path = file?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel.OutputPath = path;
        }
    }
}
