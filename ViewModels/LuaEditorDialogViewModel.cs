using System;
using SC4ModdingSuite.Models;

namespace SC4ModdingSuite.ViewModels;

/// <summary>
/// Backs the LUA Editor dialog (Views/LuaEditorDialog.axaml): a code pane on top and a
/// COMPILE/RUN output log below, toolbar-driven - the same shape as Ilive Reader's own
/// script editor frame (<c>FrameScript.cpp</c>: <c>CFormScript</c> on top,
/// <c>CFormRichEdit</c> trace pane below, Save/Go/Stop/Compile toolbar). "SAVE" here closes
/// the dialog with <c>accepted = true</c>; the caller (MainWindow) then writes <see cref="Code"/>
/// back into the selected package entry - saving to *disk* still goes through the app's own
/// normal Save/Save As, exactly like every other entry edit in this app.
/// </summary>
public sealed class LuaEditorDialogViewModel : ViewModelBase
{
    public LuaEditorDialogViewModel(string title, string initialCode)
    {
        Title = title;
        _code = initialCode;

        CompileCommand = new RelayCommand(_ => Compile());
        RunCommand = new RelayCommand(_ => Run());
        ClearOutputCommand = new RelayCommand(_ => Output = string.Empty);
        SaveCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, true));
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(this, false));
    }

    /// <summary>Raised when the dialog should close - true if "SAVE" was clicked, false for "CLOSE".</summary>
    public event EventHandler<bool>? CloseRequested;

    public string Title { get; }

    private string _code;
    public string Code
    {
        get => _code;
        set => SetField(ref _code, value);
    }

    private string _output = string.Empty;
    public string Output
    {
        get => _output;
        private set => SetField(ref _output, value);
    }

    public RelayCommand CompileCommand { get; }
    public RelayCommand RunCommand { get; }
    public RelayCommand ClearOutputCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CloseCommand { get; }

    private void AppendOutput(string line)
    {
        Output = Output.Length == 0 ? line : Output + Environment.NewLine + line;
    }

    private void Compile()
    {
        LuaScriptRunner.TryCompile(Code, out var message);
        AppendOutput($"[compile] {message}");
    }

    private void Run()
    {
        AppendOutput("--- run ---");
        LuaScriptRunner.TryRun(Code, line => AppendOutput(line), out var message);
        AppendOutput($"[run] {message}");
    }
}
