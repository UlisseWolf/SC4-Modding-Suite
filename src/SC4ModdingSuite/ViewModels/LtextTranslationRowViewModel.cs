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
public sealed class LtextTranslationRowViewModel
{
    public LtextTranslationRowViewModel(string rowLabel, TGI baseTgi)
    {
        RowLabel = rowLabel;
        BaseTgi = baseTgi;
    }

    public string RowLabel { get; }
    public TGI BaseTgi { get; }
    public ObservableCollection<LtextTranslationCellViewModel> Cells { get; } = new();
}
