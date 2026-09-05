#!/bin/sh
# Compiles the real Lua 5.0.x C source in this folder into a shared library, for
# NativeLua50.cs (LuaScriptRunner.cs) to P/Invoke into directly - no wrapper VM, no
# reimplementation, the actual lua.org C source (see LUA_SOURCE_NOTES.md).
#
# Verified working on Linux (built and round-trip tested - load/run/print/return all
# confirmed correct against the real Lua 5.0 interpreter - during this feature's
# development). Run this on each target OS to produce that platform's binary; .NET's
# runtimes/<rid>/native/ convention (see SC4ModdingSuite.csproj) picks the right one up
# automatically at publish time, no code changes needed per platform.
set -e

SOURCES="lapi.c lauxlib.c lbaselib.c lcode.c ldebug.c ldo.c ldump.c lfunc.c lgc.c llex.c \
         lmathlib.c lmem.c lobject.c lopcodes.c lparser.c lstate.c lstring.c lstrlib.c \
         ltable.c ltablib.c ltm.c lundump.c lvm.c lzio.c"

case "$(uname -s)" in
  Linux)
    mkdir -p runtimes/linux-x64/native
    gcc -O2 -fPIC -DLUA_USE_LINUX -shared -o runtimes/linux-x64/native/liblua50.so $SOURCES -lm -ldl
    echo "Built runtimes/linux-x64/native/liblua50.so"

    if command -v x86_64-w64-mingw32-gcc >/dev/null 2>&1; then
      # Cross-compiling Windows's lua50.dll from Linux (apt install gcc-mingw-w64-x86-64) -
      # this is how runtimes/win-x64/native/lua50.dll was actually produced during this
      # feature's development (no Windows machine/MSVC was available). --export-all-symbols
      # is required: unlike native Linux ELF (which exports every global symbol by default -
      # no flag needed above), mingw's ld does not, and lua.h's LUA_API/LUALIB_API macros
      # expand to plain `extern`, not __declspec(dllexport), so without this flag the DLL
      # would build "successfully" but export none of Lua's own API (silently unusable from
      # NativeLua50.cs's P/Invoke). build.bat's real-MSVC path instead uses an explicit
      # lua50.def, since MSVC has no equivalent "export everything" linker flag.
      mkdir -p runtimes/win-x64/native
      x86_64-w64-mingw32-gcc -O2 -DLUA_USE_WIN -shared -o runtimes/win-x64/native/lua50.dll \
        $SOURCES -Wl,--export-all-symbols -Wl,--out-implib,runtimes/win-x64/native/lua50.lib
      echo "Built runtimes/win-x64/native/lua50.dll (cross-compiled via mingw-w64)"
    fi
    ;;
  Darwin)
    mkdir -p runtimes/osx-x64/native runtimes/osx-arm64/native
    gcc -O2 -fPIC -DLUA_USE_MACOSX -dynamiclib -o runtimes/osx-x64/native/liblua50.dylib $SOURCES -lm
    echo "Built runtimes/osx-x64/native/liblua50.dylib (current arch only - build again on"
    echo "Apple Silicon, or use 'clang -arch arm64 ... -arch x86_64' for a universal binary,"
    echo "for runtimes/osx-arm64)"
    ;;
  *)
    echo "Unrecognized platform ($(uname -s)). On Windows, use build.bat (MSVC 'cl') instead."
    exit 1
    ;;
esac
