@echo off
rem Compiles the real Lua 5.0.x C source in this folder into lua50.dll, for NativeLua50.cs
rem (LuaScriptRunner.cs) to P/Invoke into directly.
rem
rem Produces runtimes\win-x64\native\lua50.dll - .NET's runtimes/<rid>/native/ convention
rem (see SC4ModdingSuite.csproj) picks this up automatically at publish time. Also invoked
rem automatically by SC4ModdingSuite.csproj's own BuildLua50Windows target as part of a
rem normal `dotnet build`/`dotnet publish` - running this script by hand is only needed if
rem you want to rebuild it standalone (e.g. after editing the Lua source).
rem
rem Tries real MSVC (cl.exe) first - most Windows contributors already have this via Visual
rem Studio - falling back to mingw-w64's gcc if cl isn't on PATH (a plain terminal/`dotnet
rem build` normally does NOT have cl on PATH unless launched from a "x64 Native Tools
rem Command Prompt for VS" or after running vcvarsall.bat; mingw's gcc has no such
rem restriction, which is also how this project's own committed lua50.dll was actually
rem built - see BuildLua50Windows in the .csproj, and its Linux counterpart's mingw
rem cross-compile note in build.sh).

if not exist runtimes\win-x64\native mkdir runtimes\win-x64\native

where cl >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    rem /LD alone does NOT export Lua's C API from the DLL - lua.h declares everything as
    rem plain `extern` (LUA_API expands to extern, not __declspec(dllexport)), so MSVC
    rem needs an explicit export list - see lua50.def, which lists exactly the symbols
    rem NativeLua50.cs P/Invokes.
    cl /nologo /O2 /LD /DLUA_USE_WIN /Fe:runtimes\win-x64\native\lua50.dll ^
       lapi.c lauxlib.c lbaselib.c lcode.c ldebug.c ldo.c ldump.c lfunc.c lgc.c llex.c ^
       lmathlib.c lmem.c lobject.c lopcodes.c lparser.c lstate.c lstring.c lstrlib.c ^
       ltable.c ltablib.c ltm.c lundump.c lvm.c lzio.c ^
       /link /DEF:lua50.def
    echo Built runtimes\win-x64\native\lua50.dll via MSVC
    exit /b 0
)

where gcc >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    rem mingw's ld does not export every global symbol by default the way native Linux ELF
    rem does - --export-all-symbols is required, or (same reason as the MSVC path above)
    rem the DLL builds "successfully" but exports none of Lua's own API.
    gcc -O2 -DLUA_USE_WIN -shared -o runtimes\win-x64\native\lua50.dll ^
        lapi.c lauxlib.c lbaselib.c lcode.c ldebug.c ldo.c ldump.c lfunc.c lgc.c llex.c ^
        lmathlib.c lmem.c lobject.c lopcodes.c lparser.c lstate.c lstring.c lstrlib.c ^
        ltable.c ltablib.c ltm.c lundump.c lvm.c lzio.c ^
        -Wl,--export-all-symbols -Wl,--out-implib,runtimes\win-x64\native\lua50.lib
    echo Built runtimes\win-x64\native\lua50.dll via mingw
    exit /b 0
)

echo No C compiler found (checked for "cl" and "gcc" on PATH^) - could not build lua50.dll.
echo Install Visual Studio's "Desktop development with C++" workload, or mingw-w64, then re-run.
exit /b 1
