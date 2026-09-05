using System;
using System.Collections.Generic;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>Backs the "Insert Template" dialog (Ilive Reader's DlgTemplate) - see InsertTemplates for the actual template data.</summary>
public sealed class TemplateDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;

    public TemplateDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
        InsertCommand = new RelayCommand(_ => Insert(), _ => SelectedTemplate is not null);
    }

    public IReadOnlyList<InsertTemplate> Templates => InsertTemplates.All;

    private InsertTemplate? _selectedTemplate;
    public InsertTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetField(ref _selectedTemplate, value))
            {
                InsertCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand InsertCommand { get; }

    public event EventHandler? Closed;

    private void Insert()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        try
        {
            // Random Instance so inserting the same template twice (or into a file that
            // already has an entry at Instance 0) never produces a duplicate TGI.
            var instance = TgiGenerator.GenerateRandomId();
            var tgi = new TGI(SelectedTemplate.Tgi.TypeID, SelectedTemplate.Tgi.GroupID, instance);
            var entry = _document.Service.AddNewEntryRaw(tgi, SelectedTemplate.Bytes);

            if (entry is null)
            {
                StatusMessage = "Could not insert the template entry.";
                return;
            }

            _document.ReloadEntries();
            _document.SelectEntryByTgi(tgi);
            Closed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error inserting template: {ex.Message}";
        }
    }
}
