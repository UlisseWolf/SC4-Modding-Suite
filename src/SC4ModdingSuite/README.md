# SC4 Modding Suite

A desktop tool for inspecting and editing **SimCity 4** (SC4) DBPF package files
(`.dat`, `.sc4lot`, `.sc4desc`, `.sc4model`) — built with **.NET 10** and **Avalonia UI**,
on top of the [csDBPF](https://github.com/NAMTeam) library, with several routines ported
directly from **Ilive Reader**'s C++ source.

Browse every entry in a package, edit TGIs, add/edit/remove Exemplar properties, preview
images (PNG/FSH), 3D models (S3D), and audio (WAV), export/import individual entries or
whole packages, and manage everything through a dense, keyboard-and-mouse-friendly
interface inspired by classic SC4 modding tools.

> **Status**: actively developed.

## Features

**Core package editing**
- **Open, create, and save** SC4 DBPF packages, with a from-scratch package writer
  (ported from Ilive Reader) instead of relying on a third-party library's save routine.
- **TGI editing**: a dedicated **TGI Editor** dialog covers both a masked batch edit
  (apply a hex pattern - `#` keeps the existing digit - to Type/Group/Instance across
  every selected entry at once) and, for a single selected entry, direct manual editing
  with optional Group/Instance randomization and delete - matching Ilive Reader's own
  `DlgTGIEditor`. Random values use the exact same algorithm as Ilive Reader's own TGI
  generator (a fresh GUID's first 32 bits, not a pseudo-random number).
- **Clone**, **Change Instance**, **Group Patch**, **Insert Batch**, **Insert Template**,
  and **Convert Format** - the rest of Ilive Reader's own batch/creation tools, each its
  own dialog reachable from the SC4 Editor's own toolbar.
- **Copy/paste entries between packages** via the system clipboard (open file A, copy
  entries, open file B, paste), with support for selecting several entries at once
  (click, Shift+click for a range, Ctrl+click to add/remove individual entries), plus a
  lightweight "copy/paste TGI only" mode for quickly re-targeting an entry's identifier.
- **Import/export**: pull any entry out to a file, replace an entry's content from a file,
  or export an entire package (raw or as human-readable text) at once - using Ilive
  Reader's own naming convention so exports are interchangeable between the two tools.
- **Compare**, **Merge**, and **Directory Sync** between two packages.
- **Protected system files**: SC4's core `SimCity_1.dat`–`SimCity_5.dat` and
  `SimCityLocale.dat` can always be opened, but in-place saving is blocked — "Save As..."
  is required, to avoid accidentally corrupting a game installation.

**Exemplar/Cohort properties**
- **Add, edit, and remove properties** (double-click a row, or the ADD/EDIT/REMOVE
  buttons), with names and types resolved against a downloadable `new_properties.xml`
  database (choose between the NAM Team and UlisseWolf-patched sources, or supply your
  own). Categorical (enum-like) values are shown in hexadecimal, matching SC4 modding
  convention, and multi-value properties (e.g. Occupant Groups) can be built up one value
  at a time from the option picker.
- Read using an independent, byte-verified binary Exemplar parser rather than trusting a
  third-party library's decode in isolation - see [Property reading](#property-reading).
  Exports are checked against an independent, byte-for-byte validator before being
  written, so a malformed export is flagged with an exact offset/property index instead
  of silently producing a broken file.

**Specialized structured formats** - each with its own real, from-scratch codec ported
from Ilive Reader's actual C++ source (not guessed), editable in place where the
reference implementation itself supports writing, read-only where it doesn't:
TRK (Track Definition), MCO (menu/camera preview), LDAT (Lot Data), LEV (Level Table),
HLS (Hitlist/playlist, read-only), AVP/F1B (Animation Viewpoints, read-only), and SC4Path
(Network Path, read-only). Everything else recognized-but-undecoded (RUL, SC4Path-shaped
text, KEYCFG, ...) gets a generic editable text box; anything unrecognized falls back to
a hex+ASCII dump.

**S3D (3D model) editor**
- Interactive wireframe/solid-shaded viewer (drag to orbit, scroll to zoom), with
  day/night lighting and texture sampling from the package's own FSH entries.
- **Geometry Editor**, **Material Editor** (render states, blend/depth/alpha functions,
  texture wrap/filter modes, and texture references, with a live FSH preview), **UV
  Editor**, **Animation Editor**, **REGP Editor**, and a **hex editor** for individual
  chunks - Ilive Reader's own S3D tooling ported feature-for-feature.
- **Import/Export 3DS** for exchanging geometry with other 3D tools.

**UI (dialog layout) editor**
- Element tree with per-node visibility toggles, a type-aware property editor (bool/
  rect/color/xy/string editors chosen per property, matching Ilive Reader's own property
  type table), an "ALL ELEMENTS" flattened spreadsheet view, and "add child from
  template" (picks a real, already-correctly-authored node of the same element type from
  elsewhere in the currently open package or Plugins folder, rather than guessing at a
  blank one's properties).

**LTEXT editor**
- Per-language save, "save for every detected language at once", and Poedit `.po`/`.pot`
  import/export for translation workflows.
- **Translation Grid**: every LTEXT string in the package grouped by family (same
  Instance ID, only Group differing by language offset), every language shown side by
  side for direct comparison/editing - plus, with your own LLM provider API key configured
  in Options, one-click **AI-assisted first-draft translation** into a chosen language.

**T21 (network lot) editor** - a dedicated form for T21 exemplars (IID, slope-based
prop selection, pattern flags, rotation/flip per placed object), ported from Jondor's
standalone T21 Editor tool, plus RHD→LHD mirroring.

**Lua editor** - compile and run Lua 5.0 scripts against a real, natively-compiled Lua
5.0 interpreter (not a reimplementation), with a Recorder for scripted macro playback.

**Built-in MCP server** - exposes this same package-reading/writing logic as tools an AI
assistant can call over HTTP, started/stopped from its own toolbar button. See
[MCP Server Mode](#mcp-server-mode) below.

**Other viewers**: image viewer for PNG/FSH entries (zoom, sub-image selector, handles
the PNG/BMP/JPEG Type ID ambiguity), WAV playback (cross-platform), and a Property Manager
(Analysis mode) for browsing/searching the loaded property database itself.

**External tool launcher**: quick-launch buttons for SC4 PIM-X, DataNode, Mapper,
Terraformer, and SC4pac Editor, with paths configured in Options.

**TOML-driven themes and language**: ten built-in color palettes defined as plain `.toml`
files you can edit or extend — "Bloomberg Terminal" (black/amber), "Ilive Classic"
(period-appropriate light grey), "Corporatewave" (nostalgic 80s/90s corporate pastels),
"Synthwave — Miami 1984" (Outrun neon), "IBM 3270" (green phosphor terminal), "Windows 95",
and four color-vision-deficiency themes (protanopia, deuteranopia, tritanopia,
achromatopsia) designed around the Okabe-Ito accessible color palette. A language selector
uses the same approach (English, Italian, German, Spanish, French, and Portuguese for the
main toolbar and top-level editor actions - see that panel's own scope note in
`Models/LocalizationService.cs`).

## Requirements

- **.NET SDK 10** or later.
- `csDBPF.dll` — **not included** in this repository (see [Third-party
  components](#third-party-components) below); place it in `Libs/csDBPF.dll` before
  building.
- An internet connection for the first `dotnet restore` (NuGet packages) and for
  downloading the property database / theme defaults on first run.

## Building and running

This project lives at `src/SC4ModdingSuite/` in the repository - see the
[repository root README](../../README.md) for the full clone/build sequence covering both
projects. From this folder directly:

```bash
# Place csDBPF.dll in Libs/csDBPF.dll (see "Third-party components" below)
dotnet restore
dotnet build
dotnet run
```

Or from the repository root, via the solution file: `dotnet run --project src/SC4ModdingSuite`.

**If `dotnet build`/`dotnet restore` seems to hang**, make sure the repository isn't
sitting directly inside a huge directory (e.g. your Desktop) — SDK-style projects glob
every file under their own folder recursively, and a `.csproj` dropped into a folder with
hundreds of thousands of unrelated files can look "stuck" while it's actually just
enumerating them all. Keep the repository in its own dedicated folder.

## Project structure

```
SC4ModdingSuite.csproj      This project (see ../../SC4ModdingSuite.slnx at the repo root)
Libs/csDBPF.dll             Third-party dependency (not included, see below)
Assets/, Styles/            Icon and TOML-driven theme system
Localization/               Built-in language files (embedded into the assembly)
Themes/                     Built-in color palettes (embedded into the assembly)
Mcp/                        MCP server - JSON-RPC/HTTP protocol + tools (see below)
Models/                     Application/domain logic, csDBPF integration, file I/O
ViewModels/                 MVVM view models (no external MVVM toolkit dependency)
Views/                      Avalonia windows/controls (XAML + code-behind)
```

## MCP Server Mode

This app has a built-in [Model Context Protocol](https://modelcontextprotocol.io) server,
started/stopped from its own **MCP SERVER...** toolbar button - the C# equivalent of
[dbpf-mcp](https://github.com/caspervg/dbpf-mcp) (a separate, Kotlin/JVM project by a
different author), running in-process rather than as its own separate program or mode. It
speaks the current standard **Streamable HTTP** MCP transport: pressing START opens a real
HTTP listener bound to `127.0.0.1` (never reachable from the network) at the port shown in
that panel, and a real MCP client (Claude Desktop, Claude Code, or any other HTTP-capable
MCP client) is configured with that same URL to connect to it directly while this app keeps
running - list entries, decode Exemplars/Cohorts with real property names, read LTEXT
strings, and decode every specialized format this project has its own from-scratch codec
for (see "Tools" below). START also performs a real opening handshake against the server it
just started, to confirm it actually answers correctly rather than just that it began
listening; COPY CLIENT CONFIG generates the exact JSON your client's own config needs.

### Why hand-rolled JSON-RPC instead of the official MCP C# SDK

The official `ModelContextProtocol` NuGet SDK would normally be the natural choice here.
This project's own dev sandbox had no network access to restore NuGet packages, so the
protocol layer (`Mcp/McpMessageHandler.cs`, `Mcp/McpHttpServer.cs`) is written directly
against `System.Text.Json` and the base-class-library `System.Net.HttpListener` (no
package needed for either) instead, specifically so every line of it could actually be
written *and test-compiled* in that environment. This was verified end-to-end with real
HTTP requests against a real running listener during development: `initialize` → correct
capabilities/serverInfo response plus an `Mcp-Session-Id` header, `tools/list` → all tools
with correct JSON Schema, `tools/call` on a missing file → a clean `isError: true` response
(not a crash), an unknown method → the correct JSON-RPC `-32601` error, a request to any
path other than `/mcp` → `404`, `DELETE /mcp` → `204`, and a stop-then-restart on the same
port → works cleanly with no lingering socket state.

If you'd rather use the official SDK, the protocol layer here is intentionally small and
isolated (`Mcp/McpMessageHandler.cs` plus `Mcp/Tools/ToolRegistry.cs`) - swapping out just
the HTTP listener in `Mcp/McpHttpServer.cs` would not require touching any of the
`Mcp/Tools/*.cs` tool implementations, which don't know or care how they were invoked.

### Client setup

```json
{
  "mcpServers": {
    "sc4-dbpf": {
      "url": "http://127.0.0.1:7337/mcp"
    }
  }
}
```

The GUI's own MCP SERVER... panel generates this exact snippet (with the port you're
actually running on already filled in) via its COPY CLIENT CONFIG button - only enabled
once the server is running, since there's no URL to give out before then. The server stops
the moment this app closes (or STOP is pressed); a client relying on it needs this app kept
running the whole time.

### Tools

TGI arguments accept either one `"tgi": "TTTTTTTT-GGGGGGGG-IIIIIIII"` string or separate
`"type"`/`"group"`/`"instance"` hex fields, matching dbpf-mcp's own convention.

**One important, non-obvious csDBPF behavior**: if you pass a Group ID of exactly
`00000000`, csDBPF's own `TGI` constructor silently *randomizes* it instead of keeping it
zero (confirmed directly - two identical calls produce two different Group IDs). This
isn't a bug in this app; it's inherited from the same `TGI` type the whole GUI is built on.
If you need a specific, predictable TGI, don't use an all-zero Group ID.

#### Read

| Tool | What it does | Backed by |
|---|---|---|
| `list_entries` | Every entry's TGI, classified type, size, compression | `DbpfService` |
| `summarize_package` | Entry counts by type, a few notable named Exemplars | `DbpfService` + `ExemplarBinaryParser` |
| `explain_entry` | Human-readable description (same text this app's own Details panel shows) | `EntryDescriber` |
| `read_exemplar` / `read_cohort` | Full property list: ID, name (needs `propertyRegistryPath`), type, values | `ExemplarBinaryParser` + `PropertyDefinitionsRegistry` |
| `read_ltext` | Plain string value | `DBPFEntryLTEXT` / `EntryTypeClassifier` |
| `read_trk` | Header + full field list (incl. XA/TLO/HLS cross-references) | `TkdCodec` (ported from Ilive Reader's real `_tkd::DecodeTKD`) |
| `read_mco` | FSH texture + F1B animation-viewpoint references | `McoCodec` |
| `read_ld` | Lot dimensions/wealth/type + required-plugin list | `LdCodec` |
| `read_hls` | Playlist header + XA instance references (read-only) | `HlsCodec` |
| `read_f1b` | Animation viewpoint frames (read-only) | `F1bCodec` |
| `read_sc4path` | Transit route: transport segments + stops, with coordinates (read-only) | `Sc4PathCodec` |
| `read_lev` | Header + every `#`-delimited table section | `LevCodec` |
| `read_ui` | Legacy UI element tree (IID/id/caption/props, recursive) | `UiLegacyParser` |
| `read_s3d` | Model metadata: vertex/material/mesh-group counts, material texture refs | `S3DParser` (no geometry export - matches dbpf-mcp's own scope) |
| `export_fsh_png` | Decodes an FSH entry's first image, writes it as a `.png` | `DBPFEntryFSH.Image` + SixLabors |

#### Write

All write tools accept `outputPath` (required), `overwrite` (replace an existing file
entirely), and `merge` (keep existing entries at `outputPath` not addressed by this call,
replace/append by TGI). If `outputPath` already exists and neither is set, the call fails
rather than silently overwriting. Duplicate TGIs within one call's `entries` are rejected.

| Tool | What it does | Backed by |
|---|---|---|
| `write_exemplars` | New DBPF package with new Exemplar/Cohort entries + properties (type inferred from a `propertyRegistryPath`, or declared explicitly) | `DbpfService` + `ExemplarEncodeWorkaround` (see below) |
| `write_ltext` | New DBPF package with new LTEXT entries | `DbpfService.UpsertLtextEntry` |
| `write_raw_entries` | Arbitrary bytes (base64-encoded) at any TGI, no format decoding - for kinds without a dedicated encoder here (KEYCFG, TAB, RUL, EFFDIR, PNG, FSH, ...) | `DbpfService.AddNewEntry` |

`propertyRegistryPath` (optional, on the Exemplar/Cohort/`explain_entry` tools): a
`new_properties.xml` path for real property names instead of bare `0x...` IDs - the same
file format this app's own Options → Property Database points at. Loaded once per distinct
path and cached for the life of the server process.

#### Index (whole-Plugins-folder search)

| Tool | What it does |
|---|---|
| `index_plugins` | Recursively scans a Plugins folder for `.dat`/`.SC4Lot`/`.SC4Model`/`.SC4Desc` files, writes a persistent JSONL index of every Exemplar/Cohort's TGI/name/class/property IDs under `~/.cache/sc4-mcp-server/indexes/` |
| `index_status` | Reports whether an index exists, when it was built, entry count, and whether it looks stale (a file changed since indexing) |
| `search_index` | Searches a previously built index by TGI/name/package-path substring or property ID, without rescanning |

### Three real csDBPF bugs found (and fixed) while building `write_exemplars`

Verified directly against csDBPF's real source (not just observed behavior - the actual
`.cs` files), all three exercised the very first time this app tried to create a
brand-new Exemplar with properties:

1. **`DBPFEntryEXMP.Encode(bool compress)` crashes on small entries.** Its own source ends
   with an unconditional `ByteData = QFS.Compress(ByteData);` - the `compress` argument is
   never actually checked, and the result is never null-checked. `QfsCompression.Compress`
   returns `null` whenever the payload is under 50 bytes (`UncompressedDataMinSize`) - a
   real case for any small/near-empty Exemplar, not a contrived one - so `Encode()` throws
   a bare `NullReferenceException` with no indication why.
2. **`DBPFPropertyString.ToBytes()` writes UTF-16** (2 bytes per character) while its own
   length field is written in *characters* - desynchronizing that property's byte framing
   from what a real Exemplar's STRING property actually looks like on disk (single-byte
   ANSI, length-in-bytes).
3. **`DBPFPropertyString.GetData(int)` is separately broken** - it returns the literal
   text `"System.Char[]"` (the default `ToString()` of its own internal `char[]`, not the
   actual characters) rather than the real string value. `GetTypedData()` (no index) does
   return the correct `char[]`.

`Models/ExemplarEncodeWorkaround.cs` reproduces `Encode()`'s real logic for the
Binary-encoding case this app uses, fixing all three - round-trip tested: build a property,
encode, re-decode with `ExemplarBinaryParser`, confirm the value survives exactly, for both
a normal Exemplar and a genuinely empty one. `DbpfService`/`T21LhdConverter` (the GUI's own
property-editing/compress-toggle/T21-mirroring features) use the same fix, since all three
bugs are just as reachable there as from `write_exemplars`.

### Not yet covered

- **`write_fsh`** - encoding a PNG into FSH texture formats (DXT1/3, A8R8G8B8, ...). This
  csDBPF build's public surface has decode (`FSHEntry`/`FSHImageData`) but no discovered
  encode-from-bitmap path. `write_raw_entries` (already implemented) is the working
  fallback: build the FSH bytes with another tool, then write them in as-is.
- **`index_plugins`'s cross-package parent-cohort resolution** - the index itself exists
  and covers TGI/name/class/property-ID search; resolving a Cohort's *parent* chain across
  packages via the index isn't implemented on top of it yet.
- **`read_keycfg` / `read_tab_binary`** - no documented spec to build a real decoder
  against, same reason `KnownFormats.cs` leaves TLO/EFFDIR/MAD as a hex dump.

## Property reading

The Properties panel reads Exemplar/Cohort properties using an independent binary parser
(`Models/ExemplarBinaryParser.cs`), ported and cross-checked byte-for-byte against Ilive
Reader's own Exemplar decoder, instead of relying solely on the bundled csDBPF library's
own property list. On a real Lot Configuration Exemplar (heavy on "array-mode" repeating
properties, e.g. one `LotConfigPropertyLotObject` entry per object placed on the lot),
csDBPF's own decode produced implausible property IDs not present in the file, while
independently re-parsing the exact same bytes decoded every property cleanly with zero
leftover bytes. If the independent parser and csDBPF disagree on how many properties an
entry has, the Properties panel still shows the independently-verified list, and a status
message flags the discrepancy so edits to that specific entry aren't assumed trustworthy
without further investigation. The same parser also validates Exemplar/Cohort exports
before they're written to disk (see the Import/export feature above).

## Third-party components

This repository does **not** bundle `csDBPF.dll`. You will need to obtain it separately
(from the [NAM Team](https://github.com/NAMTeam)'s csDBPF project or your own build) and
place it at `Libs/csDBPF.dll` before building — **check that library's own license terms**
before distributing a build that includes it; the MIT license below covers the original
source code in this repository only, not third-party binaries it links against.

Several file-format and save-routine details in this project were derived by reading the
publicly available C++ source of **Ilive Reader** and **DarkMatter's DatGen 4** (SC4
DBPF/S3D format documentation, not copied verbatim as code).

The community-maintained `new_properties.xml` property databases are downloaded at
runtime, at the user's choice, from:

- [NAMTeam/New_Properties.xml](https://github.com/NAMTeam/New_Properties.xml)
- [UlisseWolf/New_Properties.xml-patches](https://github.com/UlisseWolf/New_Properties.xml-patches)

## Contributing

Issues and pull requests are welcome. Given how much of this project's correctness
depends on assumptions about a closed-source-adjacent binary format and a third-party
library's exact API surface, bug
reports that include the exact error message and, where possible, a sample `.dat` file
are especially valuable.

## License

Original source code in this repository is licensed under the [MIT License](../../LICENSE)
(at the repository root - this project doesn't carry its own separate copy).

This does **not** extend to third-party components referenced above (notably
`csDBPF.dll`, which is not distributed with this repository) — confirm their own license
terms independently before redistributing a built copy of this application.
