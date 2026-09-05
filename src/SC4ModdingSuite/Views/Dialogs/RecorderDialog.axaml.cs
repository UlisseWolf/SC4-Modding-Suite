using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>Ilive Reader's DlgRecorder (Patch Manager) - see RecorderDialogViewModel for the tracked-entries/export logic.</summary>
public partial class RecorderDialog : Window
{
    public RecorderDialog()
    {
        InitializeComponent();
    }

    public RecorderDialog(RecorderDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private RecorderDialogViewModel ViewModel => (RecorderDialogViewModel)DataContext!;

    private async void OnExportPatchClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save patch as...",
            SuggestedFileName = "patch.dat",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("SC4 package (.dat)") { Patterns = new[] { "*.dat" } },
            },
        });

        var path = file?.TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        ViewModel.ExportPatch(path);
    }
}
