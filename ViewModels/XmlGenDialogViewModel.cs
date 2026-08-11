using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using csDBPF;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Ilive Reader's "XML Property Generator" (DlgXmlGen/DlgXmlGenName): picks an Exemplar of
/// a chosen ExemplarType (property 0x10) from the currently open package and builds an
/// SC4PLUGINDESC XML descriptor from its properties - the same format Ilive Reader itself
/// writes (see DlgXmlGen.cpp's OnGenerate), then adds it as a new entry in the open
/// package (type 0x88777602, matching Ilive Reader's own AddFile call).
///
/// ponytail: the model filename prompt (Ilive Reader's separate DlgXmlGenName modal) is
/// folded into a text field on this same dialog instead of a second popup - one dialog,
/// same information.
/// </summary>
public sealed class XmlGenDialogViewModel : ViewModelBase
{
    private const uint XmlEntryTypeId = 0x88777602;
    private const uint ExemplarTypePropertyId = 0x10;
    private const uint OccupantSizePropertyId = 0x27812810;
    private const uint ResourceKeyType1PropertyId = 0x27812821;

    private readonly MainWindowViewModel _document;

    public XmlGenDialogViewModel(MainWindowViewModel document)
    {
        _document = document;
        RefreshCommand = new RelayCommand(_ => Refresh());
        GenerateCommand = new RelayCommand(_ => Generate(), _ => SelectedCandidate is not null);
        Refresh();
    }

    private string _exemplarTypeHex = "02";
    public string ExemplarTypeHex
    {
        get => _exemplarTypeHex;
        set => SetField(ref _exemplarTypeHex, value);
    }

    public ObservableCollection<EntryItemViewModel> Candidates { get; } = new();

    private EntryItemViewModel? _selectedCandidate;
    public EntryItemViewModel? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (SetField(ref _selectedCandidate, value))
            {
                GenerateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _modelFileName = string.Empty;
    public string ModelFileName
    {
        get => _modelFileName;
        set => SetField(ref _modelFileName, value);
    }

    private string _statusMessage = "Set the ExemplarType (hex, e.g. 02 = Building), REFRESH, pick an exemplar, then GENERATE.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand GenerateCommand { get; }

    private void Refresh()
    {
        Candidates.Clear();
        SelectedCandidate = null;

        if (!uint.TryParse(ExemplarTypeHex.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var wantedType))
        {
            StatusMessage = "ExemplarType must be a hex byte (e.g. 02).";
            return;
        }

        foreach (var vm in _document.Entries)
        {
            if (vm.Entry is not DBPFEntryEXMP exemplar)
            {
                continue;
            }

            try
            {
                exemplar.Decode();
                if (exemplar.ListOfProperties.TryGetValue(ExemplarTypePropertyId, out var typeProp))
                {
                    var data = typeProp.GetTypedData();
                    if (data.Length > 0 && Convert.ToInt64(data.GetValue(0)) == wantedType)
                    {
                        Candidates.Add(vm);
                    }
                }
            }
            catch
            {
                // Skip exemplars that don't decode cleanly - same as every other batch scan in this app.
            }
        }

        StatusMessage = $"{Candidates.Count} exemplar(s) with ExemplarType 0x{wantedType:X2}.";
    }

    /// <summary>Same value formatting as Ilive Reader's DlgXmlGen::OnGenerate switch, per csDBPF PropertyDataType.</summary>
    private static string FormatValue(DBPFProperty property)
    {
        var data = property.GetTypedData();
        return property.DataType switch
        {
            DBPFProperty.PropertyDataType.STRING => data is char[] chars ? new string(chars) : string.Join("", data.Cast<object>()),
            DBPFProperty.PropertyDataType.UINT32 => string.Join(" ", data.Cast<object>().Select(v => $"0x{Convert.ToUInt32(v):X8}")),
            DBPFProperty.PropertyDataType.UINT8 => string.Join(" ", data.Cast<object>().Select(v => $"0x{Convert.ToByte(v):X2}")),
            DBPFProperty.PropertyDataType.FLOAT32 => string.Join(" ", data.Cast<object>().Select(v => Convert.ToSingle(v).ToString("0.000", CultureInfo.InvariantCulture))),
            DBPFProperty.PropertyDataType.BOOL or DBPFProperty.PropertyDataType.SINT32 or DBPFProperty.PropertyDataType.SINT64 =>
                string.Join(" ", data.Cast<object>().Select(v => Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture))),
            _ => string.Join(" ", data.Cast<object>().Select(v => v?.ToString() ?? string.Empty)),
        };
    }

    private void Generate()
    {
        if (SelectedCandidate?.Entry is not DBPFEntryEXMP exemplar)
        {
            return;
        }

        try
        {
            exemplar.Decode();

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<SC4PLUGINDESC Version=\"0x00000030\">\r\n");

            if (exemplar.ListOfProperties.TryGetValue(OccupantSizePropertyId, out var sizeProp))
            {
                var v = sizeProp.GetTypedData();
                if (v.Length >= 3)
                {
                    var width = Convert.ToSingle(v.GetValue(0));
                    var height = Convert.ToSingle(v.GetValue(1));
                    var depth = Convert.ToSingle(v.GetValue(2));
                    sb.Append($"<DIMENSIONS Depth=\"{depth:0.000}\" Height=\"{height:0.000}\" Width=\"{width:0.000}\"/>\r\n");
                }
            }

            if (exemplar.ListOfProperties.TryGetValue(ResourceKeyType1PropertyId, out var keyProp))
            {
                var v = keyProp.GetTypedData();
                if (v.Length >= 3)
                {
                    var t = Convert.ToUInt32(v.GetValue(0));
                    var g = Convert.ToUInt32(v.GetValue(1));
                    var i = Convert.ToUInt32(v.GetValue(2));
                    var fileName = string.IsNullOrWhiteSpace(ModelFileName)
                        ? $"model-0x{t:x8}_0x{g:x8}_0x{i:x8}.SC4Model"
                        : ModelFileName;
                    sb.Append($"<PLUGIN File=\"{fileName}\" ResKey=\"0x{t:x8}-0x{g:x8}-0x{i:x8}\"/>\r\n");
                }
            }

            foreach (var kvp in exemplar.ListOfProperties.OrderBy(p => p.Key))
            {
                try
                {
                    sb.Append($"<PROPERTY ID=\"0x{kvp.Key:x8}\" Value=\"{FormatValue(kvp.Value)}\"/>\r\n");
                }
                catch
                {
                    // ponytail: one property whose typed data can't be formatted (unexpected/corrupt) shouldn't abort the whole XML.
                }
            }

            sb.Append("</SC4PLUGINDESC>");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var tgi = new TGI(XmlEntryTypeId, exemplar.TGI.GroupID, exemplar.TGI.InstanceID);
            var newEntry = _document.Service.AddNewEntryRaw(tgi, bytes);
            if (newEntry is null)
            {
                StatusMessage = "Could not add the XML entry (internal entry type unavailable).";
                return;
            }

            _document.ReloadEntries();
            StatusMessage = $"SC4PLUGINDESC XML added as entry 0x{XmlEntryTypeId:X8}/{exemplar.TGI.GroupID:X8}/{exemplar.TGI.InstanceID:X8} (remember to save the package).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating XML: {ex.Message}";
        }
    }
}
