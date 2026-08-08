using System;
using MoonSharp.Interpreter;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Thin wrapper around MoonSharp giving the LUA Editor its "COMPILE" and "RUN" buttons -
/// the .NET/Avalonia equivalent of Ilive Reader's own embedded Lua VM
/// (<c>LuaLib/</c> + <c>LuaVirtualMachine.cpp</c>/<c>LuaScript.cpp</c>, wired up to the
/// script editor via <c>FrameScript.cpp</c>'s Compile/Go toolbar buttons). MoonSharp is used
/// instead of vendoring/porting that C Lua source (or P/Invoking a native lua5x.dll) because
/// it is a single, pure-managed NuGet package with no native binary to ship per-platform -
/// the same reasoning that already keeps this project's only other third-party dependency
/// (csDBPF's SixLabors.ImageSharp) to a minimum.
///
/// <para>
/// Note this is a real Lua interpreter (Lua 5.2-ish language, MoonSharp's own bytecode), not
/// SimCity 4's actual in-game Lua environment - SC4-specific globals/APIs the game itself
/// injects are not available here. This still fully covers what the "LUA Editor" needs:
/// catching syntax errors before saving a script back into the package, and sanity-running
/// self-contained logic (loops, string/math work, <c>print()</c> tracing, ...).
/// </para>
/// </summary>
public static class LuaScriptRunner
{
    /// <summary>
    /// Parses <paramref name="code"/> without executing it. Mirrors Ilive Reader's own
    /// "COMPILE" button (<c>CFrameScript::OnCompile</c> → <c>CReaderLua::CompileBuffer</c>):
    /// a syntax-only check, no side effects.
    /// </summary>
    public static bool TryCompile(string code, out string message)
    {
        try
        {
            new Script().LoadString(code, codeFriendlyName: "script");
            message = "Compiled OK - no syntax errors.";
            return true;
        }
        catch (SyntaxErrorException ex)
        {
            message = $"Syntax error: {ex.DecoratedMessage}";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Runs <paramref name="code"/> to completion in a fresh, sandboxed <see cref="Script"/>
    /// (<see cref="CoreModules.Preset_SoftSandbox"/> - no filesystem/OS/process access),
    /// forwarding every <c>print()</c> call to <paramref name="onOutput"/> as it happens.
    /// Mirrors Ilive Reader's "RUN"/Go button, whose script output is likewise streamed
    /// into the trace pane below the editor as it prints (there via a named pipe on
    /// <c>stdout</c>; here directly via MoonSharp's <c>DebugPrint</c> hook).
    /// </summary>
    public static bool TryRun(string code, Action<string> onOutput, out string message)
    {
        var script = new Script(CoreModules.Preset_SoftSandbox)
        {
            Options = { DebugPrint = onOutput },
        };

        try
        {
            script.DoString(code);
            message = "Run completed.";
            return true;
        }
        catch (SyntaxErrorException ex)
        {
            message = $"Syntax error: {ex.DecoratedMessage}";
            return false;
        }
        catch (ScriptRuntimeException ex)
        {
            message = $"Runtime error: {ex.DecoratedMessage}";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return false;
        }
    }
}
