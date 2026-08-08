using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

public partial class OptionsDialog : Window
{
    private PropertySourceService? _propertySourceService;
    private Action<PropertyDefinitionsRegistry>? _onPropertyRegistryChanged;

    public OptionsDialog()
    {
        InitializeComponent();
    }

    public OptionsDialog(
        OptionsDialogViewModel viewModel,
        PropertySourceService propertySourceService,
        Action<PropertyDefinitionsRegistry> onPropertyRegistryChanged) : this()
    {
        _propertySourceService = propertySourceService;
        _onPropertyRegistryChanged = onPropertyRegistryChanged;

        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
        viewModel.ChangePropertySourceRequested += OnChangePropertySourceRequested;
    }

    private OptionsDialogViewModel ViewModel => (OptionsDialogViewModel)DataContext!;

    private static readonly FilePickerFileType ExecutableFileType = new("Executables")
    {
        Patterns = new List<string> { "*.exe" },
    };

    private async void OnChangePropertySourceRequested(object? sender, EventArgs e)
    {
        if (_propertySourceService is null || _onPropertyRegistryChanged is null)
        {
            return;
        }

        var dialogVm = new PropertySourceDialogViewModel(_propertySourceService, _propertySourceService.LoadLastUsedSource());
        var dialog = new PropertySourceDialog(dialogVm);

        var accepted = await dialog.ShowDialog<bool>(this);
        if (accepted && dialogVm.Result is not null)
        {
            _onPropertyRegistryChanged(dialogVm.Result);
        }
    }

    private async void OnBrowseSimCityLocaleClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select SimCityLocale.DAT",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("SimCityLocale.DAT") { Patterns = new List<string> { "SimCityLocale.dat", "*.dat" } },
                FilePickerFileTypes.All,
            },
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel.SimCityLocalePath = path;
        }
    }

    private async void OnBrowseSc4InstallFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the SC4 installation folder",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel.Sc4InstallFolder = path;
        }
    }

    private async void OnBrowsePluginsFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the SC4 Plugins folder",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            ViewModel.PluginsFolder = path;
        }
    }

    private async Task BrowseForExecutableAsync(string title, Action<string> onSelected)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { ExecutableFileType, FilePickerFileTypes.All },
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
        {
            onSelected(path);
        }
    }

    private void OnBrowsePimXClick(object? sender, RoutedEventArgs e) =>
        _ = BrowseForExecutableAsync("Select the SC4 PIM-X executable", p => ViewModel.PimXPath = p);

    private void OnBrowseDataNodeClick(object? sender, RoutedEventArgs e) =>
        _ = BrowseForExecutableAsync("Select the SC4 DataNode executable", p => ViewModel.DataNodePath = p);

    private void OnBrowseMapperClick(object? sender, RoutedEventArgs e) =>
        _ = BrowseForExecutableAsync("Select the SC4 Mapper executable", p => ViewModel.MapperPath = p);

    private void OnBrowseTerraformerClick(object? sender, RoutedEventArgs e) =>
        _ = BrowseForExecutableAsync("Select the SC4 Terraformer executable", p => ViewModel.TerraformerPath = p);

    private void OnBrowseSc4PacEditorClick(object? sender, RoutedEventArgs e) =>
        _ = BrowseForExecutableAsync("Select the SC4pac Editor executable", p => ViewModel.Sc4PacEditorPath = p);

    private void OnBrowseNamDevelopmentSuiteClick(object? sender, RoutedEventArgs e) =>
        _ = BrowseForExecutableAsync("Select the NAM Development Suite executable", p => ViewModel.NamDevelopmentSuitePath = p);
}
