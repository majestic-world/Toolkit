# L2Toolkit engineering reference

Read `CONTEXT.md` first. This reference preserves the engineering constraints that are not obvious from the repository layout.

## Application boundary

L2Toolkit is a single-window Avalonia desktop suite for Lineage 2 server developers. It has more than fifteen data-processing tools and targets .NET 10 with nullable reference types enabled.

- Build/run in development: `make run` (`dotnet run --project src`; `dotnet build` at the root uses `L2Toolkit.sln`)
- Release publish: `make build` (Windows), `make macos`, `make linux`, `make dist` (Windows + Inno Setup installer). They run `scripts/build.ps1` under PowerShell 7; Native AOT must run on the target OS. Publish output: `build/Release/<rid>/publish/`; tool logs: `build/logs/<rid>/`.
- Build outputs: `Directory.Build.props` sends everything to `build/` (`build/<Config>/[<rid>/]`, intermediates in `build/obj/`, no target-framework folder). There are no `bin/` or `obj/` folders; do not reintroduce paths to them.
- App version: `APP_VERSION` in `.env` is the only source. `L2Toolkit.csproj` turns it into the assembly/file/informational version (the titlebar badge reads the informational version), and `scripts/build.ps1` passes it to Inno Setup (`/DMyAppVersion`) and the macOS bundle `Info.plist`. Do not hardcode versions elsewhere; `Setup.iss` refuses to compile without the define.
- There is no test project. Verify changed behavior in the running GUI, including the relevant file-processing path.

## UI and application structure

The repository root holds only repository-level files (`L2Toolkit.sln`, `Directory.Build.props`, `Makefile`, `.env`, docs). App code lives in `src/`, where each folder is a namespace under `L2Toolkit` (folder `Views/` → `L2Toolkit.Views`). Keep that folder/namespace match for new code. Installer inputs live in `packaging/` (`windows/Setup.iss`, `macos/Info.plist`).

`Views/MainWindow` owns the sidebar and content region. Its generic navigation path caches each `UserControl`, assigns it to `MainContent.Content`, then marks the selected sidebar button. New tools normally remain an AXAML plus code-behind pair under `Views/`.

| Location (`src/`) | Namespace | Responsibility |
| --- | --- | --- |
| `App.axaml`, `Program.cs` | `L2Toolkit` | Application entry and shared control themes |
| `Views/` | `L2Toolkit.Views` | Avalonia views (main window and tool pages) and tool-specific UI logic |
| `Models/` | `L2Toolkit.Models` | Plain C# models and records |
| `Parsing/` | `L2Toolkit.Parsing` | Low-level text parsers for game data |
| `Processing/` | `L2Toolkit.Processing` (`.Geodata`) | Transformations from game formats to XML or text |
| `ClientDat/` | `L2Toolkit.ClientDat` | Client `.dat` reading/writing and `.l2dat` packing |
| `Settings/` | `L2Toolkit.Settings` | User settings persistence (`AppDatabase`) |
| `Utilities/` | `L2Toolkit.Utilities` | Shared UI and data helpers, including logs and table loading |
| `Data/` | `L2Toolkit.Data` | Built-in data (`H5Names`, embedded `Presets.dat`) |
| `Tables/` | — | Embedded `.l2dat` tables shipped with the app |
| `Assets/` | — | App and installer icons |

Embedded resource names follow the folder under the project (`L2Toolkit.Tables.<name>.l2dat`, `L2Toolkit.Data.Presets.dat`); moving `Tables/` or `Data/` changes those names and breaks their loaders.

### Avalonia conventions

- Keep common control appearance in the `ControlTheme` resources in `App.axaml`; controls use the shared ComboBox, Button, TextBox, `PrimaryButton`, and `SecondaryButton` themes.
- Keep button themes free of `BrushTransition`; it produces a two-tone hover flicker.
- Buttons and inputs (TextBox, ComboBox, file-path wrappers, button `ControlTheme`s in pages and windows) use `CornerRadius` 4; `App.axaml` also sets Fluent's `ControlCornerRadius` to 4 for unthemed controls. Cards keep 8, banners 6, popups 10, gallery tiles (`TileButton`) 8.
- Themes: Dark (default) and Light, chosen in Configurações → Aparência and saved as `app_theme` in the settings file (`Utilities/AppTheme`). Every app color is a `Theme*` brush in `Themes/Colors.axaml` with a Dark and a Light value. Views use `{DynamicResource Theme*}`; controls built in code bind with `[!Border.BackgroundProperty] = AppTheme.Brush("Theme*")` (also `Border.BorderBrushProperty`, `TextElement.ForegroundProperty`, `Shape.StrokeProperty`) so a theme switch updates them live. Never hardcode a hex color in a view or code-behind; add a token to both dictionaries. Swatches that show data colors (client `.dat` values) stay literal.
- Dark palette and its restrained surface steps: titlebar/sidebar `#2A2A2A`, page `#333333`, panels/cards `#2C2C2C`, recessed inputs `#252525`, borders `#464646`, blue accent around `#5B9BD5`, page subtitles in `#E8E8E8`, secondary labels no darker than `#B8B8B8`. Get contrast from text and borders, not from large gaps between surface grays. Light uses white cards on `#F3F3F3`, and its accent, icons and primary buttons are dark gray (`#3D3D3D`), not blue; Fluent's own accent follows it through `ColorPaletteResources` in `App.axaml`.
- Fluent's `TextBox` watermark has a template-fixed `Opacity="0.5"`; `App.axaml` sets its foreground to `ThemeWatermark` (white in Dark, black in Light) so the effective placeholder stays legible. Do not set watermark colors per page.
- Color selection uses the shared `Views/Controls/HsvColorPicker` (saturation/value square + hue strip, RGB only) hosted in a page `Popup`; pages call `SetColor` when opening it and react to `ColorChanged`. Do not build per-page slider pickers.
- The custom titlebar supports drag-to-move and double-click maximize. Do not replace that behavior when changing window chrome.

### Shared runtime services

`GlobalLogs` is a per-page, thread-safe log buffer. Create/register it with the page log `TextBox`, write with `AddLog`, and let it dispatch updates to the Avalonia UI thread. It keeps the newest 120 entries.

`AppDatabase.GetInstance()` returns the settings store. Settings live in `%APPDATA%/L2Toolkit/settings.properties`; use `GetValue`/`GetInt` to read and `UpdateValue` to persist a value.

Pages select files or folders through Avalonia's storage provider, process asynchronously, and write outputs to disk. Log meaningful progress and failure context through the page's `GlobalLogs` instance.

### Product documentation

`docs/index.html` is the public, single-page product documentation. It is standalone HTML/CSS with Font Awesome and Google Fonts; update it directly when product documentation changes.

## App updates

`Utilities/AppUpdater` updates the app from GitHub Releases of `majestic-world/Toolkit` (`releases/latest`). A release counts as newer when its tag (`3.9`, `v3.9.1`; missing parts are 0) is above `APP_VERSION`, so publish each release with the tag equal to the `.env` version and the Inno Setup installer (`.exe`) as an asset.

- `CheckAsync` is the only way to query: at most one request every 10 s (anti-flood, in memory), concurrent callers share the running request, and the throttled result carries the remaining wait.
- Nothing downloads without consent. On open (`MainWindow`) and from Settings → Aplicativo → "Verificar atualizações", an available release opens `MainWindow.PromptUpdate`: a modal with current → new version, release name, publish date, installer size and the release notes (GitHub `body`), plus "Ver no GitHub", "Agora não" and "Atualizar agora". Declining just closes it; the next open asks again.
- `DownloadAsync` saves to `%TEMP%\L2Toolkit`, checks size and the asset's GitHub `digest` (SHA-256). `LaunchInstaller` opens the installer wizard (`/SP- /NORESTART /CLOSEAPPLICATIONS`, not silent) and the app shuts down right after; the wizard ends on the Finished page with the "Abrir L2 Toolkit" checkbox (`postinstall` entry in `Setup.iss`), same as a manual install. The next start deletes leftover installers.
- After "Atualizar agora" the same modal switches to download progress and can cancel the download. Outside Windows there is no installer: confirming opens the release page.

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

1. Add a mutable `DatXxx` record under `ClientDat/`.
2. Add matching `ParseXxx(byte[])` and `SerializeXxx(List<DatXxx>)` methods to `L2DatFile`.
3. Check the structure XML for `isSafePackage`; append the SafePackage footer when required.
4. Wire page load through decrypt → parse → UI mapping, and save through UI mapping → serialize → encrypt → write.

`Views/AppSettingsControl.xaml.cs` also owns the L2DAT Converter, which packs text to `.l2dat` and verifies the round trip byte for byte.

## Splash screens

`Views/SplashScreen` edits the client opening bitmaps (`SysTextures/sp_256_*.bmp`, `sp_32b_*.bmp`, `logo_*.bmp`); the codec lives in `Processing/Splash/` (`L2Toolkit.Processing.Splash`). These files are not client `.dat`: they are plain BMPs inside an XOR `Lineage2Ver###` envelope (28-byte UTF-16LE header).

- `SplashEnvelope`: version 111 XORs with `0xAC`; version 121 XORs with the low byte of the sum of the lowercase file name's UTF-16 units, so saving under another name changes the key. Other versions are rejected.
- `SplashConverter` is the single conversion path for both the page preview and `SplashFile.Save`, so the preview is the saved result. Formats without alpha flatten transparency onto the key color (default `#00FF00`, the retail chroma green). Retail 256-color files do not store the key at a fixed palette slot (`166Retail/sp_256_01.bmp` has black at index 0), so the key color is a user setting, never inferred from the palette. In 256-color output, pixels with alpha < 128 map exactly to the key color, never to a dithered neighbor.
- An image with ≤ 256 colors keeps its exact colors when re-saved (palette order may change); larger images use a weighted median cut, optional Floyd–Steinberg.
- `SplashFile.Save` writes to a sibling `.tmp` then replaces the target; the page copies the original to `<file>.bak` on the first overwrite. PNG/JPG/WEBP import and PNG export go through SkiaSharp (explicit reference, same version Avalonia.Skia brings) as unpremultiplied RGBA.
- "Pasta do client" opens `Views/SplashLibraryWindow`. It lists the folder's BMPs through `SplashLibrary` (parallel decode, 240 px thumbnails, files over 48 MB or unreadable skipped); clicking a tile raises `FileChosen` and the page loads that file. The folder persists as `splash_last_folder`; without it, the open file's folder is used. `Utilities/RgbaBitmap` converts `RgbaImage` to an Avalonia bitmap for the pages and galleries.
- "Trocar todos por imagem…" in that window applies one image (PNG/JPG/WEBP/BMP) to every listed BMP in memory only, at the image's own resolution (never resized: an 800 × 600 art saves 800 × 600 over a 640 × 480 file; the client does not require 640 × 480). Each tile shows the conversion the save will apply (its own format, `SplashConverter.RetailKeyColor`, no dithering), marked "Trocado · W × H · não salvo". "Descartar" restores the tiles; changing or reloading the folder drops pending swaps. "Salvar todos" writes each file in parallel with its original format and encryption, using the same `SplashFile.BackupOnce` + `SplashFile.Save` path as the page, then rereads the tiles and raises `FilesSaved`; the page reloads its open file when it was rewritten and has no unsaved edits.
- "Compor com brush…" opens `Views/SplashComposeWindow`: it cuts an art (preloaded from the editor canvas when there is one) with a procedural brush mask, like a Photoshop clipping mask. `BrushGenerator.Cut` multiplies each pixel's alpha by `BrushGenerator.Mask`, which renders the same brush field as the exported brushes at the art's size; at scale 100% the brush spans the art's longer side, centered, and the position sliders shift it in fractions of width/height. "Enviar para o editor" raises `ResultSent`; the page treats it like "Substituir imagem" (keeps the cut's resolution) and switches to 32-bit, since the cut only survives with a real alpha channel. "Exportar PNG…" saves the cut with transparency.
- "Ocupar a arte toda (−10 px)" switches the compose window to `BrushGenerator.MaskFilled(settings, w, h, inset: 5)`: the brush is measured at a small probe scale (so its tips never clip), then each axis is rescaled independently so the painted bounding box spans the art minus 5 px per side (640 × 480 → 630 × 470); a second pass corrects the antialias/blur drift (≈1 px). `Placement` therefore carries separate `HalfX`/`HalfY`; square brush exports keep them equal, so exported brushes are unchanged. Size/position sliders are dimmed and ignored in this mode.
- "Contorno" in the compose window adds a solid stroke around the cut, like Photoshop's Stroke layer style: thickness 0–50 px (0 = off, default), color (default `#FFFFFF`) and position Fora/Dentro/Centro. `Processing/Splash/SplashStroke.Apply` runs after `BrushGenerator.Cut`, so the stroke is part of what goes to the editor and to the PNG. The edge is alpha ≥ 128; distances come from `DistanceField` (exact Euclidean distance transform, Felzenszwalb–Huttenlocher) with half a pixel of antialias. Outside is a layer under the cut (full under the silhouette, so the antialiased rim leaves no gap); inside recolors the cut without changing its alpha, and pixels below alpha 128 count as the rim; center is half of each. Cracks, holes and debris are edges too, so they get stroked as in Photoshop.
- "Sombra projetada" (checkbox, off by default) adds a CSS box-shadow/drop-shadow style shadow under the cut and the stroke: color (default `#000000`), opacity (60%), horizontal/vertical offset (−50…50 px, default 6), blur (0–50 px, CSS semantics: σ = blur / 2, three box-blur passes) and spread (0–30 px, the alpha ≥ 128 silhouette grown with `DistanceField` before the blur). `SplashShadow.Apply` runs last; the canvas keeps its size, so shadow past the edge is clipped. Stroke and shadow share one `HsvColorPicker` popup; `_pickColor` routes the picked color to whichever button opened it. Both layers go under the image through `Compositing.PaintUnder`.

## Gallery windows

`SplashLibraryWindow`, `SplashComposeWindow` and `BrushVariantsWindow` are the app's secondary windows and follow one pattern: independent (no owner, so they do not sit on top of the page), single instance per page (clicking the page button again activates it), closed when the main window closes, and results sent back to the page through an event. Gallery tiles use the shared `TileButton` theme in `App.axaml`; the `current` class marks the item on the page's stage.

## Brush generator

`Views/BrushGeneratorPage` generates Photoshop brushes in the torn-edge style of L2 splash art; the algorithm lives in `Processing/Brush/` (`L2Toolkit.Processing.Brush`). Output is a grayscale PNG, black brush on white, up to 5000 px (Photoshop's brush limit).

- `BrushGenerator` is deterministic: the same `BrushSettings` (seed + intensities) always produce the same bytes. The page preview (720 px) and the export run the same code in normalized coordinates, so the preview matches the export at any size.
- The silhouette is a pixel field (polar radius with lobes, ridged spikes and notches, domain warp, edge-band noise, narrow cracks, holes only near the edge); shards, claws and debris are SkiaSharp paths drawn on top. Each path feature draws from its own `Random` stream, so changing one intensity does not reshuffle the others.
- Intensities go 0–1 with 0.5 as the neutral look (internally scaled ×2). "Borda suave" adds gray grain on the inner edge band and blurs the paths. The page opens with Pontas 50, Rachaduras 10, Garras 0, Estilhaços 10, Borda suave 0, Simetria 30 (`BrushGeneratorPage.axaml`); the compose window keeps its own defaults.
- "Simetria" (`BrushSettings.Symmetry`, page default 30, compose window default 0) blends toward the balanced splash look: superellipse silhouette (rounded rectangle), no rotation, smaller lobes, no deep notches, more and smaller spikes, rarer holes, and claws tucked onto the edge. The field is mirrored on the vertical axis by blending `Body(u, v)` with `Body(−u, v)` on the left half; each path is mirrored with probability `Symmetry` (folded to the right half and redrawn flipped, count thinned so density stays the same, `Stream(4)`), anchored on `MirroredRadius`. At 0 nothing changes and no extra random draws happen, so existing seeds export byte-identical.
- "Gerar vários…" opens `Views/BrushVariantsWindow`: the user sets a count (1–200, saved as `brush_variants_count`), the window draws that many random seeds with the page's current intensities and renders 180 px thumbnails. Clicking one raises `VariantChosen` with its exact `BrushSettings`, and the page applies seed and intensities. "Exportar todos…" writes the shown settings (not the page's current sliders) as `l2brush_<seed>.png` at the page's selected size; the last export folder persists as `brush_last_folder`.
