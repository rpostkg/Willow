# Privacy Page Design

**Date:** 2026-05-26  
**Status:** Approved

## Overview

Add a dedicated Privacy page to Willow between Disk Health and Optimizer in the navigation. The page has two sections:

1. **Privacy Score** — a Low/Fair/Good label derived from how many privacy tweaks are applied.
2. **App Permissions** — a flat list of apps that have previously requested camera, microphone, or location access, with per-capability checkboxes to allow or deny access.

---

## Navigation

Add a new `NavigationViewItem` with `Tag="Privacy"` between Disk Health and Optimizer in `MainWindow.xaml`. Add a `case "Privacy"` to the `NavView_SelectionChanged` switch in `MainWindow.xaml.cs`.

---

## Privacy Score

### Logic

Load tweaks from `privacy.yaml` specifically (via a new `TweakLoaderService.LoadTweaksFromFile(string fileName)` overload). Check `PreferencesService.LoadPreferences().OldRegistryData` to determine which tweak IDs are applied.

**Scoring thresholds** (4 total tweaks in privacy.yaml):
- 0 applied → **Low**
- 1–2 applied → **Fair**
- 3–4 applied → **Good**

### Display

A card at the top of the page showing a colored badge label and a subtitle explaining what it measures ("Based on applied privacy tweaks"). No progress bar or percentage — label only.

Badge background color (via `ScoreBackground` SolidColorBrush on ViewModel):
- Low → red (`#C42B1C` — WinUI critical red)
- Fair → yellow-orange (`#9D5D00` — WinUI caution)
- Good → green (`#0F7B0F` — WinUI success)

---

## App Permissions

### Data Source

Windows stores per-app capability consent in the registry under:

```
HKCU\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\{capability}
```

Where `{capability}` is `webcam`, `microphone`, or `location`. Each subkey is either:
- A UWP package family name (e.g. `Microsoft.WindowsCamera_8wekyb3d8bbwe`)
- A path-encoded Win32 app under a `NonPackaged` subkey (e.g. `C:#Windows#System32#foo.exe`)

The `Value` string entry under each app subkey is `"Allow"` or `"Deny"`.

Only apps that have previously requested access appear. The global capability toggle (top-level `Value` key) is not surfaced here.

### AppPermissionsService

New stateless service `AppPermissionsService`:

```
List<AppPermission> GetPermissions()
```
- Opens each of the three ConsentStore keys
- Enumerates subkeys (skipping `NonPackaged` as a grouping key — its children are enumerated instead, prefixed with `NonPackaged\`)
- Unions app names across all three capabilities
- Returns one `AppPermission` per app with all three capability states populated

```
void SetPermission(string appKey, string capability, bool allow)
```
- Writes `Value` = `"Allow"` or `"Deny"` to `HKCU\...\ConsentStore\{capability}\{appKey}\Value`
- `capability` is `"webcam"`, `"microphone"`, or `"location"`
- Creates the subkey if it doesn't exist (so a new denial can be stored even if the app hasn't prompted yet — though the UI only shows apps already in the store)

### Display Name

Derived from the registry key name:
- **UWP**: strip the `_xxxxxxxx` publisher suffix (everything from the last `_` onward). Example: `Microsoft.WindowsCamera_8wekyb3d8bbwe` → `Microsoft.WindowsCamera`
- **Win32** (under `NonPackaged`): replace `#` with `\`, extract the filename without extension. Example: `C:#Windows#System32#foo.exe` → `foo`

### AppPermission Model

`AppPermission : ObservableObject`
- `AppKey` (string) — raw registry subkey name used for writes
- `DisplayName` (string) — cleaned name for display
- `HasCamera` (bool, observable) — on change: calls `AppPermissionsService.SetPermission`
- `HasMic` (bool, observable) — on change: calls `AppPermissionsService.SetPermission`
- `HasLocation` (bool, observable) — on change: calls `AppPermissionsService.SetPermission`

Changes write to the registry immediately (no confirm step needed).

---

## ViewModel — PrivacyViewModel

`PrivacyViewModel : ObservableObject`

Properties:
- `ScoreLabel` (string) — "Low" / "Fair" / "Good" (localized)
- `ScoreBackground` (SolidColorBrush) — badge background color derived from score (red/yellow/green using `Windows.UI.Color` values matching WinUI semantic colors)
- `AppPermissions` (ObservableCollection\<AppPermission\>)
- `IsEmpty` (bool) — true when `AppPermissions` is empty (show empty state)

Constructor calls `Load()`:
- Loads score via `TweakLoaderService.LoadTweaksFromFile("privacy.yaml")` + `PreferencesService`
- Loads permissions via `AppPermissionsService.GetPermissions()`

---

## View — PrivacyPage.xaml

Layout (Grid, Padding="24"):

```
Row 0: Title (TitleTextBlockStyle)
Row 1: Score card (Auto height)
Row 2: Section header "App Permissions" (SubtitleTextBlockStyle)
Row 3: App permission list (*)
```

**Score card** — a `Border` (card style) containing:
- A colored `Border` badge with `ScoreLabel` text
- A subtitle: "Based on applied privacy tweaks"

**App permission list** — `ScrollViewer` > `ItemsRepeater` (StackLayout) over `AppPermissions`. Each card row is a `Border` containing a `Grid` with columns:
- `*` — `TextBlock` for `DisplayName`
- `Auto` — camera `CheckBox` (labeled with a camera glyph + "Camera")
- `Auto` — mic `CheckBox` (labeled with mic glyph + "Microphone")
- `Auto` — location `CheckBox` (labeled with location glyph + "Location")

Empty state: when `IsEmpty` is true, show a centered message ("No apps have requested access").

---

## Localization

New resource keys needed in both `en-US/Resources.resw` and `uk-UA/Resources.resw`:

| Key | en-US value |
|-----|------------|
| `Nav_Privacy.Content` | Privacy |
| `PrivacyPage_Title.Text` | Privacy |
| `PrivacyPage_ScoreSubtitle.Text` | Based on applied privacy tweaks |
| `PrivacyPage_ScoreLow` | Low |
| `PrivacyPage_ScoreFair` | Fair |
| `PrivacyPage_ScoreGood` | Good |
| `PrivacyPage_AppPermissionsHeader.Text` | App Permissions |
| `PrivacyPage_CameraLabel` | Camera |
| `PrivacyPage_MicLabel` | Microphone |
| `PrivacyPage_LocationLabel` | Location |
| `PrivacyPage_EmptyState.Text` | No apps have requested access |

---

## Files to Create

- `Willow/Services/AppPermissionsService.cs`
- `Willow/Models/AppPermission.cs`
- `Willow/ViewModels/PrivacyViewModel.cs`
- `Willow/Views/PrivacyPage.xaml`
- `Willow/Views/PrivacyPage.xaml.cs`

## Files to Modify

- `Willow/Services/TweakLoaderService.cs` — add `LoadTweaksFromFile(string fileName)` overload
- `Willow/MainWindow.xaml` — add Privacy nav item
- `Willow/MainWindow.xaml.cs` — add Privacy navigation case
- `Willow/Strings/en-US/Resources.resw` — add new keys
- `Willow/Strings/uk-UA/Resources.resw` — add new keys (Ukrainian translations)
