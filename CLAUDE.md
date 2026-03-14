# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**L2Toolkit** is a desktop utility suite for Lineage 2 server developers. It provides 15+ data processing tools (XML generation, item parsing, skill modification, log analysis, etc.) via a sidebar-based navigation UI.

- **Framework:** Avalonia UI 11.2.0 on .NET 10.0
- **Language:** C# with nullable reference types enabled
- **UI:** Single-window app with custom dark theme and left sidebar navigation
- **No test projects** — testing is manual through the GUI

## Build & Run

```bash
dotnet run          # Run in development
dotnet publish -c Release  # Release build
```

## Architecture

### Navigation Pattern
`MainWindow` hosts all navigation. Each sidebar button sets `MainContent.Content = new PageName()`. Every tool is a `UserControl` (AXAML + code-behind) in `pages/`.

### Key Layers
| Directory | Purpose |
|-----------|---------|
| `pages/` | Avalonia UserControls — one per tool |
| `DataMap/` | Plain C# record/model types |
| `Parse/` | Low-level text parsers for game data files |
| `ProcessData/` | Data transformation logic (game formats → XML/text) |
| `DatReader/` | Binary `.dat` file parsing and `.l2dat` compression (see below) |
| `Tables/` | Embedded `.l2dat` resources — pre-compressed game data shipped with the app |
| `Utilities/` | `GlobalLogs`, `Parser`, `MaterialType` |
| `database/` | `AppDatabase` singleton — key-value settings persisted to `%APPDATA%/L2Toolkit/settings.properties` |

### Logging
`GlobalLogs` is a thread-safe, UI-aware logger (max 120 entries, FIFO). Call `GlobalLogs.Log(message)` from any page or processor.

### Settings
```csharp
var db = AppDatabase.GetInstance();
db.Set("key", value);
var val = db.Get("key");
```

### UI Conventions (Avalonia/AXAML)
- Dark background: `#2C2C2C`
- Accent blue: `#5B9BD5`
- Button themes: `PrimaryButton`, `SecondaryButton`, default
- All ComboBox, Button, and TextBox controls use custom `ControlTheme` resources defined in `App.axaml`
- **Do not add `BrushTransition` to button ControlThemes** — causes two-tone hover flicker
- Page subtitle text color: `#E8E8E8` (not gray)
- Custom titlebar with drag-to-move and double-click maximize support

### File Processing Pattern
Pages use Avalonia's storage provider for folder/file selection, then pass paths to `ProcessData/` classes. Processing is async; results are logged via `GlobalLogs` and written to disk.

## DatReader Module

### Overview
The `DatReader/` directory contains the full pipeline for reading Lineage 2 Fafurion client `.dat` files (RSA-encrypted, zlib-compressed binary) and producing tab-separated text output identical to the reference Java application (L2DatEditor). Output compatibility is critical — parsed text must be re-importable by the Java app.

### Key Files
| File | Purpose |
|------|---------|
| `DatCrypto.cs` | RSA decryption + zlib decompression of `.dat` files |
| `L2DatFile.cs` | Binary parsers for each file type + `ToTextFormat()` serializers |
| `DatEnums.cs` | Enum-to-string resolution (material_type, crystal_type, weapon_type, etc.) matching Java output |
| `L2Pack.cs` | `.l2dat` proprietary compression format (Brotli) for text data |
| `Dat*.cs` | Record types for parsed data (DatSkillGrp, DatWeaponGrp, DatArmorGrp, etc.) |

### Supported .dat File Types
`L2GameDataName`, `ItemStatData`, `ItemName`, `Skillgrp`, `SkillName`, `Armorgrp`, `Weapongrp`, `EtcItemgrp`

### L2GameDataName (Name Table)
Must be loaded first via `L2DatFile.LoadNameTable()`. Provides string resolution for MAP_INT fields across all other parsers (e.g., icon names, mesh paths). Parsers that need it receive the name table via `new L2DatFile(nameTable)`.

### .l2dat Compression Format
Simple proprietary format: `[4B magic "L2DT"][2B filename length][filename UTF-8][Brotli compressed data]`. No encryption. Achieves ~98.5% compression on tabular game data. Used to embed pre-parsed data as EmbeddedResources in `Tables/`.

```csharp
// Pack text to .l2dat
L2Pack.Pack("input.txt", "output.l2dat");

// Unpack .l2dat back to text
var (fileName, content) = L2Pack.Unpack("file.l2dat");

// Read embedded .l2dat resource
var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("L2Toolkit.Tables.Skillgrp.l2dat");
```

### Embedded Tables
The `Tables/` folder contains 7 `.l2dat` files registered as `<EmbeddedResource>` in `.csproj`. The `LiveData` page reads these via `Assembly.GetManifestResourceStream()` with a `ConcurrentDictionary` cache — no disk I/O needed. Resource names follow: `L2Toolkit.Tables.{Name}.l2dat`.

### Test DAT & L2DAT Converter
Located in `pages/AppSettingsControl.xaml.cs`. **Test DAT** reads `.dat` files from a system folder, auto-discovers supported types, loads name table first, then processes all files to `.txt`. **L2DAT Converter** compresses `.txt` files to `.l2dat` with round-trip byte-by-byte verification.

## Documentation

The app's documentation is the file `docs/index.html` — a single-page HTML site (Tailwind CSS + Font Awesome). Whenever the user asks to update, add, or change anything in the documentation, edit that file.

## UI Design Notes
The app targets an enterprise/professional aesthetic: restrained dark theme inspired by Material Design / Fluent Design. Avoid decorative elements. When adding new pages, follow the layout of existing pages (header with title + subtitle, content area, log display).

**Default skill for all UI/design work:** use `/avalonia-design` whenever creating, redesigning, or styling any UI element in this project.
