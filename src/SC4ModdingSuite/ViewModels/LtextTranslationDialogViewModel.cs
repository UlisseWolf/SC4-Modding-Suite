using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>Backs LtextTranslationDialog - see that view's own doc comment.</summary>
public sealed class LtextTranslationDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;

    public LtextTranslationDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
        Languages = LtextLanguages.All;
        _selectedLanguage = LtextLanguages.All.FirstOrDefault(l => l.Name == "Italian") ?? LtextLanguages.All[0];
        Refresh();
    }

    /// <summary>Exposed so the dialog's own code-behind can reach DbpfService/AppOptions through the same MainWindowViewModel the rest of this app already shares.</summary>
    public MainWindowViewModel Document => _document;

    public IReadOnlyList<LtextLanguage> Languages { get; }

    private LtextLanguage _selectedLanguage;
    public LtextLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetField(ref _selectedLanguage, value);
    }

    public ObservableCollection<LtextTranslationRowViewModel> Rows { get; } = new();

    private string _aiStatusText = string.Empty;
    public string AiStatusText
    {
        get => _aiStatusText;
        private set => SetField(ref _aiStatusText, value);
    }

    /// <summary>Re-scans the package for LTEXT entries and rebuilds <see cref="Rows"/> - called on open and by the dialog's own REFRESH button.</summary>
    public void Refresh()
    {
        Rows.Clear();
        foreach (var row in _document.BuildLtextTranslationRows())
        {
            Rows.Add(row);
        }

        UpdateAiStatus();
    }

    public void UpdateAiStatus()
    {
        var options = _document.AppOptions;
        AiStatusText = string.IsNullOrWhiteSpace(options.LlmApiKey)
            ? "AI translation: no API key set - see Options > AI TRANSLATION (optional)"
            : string.IsNullOrWhiteSpace(options.LlmModel)
                ? "AI translation: API key set, but no model name - see Options > AI TRANSLATION (optional)"
                : $"AI translation: ready ({options.LlmModel} via {options.LlmBaseUrl})";
    }
}
