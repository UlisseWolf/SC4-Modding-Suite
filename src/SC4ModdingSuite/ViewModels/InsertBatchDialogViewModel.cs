using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Insert Batch" dialog (Ilive Reader's DlgInsertBatch): a tab-delimited
/// manifest, one line per new entry - <c>Type&lt;TAB&gt;Group&lt;TAB&gt;Instance&lt;TAB&gt;file path&lt;TAB&gt;Y/N (compress)</c> -
/// each imported as a brand-new entry (<see cref="DbpfService.AddNewEntry"/>). The
/// original always read this from a separate manifest file path chosen via Browse; here
/// the manifest is edited directly in a textbox (Browse just loads a file's text into it),
/// which is simpler for a small batch and still supports loading a prepared file for a
/// large one.
/// </summary>
public sealed class InsertBatchDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;

    public InsertBatchDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
        ProcessCommand = new RelayCommand(_ => Process());
    }

    private string _manifestText =
        "CA63E2A3\t4A5E8EF6\t01234567\tC:\\SimCity\\file.lua\tN\r\n" +
        "5AD0E817\tBADB57F1\t47087960\tC:\\SimCity\\model.bin\tN";

    public string ManifestText
    {
        get => _manifestText;
        set => SetField(ref _manifestText, value);
    }

    private string _statusMessage = "One entry per line: Type<TAB>Group<TAB>Instance<TAB>file path<TAB>Y/N (compress).";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand ProcessCommand { get; }

    private void Process()
    {
        var lines = ManifestText.Replace("\r\n", "\n").Split('\n');
        var failed = new List<string>();
        var inserted = 0;

        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            var line = lines[lineNumber].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length < 4)
            {
                failed.Add($"Line {lineNumber + 1}: expected at least 4 tab-separated fields (Type, Group, Instance, file path).");
                continue;
            }

            try
            {
                var type = EntryClipboard.ParseHex(parts[0]);
                var group = EntryClipboard.ParseHex(parts[1]);
                var instance = EntryClipboard.ParseHex(parts[2]);
                var path = parts[3].Trim();
                var compress = parts.Length > 4 && parts[4].Trim().Equals("Y", StringComparison.OrdinalIgnoreCase);

                var bytes = File.ReadAllBytes(path);
                var entry = _document.Service.AddNewEntry(new TGI(type, group, instance), bytes, compress);
                if (entry is null)
                {
                    failed.Add($"Line {lineNumber + 1}: could not create the entry.");
                }
                else
                {
                    inserted++;
                }
            }
            catch (Exception ex)
            {
                failed.Add($"Line {lineNumber + 1}: {ex.Message}");
            }
        }

        if (inserted > 0)
        {
            _document.ReloadEntries();
        }

        StatusMessage = failed.Count == 0
            ? $"Inserted {inserted} entr{(inserted == 1 ? "y" : "ies")}."
            : $"Inserted {inserted} entr{(inserted == 1 ? "y" : "ies")}, {failed.Count} failed:\n" + string.Join("\n", failed);
    }
}
