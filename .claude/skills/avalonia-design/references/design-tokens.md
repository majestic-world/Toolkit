# Design Tokens Reference

## Color Palette Philosophy

Use a maximum of **3 color roles** per interface:
1. **Neutrals** — surfaces, borders, text (always present)
2. **Accent** — one primary brand/interaction color
3. **Semantic** — success, warning, danger (use sparingly, only for status)

Never introduce a fourth hue for decoration.

---

## Light Theme Tokens

| Token | Hex | Usage |
|-------|-----|-------|
| `SurfaceBackground` | `#F5F5F5` | App background, page canvas |
| `SurfaceCard` | `#FFFFFF` | Cards, panels, inputs |
| `SurfaceElevated` | `#FAFAFA` | Subtle card variant |
| `SurfaceBorder` | `#E0E0E0` | Dividers, input borders |
| `SurfaceBorderSubtle` | `#F0F0F0` | Very subtle separators |
| `ContentPrimary` | `#1A1A1A` | Main body text, headings |
| `ContentSecondary` | `#6B7280` | Supporting text, labels |
| `ContentTertiary` | `#9CA3AF` | Placeholder text, hints |
| `ContentDisabled` | `#BDBDBD` | Disabled text |
| `ContentInverse` | `#FFFFFF` | Text on dark/colored backgrounds |
| `AccentPrimary` | `#0F7EC8` | Primary buttons, active states, links |
| `AccentHover` | `#0D6BAD` | Hover state |
| `AccentPressed` | `#0A5A92` | Pressed state |
| `AccentSubtle` | `#E8F4FD` | Accent tinted backgrounds |
| `AccentForeground` | `#FFFFFF` | Text/icons on accent background |
| `ColorSuccess` | `#2E7D32` | Success text/icon |
| `ColorSuccessBg` | `#E8F5E9` | Success chip/badge background |
| `ColorWarning` | `#E65100` | Warning text/icon |
| `ColorWarningBg` | `#FFF3E0` | Warning chip/badge background |
| `ColorDanger` | `#C62828` | Error text/icon, destructive actions |
| `ColorDangerBg` | `#FFEBEE` | Error chip/badge background |

---

## Dark Theme Tokens

| Token | Hex | Usage |
|-------|-----|-------|
| `SurfaceBackground` | `#111827` | App background |
| `SurfaceCard` | `#1F2937` | Cards, panels |
| `SurfaceElevated` | `#263041` | Elevated cards |
| `SurfaceBorder` | `#374151` | Dividers, borders |
| `SurfaceBorderSubtle` | `#2D3748` | Subtle separators |
| `ContentPrimary` | `#F9FAFB` | Main text |
| `ContentSecondary` | `#9CA3AF` | Supporting text |
| `ContentTertiary` | `#6B7280` | Placeholder text |
| `ContentDisabled` | `#4B5563` | Disabled text |
| `ContentInverse` | `#111827` | Text on light backgrounds |
| `AccentPrimary` | `#3B9EDD` | Primary buttons, links |
| `AccentHover` | `#5AAFE3` | Hover |
| `AccentPressed` | `#2A8EC8` | Pressed |
| `AccentSubtle` | `#1A3A52` | Accent tinted background |
| `AccentForeground` | `#FFFFFF` | |
| `ColorSuccess` | `#4CAF50` | |
| `ColorSuccessBg` | `#1A3320` | |
| `ColorWarning` | `#FB8C00` | |
| `ColorWarningBg` | `#3D2800` | |
| `ColorDanger` | `#EF5350` | |
| `ColorDangerBg` | `#3D1A1A` | |

---

## Typography Scale

| Token | Size | Weight | Usage |
|-------|------|--------|-------|
| `FontSizeSmall` | 12px | 400 | Captions, metadata, badges |
| `FontSizeBody` | 14px | 400 | Default body text |
| `FontSizeBodyMedium` | 14px | 500 | Emphasized body, table headers |
| `FontSizeSubtitle` | 16px | 500 | Section subtitles |
| `FontSizeTitle` | 20px | 600 | Card titles, dialog titles |
| `FontSizeHeading` | 24px | 600 | Page headings |
| `FontSizeDisplay` | 32px | 700 | Hero metrics, large KPIs |

Font family: `Inter, Segoe UI, system-ui, sans-serif`

---

## Spacing Scale (4px grid)

| Token | Value | Usage |
|-------|-------|-------|
| `SpacingXXS` | 2px | Tight icon-to-label gap |
| `SpacingXS` | 4px | Compact element gap |
| `SpacingS` | 8px | Icon padding, tight list gap |
| `SpacingM` | 12px | Input padding vertical, label margin |
| `SpacingL` | 16px | Card padding, standard section gap |
| `SpacingXL` | 24px | Between card rows |
| `SpacingXXL` | 32px | Between major sections |
| `SpacingXXXL` | 48px | Page top padding |

---

## Shape / Border Radius

| Token | Value | Usage |
|-------|-------|-------|
| `RadiusXS` | 2px | Tags, inline badges |
| `RadiusS` | 4px | Inputs, secondary buttons |
| `RadiusM` | 6px | Primary buttons, cards |
| `RadiusL` | 8px | Dialogs, panels |
| `RadiusXL` | 12px | Large cards, sidebars |
| `RadiusFull` | 9999px | Pills, avatars, toggle |

---

## Elevation (Shadow)

Avalonia does not have native drop shadow out of the box in all versions. Use border + background contrast to indicate elevation instead:

- **Level 0** — flat, same as background (`SurfaceBackground`)
- **Level 1** — card (`SurfaceCard`) + border (`SurfaceBorder`)
- **Level 2** — slightly elevated, `SurfaceElevated` + `BoxShadow="0 2 8 0 #0A000000"` if available
- **Level 3** — dialogs/modals + `BoxShadow="0 8 32 0 #18000000"`

---

## Dark Theme AXAML Structure

```xml
<!-- App.axaml -->
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ResourceInclude Source="avares://YourApp/Resources/Tokens.Light.axaml"/>
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>

<Application.Styles>
  <FluentTheme/>
  <StyleInclude Source="avares://YourApp/Styles/Controls.axaml"/>
</Application.Styles>
```

For dark theme support, use Avalonia's `ThemeVariant`:
```xml
<Application RequestedThemeVariant="Dark">
```

Or let users switch at runtime:
```csharp
Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
```

Define variant-specific resources using `ResourceDictionary` with theme variant selectors:
```xml
<ResourceDictionary>
  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
      <Color x:Key="SurfaceBackground">#F5F5F5</Color>
      <!-- ... -->
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
      <Color x:Key="SurfaceBackground">#111827</Color>
      <!-- ... -->
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
```
