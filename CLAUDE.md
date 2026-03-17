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
`L2GameDataName`, `ItemStatData`, `ItemName`, `Skillgrp`, `SkillName`, `Armorgrp`, `Weapongrp`, `EtcItemgrp`, `SystemMsg`

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

---

## Editing and Saving .dat Files Directly

Pages that allow the user to open, edit, and re-save client `.dat` files use the following pipeline. The `SystemMsgColor` page is the reference implementation (`pages/SystemMsgColor.axaml` + `pages/SystemMsgColor.xaml.cs`).

### Full Pipeline (Open → Edit → Save)

```
Open .dat  →  DatCrypto.DecryptFile()  →  L2DatFile.ParseXxx()  →  UI edit
Save       →  L2DatFile.SerializeXxx() →  DatCrypto.EncryptFile() →  File.WriteAllBytes()
```

```csharp
// --- LOAD ---
var decrypted = DatCrypto.DecryptFile(path);          // RSA decrypt + zlib decompress
var datFile   = new L2DatFile();                       // or new L2DatFile(nameTable) if MAP_INT needed
var records   = datFile.ParseSystemMsg(decrypted);     // parse binary → List<DatSystemMsg>

// --- SAVE ---
var binary    = L2DatFile.SerializeSystemMsg(records); // List<DatXxx> → binary bytes
var encrypted = DatCrypto.EncryptFile(binary);         // zlib compress + RSA encrypt (v413_encdec key)
await File.WriteAllBytesAsync(path, encrypted);
```

### Encryption Keys

All `.dat` files use version `Lineage2Ver413`. There are **two key pairs** for v413:

| Key | Modulus starts with | Exponent | Can decrypt? | Can encrypt? |
|-----|---------------------|----------|--------------|--------------|
| `v413_original` | `97df3984…` | `0x35` | ✓ (public key) | ✗ (NCSoft private key, not available) |
| `v413_encdec` | `75B4D6DE…` | `0x1d` decrypt / large private exp encrypt | ✓ | ✓ |

`DatCrypto.DecryptFile()` tries both keys automatically. `DatCrypto.EncryptFile()` always uses **v413_encdec**. This works for private server clients that have the v413_encdec public key installed. Official NCSoft clients only accept v413_original-signed files (can't be re-encrypted without NCSoft's private key).

### Binary Format Details

**File layout (encrypted):**
```
[28 bytes] UTF-16LE header "Lineage2Ver413"
[N bytes]  RSA payload (128-byte blocks)
[20 bytes] Footer: 19× 0x00 + 0x64
```

**RSA block layout (128 bytes per block, max 124 bytes payload):**
```
[4 bytes] big-endian length of payload bytes in this block
[zeros]   padding
[payload] right-aligned, with 4-byte end-alignment padding
```

**Decrypted content layout:**
```
[4 bytes LE] record count
[records]    variable-length binary records
[13 bytes]   SafePackage footer (for isSafePackage="true" files — see below)
```

### SafePackage Footer

Files with `isSafePackage="true"` in their structure XML (`example/src/dist/data/structure/dats/`) **must** have 13 bytes appended after all records in the decrypted binary. The game client validates this footer and reports `"File was corrupted"` if it is absent.

```csharp
// Append at the end of every SerializeXxx() for isSafePackage="true" files:
ms.Write(new byte[] { 12, 83, 97, 102, 101, 80, 97, 99, 107, 97, 103, 101, 0 });
// Decodes as: ASCF string "SafePackage" (compact-int 12 = 11 chars + null)
```

**How to check:** open `example/src/dist/data/structure/dats/<filename>.xml`, find the version's `<file>` tag, check `isSafePackage="true|false"`. SystemMsg = `true`. When in doubt, add the footer (parsers skip unknown trailing bytes).

### ASCF String Format

Variable-length strings used in binary `.dat` files:

| Compact-int value | Encoding | Byte count |
|-------------------|----------|------------|
| `0` | empty string | 0 bytes follow |
| `+N` (positive) | cp1252/Latin1, N bytes including null terminator | N bytes |
| `-N` (negative) | UTF-16LE, N chars including null terminator | N×2 bytes |

**Read:** `L2BinaryReader.ReadAscfString()` handles all three cases.
**Write:** `WriteAscf(ms, s)` in `L2DatFile.cs` — uses Latin1 if all chars ≤ 0xFF, UTF-16LE otherwise.

> **Note:** The reference Java tool (`ByteWriter.DEFAULT_CHARSET = US_ASCII`) uses UTF-16LE for any char > 0x7F, while our C# `WriteAscf` uses Latin1 up to 0xFF. Both are readable by the game client (it checks the compact-int sign to choose the decoder). The byte layout differs, but the content is identical.

### RGBA Color Format

The `.dat` format stores color as 4 raw bytes. `ReadRgba()` returns them as an 8-char hex string in file order. The actual byte order depends on the file — SystemMsg uses BGRA (byte0=B, byte1=G, byte2=R, byte3=A). Always serialize back in the same order as read.

For UI display (RRGGBB):
```csharp
// Load: raw "BBGGRRAA" → display RRGGBB
var bb = c[0..2]; var gg = c[2..4]; var rr = c[4..6]; var aa = c[6..8];
colorRgb = rr + gg + bb;

// Save: display RRGGBB → raw "BBGGRRAA"
source.Color = (bb + gg + rr + alpha).ToUpper();
```

### MAP_INT Fields

MAP_INT is a 4-byte LE int that indexes into the L2GameDataName string table. For binary round-trip, always store the **raw index** (e.g., `SoundIndex`, `VoiceIndex`) alongside the resolved string. Serialize back the raw index, not the string.

### CompactInt Encoding

UE compact index — byte 0: bit7=sign, bit6=continuation, bits5-0=value[5:0]. Subsequent bytes: bit7=continuation, bits6-0=next 7 bits. `ReadCompactInt()` in `L2BinaryReader.cs`, `WriteCompactInt()` in `L2DatFile.cs`.

### Adding a New Editable .dat Page

1. Add record type `DatReader/DatXxx.cs` with `set` on mutable fields.
2. Add `ParseXxx(byte[])` and `SerializeXxx(List<DatXxx>)` to `L2DatFile.cs`.
3. Check `isSafePackage` in the structure XML — if true, append the 13-byte footer in `SerializeXxx`.
4. Add `"XxxPattern"` to `SupportedPatterns` in `AppSettingsControl.xaml.cs` for Test DAT support.
5. Page load: `DatCrypto.DecryptFile` → `ParseXxx` → map to UI model.
6. Page save: sync UI edits back to record → `SerializeXxx` → `DatCrypto.EncryptFile` → `WriteAllBytes`.

## Documentation

The app's documentation is the file `docs/index.html` — a single-page HTML site (Tailwind CSS + Font Awesome). Whenever the user asks to update, add, or change anything in the documentation, edit that file.

## UI Design Notes
The app targets an enterprise/professional aesthetic: restrained dark theme inspired by Material Design / Fluent Design. Avoid decorative elements. When adding new pages, follow the layout of existing pages (header with title + subtitle, content area, log display).

**Default skill for all UI/design work:** use `/avalonia-design` whenever creating, redesigning, or styling any UI element in this project.
