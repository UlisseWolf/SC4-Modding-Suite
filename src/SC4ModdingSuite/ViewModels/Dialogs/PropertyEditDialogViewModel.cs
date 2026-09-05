using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Add property" / "Edit property" dialog. In add mode, the person
/// searches the property database (<see cref="PropertyDefinitionsRegistry"/>) by name or ID
/// to pick which property to add (or types a raw, unlisted ID/type for an advanced/custom
/// property). In edit mode, ID and known metadata are pre-filled from the existing property
/// and only the value(s) are meant to change.
/// </summary>
public sealed class PropertyEditDialogViewModel : ViewModelBase
{
    private readonly PropertyDefinitionsRegistry _registry;

    public PropertyEditDialogViewModel(PropertyDefinitionsRegistry registry, DBPFProperty? existing)
    {
        _registry = registry;
        IsEditMode = existing is not null;
        Title = IsEditMode ? "Edit property" : "Add property";

        SearchResults = new ObservableCollection<PropertyDefinition>();
        RefreshSearch();

        OkCommand = new RelayCommand(_ => TryAccept());
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, false));
        AddOptionValueCommand = new RelayCommand(_ => AddSelectedOptionValue(), _ => SelectedOption is not null);
        ClearValuesCommand = new RelayCommand(_ => ValuesText = string.Empty);

        if (existing is not null)
        {
            var definition = registry.FindById(existing.ID);
            _selectedDefinition = definition;
            _idText = $"0x{existing.ID:X8}";
            _selectedDataType = existing.DataType;
            _valuesText = FormatValues(existing);
        }
    }

    public string Title { get; }
    public bool IsAddMode => !IsEditMode;
    public bool IsEditMode { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                RefreshSearch();
            }
        }
    }

    public ObservableCollection<PropertyDefinition> SearchResults { get; }

    private PropertyDefinition? _selectedDefinition;
    public PropertyDefinition? SelectedDefinition
    {
        get => _selectedDefinition;
        set
        {
            if (SetField(ref _selectedDefinition, value) && value is not null && IsAddMode)
            {
                IdText = $"0x{value.Id:X8}";
                SelectedDataType = value.DataType;
                if (!string.IsNullOrEmpty(value.DefaultValue))
                {
                    ValuesText = value.DefaultValue;
                }
            }

            OnPropertyChanged(nameof(ResolvedName));
            OnPropertyChanged(nameof(HasOptions));
            OnPropertyChanged(nameof(OptionChoices));
            OnPropertyChanged(nameof(Description));
        }
    }

    private string _idText = "0x00000000";
    public string IdText
    {
        get => _idText;
        set => SetField(ref _idText, value);
    }

    public string ResolvedName => SelectedDefinition?.Name
        ?? (IsEditMode ? "(property not present in the database)" : "(custom property / manual ID)");

    public string? Description => SelectedDefinition?.Description;

    public static IReadOnlyList<DBPFProperty.PropertyDataType> DataTypeChoices { get; } =
        Enum.GetValues<DBPFProperty.PropertyDataType>()
            .Where(t => t != DBPFProperty.PropertyDataType.UNKNOWN)
            .ToList();

    private DBPFProperty.PropertyDataType _selectedDataType = DBPFProperty.PropertyDataType.UINT32;
    public DBPFProperty.PropertyDataType SelectedDataType
    {
        get => _selectedDataType;
        set
        {
            if (SetField(ref _selectedDataType, value))
            {
                OnPropertyChanged(nameof(HasOptions));
            }
        }
    }

    public bool HasOptions => SelectedDefinition is { Options.Count: > 0 };

    public IReadOnlyList<PropertyDefinitionOption> OptionChoices => SelectedDefinition?.Options
        ?? Array.Empty<PropertyDefinitionOption>();

    /// <summary>
    /// The option currently highlighted in the picker. Unlike an earlier version of this
    /// dialog, selecting an option no longer *replaces* whatever is already in
    /// <see cref="ValuesText"/> - many categorical properties (Occupant Groups being the
    /// classic example) legitimately hold several values at once, so a single-select
    /// "pick one, overwrite everything" control was actively wrong for them. Use
    /// <see cref="AddOptionValueCommand"/> to append this option's value to the list
    /// instead - see that command for details.
    /// </summary>
    private PropertyDefinitionOption? _selectedOption;
    public PropertyDefinitionOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (SetField(ref _selectedOption, value))
            {
                AddOptionValueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Appends <see cref="SelectedOption"/>'s value to <see cref="ValuesText"/> (comma-separated), instead of replacing it - supports properties like Occupant Groups that hold several values.</summary>
    public RelayCommand AddOptionValueCommand { get; }

    /// <summary>Empties <see cref="ValuesText"/> to start a fresh multi-value list from scratch.</summary>
    public RelayCommand ClearValuesCommand { get; }

    private void AddSelectedOptionValue()
    {
        if (SelectedOption is null)
        {
            return;
        }

        var existingValues = ValuesText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (!existingValues.Contains(SelectedOption.Value, StringComparer.OrdinalIgnoreCase))
        {
            existingValues.Add(SelectedOption.Value);
        }

        ValuesText = string.Join(", ", existingValues);
    }

    private string _valuesText = "0";
    public string ValuesText
    {
        get => _valuesText;
        set => SetField(ref _valuesText, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }

    /// <summary>Populated once <see cref="OkCommand"/> succeeds.</summary>
    public DBPFProperty? Result { get; private set; }

    public event EventHandler<bool>? CloseRequested;

    private void RefreshSearch()
    {
        SearchResults.Clear();
        foreach (var definition in _registry.Search(SearchText).Take(300))
        {
            SearchResults.Add(definition);
        }
    }

    private void TryAccept()
    {
        ErrorMessage = string.Empty;
        try
        {
            var id = EntryClipboard.ParseHex(IdText);
            Result = BuildProperty(id, SelectedDataType, ValuesText);
            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Invalid value: {ex.Message}";
        }
    }

    private static string FormatValues(DBPFProperty property)
    {
        try
        {
            var data = property.GetTypedData();
            if (data is char[] chars)
            {
                return new string(chars);
            }

            return string.Join(", ", data.Cast<object>().Select(v => PropertyValueFormatter.Format(v, property.DataType)));
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Builds a new csDBPF <see cref="DBPFProperty"/> of the concrete subtype matching
    /// <paramref name="dataType"/> (<see cref="DBPFPropertyFloat"/>,
    /// <see cref="DBPFPropertyString"/>, or <see cref="DBPFPropertyLong"/> for every integer
    /// and bool type), parsing <paramref name="rawValues"/> (comma-separated, except for
    /// STRING which is taken as a single literal string).
    /// </summary>
    private static DBPFProperty BuildProperty(uint id, DBPFProperty.PropertyDataType dataType, string rawValues)
    {
        DBPFProperty property = dataType switch
        {
            DBPFProperty.PropertyDataType.FLOAT32 => BuildFloat(rawValues),
            DBPFProperty.PropertyDataType.STRING => new DBPFPropertyString(rawValues, DBPF.Encoding.Binary),
            _ => BuildLong(dataType, rawValues),
        };

        property.ID = id;
        return property;
    }

    private static DBPFPropertyFloat BuildFloat(string rawValues)
    {
        var values = SplitValues(rawValues).Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        return new DBPFPropertyFloat(values, DBPF.Encoding.Binary);
    }

    private static DBPFPropertyLong BuildLong(DBPFProperty.PropertyDataType dataType, string rawValues)
    {
        var parts = SplitValues(rawValues);
        var values = new long[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var text = parts[i].Trim();
            values[i] = dataType == DBPFProperty.PropertyDataType.BOOL
                ? (IsTruthy(text) ? 1L : 0L)
                : ParseIntegerLiteral(text);
        }

        return new DBPFPropertyLong(dataType, values, DBPF.Encoding.Binary);
    }

    private static string[] SplitValues(string rawValues)
    {
        var parts = rawValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? new[] { "0" } : parts;
    }

    private static bool IsTruthy(string text) =>
        text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1";

    private static long ParseIntegerLiteral(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt64(text[2..], 16);
        }

        return long.Parse(text, CultureInfo.InvariantCulture);
    }
}
