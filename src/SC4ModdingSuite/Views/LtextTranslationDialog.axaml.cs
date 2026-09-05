using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SC4ModdingSuite.Models;
using SC4ModdingSuite.ViewModels;

namespace SC4ModdingSuite.Views;

/// <summary>See LtextTranslationDialog.axaml - groups LTEXT language families side by side, with optional AI-assisted first-draft translation.</summary>
public partial class LtextTranslationDialog : Window
{
    private readonly LlmTranslationService _llm = new();

    public LtextTranslationDialog()
    {
        InitializeComponent();
    }

    public LtextTranslationDialog(MainWindowViewModel document) : this()
    {
        DataContext = new LtextTranslationDialogViewModel(document);
    }

    private LtextTranslationDialogViewModel ViewModel => (LtextTranslationDialogViewModel)DataContext!;

    private void OnAddLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LtextTranslationRowViewModel row })
        {
            return;
        }

        ViewModel.Document.EnsureLtextTranslationCell(row, ViewModel.SelectedLanguage);
    }

    /// <summary>
    /// "AI TRANSLATE →": fills (creating if needed) this row's cell for the language picked
    /// in the combo above, using whichever other cell in the row already has text as the
    /// source - never auto-saved, the result just lands in the grid like any other edit.
    /// </summary>
    private async void OnAiTranslateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LtextTranslationRowViewModel row })
        {
            return;
        }

        var targetLanguage = ViewModel.SelectedLanguage;
        var targetCell = ViewModel.Document.EnsureLtextTranslationCell(row, targetLanguage);

        var sourceCell = row.Cells.FirstOrDefault(c => c != targetCell && !string.IsNullOrWhiteSpace(c.Text));
        if (sourceCell is null)
        {
            ViewModel.Document.SetStatusMessage("AI Translate: this row has no other language with text to translate from yet.");
            return;
        }

        targetCell.IsTranslating = true;
        try
        {
            var translated = await _llm.TranslateAsync(
                ViewModel.Document.AppOptions, sourceCell.Text, sourceCell.LanguageLabel, targetLanguage.Label);
            targetCell.Text = translated;
            ViewModel.Document.SetStatusMessage($"AI Translate: drafted {targetLanguage.Label} from {sourceCell.LanguageLabel} - review before SAVE ALL.");
        }
        catch (Exception ex)
        {
            ViewModel.Document.SetStatusMessage($"AI Translate error: {ex.Message}");
        }
        finally
        {
            targetCell.IsTranslating = false;
        }
    }

    private void OnSaveAllClick(object? sender, RoutedEventArgs e) => ViewModel.Document.SaveLtextTranslationGrid(ViewModel.Rows);

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => ViewModel.Refresh();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
