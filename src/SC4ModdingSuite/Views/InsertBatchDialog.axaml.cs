using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgInsertBatch - see InsertBatchDialogViewModel for the manifest parsing/insert logic.</summary>
public partial class InsertBatchDialog : Window
{
    public InsertBatchDialog()
    {
        InitializeComponent();
    }

    public InsertBatchDialog(InsertBatchDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private InsertBatchDialogViewModel ViewModel => (InsertBatchDialogViewModel)DataContext!;

    private async void OnLoadFileClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load batch manifest",
            AllowMultiple = false,
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel.ManifestText = File.ReadAllText(path);
        }
    }
}
