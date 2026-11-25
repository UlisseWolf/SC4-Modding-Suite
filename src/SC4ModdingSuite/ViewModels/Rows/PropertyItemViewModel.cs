using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Presents a single DBPFProperty of the currently selected Exemplar/Cohort, resolving its
/// friendly name via <see cref="PropertyDefinitionsRegistry"/> when possible.
/// </summary>
public sealed class PropertyItemViewModel : ViewModelBase
{
    private readonly AppOptions? _options;

    /// <param name="options">Shared app options, read for <see cref="AppOptions.ShowReadablePropertyValues"/>; null = always hex.</param>
    public PropertyItemViewModel(DBPFProperty property, PropertyDefinition? definition, AppOptions? options = null)
    {
        Property = property;
        Definition = definition;
        _options = options;
        Refresh();
    }

    public DBPFProperty Property { get; }
    public PropertyDefinition? Definition { get; private set; }

    private string _idHex = string.Empty;
    public string IdHex
    {
        get => _idHex;
        private set => SetField(ref _idHex, value);
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        private set => SetField(ref _name, value);
    }

    private string _dataTypeText = string.Empty;
    public string DataTypeText
    {
        get => _dataTypeText;
        private set => SetField(ref _dataTypeText, value);
    }

    private string _valuesText = string.Empty;
    public string ValuesText
    {
        get => _valuesText;
        private set => SetField(ref _valuesText, value);
    }

    public void SetDefinition(PropertyDefinition? definition)
    {
        Definition = definition;
        Refresh();
    }

    public void Refresh()
    {
        IdHex = $"0x{Property.ID:X8}";
        Name = Definition?.Name ?? "(not present in the property database)";
        DataTypeText = DBPFProperty.LookupDataTypeName(Property.DataType);

        string values;
        try
        {
            var data = Property.GetTypedData();
            values = data is char[] chars
                ? new string(chars)
                : _options?.ShowReadablePropertyValues == true
                    ? string.Join(", ", data.Cast<object>().Select(v => PropertyValueFormatter.FormatReadable(v, Property.DataType, Definition)))
                    : string.Join(", ", data.Cast<object>().Select(v => PropertyValueFormatter.Format(v, Property.DataType)));
        }
        catch
        {
            values = "(unreadable)";
        }

        if (values.Length > 200)
        {
            values = values[..200] + "...";
        }

        ValuesText = values;
    }
}
