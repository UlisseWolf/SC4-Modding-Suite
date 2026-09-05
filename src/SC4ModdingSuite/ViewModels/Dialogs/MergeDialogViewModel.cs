using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Merge DATs" dialog (Ilive Reader's DlgMerge): combines several .dat files'
/// entries, either into a brand-new output file (mode 0) or straight into the currently
/// active MDI tab (mode 1) - same two modes as the original (<c>m_iMode</c>). Where a TGI
/// appears in more than one source file, the last file listed wins (an explicit
/// improvement over the original, which just appended every entry unconditionally and
/// could silently write out duplicate-TGI entries - not what "merge" should mean).
/// </summary>
public sealed class MergeDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel? _targetDocument;

    public MergeDialogViewModel(MainWindowViewModel? targetDocument)
    {
        _targetDocument = targetDocument;
        CanMergeIntoOpenDocument = targetDocument is not null && targetDocument.HasOpenFile;
        _mergeIntoOpenDocument = CanMergeIntoOpenDocument;

        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedSourceFile is not null);
        MergeCommand = new RelayCommand(_ => Merge(), _ => SourceFiles.Count > 0);
    }

    public ObservableCollection<string> SourceFiles { get; } = new();

    private string? _selectedSourceFile;
    public string? SelectedSourceFile
    {
        get => _selectedSourceFile;
        set => SetField(ref _selectedSourceFile, value);
    }

    /// <summary>True only if a document is open in the active MDI tab - mirrors DlgMerge's <c>m_iMode</c> being available at all.</summary>
    public bool CanMergeIntoOpenDocument { get; }

    /// <summary>Label for the "into the active tab" radio option, e.g. "myfile.dat".</summary>
    public string TargetDocumentLabel => _targetDocument?.DocumentTitle ?? "(no document open)";

    /// <summary>Label text for the "merge into active tab" radio button.</summary>
    public string MergeIntoActiveTabLabel => $"Merge into the active tab ({TargetDocumentLabel})";

    private bool _mergeIntoOpenDocument;
    public bool MergeIntoOpenDocument
    {
        get => _mergeIntoOpenDocument;
        set => SetField(ref _mergeIntoOpenDocument, value);
    }

    private string? _outputPath;
    public string? OutputPath
    {
        get => _outputPath;
        set => SetField(ref _outputPath, value);
    }

    private string _statusMessage = "Add one or more .dat files to merge, then choose a destination.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand MergeCommand { get; }

    public void AddSourceFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!SourceFiles.Contains(path))
            {
                SourceFiles.Add(path);
            }
        }
    }

    private void RemoveSelected()
    {
        if (SelectedSourceFile is not null)
        {
            SourceFiles.Remove(SelectedSourceFile);
        }
    }

    private void Merge()
    {
        if (SourceFiles.Count == 0)
        {
            StatusMessage = "Add at least one file to merge.";
            return;
        }

        try
        {
            var sourceFiles = SourceFiles.Select(path => new DBPFFile(path)).ToList();

            if (MergeIntoOpenDocument && CanMergeIntoOpenDocument)
            {
                var target = _targetDocument!.Service.CurrentFile
                    ?? throw new InvalidOperationException("No document is currently open in the active tab.");

                foreach (var entry in MergeEntries(sourceFiles))
                {
                    DbpfFileFixes.AddOrUpdateEntry(target, entry);
                }

                _targetDocument.ReloadEntries();
                StatusMessage = $"Merged {SourceFiles.Count} file(s) into \"{_targetDocument.DocumentTitle}\" "
                    + "- use Save/Save As on that tab to write it to disk.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(OutputPath))
                {
                    StatusMessage = "Choose an output file.";
                    return;
                }

                var outputPath = Path.HasExtension(OutputPath) ? OutputPath! : OutputPath + ".dat";
                DbpfWriter.WritePackage(MergeEntries(sourceFiles), outputPath);
                StatusMessage = $"Merged {SourceFiles.Count} file(s) into: {outputPath}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while merging: {ex.Message}";
        }
    }

    /// <summary>
    /// Concatenates every source file's entries (skipping each one's own Directory subfile -
    /// DbpfWriter/AddOrUpdateEntry both rebuild it fresh anyway), last file wins on a TGI clash.
    /// </summary>
    private static List<DBPFEntry> MergeEntries(IEnumerable<DBPFFile> sourceFiles)
    {
        var order = new List<DBPFEntry>();
        var indexByTgi = new Dictionary<(uint, uint, uint), int>();

        foreach (var file in sourceFiles)
        {
            foreach (var entry in file.ListOfEntries)
            {
                var tgi = entry.TGI;
                if (tgi.TypeID == DbpfWriter.DirectoryTgi.TypeID
                    && tgi.GroupID == DbpfWriter.DirectoryTgi.GroupID
                    && tgi.InstanceID == DbpfWriter.DirectoryTgi.InstanceID)
                {
                    continue;
                }

                var key = (tgi.TypeID, tgi.GroupID, tgi.InstanceID);
                if (indexByTgi.TryGetValue(key, out var existingIndex))
                {
                    order[existingIndex] = entry;
                }
                else
                {
                    indexByTgi[key] = order.Count;
                    order.Add(entry);
                }
            }
        }

        return order;
    }
}
