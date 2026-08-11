using System;
using System.Collections.Generic;
using System.Linq;
using csDBPF;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the "Clone/Duplicate" dialog (Ilive Reader's DlgClone): makes N copies of each
/// selected entry, optionally bumping Type/Group/Instance by a fixed hex amount after
/// every copy (so cloning 3 copies with a Group increment of 1 produces G, G+1, G+2, ...).
/// </summary>
public sealed class CloneDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;
    private readonly IReadOnlyList<DBPFEntry> _sourceEntries;

    public CloneDialogViewModel(MainWindowViewModel document, IReadOnlyList<DBPFEntry> sourceEntries)
    {
        _document = document;
        _sourceEntries = sourceEntries;
        CloneCommand = new RelayCommand(_ => Clone(), _ => _sourceEntries.Count > 0);
    }

    public int SourceCount => _sourceEntries.Count;

    private int _copyCount = 1;
    public int CopyCount
    {
        get => _copyCount;
        set => SetField(ref _copyCount, value);
    }

    private bool _incrementType;
    public bool IncrementType
    {
        get => _incrementType;
        set => SetField(ref _incrementType, value);
    }

    private string _typeIncrementHex = "0x00000000";
    public string TypeIncrementHex
    {
        get => _typeIncrementHex;
        set => SetField(ref _typeIncrementHex, value);
    }

    private bool _incrementGroup;
    public bool IncrementGroup
    {
        get => _incrementGroup;
        set => SetField(ref _incrementGroup, value);
    }

    private string _groupIncrementHex = "0x00000001";
    public string GroupIncrementHex
    {
        get => _groupIncrementHex;
        set => SetField(ref _groupIncrementHex, value);
    }

    private bool _incrementInstance;
    public bool IncrementInstance
    {
        get => _incrementInstance;
        set => SetField(ref _incrementInstance, value);
    }

    private string _instanceIncrementHex = "0x00000001";
    public string InstanceIncrementHex
    {
        get => _instanceIncrementHex;
        set => SetField(ref _instanceIncrementHex, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand CloneCommand { get; }

    private void Clone()
    {
        if (CopyCount <= 0)
        {
            StatusMessage = "Copy count must be at least 1.";
            return;
        }

        try
        {
            var typeInc = IncrementType ? EntryClipboard.ParseHex(TypeIncrementHex) : 0u;
            var groupInc = IncrementGroup ? EntryClipboard.ParseHex(GroupIncrementHex) : 0u;
            var instanceInc = IncrementInstance ? EntryClipboard.ParseHex(InstanceIncrementHex) : 0u;

            var created = 0;
            foreach (var source in _sourceEntries)
            {
                var type = source.TGI.TypeID;
                var group = source.TGI.GroupID;
                var instance = source.TGI.InstanceID;
                var bytes = source.ByteData ?? Array.Empty<byte>();
                var typeName = source.GetType().AssemblyQualifiedName!;

                for (var i = 0; i < CopyCount; i++)
                {
                    type += typeInc;
                    group += groupInc;
                    instance += instanceInc;

                    var newEntry = _document.Service.AddEntryFromClipboard(typeName, new TGI(type, group, instance), bytes);
                    if (newEntry is not null)
                    {
                        created++;
                    }
                }
            }

            _document.ReloadEntries();
            StatusMessage = $"Created {created} clone(s) from {_sourceEntries.Count} source entr{(_sourceEntries.Count == 1 ? "y" : "ies")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error while cloning: {ex.Message}";
        }
    }
}
