# SC4 Modding Suite

A desktop tool for inspecting and editing **SimCity 4** (SC4) DBPF package files
(`.dat`, `.sc4lot`, `.sc4desc`, `.sc4model`) — built with **.NET 10** and **Avalonia UI**,
on top of the [csDBPF](https://github.com/NAMTeam) library, with several routines ported
directly from **Ilive Reader**'s C++ source. The same app also has a built-in
**[Model Context Protocol](https://modelcontextprotocol.io) server**, started/stopped from
its own toolbar button, that exposes the same package-reading/writing logic as tools an AI
assistant can call over HTTP - there is no separate program to install for that.

> **Status**: actively developed.

## Repository structure

```
SC4ModdingSuite.slnx        Solution file
src/
└── SC4ModdingSuite/        The app - desktop GUI, with a built-in MCP server
    ├── Mcp/                MCP server: JSON-RPC/HTTP protocol + tools (see below)
    └── README.md           Full feature list, requirements, build instructions
LICENSE                     MIT - covers original source code in this repository
.gitignore
```

## Quick start

```bash
git clone <this-repository-url>
cd SC4ModdingSuite

# Place csDBPF.dll (not included - see "Third-party components" in
# src/SC4ModdingSuite/README.md) at src/SC4ModdingSuite/Libs/csDBPF.dll

dotnet restore SC4ModdingSuite.slnx
dotnet build SC4ModdingSuite.slnx
dotnet run --project src/SC4ModdingSuite
```

The MCP server is started from inside the running app (the **MCP SERVER...** toolbar
button) rather than via any separate command or process - see the app's own README for
client setup and the tool list.

For the full feature list, requirements, MCP tool list/client setup, and detailed
build/troubleshooting notes, see
**[src/SC4ModdingSuite/README.md](src/SC4ModdingSuite/README.md)**.

## Third-party components

`csDBPF.dll` isn't bundled - see
[src/SC4ModdingSuite/README.md](src/SC4ModdingSuite/README.md#third-party-components) for
where to obtain it and its own license terms. Several file-format and save-routine details
were derived by reading the publicly available C++ source of **Ilive Reader** and
**DarkMatter's DatGen 4** (documentation, not copied verbatim as code). The MCP server is a
from-scratch C# implementation covering the same ground as
[dbpf-mcp](https://github.com/caspervg/dbpf-mcp) (a separate, Kotlin/JVM project by a
different author) - it just runs in-process, inside this same app, rather than as its own
program.

## Contributing

Issues and pull requests are welcome. Given how much of this project's correctness depends
on assumptions about a closed-source-adjacent binary format and a third-party library's
exact API surface, bug reports that include the exact error message and, where possible, a
sample `.dat` file are especially valuable.

## License

Original source code in this repository is licensed under the [MIT License](LICENSE).

This does **not** extend to third-party components referenced above (notably
`csDBPF.dll`, which is not distributed with this repository) — confirm their own license
terms independently before redistributing a built copy of this app.
