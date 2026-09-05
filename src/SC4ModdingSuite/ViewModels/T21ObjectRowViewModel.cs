using System;
using System.Collections.Generic;
using System.Linq;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// One row in the T21 Editor's "LOT OBJECTS" grid - one Prop or Flora placed on the lot,
/// backed by one <c>LotConfigPropertyLotObject</c>-style property
/// (<see cref="T21Constants.ObjectsBase"/> + N). A faithful, buffered (no live/underlying
/// property until SAVE, matching this app's LUA/S3D/UI editors) re-implementation of
/// Jondor's own <c>T21EditWindow.PropFloraEncap</c> inner class.
///
/// Position/bounds are kept here as plain floats (already converted from the file's
/// fixed-point 16.16 <c>long</c> encoding - divide/multiply by 0x10000, exactly as Jondor's
/// own <c>updatePropPos</c>/<c>saveData</c> do) so the grid/detail panel can bind to them
/// directly without any per-keystroke fixed-point math in the view.
/// </summary>
public sealed class T21ObjectRowViewModel : ViewModelBase
{
    private string _objectType = "Prop";
    public string ObjectType
    {
        get => _objectType;
        set { if (SetField(ref _objectType, value)) OnPropertyChanged(nameof(Summary)); }
    }

    private string _lod = "All";
    public string Lod
    {
        get => _lod;
        set => SetField(ref _lod, value);
    }

    /// <summary>Jondor's unlabeled per-object "Flag" checkbox (low bit of the LOD byte).</summary>
    private bool _flag;
    public bool Flag
    {
        get => _flag;
        set => SetField(ref _flag, value);
    }

    private string _rotation = "South (0)";
    public string Rotation
    {
        get => _rotation;
        set => SetField(ref _rotation, value);
    }

    private float _x;
    public float X { get => _x; set => SetField(ref _x, value); }

    private float _y;
    public float Y { get => _y; set => SetField(ref _y, value); }

    private float _z;
    public float Z { get => _z; set => SetField(ref _z, value); }

    private float _xMin;
    public float XMin { get => _xMin; set => SetField(ref _xMin, value); }

    private float _zMin;
    public float ZMin { get => _zMin; set => SetField(ref _zMin, value); }

    private float _xMax;
    public float XMax { get => _xMax; set => SetField(ref _xMax, value); }

    private float _zMax;
    public float ZMax { get => _zMax; set => SetField(ref _zMax, value); }

    /// <summary>Jondor's "Object#" - value 11 of the raw property, a free-form grouping/key value (not itself an IID).</summary>
    private string _objectKeyHex = "0x00000000";
    public string ObjectKeyHex
    {
        get => _objectKeyHex;
        set => SetField(ref _objectKeyHex, value);
    }

    /// <summary>
    /// The prop/flora exemplar IID(s) this row can randomly place, comma-separated hex.
    /// A Flora row only ever uses the first value; a Prop row may list several variants
    /// the game picks between at random (Jondor's <c>PropFloraEncap.IIDs</c> list).
    /// </summary>
    private string _iidsText = "0x00000000";
    public string IidsText
    {
        get => _iidsText;
        set { if (SetField(ref _iidsText, value)) OnPropertyChanged(nameof(Summary)); }
    }

    /// <summary>Short label for the grid row / list, e.g. "Prop  0x1234ABCD".</summary>
    public string Summary => $"{ObjectType}  {IidsText.Split(',').FirstOrDefault()?.Trim()}";

    public T21ObjectRowViewModel()
    {
    }

    /// <summary>Builds a row from one decoded <c>LotConfigPropertyLotObject</c>-style property's raw values (Jondor's own load loop in <c>initData</c>).</summary>
    public static T21ObjectRowViewModel FromRawValues(long[] v)
    {
        var row = new T21ObjectRowViewModel();
        if (v.Length < 12)
        {
            return row;
        }

        row.ObjectType = T21Constants.ObjectTypeName(v[0]);
        row.Lod = T21Constants.LodName(v[1] & 0xF0L);
        row.Flag = (v[1] & 0x0FL) != 0;

        var rot = v[2];
        rot = rot is < 0 or > 3 ? 0 : rot;
        row.Rotation = T21Constants.RotationOptions[(int)rot];

        row.X = (float)v[3] / 0x10000;
        row.Y = (float)(int)v[4] / 0x10000; // signed - height can be negative
        row.Z = (float)v[5] / 0x10000;
        row.XMin = (float)v[6] / 0x10000;
        row.ZMin = (float)v[7] / 0x10000;
        row.XMax = (float)v[8] / 0x10000;
        row.ZMax = (float)v[9] / 0x10000;
        // v[10] is always reserved/unused (Jondor writes a literal 0 there on save).
        row.ObjectKeyHex = $"0x{(uint)v[11]:X8}";
        row.IidsText = string.Join(", ", v.Skip(12).Select(iid => $"0x{(uint)iid:X8}"));

        return row;
    }

    /// <summary>Builds the raw value list for this row's <c>LotConfigPropertyLotObject</c>-style property (Jondor's own <c>saveData</c> write loop).</summary>
    public long[] ToRawValues()
    {
        var rotIndex = Math.Max(0, T21Constants.RotationOptions.ToList().IndexOf(Rotation));

        var iids = IidsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseHex)
            .ToList();
        if (iids.Count == 0)
        {
            iids.Add(0L);
        }

        // A Flora row only ever stores one IID (Jondor: "else values.add(pfe.IIDs.get(0))").
        if (ObjectType == "Flora" && iids.Count > 1)
        {
            iids = new List<long> { iids[0] };
        }

        var values = new List<long>
        {
            T21Constants.ObjectTypeCode(ObjectType),
            T21Constants.LodCode(Lod) + (Flag ? 1 : 0),
            rotIndex,
            (long)(X * 0x10000),
            (long)(int)(Y * 0x10000),
            (long)(Z * 0x10000),
            (long)(XMin * 0x10000),
            (long)(ZMin * 0x10000),
            (long)(XMax * 0x10000),
            (long)(ZMax * 0x10000),
            0L,
            ParseHex(ObjectKeyHex),
        };
        values.AddRange(iids);

        return values.ToArray();
    }

    private static long ParseHex(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return Convert.ToInt64(string.IsNullOrEmpty(text) ? "0" : text, 16) & 0xFFFFFFFFL;
    }
}
