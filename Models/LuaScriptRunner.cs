using System;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Gives the LUA Editor its "COMPILE" and "RUN" buttons - now backed by the real, actual
/// Lua 5.0 interpreter (<see cref="NativeLua50"/>, <c>Native/lua50/</c>), the same version
/// SimCity 4 itself embeds. This replaced an earlier MoonSharp-based implementation
/// (a pure-managed, Lua-5.2-targeting interpreter) specifically because MoonSharp's language
/// version didn't match SC4's own Lua 5.0 - the .NET-idiomatic "no native dependency to
/// ship" tradeoff MoonSharp offered wasn't the priority here; running the actual scripting
/// language a real SC4 script would run under was.
///
/// <para>
/// Every script gets a fresh Lua state (<c>lua_open</c>/<c>lua_close</c>) with only the
/// base/table/string/math standard libraries loaded (see
/// <see cref="NativeLua50.OpenStandardLibraries"/> - <c>io</c>/dynamic-library-loading/
/// <c>debug</c> aren't even compiled into <c>liblua50</c> in the first place, so there is no
/// filesystem/OS access reachable from a pasted-in script), and its own <c>print</c>
/// replaced with <see cref="PrintCallback"/>, which streams output into the LUA Editor's
/// trace pane exactly as it's produced, the same way Ilive Reader's own "RUN"/Go button
/// streams script output into its trace pane live.
/// </para>
/// </summary>
public static class LuaScriptRunner
{
    [ThreadStatic]
    private static Action<string>? _currentOutput;

    /// <summary>
    /// Parses <paramref name="code"/> without executing it (<c>luaL_loadbuffer</c> - the
    /// real Lua 5.0 compiler, not an approximation of it). Mirrors Ilive Reader's own
    /// "COMPILE" button (<c>CFrameScript::OnCompile</c> → <c>CReaderLua::CompileBuffer</c>):
    /// a syntax-only check, no side effects.
    /// </summary>
    public static bool TryCompile(string code, out string message)
    {
        var state = NativeLua50.lua_open();
        if (state == IntPtr.Zero)
        {
            message = "Failed to create a Lua 5.0 state.";
            return false;
        }

        try
        {
            NativeLua50.OpenStandardLibraries(state);

            if (NativeLua50.LoadBuffer(state, code, "script") != 0)
            {
                message = $"Syntax error: {NativeLua50.ToStringValue(state, -1)}";
                return false;
            }

            message = "Compiled OK - no syntax errors.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return false;
        }
        finally
        {
            NativeLua50.lua_close(state);
        }
    }

    /// <summary>
    /// Runs <paramref name="code"/> to completion in a fresh Lua 5.0 state, forwarding every
    /// <c>print()</c> call to <paramref name="onOutput"/> as it happens. Mirrors Ilive
    /// Reader's "RUN"/Go button.
    /// </summary>
    public static bool TryRun(string code, Action<string> onOutput, out string message)
    {
        var state = NativeLua50.lua_open();
        if (state == IntPtr.Zero)
        {
            message = "Failed to create a Lua 5.0 state.";
            return false;
        }

        _currentOutput = onOutput;
        try
        {
            NativeLua50.OpenStandardLibraries(state);
            unsafe
            {
                delegate* unmanaged[Cdecl]<IntPtr, int> print = &PrintCallback;
                NativeLua50.RegisterGlobalFunction(state, "print", (IntPtr)print);
            }

            if (NativeLua50.LoadBuffer(state, code, "script") != 0)
            {
                message = $"Syntax error: {NativeLua50.ToStringValue(state, -1)}";
                return false;
            }

            if (NativeLua50.lua_pcall(state, 0, 0, 0) != 0)
            {
                message = $"Runtime error: {NativeLua50.ToStringValue(state, -1)}";
                return false;
            }

            message = "Run completed.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Error: {ex.Message}";
            return false;
        }
        finally
        {
            _currentOutput = null;
            NativeLua50.lua_close(state);
        }
    }

    /// <summary>
    /// Replaces Lua's own built-in <c>print</c> - reproduces <c>lbaselib.c</c>'s
    /// <c>luaB_print</c> algorithm exactly (fetch the real global <c>tostring</c>, call it
    /// on each argument, tab-join the results) but forwards the finished line to
    /// <see cref="_currentOutput"/> instead of writing to a process-level stdout stream a
    /// GUI app doesn't meaningfully have. A <c>[UnmanagedCallersOnly]</c> static method is
    /// the direct, modern .NET way to hand a real C-callable function pointer to native code
    /// (<c>lua_CFunction</c>) - Lua calls this exactly as if it were one of its own C
    /// library functions.
    /// </summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = new[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static int PrintCallback(IntPtr state)
    {
        try
        {
            var argCount = NativeLua50.lua_gettop(state);
            NativeLua50.GetGlobal(state, "tostring");

            var line = new StringBuilder();
            for (var i = 1; i <= argCount; i++)
            {
                NativeLua50.PushValue(state, -1); // the "tostring" function, to be called again
                NativeLua50.PushValue(state, i);  // the i-th argument to print
                NativeLua50.Call(state, 1, 1);

                if (i > 1)
                {
                    line.Append('\t');
                }

                line.Append(NativeLua50.ToStringValue(state, -1));
                NativeLua50.Pop(state, 1);
            }

            _currentOutput?.Invoke(line.ToString());
        }
        catch
        {
            // Never let a managed exception unwind across the native call boundary - a
            // failed print() shouldn't crash the whole script/host process.
        }

        return 0;
    }
}
