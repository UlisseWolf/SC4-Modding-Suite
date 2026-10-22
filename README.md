<div align="center">

# SC4 Modding Suite

**A modern, cross-platform toolkit for inspecting and editing SimCity 4 DBPF packages.**

[![CI](https://github.com/UlisseWolf/SC4-Modding-Suite/actions/workflows/ci.yml/badge.svg)](https://github.com/UlisseWolf/SC4-Modding-Suite/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Avalonia UI](https://img.shields.io/badge/UI-Avalonia-8B44AC)
![Platforms](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)

</div>

SC4 Modding Suite opens `.dat`, `.sc4lot`, `.sc4desc` and `.sc4model` files and lets you
browse every entry, edit Exemplar properties, preview and edit 3D models, work with
localization and legacy UI resources, and batch-process whole plugin libraries. It brings the
workflows of classic SC4 tools (Ilive Reader, SC4 Model Tweaker, the T21 Editor, Poedit-style
translation) into one dense, keyboard-friendly application, and also ships a built-in
[Model Context Protocol](https://modelcontextprotocol.io) server so AI assistants can read
and write packages.

> **Status:** actively developed. Always keep backups of your plugins: back up before saving
> over files you care about.

---

## Table of contents

- [Highlights](#highlights)
- [Download](#download)
- [Features](#features)
  - [Package editing](#package-editing)
  - [Exemplar / Cohort properties](#exemplar--cohort-properties)
  - [S3D 3D model editor](#s3d-3d-model-editor)
  - [RHD → LHD conversion](#rhd--lhd-conversion)
  - [T21 network lot editor](#t21-network-lot-editor)
  - [Analysis tools](#analysis-tools)
  - [Specialized formats](#specialized-formats)
  - [LTEXT and translation](#ltext-and-translation)
  - [UI dialog editor](#ui-dialog-editor)
  - [Lua editor and recorder](#lua-editor-and-recorder)
  - [Built-in MCP server](#built-in-mcp-server)
  - [Themes, languages and extras](#themes-languages-and-extras)
- [Build from source](#build-from-source)
- [Project layout](#project-layout)
- [Third-party components](#third-party-components)
- [Contributing](#contributing)
- [License](#license)

---

## Highlights

- **Full DBPF workflow:** open, create, save, compare, merge and sync packages, with a
  from-scratch package writer.
- **Exemplar/Cohort editing** with real property names from the community
  `new_properties.xml` databases.
- **S3D editor** with interactive viewer, geometry/material/UV/animation editors and 3DS
  import/export, plus **batch tools** (flip, mirror, move, scale, rotate, retexture,
  transparency).
- **RHD → LHD conversion** for 20-view S3D sets and T21 exemplars, and **Reorient** for
  rotation-slot reassignment.
- **Analysis mode** for searching and auditing entire Plugins folders.
- **Built-in MCP server** exposing the same logic to AI assistants over local HTTP.
- **Ten themes, six languages**, all defined in plain TOML.

## Download

Pre-built, self-contained binaries (no .NET installation needed) are published on the
[Releases](../../releases) page:

| Platform | Archive | Notes |
|---|---|---|
| Windows x64 | `SC4ModdingSuite-<version>-windows-x64.zip` | Primary platform. Unzip and run `SC4ModdingSuite.exe`. |
| Linux x64 | `SC4ModdingSuite-<version>-linux-x64.zip` | Experimental. Unzip and run `./SC4ModdingSuite`. |
| macOS arm64 | `SC4ModdingSuite-<version>-macos-arm64.zip` | Experimental, Apple Silicon only, unnotarized: see the release notes for first-launch steps. |

## Features

### Package editing

- **Open / New / Save / Save As** with a dedicated package writer ported from Ilive Reader.
  SC4's core `SimCity_1.dat`–`SimCity_5.dat` and `SimCityLocale.dat` open normally, but
  in-place saving is blocked (use *Save As*), protecting your game installation.
- **Browse every entry** with type classification, TGI, size and compression state; filter
  by Type, Group or Instance.
- **TGI Editor:** masked batch edits (`#` keeps the existing digit) across many entries, or
  manual single-entry editing with optional randomization (same algorithm as Ilive Reader).
- **Clone, Change Instance, Group Patch, Insert Batch, Insert Template, Reindex and Convert
  Format**, each in its own dialog.
- **Clipboard tools:** Copy/Paste entries between open packages, Copy/Paste TGI only, and
  **Select From List**, which selects every entry matching a TGI list from the clipboard.
- **Import / Export:** extract any entry, replace an entry from a file, export a whole
  package (raw or readable text), save decoded data, and write header dumps. Exported file
  names follow Ilive Reader's convention, so they stay interchangeable.
- **Compare DATs**, **Merge DATs** and **Directory Sync** between packages.
- **Compression control:** toggle QFS compression per entry.

### Exemplar / Cohort properties

- Add, edit and remove properties (double-click a row or use *ADD / EDIT / REMOVE*).
- Names and types are resolved from a downloadable `new_properties.xml` database. Choose the
  NAM Team or the UlisseWolf-patched source, or supply your own.
- Enum-like values display in hexadecimal; multi-value properties (e.g. Occupant Groups)
  can be built one value at a time.
- Reading uses an independent, byte-verified binary parser, so lot configuration exemplars
  with heavy array-mode properties decode correctly.
- Built-in workarounds for known [csDBPF](https://github.com/noah-severyn/csDBPF) defects
  keep saves and exports reliable.

### S3D 3D model editor

- **Interactive viewer:** wireframe or textured-solid rendering, day/night lighting, drag to
  orbit, scroll to zoom, texture sampling from the package's own FSH entries.
- **Editors:** Geometry (VERT/INDX/PRIM), Material (render states, blend/depth/alpha
  functions, texture wrap and filter, live FSH preview), UV, Animation, Prop, REGP and a
  per-chunk hex editor.
- **Import / Export 3DS** for round-tripping with Blender or 3ds Max, including
  per-group export.
- **Merge** another model's groups into the current one.
- **Batch Tools** (apply to many selected models at once, SC4 Model Tweaker–style):
  - **Flip / Mirror / Invert** along any axis. Triangle winding is corrected automatically
    so mirrored models are not inside-out.
  - **Move**, **Scale**, **Rotate** with explicit values.
  - **Replace Texture** and **Set Transparency**.
  - Instance-ID masks restrict the operation to selected zoom/rotation views.
- **Reorient:** reassign which rotation slot each selected model's content occupies across
  the 20 zoom/rotation views, without touching geometry or TGIs.

### RHD → LHD conversion

- **S3D RHD → LHD:** converts a complete 20-view set (5 zooms × 4 rotations) using a
  per-view offset that follows each view's rotation slot, and optionally renames `RHD` to
  `LHD` in material and animation names. Offsets were derived and verified vertex-by-vertex
  against real RHD/LHD model pairs (EU and US Mast sets). The dialog warns if the selection
  is not a multiple of 20.
- **T21 RHD → LHD:** mirrors T21 network-lot exemplars.
- Both are available from the SC4 Editor toolbar and from S3D Editor mode.

### T21 network lot editor

A dedicated form for T21 exemplars: IID, slope-based prop selection, pattern flags and
rotation/flip per placed object. Ported from Jondor's standalone T21 Editor.

### Analysis tools

Analysis mode works across whole folders rather than single files:

- **Find / Index Analyser:** search entries by Type, Group or Instance with saved filters.
- **Plugins Analyser:** scan a Plugins folder and report on its contents.
- **Exemplar / Cohort Analyser:** inspect exemplar usage across a library.
- **Property Find / Count:** search and count by property ID.
- **Property Manager:** browse and search the loaded property database.

### Specialized formats

Real, from-scratch codecs ported from Ilive Reader's source (not guesses). Editable where
the reference implementation supports writing, read-only where it does not.

| Format | Support |
|---|---|
| TRK (track definition) | Read / edit |
| MCO (menu/camera preview) | Read / edit |
| LDAT (lot data) | Read / edit |
| LEV (level table) | Read / edit |
| HLS (hitlist/playlist) | Read-only |
| AVP / F1B (animation viewpoints) | Read-only |
| SC4Path (network path) | Read-only |

Other recognized text-like formats get an editable text box; anything unrecognized falls
back to a hex + ASCII dump. PNG/FSH entries open in an image viewer with zoom and sub-image
selection, and WAV entries can be played back on every platform.

### LTEXT and translation

- Per-language save, or save for every detected language at once.
- **Poedit `.po` / `.pot`** import and export.
- **Translation Grid:** every LTEXT string grouped by family with all languages side by
  side, plus one-click **AI-assisted first-draft translation** using your own LLM provider
  API key (configured in Options).

### UI dialog editor

Element tree with per-node visibility, a type-aware property editor (bool, rect, color, xy,
string), an *All Elements* spreadsheet view, and *add child from template*, which copies a
correctly authored node of the same type from your open package or Plugins folder.

### Lua editor and recorder

Compile and run **Lua 5.0** scripts on a real, natively compiled Lua 5.0 interpreter, with
a **Recorder** for scripted macro playback.

### Built-in MCP server

Start it from the **MCP SERVER...** toolbar button. It speaks Streamable HTTP on
`127.0.0.1` only (never reachable from the network), so Claude Desktop, Claude Code or any
HTTP-capable MCP client can use it while the app runs.

```json
{
  "mcpServers": {
    "sc4-dbpf": { "url": "http://127.0.0.1:7337/mcp" }
  }
}
```

The panel's *COPY CLIENT CONFIG* button generates this snippet with your actual port.

| Group | Tools |
|---|---|
| Read | `list_entries`, `summarize_package`, `explain_entry`, `read_exemplar`, `read_cohort`, `read_ltext`, `read_trk`, `read_mco`, `read_ld`, `read_hls`, `read_f1b`, `read_sc4path`, `read_lev`, `read_ui`, `read_s3d`, `export_fsh_png` |
| Write | `write_exemplars`, `write_ltext`, `write_raw_entries` (all support `overwrite` and `merge`) |
| Index | `index_plugins`, `index_status`, `search_index` |

Full details are in the [application README](src/SC4ModdingSuite/README.md#mcp-server-mode).

### Themes, languages and extras

- **Ten built-in themes** as editable TOML files: Bloomberg Terminal, Ilive Classic,
  Corporatewave, Synthwave Miami 1984, IBM 3270, Windows 95, and four color-vision-deficiency
  themes (protanopia, deuteranopia, tritanopia, achromatopsia) based on the Okabe-Ito palette.
- **Six interface languages:** English, Italian, German, Spanish, French and Portuguese.
- **External tool launcher** for SC4 PIM-X, DataNode, Mapper, Terraformer and SC4pac Editor.

## Build from source

Requirements: **.NET SDK 10** and an internet connection for the first restore.

```bash
git clone https://github.com/UlisseWolf/SC4-Modding-Suite.git
cd SC4-Modding-Suite

dotnet restore SC4ModdingSuite.slnx
dotnet build SC4ModdingSuite.slnx -c Release
dotnet run --project src/SC4ModdingSuite
```

Keep the repository in its own dedicated folder: SDK-style projects glob every file beneath
them, so cloning straight into a huge directory (such as your Desktop) can make restore and
build appear to hang.

Continuous integration builds on Windows, macOS and Ubuntu 26.04. Pushing a `vMAJOR.MINOR.PATCH` tag
publishes a stable release with self-contained archives for each platform; every push to
`master` refreshes a `latest` pre-release.

## Project layout

```
SC4ModdingSuite.slnx        Solution file
src/SC4ModdingSuite/
├── Models/                 Domain logic, file I/O, format codecs
│   ├── Dbpf/               Package I/O, writer, clipboard, TGI helpers
│   ├── Exemplar/           Property parsing and the property-name database
│   ├── FormatCodecs/       TRK / MCO / LDAT / LEV / HLS / AVP / SC4Path codecs
│   ├── S3D/                Parsing, editing, RHD→LHD, 3DS import/export
│   ├── T21/                T21 helpers and RHD→LHD mirroring
│   ├── Ltext/              Translation helpers (Poedit, LLM)
│   ├── Lua/                Native Lua 5.0 integration
│   └── UiLegacy/, Theming/
├── ViewModels/             MVVM view models
├── Views/                  Avalonia windows, dialogs and custom controls
├── Mcp/                    MCP server (JSON-RPC over HTTP) and tools
├── Themes/, Localization/  TOML themes and language files
└── Libs/csDBPF.dll         Vendored third-party dependency
```

## Third-party components

- **[csDBPF](https://github.com/noah-severyn/csDBPF)** (MIT) is committed as a compiled DLL
  in `Libs/`, because upstream publishes neither a NuGet package nor binary releases.
- **[Avalonia UI](https://avaloniaui.net/)** for the cross-platform interface.
- **[SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)** (a csDBPF dependency)
  is pinned to 3.1.x. Version 4.x needs a commercial license key, so known advisories are
  suppressed in the project file with a documented rationale.
- File-format and save-routine details were derived by reading the public C++ source of
  **Ilive Reader** and **DarkMatter's DatGen 4**. This is documentation, not copied code.
- The feature set of **SC4 Model Tweaker** was re-implemented from its public
  documentation. No code from that program is used.
- Property databases are downloaded at your choice from
  [NAMTeam/New_Properties.xml](https://github.com/NAMTeam/New_Properties.xml) and
  [UlisseWolf/New_Properties.xml-patches](https://github.com/UlisseWolf/New_Properties.xml-patches).
- The MCP server is a from-scratch C# implementation covering the same ground as
  [dbpf-mcp](https://github.com/caspervg/dbpf-mcp).

## Contributing

Issues and pull requests are welcome. Because correctness depends on a binary format and a
third-party library, bug reports that include the exact error message and, ideally, a
sample `.dat` file are especially valuable.

## License

Original source code is released under the [MIT License](LICENSE). Third-party components
keep their own licenses; check them before redistributing a build.

*SimCity 4 is a trademark of Electronic Arts Inc. This project is not affiliated with or
endorsed by Electronic Arts.*
