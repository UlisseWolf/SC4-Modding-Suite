using System;
using System.Collections.Generic;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>Which batch tool page is currently selected in the dialog.</summary>
public enum S3DBatchToolMode
{
    FlipMirrorInvert,
    Move,
    Scale,
    Rotate,
    ReplaceTexture,
    SetTransparency,
}

/// <summary>
/// Backs the "S3D Batch Tools" dialog: a clean-room reimplementation (from documented/
/// observed behavior only, no decompiled code) of the geometry/material batch operations
/// a well-known, now-abandoned third-party SC4 modding utility offered for S3D models -
/// Flip/Mirror/Invert, Move, Scale, Rotate, Replace Texture and Set Transparency. Every
/// operation mutates the document's currently selected in-memory S3D model only; nothing
/// is written to the entry until the S3D Editor's own APPLY/SAVE button is used afterwards
/// - same contract as the existing Merge/Import 3DS tools next to it.
/// </summary>
public sealed class S3DBatchToolsDialogViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _document;

    public S3DBatchToolsDialogViewModel(MainWindowViewModel document)
    {
        _document = document;

        ApplyFlipMirrorInvertCommand = new RelayCommand(_ => ApplyFlipMirrorInvert());
        ApplyMoveCommand = new RelayCommand(_ => ApplyMove());
        ApplyScaleCommand = new RelayCommand(_ => ApplyScale());
        ApplyRotateCommand = new RelayCommand(_ => ApplyRotate());
        ApplyReplaceTextureCommand = new RelayCommand(_ => ApplyReplaceTexture());
        ApplySetTransparencyCommand = new RelayCommand(_ => ApplySetTransparency());
    }

    public IReadOnlyList<S3DBatchToolMode> Modes { get; } = new[]
    {
        S3DBatchToolMode.FlipMirrorInvert,
        S3DBatchToolMode.Move,
        S3DBatchToolMode.Scale,
        S3DBatchToolMode.Rotate,
        S3DBatchToolMode.ReplaceTexture,
        S3DBatchToolMode.SetTransparency,
    };

    private S3DBatchToolMode _selectedMode = S3DBatchToolMode.FlipMirrorInvert;
    public S3DBatchToolMode SelectedMode
    {
        get => _selectedMode;
        set => SetField(ref _selectedMode, value);
    }

    // --- Flip/Mirror/Invert ---
    public IReadOnlyList<S3DEditOps.FlipMode> FlipModes { get; } = new[]
    {
        S3DEditOps.FlipMode.Flip,
        S3DEditOps.FlipMode.Mirror,
        S3DEditOps.FlipMode.Invert,
    };

    private S3DEditOps.FlipMode _selectedFlipMode = S3DEditOps.FlipMode.Flip;
    public S3DEditOps.FlipMode SelectedFlipMode
    {
        get => _selectedFlipMode;
        set => SetField(ref _selectedFlipMode, value);
    }

    // --- Move ---
    private string _moveXText = "0";
    public string MoveXText { get => _moveXText; set => SetField(ref _moveXText, value); }

    private string _moveYText = "0";
    public string MoveYText { get => _moveYText; set => SetField(ref _moveYText, value); }

    private string _moveZText = "0";
    public string MoveZText { get => _moveZText; set => SetField(ref _moveZText, value); }

    // --- Scale ---
    private string _scaleFactorText = "1";
    public string ScaleFactorText { get => _scaleFactorText; set => SetField(ref _scaleFactorText, value); }

    // --- Rotate ---
    private string _rotateDegreesText = "90";
    public string RotateDegreesText { get => _rotateDegreesText; set => SetField(ref _rotateDegreesText, value); }

    private bool _rotateClockwise = true;
    public bool RotateClockwise { get => _rotateClockwise; set => SetField(ref _rotateClockwise, value); }

    // --- Replace Texture ---
    private string _findMask = "xxxxxxxx";
    public string FindMask { get => _findMask; set => SetField(ref _findMask, value); }

    private string _replaceMask = "xxxxxxxx";
    public string ReplaceMask { get => _replaceMask; set => SetField(ref _replaceMask, value); }

    // --- Set Transparency ---
    private string _depthFuncText = "1";
    public string DepthFuncText { get => _depthFuncText; set => SetField(ref _depthFuncText, value); }

    private bool _transparencyEnabled = true;
    public bool TransparencyEnabled { get => _transparencyEnabled; set => SetField(ref _transparencyEnabled, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public RelayCommand ApplyFlipMirrorInvertCommand { get; }
    public RelayCommand ApplyMoveCommand { get; }
    public RelayCommand ApplyScaleCommand { get; }
    public RelayCommand ApplyRotateCommand { get; }
    public RelayCommand ApplyReplaceTextureCommand { get; }
    public RelayCommand ApplySetTransparencyCommand { get; }

    private void ApplyFlipMirrorInvert()
    {
        _document.ApplyS3DFlipMirrorInvert(SelectedFlipMode);
        StatusMessage = _document.StatusMessage;
    }

    private void ApplyMove()
    {
        if (!TryParseFloat(MoveXText, out var dx) || !TryParseFloat(MoveYText, out var dy) || !TryParseFloat(MoveZText, out var dz))
        {
            StatusMessage = "X/Y/Z offsets must be valid numbers.";
            return;
        }

        _document.ApplyS3DMove(dx, dy, dz);
        StatusMessage = _document.StatusMessage;
    }

    private void ApplyScale()
    {
        if (!TryParseFloat(ScaleFactorText, out var factor))
        {
            StatusMessage = "Scale factor must be a valid number.";
            return;
        }

        _document.ApplyS3DScale(factor);
        StatusMessage = _document.StatusMessage;
    }

    private void ApplyRotate()
    {
        if (!double.TryParse(RotateDegreesText, System.Globalization.CultureInfo.InvariantCulture, out var degrees))
        {
            StatusMessage = "Angle must be a valid number of degrees.";
            return;
        }

        _document.ApplyS3DRotate(degrees, RotateClockwise);
        StatusMessage = _document.StatusMessage;
    }

    private void ApplyReplaceTexture()
    {
        _document.ApplyS3DReplaceTexture(FindMask, ReplaceMask);
        StatusMessage = _document.StatusMessage;
    }

    private void ApplySetTransparency()
    {
        if (!byte.TryParse(DepthFuncText, out var depthFunc))
        {
            StatusMessage = "Depth Func must be a whole number between 0 and 255.";
            return;
        }

        _document.ApplyS3DSetTransparency(depthFunc, TransparencyEnabled);
        StatusMessage = _document.StatusMessage;
    }

    private static bool TryParseFloat(string text, out float value) =>
        float.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out value);
}
