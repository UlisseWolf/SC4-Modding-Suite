using System;
using System.Collections.Generic;
using System.IO;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Convert Format" dialog. Ilive Reader's own <c>CWorkspaceConvert</c>
/// (WorkspaceConvert.cpp) - despite the name - turned out to be an unfinished leftover
/// from Visual Studio's MFC "Fluent UI" sample pane: it builds a property-filter tree and
/// declares an <c>OnConvert</c> handler that is never actually wired up or implemented -
/// there is no real conversion logic to port. What "convert" sensibly means for this app's
/// file family is what's implemented here instead: SC4's four DBPF-based extensions
/// (.dat/.sc4lot/.sc4desc/.sc4model) are all the exact same binary format - only the
/// extension differs by convention - so "converting" between them is just writing the
/// currently open package's entries out under a different extension.
/// </summary>
public sealed class ConvertFormatDialogViewModel : ViewModelBase
{
    public static IReadOnlyList<string> Extensions { get; } = new[] { ".dat", ".sc4lot", ".sc4desc", ".sc4model" };

    private readonly MainWindowViewModel _document;

    public ConvertFormatDialogViewModel(MainWindowViewModel document)
    {
        _document = document;

        var currentPath = document.Service.CurrentPath;
        var baseName = string.IsNullOrEmpty(currentPath) ? "converted" : Path.GetFileNameWithoutExtension(currentPath);
        var folder = string.IsNullOrEmpty(currentPath) ? string.Empty : Path.GetDirectoryName(currentPath) ?? string.Empty;

        _selectedExtension = Extensions[0];
        _outputPath = Path.Combine(folder, baseName + _selectedExtension);

        ConvertCommand = new RelayCommand(_ => Convert(), _ => document.HasOpenFile);
    }

    private string _selectedExtension;
    public string SelectedExtension
    {
        get => _selectedExtension;
        set
        {
            if (SetField(ref _selectedExtension, value))
            {
                OutputPath = Path.ChangeExtension(OutputPath, value);
            }
        }
    }

    private string _outputPath;
    public string OutputPath
    {
        get => _outputPath;
        set => SetField(ref _outputPath, value);
    }

    private string _statusMessage = "SC4's .dat/.sc4lot/.sc4desc/.sc4model are all the same DBPF format - this just re-saves under a different extension.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand ConvertCommand { get; }

    private void Convert()
    {
        if (_document.Service.CurrentFile is null)
        {
            StatusMessage = "No document is open in this tab.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            StatusMessage = "Choose an output file.";
            return;
        }

        try
        {
            var outputPath = Path.HasExtension(OutputPath) ? OutputPath : OutputPath + SelectedExtension;
            DbpfWriter.WritePackage(_document.Service.CurrentFile.ListOfEntries, outputPath);
            StatusMessage = $"Converted copy written to: {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while converting: {ex.Message}";
        }
    }
}
