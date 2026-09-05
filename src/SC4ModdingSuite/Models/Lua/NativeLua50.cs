using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SC4ModdingSuite.Models;

/// <summary>
/// P/Invoke bindings straight into the real Lua 5.0 C library (<c>Native/lua50/</c> -
/// the actual lua.org source, compiled as-is; see <c>LUA_SOURCE_NOTES.md</c> there) -
/// SimCity 4's own embedded Lua version. No wrapper VM, no reimplementation: every method
/// below either calls one of Lua's own exported C functions directly, or replicates one of
/// <c>lua.h</c>'s <c>#define</c> macros (<c>lua_setglobal</c>, <c>lua_pop</c>,
/// <c>lua_pushcfunction</c>, ...) exactly, since a preprocessor macro has no compiled symbol
/// of its own to P/Invoke against - the sequence of real API calls each one expands to is
/// reproduced verbatim below (see each method's comment for the exact macro it stands in for).
///
/// <para>
/// Only the four standard libraries needed for a self-contained script (base/table/string/
/// math) are ever opened - <c>io</c>/<c>os</c>/dynamic-library-loading/<c>debug</c> aren't
/// even compiled into <c>liblua50</c> in the first place (see LUA_SOURCE_NOTES.md), so
/// there is no filesystem/process/OS access reachable from a pasted-in script, matching this
/// editor's existing sandboxing intent.
/// </para>
/// </summary>
internal static class NativeLua50
{
    private const string Lib = "lua50";

    // --- State lifecycle ---
    [DllImport(Lib)] internal static extern IntPtr lua_open();
    [DllImport(Lib)] internal static extern void lua_close(IntPtr L);

    // --- Standard libraries (each is itself a lua_CFunction; calling it directly, the same
    //     way lua.c's own luaL_openlibs-equivalent init sequence does, registers that
    //     library's functions into the global table). ---
    [DllImport(Lib)] private static extern int luaopen_base(IntPtr L);
    [DllImport(Lib)] private static extern int luaopen_table(IntPtr L);
    [DllImport(Lib)] private static extern int luaopen_string(IntPtr L);
    [DllImport(Lib)] private static extern int luaopen_math(IntPtr L);

    // --- Loading/running ---
    [DllImport(Lib)] private static extern int luaL_loadbuffer(IntPtr L, byte[] buff, UIntPtr sz, byte[] name);
    [DllImport(Lib)] internal static extern int lua_pcall(IntPtr L, int nargs, int nresults, int errfunc);
    [DllImport(Lib)] private static extern void lua_call(IntPtr L, int nargs, int nresults);

    // --- Stack access ---
    [DllImport(Lib)] internal static extern int lua_gettop(IntPtr L);
    [DllImport(Lib)] internal static extern void lua_settop(IntPtr L, int idx);
    [DllImport(Lib)] private static extern void lua_pushvalue(IntPtr L, int idx);
    [DllImport(Lib)] private static extern IntPtr lua_tostring(IntPtr L, int idx);
    [DllImport(Lib)] private static extern void lua_pushstring(IntPtr L, byte[] s);
    [DllImport(Lib)] private static extern void lua_pushcclosure(IntPtr L, IntPtr fn, int n);
    [DllImport(Lib)] private static extern void lua_settable(IntPtr L, int idx);
    [DllImport(Lib)] private static extern void lua_gettable(IntPtr L, int idx);

    /// <summary><c>LUA_GLOBALSINDEX</c> from lua.h - a fixed pseudo-index, not a real stack position, used to read/write Lua's global table directly.</summary>
    private const int LUA_GLOBALSINDEX = -10001;

    /// <summary>Opens the base/table/string/math libraries - see this class's own doc comment for why only these four.</summary>
    internal static void OpenStandardLibraries(IntPtr L)
    {
        luaopen_base(L);
        luaopen_table(L);
        luaopen_string(L);
        luaopen_math(L);
    }

    /// <summary>Replicates lua.h's <c>lua_pop(L,n)</c> macro: <c>lua_settop(L, -(n)-1)</c>.</summary>
    internal static void Pop(IntPtr L, int n) => lua_settop(L, -n - 1);

    /// <summary>Compiles <paramref name="code"/> (UTF-8) without running it - <c>luaL_loadbuffer</c>, the same function Lua's own <c>lua_dostring</c>/standalone interpreter build on. Returns 0 on success, matching the raw Lua convention (non-zero = error, message left on top of the stack).</summary>
    internal static int LoadBuffer(IntPtr L, string code, string chunkName)
    {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var nameBytes = NullTerminate(chunkName);
        return luaL_loadbuffer(L, codeBytes, (UIntPtr)codeBytes.Length, nameBytes);
    }

    /// <summary>Reads the string at stack index <paramref name="idx"/> (commonly -1, the top - where an error message from <see cref="LoadBuffer"/>/<see cref="lua_pcall"/> is left) as a managed string.</summary>
    internal static string ToStringValue(IntPtr L, int idx)
    {
        var ptr = lua_tostring(L, idx);
        return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    /// <summary>
    /// Replicates lua.h's <c>lua_register(L,n,f)</c> macro exactly: pushes the name, pushes
    /// the C function (<c>lua_pushcfunction</c>, itself <c>lua_pushcclosure(L,f,0)</c>), then
    /// <c>lua_settable(L, LUA_GLOBALSINDEX)</c> - i.e. sets a global to a native function.
    /// Used to install this app's own <paramref name="function"/> as the global
    /// <paramref name="name"/> (see <c>LuaScriptRunner</c>'s <c>print</c> override).
    /// </summary>
    internal static void RegisterGlobalFunction(IntPtr L, string name, IntPtr function)
    {
        lua_pushstring(L, NullTerminate(name));
        lua_pushcclosure(L, function, 0);
        lua_settable(L, LUA_GLOBALSINDEX);
    }

    /// <summary>Reads the global <paramref name="name"/> and pushes it onto the stack - replicates <c>lua_getglobal</c>'s macro (<c>lua_pushstring</c> + <c>lua_gettable(L, LUA_GLOBALSINDEX)</c>). Used by the print-callback replica of <c>luaB_print</c> to fetch the real global <c>tostring</c>.</summary>
    internal static void GetGlobal(IntPtr L, string name)
    {
        lua_pushstring(L, NullTerminate(name));
        lua_gettable(L, LUA_GLOBALSINDEX);
    }

    internal static void PushValue(IntPtr L, int idx) => lua_pushvalue(L, idx);

    internal static void Call(IntPtr L, int nargs, int nresults) => lua_call(L, nargs, nresults);

    private static byte[] NullTerminate(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        var result = new byte[bytes.Length + 1];
        Array.Copy(bytes, result, bytes.Length);
        return result;
    }
}
