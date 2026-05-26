# Privacy Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dedicated Privacy page between Disk Health and Optimizer that shows a Low/Fair/Good privacy score derived from applied privacy tweaks, and lets users allow/deny camera, microphone, and location access per app.

**Architecture:** A new `PrivacyViewModel` drives the page; `TweakLoaderService` gets a `LoadTweaksFromFile` overload for scoped loading, and a new `AppPermissionsService` reads/writes the Windows CapabilityAccessManager registry keys. Write-back on checkbox toggle follows the same PropertyChanged subscription pattern used by `StartupViewModel`.

**Tech Stack:** WinUI 3, .NET 8, CommunityToolkit.Mvvm (ObservableObject, ObservableProperty), Microsoft.Win32 registry API, xUnit

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `Willow/Services/TweakLoaderService.cs` | Add `LoadTweaksFromFile(string)` public method; make `ApplyLocale` internal |
| Create | `Willow/Models/AppPermission.cs` | Observable model: AppKey, DisplayName, HasCamera, HasMic, HasLocation |
| Create | `Willow/Services/AppPermissionsService.cs` | Read/write per-app capability consent from registry; `internal static GetDisplayName` |
| Create | `Willow/ViewModels/PrivacyViewModel.cs` | ScoreLabel, ScoreBackground, AppPermissions; `internal static CalculateScore` |
| Create | `Willow/Views/PrivacyPage.xaml` | Score card + app permission list |
| Create | `Willow/Views/PrivacyPage.xaml.cs` | Code-behind; no logic |
| Modify | `Willow/MainWindow.xaml` | Add Privacy nav item between Disk Health and Optimizer |
| Modify | `Willow/MainWindow.xaml.cs` | Add `case "Privacy"` to navigation switch |
| Modify | `Willow/Strings/en-US/Resources.resw` | Add Privacy page string resources |
| Modify | `Willow/Strings/uk-UA/Resources.resw` | Add Ukrainian translations |
| Create | `Willow.Tests/AppPermissionsServiceTests.cs` | Unit tests for GetDisplayName |
| Create | `Willow.Tests/PrivacyViewModelTests.cs` | Unit tests for CalculateScore |

---

## Task 1: Extend TweakLoaderService

**Files:**
- Modify: `Willow/Services/TweakLoaderService.cs`

- [ ] **Step 1: Make `ApplyLocale` internal and add `LoadTweaksFromFile`**

  Open `Willow/Services/TweakLoaderService.cs`. Change `private void ApplyLocale` to `internal void ApplyLocale` (line ~57). Then add this method after `LoadTweaks()`:

  ```csharp
  public List<Tweak> LoadTweaksFromFile(string fileName)
  {
      var tweaks = new List<Tweak>();
      var tweaksFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tweaks");
      var filePath = Path.Combine(tweaksFolder, fileName);

      if (!File.Exists(filePath))
          return tweaks;

      var deserializer = new DeserializerBuilder()
          .WithNamingConvention(CamelCaseNamingConvention.Instance)
          .IgnoreUnmatchedProperties()
          .Build();

      int currentBuild = SystemVersionService.GetCurrentBuildNumber();

      try
      {
          var yaml = File.ReadAllText(filePath);
          var tweakFile = deserializer.Deserialize<TweakFile>(yaml);
          if (tweakFile?.Tweaks != null)
              foreach (var tweak in tweakFile.Tweaks)
                  if (PassesVersionFilter(tweak, currentBuild))
                      tweaks.Add(tweak);
      }
      catch { }

      var locale = new PreferencesService().LoadPreferences().Language;
      if (!string.IsNullOrEmpty(locale))
          ApplyLocale(tweaks, tweaksFolder, locale);

      return tweaks;
  }
  ```

- [ ] **Step 2: Build to verify compilation**

  Run: `dotnet build Willow\Willow.csproj`  
  Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

  ```bash
  git add Willow/Services/TweakLoaderService.cs
  git commit -m "feat: add TweakLoaderService.LoadTweaksFromFile for per-file loading"
  ```

---

## Task 2: Create AppPermission Model

**Files:**
- Create: `Willow/Models/AppPermission.cs`

- [ ] **Step 1: Create the model**

  Create `Willow/Models/AppPermission.cs`:

  ```csharp
  using CommunityToolkit.Mvvm.ComponentModel;

  namespace Willow.Models;

  public partial class AppPermission : ObservableObject
  {
      public string AppKey { get; init; } = string.Empty;
      public string DisplayName { get; init; } = string.Empty;

      [ObservableProperty]
      private bool hasCamera;

      [ObservableProperty]
      private bool hasMic;

      [ObservableProperty]
      private bool hasLocation;
  }
  ```

- [ ] **Step 2: Build to verify compilation**

  Run: `dotnet build Willow\Willow.csproj`  
  Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

  ```bash
  git add Willow/Models/AppPermission.cs
  git commit -m "feat: add AppPermission model"
  ```

---

## Task 3: Create AppPermissionsService with Tests

**Files:**
- Create: `Willow/Services/AppPermissionsService.cs`
- Create: `Willow.Tests/AppPermissionsServiceTests.cs`

- [ ] **Step 1: Write failing tests for GetDisplayName**

  Create `Willow.Tests/AppPermissionsServiceTests.cs`:

  ```csharp
  using Willow.Services;
  using Xunit;

  namespace Willow.Tests;

  public class AppPermissionsServiceTests
  {
      [Fact]
      public void GetDisplayName_UwpKey_StripsPublisherSuffix()
      {
          var result = AppPermissionsService.GetDisplayName("Microsoft.WindowsCamera_8wekyb3d8bbwe");
          Assert.Equal("Microsoft.WindowsCamera", result);
      }

      [Fact]
      public void GetDisplayName_UwpKeyNoUnderscore_ReturnsAsIs()
      {
          var result = AppPermissionsService.GetDisplayName("SomeApp");
          Assert.Equal("SomeApp", result);
      }

      [Fact]
      public void GetDisplayName_NonPackagedKey_ExtractsFilenameWithoutExtension()
      {
          var result = AppPermissionsService.GetDisplayName(@"NonPackaged\C:#Windows#System32#notepad.exe");
          Assert.Equal("notepad", result);
      }

      [Fact]
      public void GetDisplayName_NonPackagedNoExtension_ExtractsFilename()
      {
          var result = AppPermissionsService.GetDisplayName(@"NonPackaged\C:#Program Files#MyApp#myapp");
          Assert.Equal("myapp", result);
      }
  }
  ```

- [ ] **Step 2: Run tests to verify they fail to compile**

  Run: `dotnet test Willow.Tests\Willow.Tests.csproj --runtime win-x64`  
  Expected: Build error — `AppPermissionsService` does not exist yet.

- [ ] **Step 3: Create AppPermissionsService**

  Create `Willow/Services/AppPermissionsService.cs`:

  ```csharp
  using Microsoft.Win32;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using Willow.Models;

  namespace Willow.Services;

  public static class AppPermissionsService
  {
      private const string ConsentStoreBase =
          @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

      public static List<AppPermission> GetPermissions()
      {
          var camStates = ReadCapabilityStore($@"{ConsentStoreBase}\webcam");
          var micStates = ReadCapabilityStore($@"{ConsentStoreBase}\microphone");
          var locStates = ReadCapabilityStore($@"{ConsentStoreBase}\location");

          var allKeys = new HashSet<string>(camStates.Keys, StringComparer.OrdinalIgnoreCase);
          allKeys.UnionWith(micStates.Keys);
          allKeys.UnionWith(locStates.Keys);

          return allKeys
              .Select(key => new AppPermission
              {
                  AppKey      = key,
                  DisplayName = GetDisplayName(key),
                  HasCamera   = camStates.GetValueOrDefault(key, false),
                  HasMic      = micStates.GetValueOrDefault(key, false),
                  HasLocation = locStates.GetValueOrDefault(key, false),
              })
              .OrderBy(a => a.DisplayName)
              .ToList();
      }

      public static void SetPermission(string appKey, string capability, bool allow)
      {
          var path = $@"{ConsentStoreBase}\{capability}\{appKey}";
          RegistryService.WriteValue("CurrentUser", path, "Value",
              allow ? "Allow" : "Deny", "String");
      }

      internal static string GetDisplayName(string appKey)
      {
          // Win32 apps: "NonPackaged\C:#Windows#System32#notepad.exe"
          const string prefix = @"NonPackaged\";
          if (appKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
          {
              var encoded = appKey[prefix.Length..];
              var path = encoded.Replace('#', Path.DirectorySeparatorChar);
              var name = Path.GetFileNameWithoutExtension(path);
              return string.IsNullOrEmpty(name) ? appKey : name;
          }

          // UWP apps: "Microsoft.WindowsCamera_8wekyb3d8bbwe"
          var lastUnderscore = appKey.LastIndexOf('_');
          return lastUnderscore > 0 ? appKey[..lastUnderscore] : appKey;
      }

      private static Dictionary<string, bool> ReadCapabilityStore(string registryPath)
      {
          var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
          try
          {
              using var root = Registry.CurrentUser.OpenSubKey(registryPath);
              if (root == null) return result;

              foreach (var subKeyName in root.GetSubKeyNames())
              {
                  if (subKeyName.Equals("NonPackaged", StringComparison.OrdinalIgnoreCase))
                  {
                      using var nonPkg = root.OpenSubKey("NonPackaged");
                      if (nonPkg == null) continue;
                      foreach (var win32Key in nonPkg.GetSubKeyNames())
                      {
                          var appKey = $@"NonPackaged\{win32Key}";
                          var allowed = IsAllowed(nonPkg, win32Key);
                          if (allowed.HasValue)
                              result[appKey] = allowed.Value;
                      }
                  }
                  else
                  {
                      var allowed = IsAllowed(root, subKeyName);
                      if (allowed.HasValue)
                          result[subKeyName] = allowed.Value;
                  }
              }
          }
          catch { }
          return result;
      }

      private static bool? IsAllowed(RegistryKey parent, string subKeyName)
      {
          try
          {
              using var key = parent.OpenSubKey(subKeyName);
              var val = key?.GetValue("Value") as string;
              if (val == null) return null;
              return val.Equals("Allow", StringComparison.OrdinalIgnoreCase);
          }
          catch { return null; }
      }
  }
  ```

- [ ] **Step 4: Run tests to verify they pass**

  Run: `dotnet test Willow.Tests\Willow.Tests.csproj --runtime win-x64`  
  Expected: All tests pass including the 4 new `AppPermissionsServiceTests`.

- [ ] **Step 5: Commit**

  ```bash
  git add Willow/Services/AppPermissionsService.cs Willow.Tests/AppPermissionsServiceTests.cs
  git commit -m "feat: add AppPermissionsService with registry read/write and GetDisplayName"
  ```

---

## Task 4: Create PrivacyViewModel with Tests

**Files:**
- Create: `Willow/ViewModels/PrivacyViewModel.cs`
- Create: `Willow.Tests/PrivacyViewModelTests.cs`

- [ ] **Step 1: Write failing tests for CalculateScore**

  Create `Willow.Tests/PrivacyViewModelTests.cs`:

  ```csharp
  using Willow.ViewModels;
  using Xunit;

  namespace Willow.Tests;

  public class PrivacyViewModelTests
  {
      [Theory]
      [InlineData(0, 4, PrivacyScore.Low)]
      [InlineData(1, 4, PrivacyScore.Fair)]
      [InlineData(2, 4, PrivacyScore.Fair)]
      [InlineData(3, 4, PrivacyScore.Good)]
      [InlineData(4, 4, PrivacyScore.Good)]
      [InlineData(0, 0, PrivacyScore.Low)]
      public void CalculateScore_ReturnsExpectedTier(int applied, int total, PrivacyScore expected)
      {
          Assert.Equal(expected, PrivacyViewModel.CalculateScore(applied, total));
      }
  }
  ```

- [ ] **Step 2: Run tests to verify they fail to compile**

  Run: `dotnet test Willow.Tests\Willow.Tests.csproj --runtime win-x64`  
  Expected: Build error — `PrivacyViewModel` and `PrivacyScore` do not exist yet.

- [ ] **Step 3: Create PrivacyViewModel**

  Create `Willow/ViewModels/PrivacyViewModel.cs`:

  ```csharp
  using CommunityToolkit.Mvvm.ComponentModel;
  using Microsoft.UI.Xaml;
  using Microsoft.UI.Xaml.Media;
  using Microsoft.Windows.ApplicationModel.Resources;
  using System.Collections.ObjectModel;
  using System.ComponentModel;
  using System.Linq;
  using Windows.UI;
  using Willow.Models;
  using Willow.Services;

  namespace Willow.ViewModels;

  internal enum PrivacyScore { Low, Fair, Good }

  public partial class PrivacyViewModel : ObservableObject
  {
      private bool _applying;

      [ObservableProperty]
      private string scoreLabel = string.Empty;

      [ObservableProperty]
      private SolidColorBrush scoreBackground =
          new(Color.FromArgb(255, 136, 136, 136));

      [ObservableProperty]
      private Visibility emptyStateVisibility = Visibility.Collapsed;

      public ObservableCollection<AppPermission> AppPermissions { get; } = new();

      public PrivacyViewModel()
      {
          var res = new ResourceLoader();
          LoadScore(res);
          LoadPermissions();
      }

      private void LoadScore(ResourceLoader res)
      {
          var tweaks = new TweakLoaderService().LoadTweaksFromFile("privacy.yaml");
          var prefs = new PreferencesService().LoadPreferences();
          var applied = tweaks.Count(t => prefs.OldRegistryData.ContainsKey(t.Id));

          var score = CalculateScore(applied, tweaks.Count);
          ScoreLabel = LabelForScore(score, res);
          ScoreBackground = BrushForScore(score);
      }

      private void LoadPermissions()
      {
          foreach (var item in AppPermissions)
              item.PropertyChanged -= OnPermissionChanged;
          AppPermissions.Clear();

          foreach (var perm in AppPermissionsService.GetPermissions())
          {
              perm.PropertyChanged += OnPermissionChanged;
              AppPermissions.Add(perm);
          }

          EmptyStateVisibility = AppPermissions.Any()
              ? Visibility.Collapsed
              : Visibility.Visible;
      }

      private void OnPermissionChanged(object? sender, PropertyChangedEventArgs e)
      {
          if (_applying || sender is not AppPermission perm) return;

          var capability = e.PropertyName switch
          {
              nameof(AppPermission.HasCamera)   => "webcam",
              nameof(AppPermission.HasMic)      => "microphone",
              nameof(AppPermission.HasLocation) => "location",
              _ => null
          };
          if (capability == null) return;

          var allow = e.PropertyName switch
          {
              nameof(AppPermission.HasCamera)   => perm.HasCamera,
              nameof(AppPermission.HasMic)      => perm.HasMic,
              _ => perm.HasLocation
          };

          _applying = true;
          try { AppPermissionsService.SetPermission(perm.AppKey, capability, allow); }
          finally { _applying = false; }
      }

      internal static PrivacyScore CalculateScore(int applied, int total)
      {
          if (total == 0) return PrivacyScore.Low;
          var ratio = (double)applied / total;
          if (ratio >= 0.75) return PrivacyScore.Good;
          if (ratio >= 0.25) return PrivacyScore.Fair;
          return PrivacyScore.Low;
      }

      private static string LabelForScore(PrivacyScore score, ResourceLoader res) => score switch
      {
          PrivacyScore.Good => res.GetString("PrivacyPage_ScoreGood"),
          PrivacyScore.Fair => res.GetString("PrivacyPage_ScoreFair"),
          _                 => res.GetString("PrivacyPage_ScoreLow"),
      };

      internal static SolidColorBrush BrushForScore(PrivacyScore score) => score switch
      {
          PrivacyScore.Good => new SolidColorBrush(Color.FromArgb(255, 22,  198,  12)),
          PrivacyScore.Fair => new SolidColorBrush(Color.FromArgb(255, 202,  80,  16)),
          _                 => new SolidColorBrush(Color.FromArgb(255, 196,  43,  28)),
      };
  }
  ```

- [ ] **Step 4: Run tests to verify they pass**

  Run: `dotnet test Willow.Tests\Willow.Tests.csproj --runtime win-x64`  
  Expected: All tests pass including the 6 new `PrivacyViewModelTests`.

- [ ] **Step 5: Commit**

  ```bash
  git add Willow/ViewModels/PrivacyViewModel.cs Willow.Tests/PrivacyViewModelTests.cs
  git commit -m "feat: add PrivacyViewModel with score calculation and permission write-back"
  ```

---

## Task 5: Add Localization Strings

**Files:**
- Modify: `Willow/Strings/en-US/Resources.resw`
- Modify: `Willow/Strings/uk-UA/Resources.resw`

- [ ] **Step 1: Add en-US strings**

  Open `Willow/Strings/en-US/Resources.resw`. Before the closing `</root>` tag, add:

  ```xml
  <data name="Nav_Privacy.Content" xml:space="preserve">
    <value>Privacy</value>
  </data>
  <data name="PrivacyPage_Title.Text" xml:space="preserve">
    <value>Privacy</value>
  </data>
  <data name="PrivacyPage_ScoreSubtitle.Text" xml:space="preserve">
    <value>Based on applied privacy tweaks</value>
  </data>
  <data name="PrivacyPage_ScoreLow" xml:space="preserve">
    <value>Low</value>
  </data>
  <data name="PrivacyPage_ScoreFair" xml:space="preserve">
    <value>Fair</value>
  </data>
  <data name="PrivacyPage_ScoreGood" xml:space="preserve">
    <value>Good</value>
  </data>
  <data name="PrivacyPage_AppPermissionsHeader.Text" xml:space="preserve">
    <value>App Permissions</value>
  </data>
  <data name="PrivacyPage_CameraLabel.Text" xml:space="preserve">
    <value>Camera</value>
  </data>
  <data name="PrivacyPage_MicLabel.Text" xml:space="preserve">
    <value>Microphone</value>
  </data>
  <data name="PrivacyPage_LocationLabel.Text" xml:space="preserve">
    <value>Location</value>
  </data>
  <data name="PrivacyPage_EmptyState.Text" xml:space="preserve">
    <value>No apps have requested access</value>
  </data>
  ```

- [ ] **Step 2: Add uk-UA strings**

  Open `Willow/Strings/uk-UA/Resources.resw`. Before the closing `</root>` tag, add:

  ```xml
  <data name="Nav_Privacy.Content" xml:space="preserve">
    <value>Приватність</value>
  </data>
  <data name="PrivacyPage_Title.Text" xml:space="preserve">
    <value>Приватність</value>
  </data>
  <data name="PrivacyPage_ScoreSubtitle.Text" xml:space="preserve">
    <value>На основі застосованих твіків конфіденційності</value>
  </data>
  <data name="PrivacyPage_ScoreLow" xml:space="preserve">
    <value>Низький</value>
  </data>
  <data name="PrivacyPage_ScoreFair" xml:space="preserve">
    <value>Середній</value>
  </data>
  <data name="PrivacyPage_ScoreGood" xml:space="preserve">
    <value>Хороший</value>
  </data>
  <data name="PrivacyPage_AppPermissionsHeader.Text" xml:space="preserve">
    <value>Доступ застосунків</value>
  </data>
  <data name="PrivacyPage_CameraLabel.Text" xml:space="preserve">
    <value>Камера</value>
  </data>
  <data name="PrivacyPage_MicLabel.Text" xml:space="preserve">
    <value>Мікрофон</value>
  </data>
  <data name="PrivacyPage_LocationLabel.Text" xml:space="preserve">
    <value>Геолокація</value>
  </data>
  <data name="PrivacyPage_EmptyState.Text" xml:space="preserve">
    <value>Жоден застосунок не запитував доступ</value>
  </data>
  ```

- [ ] **Step 3: Build to verify no resource errors**

  Run: `dotnet build Willow\Willow.csproj`  
  Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

  ```bash
  git add Willow/Strings/en-US/Resources.resw Willow/Strings/uk-UA/Resources.resw
  git commit -m "feat: add Privacy page localization strings (en-US and uk-UA)"
  ```

---

## Task 6: Wire Up Navigation

**Files:**
- Modify: `Willow/MainWindow.xaml`
- Modify: `Willow/MainWindow.xaml.cs`

- [ ] **Step 1: Add Privacy nav item to MainWindow.xaml**

  In `Willow/MainWindow.xaml`, find the `Nav_DiskHealth` item and add the Privacy item immediately after it (before `Nav_Optimizer`):

  ```xml
  <NavigationViewItem x:Uid="Nav_DiskHealth" Tag="DiskHealth">
      <NavigationViewItem.Icon>
          <FontIcon Glyph="&#xE95E;" />
      </NavigationViewItem.Icon>
  </NavigationViewItem>
  <NavigationViewItem x:Uid="Nav_Privacy" Tag="Privacy">
      <NavigationViewItem.Icon>
          <FontIcon Glyph="&#xE72E;" />
      </NavigationViewItem.Icon>
  </NavigationViewItem>
  <NavigationViewItem x:Uid="Nav_Optimizer" Tag="Optimizer" Icon="Repair" />
  ```

  The glyph `&#xE72E;` is the Lock icon in Segoe MDL2 Assets.

- [ ] **Step 2: Add Privacy case to MainWindow.xaml.cs**

  In `Willow/MainWindow.xaml.cs`, inside `NavView_SelectionChanged`, add a new case to the switch:

  ```csharp
  case "Privacy":
      ContentFrame.Navigate(typeof(PrivacyPage));
      break;
  ```

  Also add the using if not already present (the `PrivacyPage` class is in `Willow.Views` which is already referenced by the existing using pattern).

  Do not build yet — `PrivacyPage` doesn't exist until Task 7 so the build will fail. The commit for these changes is bundled into Task 7 Step 5.

---

## Task 7: Create PrivacyPage View

**Files:**
- Create: `Willow/Views/PrivacyPage.xaml`
- Create: `Willow/Views/PrivacyPage.xaml.cs`

- [ ] **Step 1: Create PrivacyPage.xaml.cs**

  Create `Willow/Views/PrivacyPage.xaml.cs`:

  ```csharp
  using Microsoft.UI.Xaml.Controls;
  using Willow.ViewModels;

  namespace Willow.Views;

  public sealed partial class PrivacyPage : Page
  {
      public PrivacyViewModel ViewModel { get; } = new();

      public PrivacyPage()
      {
          this.InitializeComponent();
      }
  }
  ```

- [ ] **Step 2: Create PrivacyPage.xaml**

  Create `Willow/Views/PrivacyPage.xaml`:

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <Page
      x:Class="Willow.Views.PrivacyPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
      xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
      xmlns:models="using:Willow.Models"
      mc:Ignorable="d">

      <Grid Padding="24">
          <Grid.RowDefinitions>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="*"/>
          </Grid.RowDefinitions>

          <!-- Title -->
          <TextBlock x:Uid="PrivacyPage_Title"
                     Grid.Row="0"
                     Style="{StaticResource TitleTextBlockStyle}"
                     Margin="0,0,0,20"/>

          <!-- Score card -->
          <Border Grid.Row="1"
                  Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                  BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                  BorderThickness="1"
                  CornerRadius="8"
                  Padding="16,12"
                  Margin="0,0,0,20">
              <StackPanel Spacing="6">
                  <Border Background="{x:Bind ViewModel.ScoreBackground, Mode=OneWay}"
                          CornerRadius="4"
                          Padding="8,3"
                          HorizontalAlignment="Left">
                      <TextBlock Text="{x:Bind ViewModel.ScoreLabel, Mode=OneWay}"
                                 FontWeight="SemiBold"
                                 FontSize="13"
                                 Foreground="{ThemeResource TextOnAccentFillColorPrimaryBrush}"/>
                  </Border>
                  <TextBlock x:Uid="PrivacyPage_ScoreSubtitle"
                             FontSize="12"
                             Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
              </StackPanel>
          </Border>

          <!-- App Permissions header -->
          <TextBlock x:Uid="PrivacyPage_AppPermissionsHeader"
                     Grid.Row="2"
                     Style="{StaticResource SubtitleTextBlockStyle}"
                     Opacity="0.6"
                     Margin="0,0,0,12"/>

          <!-- App list / empty state -->
          <Grid Grid.Row="3">
              <ScrollViewer>
                  <ItemsRepeater ItemsSource="{x:Bind ViewModel.AppPermissions, Mode=OneWay}">
                      <ItemsRepeater.Layout>
                          <StackLayout Spacing="8"/>
                      </ItemsRepeater.Layout>
                      <ItemsRepeater.ItemTemplate>
                          <DataTemplate x:DataType="models:AppPermission">
                              <Border BorderThickness="1"
                                      BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                                      Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                                      Padding="16,12"
                                      CornerRadius="8">
                                  <Grid ColumnSpacing="16">
                                      <Grid.ColumnDefinitions>
                                          <ColumnDefinition Width="*"/>
                                          <ColumnDefinition Width="Auto"/>
                                          <ColumnDefinition Width="Auto"/>
                                          <ColumnDefinition Width="Auto"/>
                                      </Grid.ColumnDefinitions>

                                      <TextBlock Grid.Column="0"
                                                 Text="{x:Bind DisplayName}"
                                                 VerticalAlignment="Center"
                                                 FontWeight="SemiBold"
                                                 TextTrimming="CharacterEllipsis"/>

                                      <!-- Camera -->
                                      <CheckBox Grid.Column="1"
                                                IsChecked="{x:Bind HasCamera, Mode=TwoWay}"
                                                VerticalAlignment="Center">
                                          <StackPanel Orientation="Horizontal" Spacing="6">
                                              <FontIcon Glyph="&#xE722;" FontSize="14" VerticalAlignment="Center"/>
                                              <TextBlock x:Uid="PrivacyPage_CameraLabel" VerticalAlignment="Center"/>
                                          </StackPanel>
                                      </CheckBox>

                                      <!-- Microphone -->
                                      <CheckBox Grid.Column="2"
                                                IsChecked="{x:Bind HasMic, Mode=TwoWay}"
                                                VerticalAlignment="Center">
                                          <StackPanel Orientation="Horizontal" Spacing="6">
                                              <FontIcon Glyph="&#xE720;" FontSize="14" VerticalAlignment="Center"/>
                                              <TextBlock x:Uid="PrivacyPage_MicLabel" VerticalAlignment="Center"/>
                                          </StackPanel>
                                      </CheckBox>

                                      <!-- Location -->
                                      <CheckBox Grid.Column="3"
                                                IsChecked="{x:Bind HasLocation, Mode=TwoWay}"
                                                VerticalAlignment="Center">
                                          <StackPanel Orientation="Horizontal" Spacing="6">
                                              <FontIcon Glyph="&#xE81D;" FontSize="14" VerticalAlignment="Center"/>
                                              <TextBlock x:Uid="PrivacyPage_LocationLabel" VerticalAlignment="Center"/>
                                          </StackPanel>
                                      </CheckBox>
                                  </Grid>
                              </Border>
                          </DataTemplate>
                      </ItemsRepeater.ItemTemplate>
                  </ItemsRepeater>
              </ScrollViewer>

              <TextBlock x:Uid="PrivacyPage_EmptyState"
                         HorizontalAlignment="Center"
                         VerticalAlignment="Center"
                         Opacity="0.5"
                         Visibility="{x:Bind ViewModel.EmptyStateVisibility, Mode=OneWay}"/>
          </Grid>
      </Grid>
  </Page>
  ```

- [ ] **Step 3: Build to verify**

  Run: `dotnet build Willow\Willow.csproj`  
  Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all tests**

  Run: `dotnet test Willow.Tests\Willow.Tests.csproj --runtime win-x64`  
  Expected: All tests pass.

- [ ] **Step 5: Commit everything (including Task 6 nav changes)**

  ```bash
  git add Willow/Views/PrivacyPage.xaml Willow/Views/PrivacyPage.xaml.cs Willow/MainWindow.xaml Willow/MainWindow.xaml.cs
  git commit -m "feat: add Privacy page with score card and per-app permission controls"
  ```
