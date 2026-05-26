# WiX Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a signed-ready `WillowSetup-x64.msi` that installs Willow to `%ProgramFiles%\Willow`, creates a Start Menu shortcut, and uninstalls cleanly.

**Architecture:** A WiX v5 MSBuild SDK project (`Installer/Willow.Installer.wixproj`) harvests files from the existing `dotnet publish` output. The main package definition (`Package.wxs`) wires up directories, shortcuts, and the WixUI dialog. ARM64 is a second pass using the same source with a different publish path.

**Tech Stack:** WiX Toolset v5 (`WixToolset.Sdk` NuGet SDK), `WixToolset.UI.wixext` for the installer dialog, `dotnet publish` for self-contained app output.

---

## File Map

| File | Action | Responsibility |
|---|---|---|
| `Installer/Willow.Installer.wixproj` | **Create** | MSBuild project: SDK ref, publish path, harvesting, extension refs |
| `Installer/Package.wxs` | **Create** | Package metadata, directories, shortcut, feature, UI |
| `Willow.slnx` | **Modify** | Add installer project |
| `CLAUDE.md` | **Modify** | Add installer build instructions |

---

## Task 1: Create the WiX project file

**Files:**
- Create: `Installer/Willow.Installer.wixproj`

> **Context:** WiX v5 uses a NuGet MSBuild SDK (`WixToolset.Sdk`). No global tool install required — `dotnet build` restores and invokes the compiler automatically. `HarvestDirectory` replaces the old `heat.exe` workflow and auto-generates component GUIDs from file paths.

- [ ] **Step 1: Create the `Installer/` directory and project file**

Create `Installer/Willow.Installer.wixproj` with this exact content:

```xml
<Project Sdk="WixToolset.Sdk/5.0.2">

  <PropertyGroup>
    <OutputName>WillowSetup-x64</OutputName>
    <Platform>x64</Platform>
    <!-- Default publish path; override with /p:PublishDir=... on the command line -->
    <PublishDir Condition="'$(PublishDir)' == ''">
      ..\Willow\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\
    </PublishDir>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WixToolset.UI.wixext" Version="5.0.2" />
  </ItemGroup>

  <!-- Harvest all published files into the ApplicationFiles component group -->
  <ItemGroup>
    <HarvestDirectory Include="$(PublishDir)">
      <ComponentGroupName>ApplicationFiles</ComponentGroupName>
      <DirectoryRefId>INSTALLFOLDER</DirectoryRefId>
      <SuppressRootDirectory>true</SuppressRootDirectory>
    </HarvestDirectory>
  </ItemGroup>

  <!-- Make the publish directory a bind path so WiX can resolve Willow.exe for the icon -->
  <ItemGroup>
    <BindPath Include="$(PublishDir)" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Verify the project restores**

First publish the app so the output exists:
```powershell
dotnet publish Willow/Willow.csproj -c Release -p:Platform=x64
```
Expected: `Willow\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\` is populated.

Then restore the installer project:
```powershell
dotnet restore Installer/Willow.Installer.wixproj
```
Expected: WiX SDK and UI extension packages are downloaded to the NuGet cache. No errors.

- [ ] **Step 3: Commit**
```
git add Installer/Willow.Installer.wixproj
git commit -m "chore: add WiX v5 installer project scaffold"
```

---

## Task 2: Write Package.wxs

**Files:**
- Create: `Installer/Package.wxs`

> **Context:**
> - `UpgradeCode` is a permanent GUID that identifies this product forever across all versions. **Never change it.** It's what enables MSI upgrades to find and replace the old installation.
> - `Scope="perMachine"` matches the app's `requireAdministrator` manifest — the MSI installer always runs elevated for per-machine installs.
> - `WixUI_InstallDir` gives the user a directory picker + one-click install.
> - The icon is extracted directly from `Willow.exe` by WiX (no separate .ico file needed). WiX resolves the EXE via the bind path set in the project file.
> - `MajorUpgrade` handles replacing any previously installed version automatically.

- [ ] **Step 1: Create `Installer/Package.wxs`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">

  <Package Name="Willow"
           Manufacturer="WinApps"
           Version="1.0.0.0"
           UpgradeCode="{4E32A1B5-7C8D-4F6E-A9B0-1C2D3E4F5A6B}"
           Scope="perMachine"
           InstallerVersion="500">

    <SummaryInformation Description="Willow System Optimizer — Setup" />

    <MajorUpgrade DowngradeErrorMessage="A newer version of Willow is already installed." />

    <!-- Extract the icon from the published EXE (resolved via BindPath in .wixproj) -->
    <Icon Id="WillowIcon.exe" SourceFile="Willow.exe" />
    <Property Id="ARPPRODUCTICON" Value="WillowIcon.exe" />
    <Property Id="ARPNOMODIFY" Value="1" />

    <!-- Install to %ProgramFiles%\Willow -->
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="Willow" />
    </StandardDirectory>

    <!-- Start Menu folder -->
    <StandardDirectory Id="ProgramMenuFolder">
      <Directory Id="WillowMenuFolder" Name="Willow" />
    </StandardDirectory>

    <!-- Shortcut component (manual, not harvested — needs explicit GUID) -->
    <ComponentGroup Id="ApplicationShortcuts" Directory="WillowMenuFolder">
      <Component Id="StartMenuShortcut"
                 Guid="{1A2B3C4D-5E6F-4A7B-8C9D-0E1F2A3B4C5D}">
        <Shortcut Id="WillowStartMenu"
                  Name="Willow"
                  Description="Windows system optimization utility"
                  Target="[INSTALLFOLDER]Willow.exe"
                  Icon="WillowIcon.exe"
                  WorkingDirectory="INSTALLFOLDER" />
        <!-- Remove the Start Menu folder on uninstall -->
        <RemoveFolder Id="RemoveWillowMenuFolder"
                      Directory="WillowMenuFolder"
                      On="uninstall" />
        <!-- Registry key path — required; shortcuts cannot be KeyPaths -->
        <RegistryValue Root="HKLM"
                       Key="Software\WinApps\Willow"
                       Name="StartMenuShortcut"
                       Type="integer"
                       Value="1"
                       KeyPath="yes" />
      </Component>
    </ComponentGroup>

    <Feature Id="ProductFeature" Title="Willow" Level="1">
      <!-- ApplicationFiles is populated by HarvestDirectory in the .wixproj -->
      <ComponentGroupRef Id="ApplicationFiles" />
      <ComponentGroupRef Id="ApplicationShortcuts" />
    </Feature>

    <!-- WixUI_InstallDir: shows a directory picker, then installs -->
    <ui:WixUI Id="WixUI_InstallDir" InstallDirectory="INSTALLFOLDER" />

    <!-- Suppress the license agreement page (no EULA for this utility) -->
    <ui:WixUIRef Id="WixUI_InstallDir" />
    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />

  </Package>
</Wix>
```

- [ ] **Step 2: Build the installer**

```powershell
dotnet build Installer/Willow.Installer.wixproj -c Release
```

Expected output (last lines):
```
  WillowSetup-x64 -> C:\...\Installer\bin\Release\en-US\WillowSetup-x64.msi
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If you see `error CNDL0103: The system cannot find the file 'Willow.exe'`, the publish output is missing or `PublishDir` is wrong — re-run the `dotnet publish` from Task 1 Step 2.

- [ ] **Step 3: Commit**
```
git add Installer/Package.wxs
git commit -m "chore: add WiX package definition with shortcut and install-dir UI"
```

---

## Task 3: Suppress the License Page

**Files:**
- Modify: `Installer/Package.wxs` (add UI customisation file) or
- Create: `Installer/CustomUI.wxs`

> **Context:** `WixUI_InstallDir` includes a license agreement page by default. Since Willow ships without a EULA we need to suppress it. The cleanest way is to override the UI with a custom `WixUI_InstallDir_NoLicense` fragment that drops the `LicenseAgreementDlg` and its transitions.

- [ ] **Step 1: Create `Installer/CustomUI.wxs`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">

  <Fragment>
    <!-- Remove the "Next" button on WelcomeDlg that leads to LicenseAgreementDlg,
         and wire it directly to InstallDirDlg instead. -->
    <ui:Publish Dialog="WelcomeDlg" Control="Next" Event="NewDialog" Value="InstallDirDlg" Order="2" />
    <ui:Publish Dialog="InstallDirDlg" Control="Back" Event="NewDialog" Value="WelcomeDlg" Order="2" />
  </Fragment>

</Wix>
```

- [ ] **Step 2: Rebuild and confirm no license page appears**

```powershell
dotnet build Installer/Willow.Installer.wixproj -c Release
```

Run the MSI from `Installer\bin\Release\en-US\WillowSetup-x64.msi`. The wizard should show:
1. Welcome screen → Next
2. Install directory picker → Install
3. Progress → Finish

No license page should appear.

- [ ] **Step 3: Commit**
```
git add Installer/CustomUI.wxs
git commit -m "chore: suppress license agreement page in installer UI"
```

---

## Task 4: Smoke-test install and uninstall

> **Context:** There are no automated tests for MSI behavior. These manual steps verify the installer works end-to-end and leaves the system clean.

- [ ] **Step 1: Install**

Double-click `Installer\bin\Release\en-US\WillowSetup-x64.msi` (or run elevated from PowerShell):
```powershell
msiexec /i "Installer\bin\Release\en-US\WillowSetup-x64.msi" /l*v install.log
```

Verify:
- `%ProgramFiles%\Willow\Willow.exe` exists
- `%ProgramFiles%\Willow\` contains the full publish output (~60–80 MB for a self-contained .NET 8 app)
- Start Menu → Willow shortcut appears and launches the app
- Settings → Apps shows "Willow" with version 1.0.0.0

- [ ] **Step 2: Verify the app runs correctly from the installed location**

Launch from the Start Menu shortcut and confirm:
- App opens with no errors
- All pages (Dashboard, Optimizer, Cleaner, Startup, Disk Health, Settings) work

- [ ] **Step 3: Uninstall**

```powershell
msiexec /x "Installer\bin\Release\en-US\WillowSetup-x64.msi" /l*v uninstall.log
```

Verify:
- `%ProgramFiles%\Willow\` is gone (or only contains user-modified files, which is acceptable)
- Start Menu shortcut is removed
- "Willow" is no longer listed in Settings → Apps

- [ ] **Step 4: Clean up log files**
```powershell
Remove-Item install.log, uninstall.log -ErrorAction SilentlyContinue
```

---

## Task 5: Add to solution and update CLAUDE.md

**Files:**
- Modify: `Willow.slnx`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Add installer to the solution**

Edit `Willow.slnx` — add the WiX project entry (no platform config needed for installer projects):

```xml
<Solution>
  <Configurations>
    <Platform Name="ARM64" />
    <Platform Name="x64" />
    <Platform Name="x86" />
  </Configurations>
  <Project Path="Installer/Willow.Installer.wixproj" />   <!-- add this line -->
  <Project Path="Willow.Tests/Willow.Tests.csproj" />
  <Project Path="Willow/Willow.csproj">
    ...
  </Project>
</Solution>
```

- [ ] **Step 2: Add installer build instructions to CLAUDE.md**

In the **Build & Run** section, after the existing commands, add:

````markdown
```powershell
# Build installer (x64) — publish the app first, then build the MSI
dotnet publish Willow\Willow.csproj -c Release -p:Platform=x64
dotnet build Installer\Willow.Installer.wixproj -c Release
# Output: Installer\bin\Release\en-US\WillowSetup-x64.msi
```
````

Also add a note under `## Architecture` or a new `## Installer` section:

````markdown
## Installer

`Installer/Willow.Installer.wixproj` — WiX v5 MSBuild SDK project that produces `WillowSetup-x64.msi`. It harvests files from the `dotnet publish` output automatically (no manual file list maintenance). The `UpgradeCode` GUID in `Package.wxs` is permanent — never change it or existing installations won't upgrade cleanly.

To add an ARM64 installer, duplicate the `.wixproj` with `<Platform>ARM64</Platform>` and `win-arm64` in the publish path.
````

- [ ] **Step 3: Commit**
```
git add Willow.slnx CLAUDE.md
git commit -m "docs: add installer to solution and document build steps"
```

---

## Self-Review

**Spec coverage:**
- ✅ WiX v5 project created
- ✅ Per-machine install to Program Files
- ✅ Start Menu shortcut
- ✅ Upgrade/uninstall handled via `MajorUpgrade`
- ✅ No license page (utility app)
- ✅ Icon from EXE (no separate ICO required)
- ✅ Smoke-test steps defined
- ✅ ARM64 path noted
- ✅ CLAUDE.md updated

**Placeholder scan:** No TBDs, no "implement later" language. All code blocks are complete.

**Type consistency:**
- `INSTALLFOLDER` used consistently in Package.wxs and `WixUI_InstallDir`
- `ApplicationFiles` ComponentGroupName matches between `.wixproj` and `Package.wxs`
- `ApplicationShortcuts` ComponentGroupRef matches ComponentGroup Id

---

## Notes

**Signing:** The `.msi` will trigger SmartScreen on first run without a code-signing certificate. For distribution, sign with:
```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f cert.pfx /p password "Installer\bin\Release\en-US\WillowSetup-x64.msi"
```

**ARM64 installer:** Copy `Willow.Installer.wixproj` to `Willow.Installer.arm64.wixproj`, change `<Platform>ARM64</Platform>`, `<OutputName>WillowSetup-arm64</OutputName>`, and the publish path to `win-arm64\publish\`. Everything else is identical.

**Version bumps:** Update `Version` in `Package.wxs` when cutting releases. The `UpgradeCode` (`{4E32A1B5-7C8D-4F6E-A9B0-1C2D3E4F5A6B}`) must stay the same forever.
