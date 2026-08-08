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

- **Open, create, and save** SC4 DBPF packages, with a from-scratch package writer
  (ported from Ilive Reader) instead of relying on a third-party library's save routine.
- **TGI editing**: Type ID is always set manually; Group and Instance can be set manually
  or generated randomly. Random values use the exact same algorithm as Ilive Reader's own
  TGI generator (a fresh GUID's first 32 bits, not a pseudo-random number) instead of
  trusting the bundled third-party library's own undocumented behavior.
- **Exemplar/Cohort property editor**: add, edit, and remove properties, with names and
  types resolved against a downloadable `new_properties.xml` database (choose between the
  NAM Team and UlisseWolf-patched sources, or supply your own). Categorical (enum-like)
  property values are displayed in hexadecimal, matching SC4 modding convention, and
  multi-value properties (e.g. Occupant Groups) can be built up one value at a time from
  the option picker. Properties are read using an independent, byte-verified binary
  Exemplar parser rather than trusting a third-party library's decode in isolation — see
  [Property reading](#property-reading) below.
- **Image viewer** for PNG and FSH entries, with zoom and a sub-image selector for
  multi-image FSH files. Handles the PNG/BMP/JPEG Type ID ambiguity gracefully.
- **S3D model viewer**: interactive wireframe/solid-shaded 3D preview (drag to orbit,
  scroll to zoom), with day/night lighting and (best-effort) texture sampling from the
  package's own FSH entries.
- **WAV playback**, cross-platform (Windows via `winmm.dll`, macOS via `afplay`, Linux via
  `paplay`/`aplay`/`ffplay`).
- **Read-only preview** for LTEXT, UI, Directory, and other recognized-but-undecoded SC4
  formats (Lua scripts, network rules, etc.), with a hex+ASCII fallback for anything else.
- **Import/export**: pull any entry out to a file, replace an entry's content from a file,
  or export an entire package at once — using Ilive Reader's own naming convention so
  exports are interchangeable between the two tools. Exemplar/Cohort exports are checked
  against an independent, byte-for-byte validator (ported from Ilive Reader's own binary
  Exemplar decoder) before being written, so a malformed export is flagged with an exact
  offset/property index instead of silently producing a broken file.
- **Copy/paste entries between packages** via the system clipboard (open file A, copy
  entries, open file B, paste), with support for selecting several entries at once
  (click, Shift+click for a range, Ctrl+click to add/remove individual entries), plus a
  lightweight "copy/paste TGI only" mode for quickly re-targeting an entry's identifier.
- **Protected system files**: SC4's core `SimCity_1.dat`–`SimCity_5.dat` and
  `SimCityLocale.dat` can always be opened, but in-place saving is blocked — "Save As..."
  is required, to avoid accidentally corrupting a game installation.
- **External tool launcher**: quick-launch buttons for SC4 PIM-X, DataNode, Mapper,
  Terraformer, and SC4pac Editor, with paths configured in Options.
- **TOML-driven themes and language**: eight built-in color palettes defined as plain
  `.toml` files you can edit or extend — "Bloomberg Terminal" (black/amber), "Ilive
  Classic" (period-appropriate light grey), "Corporatewave" (nostalgic 80s/90s corporate
  pastels), "Synthwave — Miami 1984" (Outrun neon), and four color-vision-deficiency
  themes (protanopia, deuteranopia, tritanopia, achromatopsia) designed around the
  Okabe-Ito accessible color palette. A language selector uses the same approach (English
  and Italian included).

## Requirements

- **.NET SDK 10** or later.
- `csDBPF.dll` — **not included** in this repository (see [Third-party
  components](#third-party-components) below); place it in `Libs/csDBPF.dll` before
  building.
- An internet connection for the first `dotnet restore` (NuGet packages) and for
  downloading the property database / theme defaults on first run.

## Building and running

```bash
git clone <this-repository-url>
cd SC4ModdingSuite
# Place csDBPF.dll in Libs/csDBPF.dll (see "Third-party components" below)
dotnet restore
dotnet build
dotnet run
```

There is no `.sln` file — the project is a single `.csproj`, so `dotnet` commands run
directly from the project's root folder without needing to specify a project path.

**If `dotnet build`/`dotnet restore` seems to hang**, make sure the project folder isn't
sitting directly inside a huge directory (e.g. your Desktop) — SDK-style projects glob
every file under their own folder recursively, and a `.csproj` dropped into a folder with
hundreds of thousands of unrelated files can look "stuck" while it's actually just
enumerating them all. Keep the project in its own dedicated folder.

## Project structure

```
SC4ModdingSuite.csproj      Single project, no solution file needed
Libs/csDBPF.dll             Third-party dependency (not included, see below)
Assets/, Styles/            Icon and TOML-driven theme system
Localization/               Built-in language files (embedded into the assembly)
Themes/                     Built-in color palettes (embedded into the assembly)
Models/                     Application/domain logic, csDBPF integration, file I/O
ViewModels/                 MVVM view models (no external MVVM toolkit dependency)
Views/                      Avalonia windows/controls (XAML + code-behind)
```

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

Original source code in this repository is licensed under the [MIT License](LICENSE).

This does **not** extend to third-party components referenced above (notably
`csDBPF.dll`, which is not distributed with this repository) — confirm their own license
terms independently before redistributing a built copy of this application.
