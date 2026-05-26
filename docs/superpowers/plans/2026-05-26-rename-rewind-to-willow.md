# Rename Project: Willow → Willow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the project from "Willow" to "Willow" — all directory names, file names, namespaces, display names, and content references throughout the codebase.

**Architecture:** Three phases: (1) rename directories and files with `git mv` to preserve history, (2) bulk-replace "Willow" → "Willow" in all text file contents, (3) verify the build and tests pass with the new names.

**Tech Stack:** .NET 8 / WinUI 3 / WiX v5 / PowerShell / git

---

## File Structure

**Directories renamed (git mv):**
- `Willow/` → `Willow/`
- `Willow.Tests/` → `Willow.Tests/`

**Files renamed (git mv):**
- `Willow.slnx` → `Willow.slnx`
- `Willow/Willow.csproj` → `Willow/Willow.csproj`
- `Willow/Willow.csproj.user` → `Willow/Willow.csproj.user`
- `Willow.Tests/Willow.Tests.csproj` → `Willow.Tests/Willow.Tests.csproj`
- `Installer/Willow.Installer.wixproj` → `Installer/Willow.Installer.wixproj`

**Content changes across all text files (bulk replace):**
- Namespaces: `namespace Willow` → `namespace Willow`; sub-namespaces (`.Services`, `.Models`, `.ViewModels`, `.Views`, `.Tests`) updated automatically
- Using directives: `using Willow.Services` → `using Willow.Services`, etc.
- XAML class names: `x:Class="Willow.` → `x:Class="Willow.`
- XAML xmlns: `xmlns:local="using:Willow"` → `xmlns:local="using:Willow"`
- Window title: `Title="Willow"` → `Title="Willow"`
- Package/app display names in manifests and WiX
- App data folder: `%LOCALAPPDATA%\Willow` → `%LOCALAPPDATA%\Willow` (in `PreferencesService.cs`)
- Assembly identity: `name="Willow.app"` → `name="Willow.app"`
- InternalsVisibleTo: `Willow.Tests` → `Willow.Tests`
- Launch profiles in `launchSettings.json` and `csproj.user`
- WiX: executable reference (`Willow.exe` → `Willow.exe`), install directory, shortcut, registry key
- README.md and CLAUDE.md documentation strings
- Installer output name: `WillowSetup-x64` → `WillowSetup-x64`

**Not changed by this plan:**
- GUIDs (UpgradeCode, PhoneProductId, component Guids) — these are identity values, not names
- The root repository directory `C:\Users\WinApps\Documents\DP\Willow` — rename manually as a final step (see Task 5)

---

### Task 1: Rename directories with git mv

All steps run from the repository root `C:\Users\WinApps\Documents\DP\Willow`.

**Files:**
- Rename: `Willow/` → `Willow/`
- Rename: `Willow.Tests/` → `Willow.Tests/`

- [ ] **Step 1: Rename the main project directory**

```powershell
git mv Willow Willow
```

Expected: no output on success.

- [ ] **Step 2: Rename the test project directory**

```powershell
git mv Willow.Tests Willow.Tests
```

Expected: no output on success.

- [ ] **Step 3: Spot-check staging**

```powershell
git status --short | Select-String "^R"
```

Expected: Many lines like `R  Willow/App.xaml -> Willow/App.xaml` confirming git tracked the renames.

---

### Task 2: Rename project and solution files with git mv

**Files:**
- Rename: `Willow.slnx` → `Willow.slnx`
- Rename: `Willow/Willow.csproj` → `Willow/Willow.csproj`
- Rename: `Willow/Willow.csproj.user` → `Willow/Willow.csproj.user`
- Rename: `Willow.Tests/Willow.Tests.csproj` → `Willow.Tests/Willow.Tests.csproj`
- Rename: `Installer/Willow.Installer.wixproj` → `Installer/Willow.Installer.wixproj`

- [ ] **Step 1: Rename solution file**

```powershell
git mv Willow.slnx Willow.slnx
```

- [ ] **Step 2: Rename main project file**

```powershell
git mv Willow/Willow.csproj Willow/Willow.csproj
```

- [ ] **Step 3: Rename project user settings file**

```powershell
git mv Willow/Willow.csproj.user Willow/Willow.csproj.user
```

- [ ] **Step 4: Rename test project file**

```powershell
git mv Willow.Tests/Willow.Tests.csproj Willow.Tests/Willow.Tests.csproj
```

- [ ] **Step 5: Rename installer project file**

```powershell
git mv Installer/Willow.Installer.wixproj Installer/Willow.Installer.wixproj
```

- [ ] **Step 6: Verify all renames staged**

```powershell
git status --short
```

Expected: Staged renames for all five files above.

---

### Task 3: Bulk-replace "Willow" → "Willow" in all text file contents

This single PowerShell command updates every in-content reference to "Willow" across all source, project, config, markup, and documentation files. It skips `.git`, `bin`, and `obj` directories.

**Files:**
- Modify: all `.cs`, `.csproj`, `.slnx`, `.yaml`, `.yml`, `.xml`, `.json`, `.xaml`, `.resw`, `.wxs`, `.wixproj`, `.manifest`, `.pubxml`, `.md` files (excluding build artifacts)

- [ ] **Step 1: Run bulk replacement**

```powershell
$root = "C:\Users\WinApps\Documents\DP\Willow"
$extensions = "*.cs","*.csproj","*.slnx","*.yaml","*.yml","*.xml","*.json","*.xaml","*.resw","*.wxs","*.wixproj","*.manifest","*.pubxml","*.md"
Get-ChildItem -Path $root -Recurse -File -Include $extensions |
    Where-Object { $_.FullName -notmatch '\\.git\\' -and $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' } |
    ForEach-Object {
        $content = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
        if ($content.Contains("Willow")) {
            $newContent = $content.Replace("Willow", "Willow")
            [System.IO.File]::WriteAllText($_.FullName, $newContent, [System.Text.Encoding]::UTF8)
            Write-Host "Updated: $($_.Name)"
        }
    }
```

Expected output: ~30–40 lines like `Updated: App.xaml.cs`, `Updated: Willow.csproj`, etc.

- [ ] **Step 2: Fix the .claude/settings.local.json path**

The bulk replace also changed the root directory name inside the path-based permission string in `.claude/settings.local.json`, turning `...\Willow\Willow\...` into `...\Willow\Willow\...`. Since the root directory is not being renamed yet, restore the first segment:

Open `C:\Users\WinApps\Documents\DP\Willow\.claude\settings.local.json` and change:

```json
"Bash(Get-ChildItem -Path \"C:\\\\Users\\\\WinApps\\\\Documents\\\\DP\\\\Willow\\\\Willow\" -Recurse -Filter \"*.xaml\")"
```

back to:

```json
"Bash(Get-ChildItem -Path \"C:\\\\Users\\\\WinApps\\\\Documents\\\\DP\\\\Willow\\\\Willow\" -Recurse -Filter \"*.xaml\")"
```

(Only the first `Willow` in the path reverts to `Willow`; the second stays `Willow`.)

- [ ] **Step 3: Verify no "Willow" references remain in source files**

```powershell
$root = "C:\Users\WinApps\Documents\DP\Willow"
$extensions = "*.cs","*.csproj","*.slnx","*.yaml","*.yml","*.xml","*.json","*.xaml","*.resw","*.wxs","*.wixproj","*.manifest","*.pubxml","*.md"
Get-ChildItem -Path $root -Recurse -File -Include $extensions |
    Where-Object { $_.FullName -notmatch '\\.git\\' -and $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' } |
    ForEach-Object { Select-String -Path $_.FullName -Pattern "Willow" } |
    Select-Object Path, LineNumber, Line |
    Format-Table -AutoSize
```

Expected: No output. Any remaining hits (other than `.claude/settings.local.json` after the fix above) indicate a missed replacement and must be fixed manually.

---

### Task 4: Build and test

**Files:** No changes — this task only runs verification commands.

- [ ] **Step 1: Restore NuGet packages and build**

```powershell
dotnet build "C:\Users\WinApps\Documents\DP\Willow\Willow.slnx" -p:Platform=x64
```

Expected: `Build succeeded.` with 0 errors, 0 warnings (or only pre-existing warnings).

If the build fails with "project not found" errors, confirm the solution file (`Willow.slnx`) references `Willow/Willow.csproj` and `Willow.Tests/Willow.Tests.csproj` (not the old `Willow/` paths).

- [ ] **Step 2: Run unit tests**

```powershell
dotnet test "C:\Users\WinApps\Documents\DP\Willow\Willow.Tests\Willow.Tests.csproj" --runtime win-x64
```

Expected: `Passed!  - Failed: 0, Passed: N, Skipped: 0`.

---

### Task 5: Commit

- [ ] **Step 1: Stage all changes**

```powershell
git add -A
```

- [ ] **Step 2: Preview staged changes**

```powershell
git status --short | Select-Object -First 30
```

Expected: Mix of `R ` (renames) and `M ` (content modifications). No unexpected `?? ` untracked files.

- [ ] **Step 3: Commit**

```powershell
git commit -m "chore: rename project from Willow to Willow"
```

---

### Task 6: (Optional) Rename the root repository directory

The repository root folder is still named `Willow`. This must be done outside any active terminal or editor session (close VS, VS Code, and all terminals pointing to the repo first):

```powershell
Rename-Item "C:\Users\WinApps\Documents\DP\Willow" "C:\Users\WinApps\Documents\DP\Willow"
```

After renaming, update:
- `.claude\settings.local.json`: change the remaining `Willow` in the path permission to `Willow` (so the full path is `C:\\\\Users\\\\WinApps\\\\Documents\\\\DP\\\\Willow\\\\Willow`)
- `CLAUDE.md`: if it contains the absolute path (it currently doesn't)
- Any IDE workspace/project files that store the absolute path

The git history is unaffected by this rename — git tracks content, not the parent directory name.
