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

## UI Design Notes
The app targets an enterprise/professional aesthetic: restrained dark theme inspired by Material Design / Fluent Design. Avoid decorative elements. When adding new pages, follow the layout of existing pages (header with title + subtitle, content area, log display).

**Default skill for all UI/design work:** use `/avalonia-design` whenever creating, redesigning, or styling any UI element in this project.
