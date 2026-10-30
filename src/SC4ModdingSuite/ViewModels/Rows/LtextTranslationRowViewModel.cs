using System;
using System.Collections.ObjectModel;
using csDBPF;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One editable language cell in the LTEXT Translation Grid - see
/// MainWindowViewModel.BuildLtextTranslationRows(). <see cref="Tgi"/> is the exact TGI this
/// cell's text either already lives at, or will be created at once saved;
/// <see cref="Exists"/> just tracks whether an entry is already there (cosmetic - SAVE
/// upserts either way).
/// </summary>
public sealed class LtextTranslationCellViewModel : ViewModelBase
{
    public LtextTranslationCellViewModel(string languageLabel, TGI tgi, string text, bool exists)
    {
        LanguageLabel = languageLabel;
        Tgi = tgi;
        _text = text;
        Exists = exists;
    }

    public string LanguageLabel { get; }
    public TGI Tgi { get; }
    public bool Exists { get; set; }

    private string _text;
    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    private bool _isTranslating;
    public bool IsTranslating
    {
        get => _isTranslating;
        set => SetField(ref _isTranslating, value);
    }
}

/// <summary>One LTEXT "family" (same Type + Instance ID, only Group differs by language offset - see LtextTgiLanguage) - one row of the Translation Grid, one cell per language currently present.</summary>
public sealed class LtextTranslationRowViewModel : ViewModelBase
{
    /// <summary>Most language cells shown side by side in one row; further languages wrap onto a new line instead of squeezing every box narrower.</summary>
    private const int MaxCellColumns = 3;

    public LtextTranslationRowViewModel(string rowLabel, TGI baseTgi)
    {
        RowLabel = rowLabel;
        BaseTgi = baseTgi;
        Cells.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CellColumns));
    }

    public string RowLabel { get; }
    public TGI BaseTgi { get; }
    public ObservableCollection<LtextTranslationCellViewModel> Cells { get; } = new();

    /// <summary>
    /// Columns of the row's cell grid: one per language up to <see cref="MaxCellColumns"/>, so
    /// the edit boxes share the full available width (a single language gets the whole row)
    /// instead of staying a fixed narrow width with the rest of the window empty.
    /// </summary>
    public int CellColumns => Math.Clamp(Cells.Count, 1, MaxCellColumns);
}
