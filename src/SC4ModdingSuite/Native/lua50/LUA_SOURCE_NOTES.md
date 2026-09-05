# Lua 5.0.x - vendored source

This folder contains the **real, unmodified Lua 5.0 C source**, straight from Lua's own
official GitHub mirror (`https://github.com/lua/lua`, tag `v5.0`), which is exactly what
SimCity 4 itself embeds - not a rewrite, not a compatible-ish alternate implementation
(MoonSharp, this project's previous LUA Editor engine, targets Lua 5.2 semantics and is a
separate, pure-.NET interpreter - see the git history of `Models/LuaScriptRunner.cs`
for why that was the original choice, and why it was replaced by this).

`Models/NativeLua50.cs` P/Invokes directly into this compiled library - no wrapper VM,
no reimplementation of Lua's semantics in C#. `luaL_loadbuffer`/`lua_pcall`/etc. are the
literal functions Lua 5.0 itself exports; the only C# code involved is marshaling values
across the P/Invoke boundary and one small callback (`print`) that forwards output to the
LUA Editor's UI instead of a terminal.

## License

Lua is MIT-licensed (see the license text embedded in `lua.h`, near the top) -
`Copyright (C) 1994-2003 Tecgraf, PUC-Rio`. Freely embeddable/redistributable with
attribution kept intact, which this file (and `lua.h`'s own copyright header) provides.

## What's included, and what's deliberately left out

Included: the actual language core (parser, lexer, VM, garbage collector) plus the
`base`, `table`, `string`, and `math` standard libraries - `luaopen_base`/`luaopen_table`/
`luaopen_string`/`luaopen_math`, called explicitly by `NativeLua50.OpenStandardLibraries`.

Deliberately **not** compiled in at all (not merely left uncalled): `liolib.c` (file I/O),
`loadlib.c` (dynamic library loading/`os.execute`-adjacent things), `ldblib.c` (the debug
library, which can inspect/modify running Lua state in ways not needed for a sandboxed
script check-and-run), and `lua.c`/`luac.c`/`print.c`/`ltests.c` (Lua's own standalone
interpreter/compiler/test-harness executables - not needed when embedding as a library).
This keeps the same "no filesystem/OS access from a pasted-in script" sandboxing property
the previous MoonSharp-based implementation had (`CoreModules.Preset_SoftSandbox`), but
achieved by never linking those capabilities in at all, rather than a runtime permission
flag - there is no C API surface to even accidentally call into.

## Building

**You normally don't need to run anything here by hand** - `SC4ModdingSuite.csproj` has its
own `BuildLua50Linux`/`BuildLua50Windows`/`BuildLua50Mac` MSBuild targets that invoke
`build.sh`/`build.bat` automatically as part of an ordinary `dotnet build`/`dotnet publish`,
whenever a C compiler is available (skipping gracefully, with a warning rather than a build
failure, if none is found - falling back to whichever binary is already committed here).
It's also incremental: a rebuild is skipped entirely if none of the `.c`/`.h` files changed
since the library was last built.

`build.sh`/`build.bat` are still there directly runnable on their own too, if you want to
rebuild just the native library without a full `dotnet build` (e.g. right after editing the
Lua source itself). The Linux (`runtimes/linux-x64/native/liblua50.so`) and Windows
(`runtimes/win-x64/native/lua50.dll`) binaries are already built and committed here, and
were round-trip tested during development (load, run a loop, `print()`, arithmetic, return
value - all confirmed against the real interpreter, `LUA_VERSION` reads exactly
`"Lua 5.0"`); win-x64's own binary was itself produced by cross-compiling from Linux via
mingw-w64 (`apt install gcc-mingw-w64-x86-64`) rather than real MSVC - see `build.sh`'s own
comments on that path, and `build.bat`'s "try cl, fall back to gcc" logic for the reverse
situation on an actual Windows machine. macOS's binary needs an actual Mac to build/test on
(this dev environment didn't have one) - `build.sh`'s `Darwin` branch is written and should
work, but is unverified.
