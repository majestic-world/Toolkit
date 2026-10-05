# L2Toolkit engineering reference

Read `CONTEXT.md` first. This reference preserves the engineering constraints that are not obvious from the repository layout.

## Application boundary

L2Toolkit is a single-window Avalonia desktop suite for Lineage 2 server developers. It has more than fifteen data-processing tools and targets .NET 10 with nullable reference types enabled.

- Build/run in development: `dotnet run`
- Release publish: `dotnet publish -c Release`
- There is no test project. Verify changed behavior in the running GUI, including the relevant file-processing path.

## UI and application structure

`pages/MainWindow` owns the sidebar and content region. Its generic navigation path caches each `UserControl`, assigns it to `MainContent.Content`, then marks the selected sidebar button. New tools normally remain an AXAML plus code-behind pair under `pages/`.

| Location | Responsibility |
| --- | --- |
| `pages/` | Avalonia views and tool-specific UI logic |
| `DataMap/` | Plain C# models and records |
| `Parse/` | Low-level text parsers for game data |
| `ProcessData/` | Transformations from game formats to XML or text |
| `DatReader/` | Client `.dat` reading/writing and `.l2dat` packing |
| `Tables/` | Embedded `.l2dat` tables shipped with the app |
| `Utilities/` | Shared UI and data helpers, including logs and table loading |
| `database/` | User settings persistence |

### Avalonia conventions

- Keep common control appearance in the `ControlTheme` resources in `App.axaml`; controls use the shared ComboBox, Button, TextBox, `PrimaryButton`, and `SecondaryButton` themes.
- Keep button themes free of `BrushTransition`; it produces a two-tone hover flicker.
- Preserve the dark professional visual language and its restrained surface steps: titlebar/sidebar `#2A2A2A`, page `#333333`, panels/cards `#2C2C2C`, recessed inputs `#252525`, borders `#464646`, blue accent around `#5B9BD5`, page subtitles in `#E8E8E8`, secondary labels no darker than `#B8B8B8`. Get contrast from text and borders, not from large gaps between surface grays.
- Fluent's `TextBox` watermark has a template-fixed `Opacity="0.5"`; `App.axaml` sets its foreground to white so the effective placeholder stays legible (~`#929292` on inputs). Do not set watermark colors per page.
- The custom titlebar supports drag-to-move and double-click maximize. Do not replace that behavior when changing window chrome.

### Shared runtime services

`GlobalLogs` is a per-page, thread-safe log buffer. Create/register it with the page log `TextBox`, write with `AddLog`, and let it dispatch updates to the Avalonia UI thread. It keeps the newest 120 entries.

`AppDatabase.GetInstance()` returns the settings store. Settings live in `%APPDATA%/L2Toolkit/settings.properties`; use `GetValue`/`GetInt` to read and `UpdateValue` to persist a value.

Pages select files or folders through Avalonia's storage provider, process asynchronously, and write outputs to disk. Log meaningful progress and failure context through the page's `GlobalLogs` instance.

### Product documentation

`docs/index.html` is the public, single-page product documentation. It is standalone HTML/CSS with Font Awesome and Google Fonts; update it directly when product documentation changes.

## Embedded tables

`Tables/*.l2dat` are embedded resources. Application startup calls `TableManager.EnsureTables()`, which materializes any missing resource in `AppContext.BaseDirectory/tables/`.

`TableManager.LoadTable(name)` prefers `tables/{name}.l2dat` on disk, falls back to `L2Toolkit.Tables.{name}.l2dat` in the assembly, unpacks it, then shares the result through a `ConcurrentDictionary` cache. This disk-first rule deliberately lets users replace a table without recompiling. Call `TableManager.InvalidateCache()` after replacing a disk table in a running process.

The `.l2dat` container is not encrypted: `L2DT` magic, two-byte filename length, UTF-8 filename, then Brotli content. `L2Pack.Pack` and `L2Pack.Unpack` are its canonical APIs.

## Client `.dat` pipeline

The Fafurion client `.dat` pipeline must remain compatible with the Java L2DatEditor text representation and with client binary expectations.

### Module map and supported files

- `DatCrypto.cs`: RSA decryption and zlib decompression.
- `L2DatFile.cs`: binary parsers and text serializers.
- `DatEnums.cs`: enum-to-string mappings that must match Java text output.
- `L2Pack.cs`: Brotli-based `.l2dat` container.
- `Dat*.cs`: parsed record types.

Supported file families are `L2GameDataName`, `ItemStatData`, `ItemName`, `Skillgrp`, `SkillName`, `Armorgrp`, `Weapongrp`, `EtcItemgrp`, and `SystemMsg`.

### Read and write path

1. Load the Name table first when the target file has `MAP_INT` fields: `L2DatFile.LoadNameTable()`.
2. Read with `DatCrypto.DecryptFile(path)`, then parse with the matching `L2DatFile.ParseXxx` method.
3. Map records to the UI model without discarding raw values needed for serialization.
4. On save, map UI changes back to records, call `L2DatFile.SerializeXxx`, then `DatCrypto.EncryptFile` and write the encrypted bytes.

`SystemMsgColor` is the reference page for direct `.dat` editing.

### Crypto and file integrity

All supported files use the `Lineage2Ver413` container: a UTF-16LE header, 128-byte RSA payload blocks, and a footer of nineteen `0x00` bytes followed by `0x64`.

`DatCrypto.DecryptFile()` tries both v413 key pairs. `DatCrypto.EncryptFile()` always uses `v413_encdec`; the output is for private clients configured with that public key. An official NCSoft client requires `v413_original` signing, which cannot be generated without NCSoft's private key.

For every file marked `isSafePackage="true"` in the structure XML, the serializer must append the 13-byte ASCF `SafePackage` footer. `SystemMsg`, `DatFullArmorEnchantEffect`, and `DatWeaponEnchantEffect` are known examples. Missing it causes the client to report that the file is corrupted.

### Binary conventions that must survive edits

- `MAP_INT` is a four-byte little-endian Name table index. Preserve and serialize the raw index, not merely its resolved string.
- ASCF strings use a compact length: zero for empty, a positive value for null-terminated Latin-1, and a negative value for null-terminated UTF-16LE. Use `L2BinaryReader.ReadAscfString()` and `L2DatFile.WriteAscf`.
- The Java reference writer uses UTF-16LE for characters above `0x7F`; the C# writer uses Latin-1 through `0xFF`. Both forms are readable by the client, but their byte sequences differ.
- Preserve color byte ordering for the exact file type. `SystemMsg` stores BGRA; map to RRGGBB only for UI presentation, then restore the source byte order on save.
- UE compact integers use bit 7 as sign, bit 6 as continuation, and the remaining six bits in the first byte as value. Use `ReadCompactInt` and `WriteCompactInt` rather than open-coding it.

### Adding a directly editable file type

1. Add a mutable `DatXxx` record under `DatReader/`.
2. Add matching `ParseXxx(byte[])` and `SerializeXxx(List<DatXxx>)` methods to `L2DatFile`.
3. Check the structure XML for `isSafePackage`; append the SafePackage footer when required.
4. Add the filename pattern to `SupportedPatterns` in `pages/AppSettingsControl.xaml.cs` so Test DAT discovers it.
5. Wire page load through decrypt → parse → UI mapping, and save through UI mapping → serialize → encrypt → write.

`pages/AppSettingsControl.xaml.cs` also owns Test DAT and the L2DAT Converter. Test DAT discovers supported files, loads the Name table before dependent files, and exports text. The converter packs text to `.l2dat` and verifies the round trip byte for byte.
