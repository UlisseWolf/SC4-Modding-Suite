using System;
using System.Collections.Generic;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// LTEXT Editor: only active/shown while <see cref="IsLtextEditorMode"/> is selected (see
/// <c>DbpfWorkspaceView.axaml</c>'s "LTEXT Editor" radio button). Lives in the third panel
/// (Grid.Column="2") next to the ordinary TGI editor/preview in Column 1 - the panel there
/// already shows the selected LTEXT entry's TGI and read-only text (see
/// <c>LoadSimplePreview</c>); this one adds the actual editing surface: an editable text
/// box, SAVE FILE (writes the box into the selected entry and saves the package to disk in
/// one step), SAVE ALL FILE FOR LANGUAGE (clones every LTEXT entry currently in the package
/// to a chosen target language's TGI, then saves), and Export/Import Poedit for round-
/// tripping the whole file's strings through a translator's .po/.pot workflow.
///
/// <para>
/// <b>The critical piece</b> - and the reason none of the above is just "swap some text" -
/// is that SC4 does not address a translated LTEXT string by its own TGI: it derives the
/// TGI of each language's variant from a shared base TGI by adding a fixed offset to the
/// Group ID only (Type ID and Instance ID never change). That whole scheme, plus the offset
/// table itself, is documented in RippleJet's SC4Devotion tutorial and implemented here in
/// <see cref="LtextTgiLanguage"/>: <see href="https://www.sc4devotion.com/forums/index.php?topic=532.0"/>.
/// </para>
/// </summary>
public sealed partial class MainWindowViewModel
{
    // ---------------------------------------------------------------
    // Language pickers - shared by every multi-language action below. "Source" is the
    // language the currently-open/currently-exported text is assumed to already sit at
    // (almost always "Default", offset 0x00 - the conventional, unshifted "root" TGI a
    // single-language source file already uses, per the tutorial); "Target" is the
    // language being written to (Save all for language, Import Poedit) or looked up for
    // msgstr pre-fill (Export Poedit).
    // ---------------------------------------------------------------

    /// <summary>Every language in the SC4Devotion Group ID offset table, for the two ComboBoxes below.</summary>
    public IReadOnlyList<LtextLanguage> LtextLanguageOptions => LtextLanguages.All;

    private LtextLanguage _ltextSourceLanguage = LtextLanguages.Default;
    public LtextLanguage LtextSourceLanguage
    {
        get => _ltextSourceLanguage;
        set => SetField(ref _ltextSourceLanguage, value);
    }

    private LtextLanguage _ltextTargetLanguage = LtextLanguages.All.First(l => l.Name == "Italian");
    public LtextLanguage LtextTargetLanguage
    {
        get => _ltextTargetLanguage;
        set => SetField(ref _ltextTargetLanguage, value);
    }

    // ---------------------------------------------------------------
    // Editable text box (the "casella di testo modificabile") - buffered exactly like the
    // LUA/T21/UI editors elsewhere in this app: editing it only changes this in-memory
    // buffer, nothing is written back to the entry until SAVE FILE / SAVE ALL FOR LANGUAGE
    // runs.
    // ---------------------------------------------------------------

    private string _ltextEditText = string.Empty;
    public string LtextEditText
    {
        get => _ltextEditText;
        set => SetField(ref _ltextEditText, value);
    }

    /// <summary>True while the selected entry is something this panel can actually save (a real, writable DBPFEntryLTEXT).</summary>
    public bool IsLtextEntrySelected => SelectedEntry?.Entry is DBPFEntryLTEXT;

    public RelayCommand SaveLtextFileCommand { get; private set; } = null!;
    public RelayCommand SaveLtextForLanguageCommand { get; private set; } = null!;

    /// <summary>Wires up the LTEXT Editor's commands. Called once from the main constructor.</summary>
    private void InitializeLtextCommands()
    {
        SaveLtextFileCommand = new RelayCommand(_ => SaveLtextFile(), _ => IsLtextEntrySelected);

        // Not gated on HasOpenFile via CanExecute: RelayCommand.CanExecuteChanged is never
        // raised when a document's underlying file gets opened/created (see
        // OpenFile/CreateNewPackage above - they only raise property-changed for direct
        // bindings, not for any RelayCommand), so a CanExecute tied to that would go stale
        // and leave the button looking permanently disabled. SaveLtextForLanguage already
        // checks HasOpenFile itself and reports a clear status message instead - same
        // pattern the main toolbar's own Save/Save As buttons use (plain Click handlers,
        // no CanExecute at all).
        SaveLtextForLanguageCommand = new RelayCommand(_ => SaveLtextForLanguage());
    }

    /// <summary>
    /// Shared "does this entry belong to the LTEXT family" test - same rule
    /// <c>MatchesCurrentEditorMode</c> uses to build the filtered entry list for "LTEXT
    /// Editor" mode: a genuine <see cref="DBPFEntryLTEXT"/>, or a "special"/non-standard-
    /// group entry under the shared WAV/LTEXT/XA Type ID that decodes as the LTEXT binary
    /// layout (see <see cref="EntryTypeClassifier"/>) without looking like a RIFF/WAVE file.
    /// </summary>
    private static bool IsLtextFamilyEntry(EntryItemViewModel vm)
    {
        var tgi = vm.Entry.TGI;

        if (!EntryTypeClassifier.IsLtextWavXaType(tgi))
        {
            return vm.Entry is DBPFEntryLTEXT;
        }

        if (vm.Entry is DBPFEntryLTEXT)
        {
            return true;
        }

        var bytes = RawEntryBytes.GetDecompressed(vm.Entry);
        return !EntryTypeClassifier.LooksLikeRiffWav(bytes) && EntryTypeClassifier.TryDecodeAsLtext(bytes) is not null;
    }

    /// <summary>Best-effort text for any LTEXT-family entry, whether or not csDBPF decoded it as a real DBPFEntryLTEXT (see <see cref="IsLtextFamilyEntry"/>).</summary>
    private static string? GetLtextText(DBPFEntry entry)
    {
        if (entry is DBPFEntryLTEXT ltext)
        {
            entry.Decode();
            return ltext.Text;
        }

        var bytes = RawEntryBytes.GetDecompressed(entry);
        return EntryTypeClassifier.LooksLikeRiffWav(bytes) ? null : EntryTypeClassifier.TryDecodeAsLtext(bytes);
    }

    /// <summary>
    /// Refreshes <see cref="LtextEditText"/> for the newly-selected entry - called from
    /// <c>OnSelectedEntryChanged</c> alongside the other per-editor "Load...ForSelectedEntry"
    /// methods.
    /// </summary>
    private void LoadLtextEditorForSelectedEntry()
    {
        OnPropertyChanged(nameof(IsLtextEntrySelected));
        SaveLtextFileCommand.RaiseCanExecuteChanged();

        if (SelectedEntry is null)
        {
            LtextEditText = string.Empty;
            return;
        }

        LtextEditText = GetLtextText(SelectedEntry.Entry) ?? string.Empty;
    }

    /// <summary>
    /// Adds or refreshes this entry's <see cref="EntryItemViewModel"/> wrapper in
    /// <see cref="Entries"/> by matching on TGI - shared by every LTEXT action that may
    /// either update an entry already in the list (same object, just re-encoded) or create
    /// a brand-new one at a language-offset TGI nothing occupied yet.
    /// </summary>
    private void UpsertEntryViewModel(DBPFEntry entry)
    {
        var index = Entries.ToList().FindIndex(vm => vm.Entry.TGI.Matches(entry.TGI));
        if (index < 0)
        {
            Entries.Add(new EntryItemViewModel(entry));
            return;
        }

        if (ReferenceEquals(Entries[index].Entry, entry))
        {
            Entries[index].Refresh();
        }
        else
        {
            Entries[index] = new EntryItemViewModel(entry);
        }
    }

    /// <summary>
    /// "SAVE FILE": writes the text box back into the selected LTEXT entry, then saves the
    /// whole package to disk right away (unlike the LUA/T21/UI editors' own SAVE buttons,
    /// which only update the in-memory entry and leave disk persistence to the main
    /// toolbar - this panel's own SAVE FILE button does both in one click, as requested).
    /// </summary>
    private void SaveLtextFile()
    {
        if (SelectedEntry?.Entry is not DBPFEntryLTEXT ltext)
        {
            StatusMessage = "Select an LTEXT entry first.";
            return;
        }

        try
        {
            _service.UpsertLtextEntry(ltext.TGI, LtextEditText);
            SelectedEntry.Refresh();
            SaveInPlace();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving LTEXT entry: {ex.Message}";
        }
    }

    /// <summary>
    /// "SALVA TUTTO IL FILE PER LINGUA" / "SAVE ALL FILE FOR LANGUAGE": clones every LTEXT
    /// entry currently in the package to <see cref="LtextTargetLanguage"/>'s TGI (see
    /// <see cref="LtextTgiLanguage.TgiForLanguage"/>, treating each entry's own TGI as
    /// already sitting at <see cref="LtextSourceLanguage"/>'s offset) - creating a new
    /// entry where none exists yet at that language, or overwriting the text of one that
    /// does - then saves the whole package to disk. If the selected entry is itself an
    /// LTEXT entry, whatever's currently in the text box is committed first, exactly like
    /// SAVE FILE would.
    /// </summary>
    private void SaveLtextForLanguage()
    {
        if (!HasOpenFile)
        {
            StatusMessage = "Open a package first.";
            return;
        }

        try
        {
            if (SelectedEntry?.Entry is DBPFEntryLTEXT)
            {
                _service.UpsertLtextEntry(SelectedEntry.Entry.TGI, LtextEditText);
                SelectedEntry.Refresh();
            }

            var created = 0;
            var updated = 0;

            foreach (var vm in Entries.ToList())
            {
                if (!IsLtextFamilyEntry(vm) || vm.Entry.TGI.TypeID != DBPFTGI.LTEXT.TypeID)
                {
                    continue;
                }

                var text = GetLtextText(vm.Entry);
                if (text is null)
                {
                    continue;
                }

                var targetTgi = LtextTgiLanguage.TgiForLanguage(vm.Entry.TGI, LtextSourceLanguage, LtextTargetLanguage);
                if (targetTgi.Matches(vm.Entry.TGI))
                {
                    // Source and target language resolve to the same TGI (e.g. both left
                    // at "Default") - nothing to clone, this *is* that language's entry.
                    continue;
                }

                var existedBefore = _service.TryGetEntry(targetTgi) is not null;
                var newEntry = _service.UpsertLtextEntry(targetTgi, text);
                UpsertEntryViewModel(newEntry);

                if (existedBefore)
                {
                    updated++;
                }
                else
                {
                    created++;
                }
            }

            RefreshDisplayedEntries();
            _service.Save();
            StatusMessage =
                $"Saved whole file for {LtextTargetLanguage.Label}: {created} new + {updated} updated LTEXT " +
                $"entries (source language: {LtextSourceLanguage.Label}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving whole file for language: {ex.Message}";
        }
    }

    /// <summary>
    /// "ESPORTA POEDIT": writes every current LTEXT entry's own TGI/text as one
    /// msgctxt/msgid pair (matching the shape of real-world LTEXT .po/.pot files, e.g. a
    /// mod's own bundled translation template), with msgstr pre-filled from
    /// <see cref="LtextTargetLanguage"/>'s sibling entry where one already exists in the
    /// package (see <see cref="LtextTgiLanguage.TgiForLanguage"/>) - so re-exporting after
    /// partially translating in-app still round-trips correctly through Poedit.
    /// </summary>
    public void ExportLtextPoedit(string path)
    {
        try
        {
            var poEntries = new List<PoEntry>();

            foreach (var vm in Entries)
            {
                if (!IsLtextFamilyEntry(vm) || vm.Entry.TGI.TypeID != DBPFTGI.LTEXT.TypeID)
                {
                    continue;
                }

                var text = GetLtextText(vm.Entry);
                if (text is null)
                {
                    continue;
                }

                var tgi = vm.Entry.TGI;
                var msgctxt = PoFile.FormatMsgctxt(tgi.TypeID, tgi.GroupID, tgi.InstanceID);

                var targetTgi = LtextTgiLanguage.TgiForLanguage(tgi, LtextSourceLanguage, LtextTargetLanguage);
                var msgstr = string.Empty;
                if (!targetTgi.Matches(tgi) && _service.TryGetEntry(targetTgi) is { } targetEntry)
                {
                    msgstr = GetLtextText(targetEntry) ?? string.Empty;
                }

                poEntries.Add(new PoEntry(msgctxt, text, msgstr));
            }

            var languageCode = LtextTargetLanguage.Name;
            PoFile.Write(path, poEntries, languageCode);
            StatusMessage = $"Exported {poEntries.Count} LTEXT strings to: {path} (msgstr pre-filled from existing {LtextTargetLanguage.Label} entries where present).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting Poedit file: {ex.Message}";
        }
    }

    /// <summary>
    /// "IMPORTA POEDIT": for every translated (non-empty msgstr) entry in a .po file, parses
    /// its msgctxt back into a source TGI (see <see cref="PoFile.TryParseMsgctxt"/>) - the
    /// LTEXT entry the string was originally exported from - and writes the translated text
    /// into <see cref="LtextTargetLanguage"/>'s TGI for that same Type/Instance (see
    /// <see cref="LtextTgiLanguage.TgiForLanguage"/>), creating the entry if this package
    /// doesn't have one there yet. This is the actual "cambio TGI legato alla lingua" step -
    /// every other LTEXT action either reads or writes exactly this TGI.
    /// </summary>
    public void ImportLtextPoedit(string path)
    {
        try
        {
            var poEntries = PoFile.Parse(path);
            var applied = 0;
            var skippedUntranslated = 0;
            var skippedBadKey = 0;

            foreach (var poEntry in poEntries)
            {
                if (string.IsNullOrEmpty(poEntry.Msgstr))
                {
                    skippedUntranslated++;
                    continue;
                }

                if (!PoFile.TryParseMsgctxt(poEntry.Msgctxt, out var typeId, out var groupId, out var instanceId))
                {
                    skippedBadKey++;
                    continue;
                }

                var sourceTgi = new TGI(typeId, groupId, instanceId);
                var targetTgi = LtextTgiLanguage.TgiForLanguage(sourceTgi, LtextSourceLanguage, LtextTargetLanguage);

                var newEntry = _service.UpsertLtextEntry(targetTgi, poEntry.Msgstr);
                UpsertEntryViewModel(newEntry);
                applied++;
            }

            RefreshDisplayedEntries();

            if (SelectedEntry is not null)
            {
                LoadLtextEditorForSelectedEntry();
            }

            var extra = string.Empty;
            if (skippedUntranslated > 0)
            {
                extra += $", {skippedUntranslated} untranslated skipped";
            }
            if (skippedBadKey > 0)
            {
                extra += $", {skippedBadKey} invalid msgctxt skipped";
            }

            StatusMessage = $"Imported {applied} {LtextTargetLanguage.Label} translations from: {path}{extra} (remember to save).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error importing Poedit file: {ex.Message}";
        }
    }
}
