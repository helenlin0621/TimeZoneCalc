# 時區計算器 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 做一個 WinUI 3 的 Windows 桌面時區計算器：在任一時區列輸入日期時間，其他列即時換算；免安裝，可帶到任何 Win10 1809+/Win11 電腦執行。

**Architecture:** 所有邏輯（時區、換算、數字填入、整體狀態）放在不引用 UI 的 `TimeZoneCalc.Core` 類別庫，用 xUnit 完整測試；`TimeZoneCalc.App`（WinUI 3）只把 `CalculatorState` 畫出來並把按鍵轉給它。發佈為 unpackaged + 自帶 .NET 與 Windows App SDK 的資料夾。

**Tech Stack:** C#、.NET 9 SDK（本機已有 9.0.318）、Windows App SDK（WinUI 3）、xUnit。

**Spec:** `docs/superpowers/specs/2026-09-24-timezone-calculator-design.md`

## Global Constraints

- 工作目錄一律是 repo 根目錄 `TimeZoneCalc`；指令寫法同時可在 cmd.exe 與 PowerShell 執行，標明「PowerShell」的除外。
- 開發機沒有管理者權限：不得使用需要系統安裝或提權的步驟。
- App：`TargetFramework` = `net9.0-windows10.0.19041.0`，`TargetPlatformMinVersion` = `10.0.17763.0`，`RuntimeIdentifier` = `win-x64`，`WindowsPackageType=None`，`WindowsAppSDKSelfContained=true`，`SelfContained=true`。
- Core 與測試：`net9.0`，不得引用任何 WinUI／Windows 專屬 API。
- 不做單一 exe、不做 MSIX、不開 trimming。
- 時間精度到秒，24 小時制；複製格式 `yyyy-MM-dd HH:mm:ss`。
- 年份範圍 1900–2100。
- 預設 2 列（UTC / 台北），列數 1–5，新增列預設 UTC，開啟時輸入列為 UTC、時間為現在，不記住設定。
- 台北城市時區 Windows ID `Taipei Standard Time`，顯示名稱 `台北 (Asia/Taipei)`。
- 含中文的 `.cs`／`.xaml` 以 UTF-8 存檔；`.bat`／`.cmd`／`.ps1` 保持純 ASCII。
- 每個 commit 訊息結尾加 `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`（以第二個 `-m` 傳入）。

## Review Focus

1. 切換輸入列到一個「顯示值剛好是重複時間的第二次出現」的時區（例：UTC 2026-11-01 06:30 → 紐約 01:30 EST）——預期瞬間不變，其他列數值不跳動。→ Task 6 `Selecting_row_showing_second_occurrence_keeps_instant`
2. 改時區：改輸入列的時區時保留打好的牆上時間；改其他列的時區時保留同一瞬間。→ Task 6 `Changing_active_row_zone_keeps_typed_wall_time`、`Changing_other_row_zone_keeps_instant`
3. 換算結果跨出 1900–2100（例：UTC 2100-12-31 23:00 → 台北 2101-01-01）後點那一列——預期顯示日期無效，程式不崩潰。→ Task 5 `Loaded_out_of_range_date_displays_but_is_null`、Task 6 `Selecting_row_whose_date_is_past_2100_reports_invalid_date`
4. 日期打到一半（或打錯）就點別列——預期載入上一個有效瞬間，而不是卡在無效狀態。→ Task 6 `Selecting_row_while_date_incomplete_loads_last_valid_instant`
5. 刪除輸入列本身或排在它前面的列——預期輸入列仍指向正確的那一列（或回到第一列並保留瞬間）。→ Task 6 `Removing_active_row_activates_first_row_with_same_instant`、`Removing_row_before_active_keeps_same_active_row`

---

## File Structure

```
TimeZoneCalc/
  .gitignore
  README.md                                   Task 9
  build/publish.cmd                           Task 9  發佈 + 壓 zip（cmd.exe）
  tools/capture-window.ps1                    Task 7  擷取視窗畫面做目視驗證
  docs/manual-test-checklist.md               Task 9
  src/TimeZoneCalc.Core/
    TimeZoneCalc.Core.csproj                  Task 2
    TimeFormat.cs                             Task 2  牆上時間與偏移量的字串格式
    ZoneOffsetInfo.cs                         Task 2  偏移量 + 是否夏令
    ZoneOption.cs                             Task 2  一個可選時區（城市 or 固定偏移）
    Converter.cs                              Task 3  牆上時間 ↔ 瞬間
    ZoneCatalog.cs                            Task 4  時區清單與篩選
    DigitEntry.cs                             Task 5  由左往右填入的數字狀態機
    CalculatorState.cs                        Task 6  整個計算器的狀態（列、輸入列、狀態）
  tests/TimeZoneCalc.Core.Tests/
    TimeZoneCalc.Core.Tests.csproj            Task 2
    TestZones.cs                              Task 2
    TimeFormatTests.cs / ZoneOptionTests.cs   Task 2
    ConverterTests.cs                         Task 3
    ZoneCatalogTests.cs                       Task 4
    DigitEntryTests.cs                        Task 5
    CalculatorStateTests.cs                   Task 6
  src/TimeZoneCalc.App/
    TimeZoneCalc.App.csproj                   Task 1
    app.manifest                              Task 1
    App.xaml / App.xaml.cs                    Task 1
    MainWindow.xaml / MainWindow.xaml.cs      Task 1 骨架 → Task 7 完整 → Task 8 鍵盤
    RowView.cs                                Task 7  一列時區的 UI
    Theme.cs                                  Task 7  取主題筆刷
```

---

### Task 1: WinUI 3 骨架可建置、可發佈、可在別台電腦執行（風險驗證）

**Files:**
- Create: `.gitignore`
- Create: `src/TimeZoneCalc.App/TimeZoneCalc.App.csproj`
- Create: `src/TimeZoneCalc.App/app.manifest`
- Create: `src/TimeZoneCalc.App/App.xaml`、`App.xaml.cs`
- Create: `src/TimeZoneCalc.App/MainWindow.xaml`、`MainWindow.xaml.cs`

**Interfaces:**
- Consumes: 無
- Produces: 可建置的 WinUI 3 專案；namespace `TimeZoneCalc`；類別 `TimeZoneCalc.App`、`TimeZoneCalc.MainWindow`；建置指令 `dotnet build src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -p:Platform=x64`

- [ ] **Step 1: 建立 `.gitignore`**

```
bin/
obj/
publish/
.vs/
*.user
```

- [ ] **Step 2: 建立 `src/TimeZoneCalc.App/TimeZoneCalc.App.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>TimeZoneCalc</RootNamespace>
    <AssemblyName>TimeZoneCalc</AssemblyName>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>false</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: 加入 NuGet 套件（取最新穩定版）**

```
dotnet add src\TimeZoneCalc.App\TimeZoneCalc.App.csproj package Microsoft.WindowsAppSDK
dotnet add src\TimeZoneCalc.App\TimeZoneCalc.App.csproj package Microsoft.Windows.SDK.BuildTools
```

記下 csproj 裡被寫入的兩個版本號，Task 9 的 README 會用到。

- [ ] **Step 4: 建立 `src/TimeZoneCalc.App/app.manifest`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="TimeZoneCalc.app"/>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2, PerMonitor</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 5: 建立 `App.xaml` 與 `App.xaml.cs`**

`src/TimeZoneCalc.App/App.xaml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Application
    x:Class="TimeZoneCalc.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

`src/TimeZoneCalc.App/App.xaml.cs`：

```csharp
using Microsoft.UI.Xaml;

namespace TimeZoneCalc;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        // WinUI 崩潰時不會有任何畫面，留一份紀錄方便在別台電腦上查
        UnhandledException += (_, e) =>
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "TimeZoneCalc-crash.txt"), e.Exception.ToString());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
```

- [ ] **Step 6: 建立骨架 `MainWindow`**

`src/TimeZoneCalc.App/MainWindow.xaml`：

```xml
<?xml version="1.0" encoding="utf-8"?>
<Window
    x:Class="TimeZoneCalc.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="時區計算器">
    <Grid>
        <TextBlock Text="時區計算器 骨架" HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="28" />
    </Grid>
</Window>
```

`src/TimeZoneCalc.App/MainWindow.xaml.cs`：

```csharp
using Microsoft.UI.Xaml;

namespace TimeZoneCalc;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 7: 建置**

Run: `dotnet build src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -p:Platform=x64`
Expected: `建置成功` / `Build succeeded`，0 個錯誤。

若出現與 `Microsoft.Windows.SDK.NET.Ref` 或 CsWinRT 版本相關的錯誤，在 csproj 的 `PropertyGroup` 加上 `<WindowsSdkPackageVersion>10.0.19041.57</WindowsSdkPackageVersion>` 後重建。若最新版 Windows App SDK 本身無法以指令列建置，改用 `dotnet add src\TimeZoneCalc.App\TimeZoneCalc.App.csproj package Microsoft.WindowsAppSDK --version 1.8.*` 鎖定 1.8 系列重試，並把原因記進 commit 訊息。

- [ ] **Step 8: 發佈並確認能啟動（PowerShell）**

```powershell
dotnet publish src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -c Release -p:Platform=x64 -o publish\TimeZoneCalc
$p = Start-Process -FilePath .\publish\TimeZoneCalc\TimeZoneCalc.exe -PassThru; Start-Sleep -Seconds 5; $p.HasExited; Stop-Process -Id $p.Id
```

Expected: `False`（啟動 5 秒後仍在執行）。若是 `True`，看 `%TEMP%\TimeZoneCalc-crash.txt`。

- [ ] **Step 9: 量大小並壓成 zip（PowerShell）**

```powershell
"{0:N0} MB" -f ((Get-ChildItem publish\TimeZoneCalc -Recurse | Measure-Object Length -Sum).Sum / 1MB)
Compress-Archive -Path publish\TimeZoneCalc -DestinationPath publish\TimeZoneCalc-win-x64.zip -Force
```

記下資料夾大小。

- [ ] **Step 10: 關卡——在乾淨電腦試跑（需使用者）**

請使用者把 `publish\TimeZoneCalc-win-x64.zip` 複製到一台沒有裝 .NET SDK／Visual Studio 的 Win10 或 Win11 電腦，解壓後雙擊 `TimeZoneCalc.exe`，確認出現「時區計算器 骨架」視窗。**等使用者回報結果。** 失敗就停下來排查，不進 Task 2；使用者表示目前沒有別台電腦時，記在 commit 訊息裡並繼續，Task 9 再驗一次。

- [ ] **Step 11: Commit**

```
git add .gitignore src\TimeZoneCalc.App
git commit -m "feat: WinUI 3 unpackaged self-contained skeleton" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Core 專案、測試專案、時區基本型別

**Files:**
- Create: `src/TimeZoneCalc.Core/TimeZoneCalc.Core.csproj`
- Create: `src/TimeZoneCalc.Core/TimeFormat.cs`、`ZoneOffsetInfo.cs`、`ZoneOption.cs`
- Create: `tests/TimeZoneCalc.Core.Tests/`（xUnit 範本）、`TestZones.cs`、`TimeFormatTests.cs`、`ZoneOptionTests.cs`

**Interfaces:**
- Consumes: 無
- Produces:
  - `static class TimeFormat { string Wall(DateTime value); string Offset(TimeSpan offset); }` → `"2026-09-24 14:32:05"`、`"UTC+08:00"`
  - `readonly record struct ZoneOffsetInfo(TimeSpan Offset, bool IsDaylight) { string Label }` → `"UTC-04:00 夏令"` / `"UTC+08:00"`
  - `sealed class ZoneOption { string Id; string DisplayName; TimeZoneInfo? City; TimeSpan FixedOffset; bool IsFixed; bool IsFallback; static ZoneOption FromCity(TimeZoneInfo tz, string? displayName = null); static ZoneOption FromOffset(TimeSpan offset, string? displayName = null, string? id = null, bool isFallback = false); ZoneOffsetInfo GetOffset(DateTimeOffset instant); }`
  - 測試輔助 `TestZones.Utc / Taipei / NewYork / Kathmandu`、`TestZones.Wall(y, mo, d, h, mi, s = 0)`

- [ ] **Step 1: 建立兩個專案**

```
dotnet new classlib -o src\TimeZoneCalc.Core -n TimeZoneCalc.Core -f net9.0
dotnet new xunit -o tests\TimeZoneCalc.Core.Tests -n TimeZoneCalc.Core.Tests -f net9.0
dotnet add tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj reference src\TimeZoneCalc.Core\TimeZoneCalc.Core.csproj
del src\TimeZoneCalc.Core\Class1.cs
del tests\TimeZoneCalc.Core.Tests\UnitTest1.cs
```

打開 `src/TimeZoneCalc.Core/TimeZoneCalc.Core.csproj`，確認有 `<Nullable>enable</Nullable>` 與 `<ImplicitUsings>enable</ImplicitUsings>`（範本預設有）。

- [ ] **Step 2: 寫失敗的測試**

`tests/TimeZoneCalc.Core.Tests/TestZones.cs`：

```csharp
using TimeZoneCalc.Core;

namespace TimeZoneCalc.Core.Tests;

internal static class TestZones
{
    public static readonly ZoneOption Utc = ZoneOption.FromOffset(TimeSpan.Zero, "UTC", "UTC");
    public static readonly ZoneOption Taipei =
        ZoneOption.FromCity(TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"), "台北 (Asia/Taipei)");
    public static readonly ZoneOption NewYork =
        ZoneOption.FromCity(TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
    public static readonly ZoneOption Kathmandu = ZoneOption.FromOffset(new TimeSpan(5, 45, 0));

    public static DateTime Wall(int y, int mo, int d, int h, int mi, int s = 0) =>
        new(y, mo, d, h, mi, s, DateTimeKind.Unspecified);
}
```

`tests/TimeZoneCalc.Core.Tests/TimeFormatTests.cs`：

```csharp
using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class TimeFormatTests
{
    [Fact]
    public void Wall_formats_as_yyyy_MM_dd_HH_mm_ss() =>
        Assert.Equal("2026-09-24 14:32:05", TimeFormat.Wall(new DateTime(2026, 9, 24, 14, 32, 5)));

    [Theory]
    [InlineData(8, 0, "UTC+08:00")]
    [InlineData(-4, 0, "UTC-04:00")]
    [InlineData(0, 0, "UTC+00:00")]
    [InlineData(5, 45, "UTC+05:45")]
    [InlineData(-9, -30, "UTC-09:30")]
    public void Offset_formats_sign_hours_minutes(int hours, int minutes, string expected) =>
        Assert.Equal(expected, TimeFormat.Offset(new TimeSpan(hours, minutes, 0)));
}
```

`tests/TimeZoneCalc.Core.Tests/ZoneOptionTests.cs`：

```csharp
using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class ZoneOptionTests
{
    [Fact]
    public void Fixed_offset_never_changes_and_is_not_daylight()
    {
        var info = TestZones.Kathmandu.GetOffset(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new ZoneOffsetInfo(new TimeSpan(5, 45, 0), false), info);
        Assert.Equal("UTC+05:45", TestZones.Kathmandu.DisplayName);
        Assert.Equal("fixed:UTC+05:45", TestZones.Kathmandu.Id);
        Assert.True(TestZones.Kathmandu.IsFixed);
    }

    [Fact]
    public void City_offset_follows_daylight_saving()
    {
        var summer = TestZones.NewYork.GetOffset(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var winter = TestZones.NewYork.GetOffset(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(new ZoneOffsetInfo(TimeSpan.FromHours(-4), true), summer);
        Assert.Equal(new ZoneOffsetInfo(TimeSpan.FromHours(-5), false), winter);
        Assert.Equal("UTC-04:00 夏令", summer.Label);
        Assert.Equal("UTC-05:00", winter.Label);
        Assert.False(TestZones.NewYork.IsFixed);
    }
}
```

- [ ] **Step 3: 跑測試確認失敗**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj`
Expected: 編譯失敗，`找不到類型或命名空間名稱 'ZoneOption'`（或英文 `The type or namespace name 'ZoneOption' could not be found`）。

- [ ] **Step 4: 實作**

`src/TimeZoneCalc.Core/TimeFormat.cs`：

```csharp
using System.Globalization;

namespace TimeZoneCalc.Core;

public static class TimeFormat
{
    public static string Wall(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Offset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return $"UTC{sign}{abs.Hours:00}:{abs.Minutes:00}";
    }
}
```

`src/TimeZoneCalc.Core/ZoneOffsetInfo.cs`：

```csharp
namespace TimeZoneCalc.Core;

public readonly record struct ZoneOffsetInfo(TimeSpan Offset, bool IsDaylight)
{
    public string Label => TimeFormat.Offset(Offset) + (IsDaylight ? " 夏令" : "");
}
```

`src/TimeZoneCalc.Core/ZoneOption.cs`：

```csharp
namespace TimeZoneCalc.Core;

// 一個可選的時區：城市（依日期套用夏令時間）或固定偏移量
public sealed class ZoneOption
{
    private ZoneOption(string id, string displayName, TimeZoneInfo? city, TimeSpan fixedOffset, bool isFallback)
    {
        Id = id;
        DisplayName = displayName;
        City = city;
        FixedOffset = fixedOffset;
        IsFallback = isFallback;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public TimeZoneInfo? City { get; }
    public TimeSpan FixedOffset { get; }
    public bool IsFixed => City is null;
    public bool IsFallback { get; }

    public static ZoneOption FromCity(TimeZoneInfo tz, string? displayName = null) =>
        new(tz.Id, displayName ?? tz.DisplayName, tz, TimeSpan.Zero, false);

    public static ZoneOption FromOffset(TimeSpan offset, string? displayName = null, string? id = null, bool isFallback = false) =>
        new(id ?? "fixed:" + TimeFormat.Offset(offset), displayName ?? TimeFormat.Offset(offset), null, offset, isFallback);

    public ZoneOffsetInfo GetOffset(DateTimeOffset instant) => City is null
        ? new ZoneOffsetInfo(FixedOffset, false)
        : new ZoneOffsetInfo(City.GetUtcOffset(instant), City.IsDaylightSavingTime(instant));

    public override string ToString() => DisplayName;
}
```

- [ ] **Step 5: 跑測試確認通過**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj`
Expected: 全部通過（7 個測試）。

- [ ] **Step 6: Commit**

```
git add src\TimeZoneCalc.Core tests\TimeZoneCalc.Core.Tests
git commit -m "feat(core): zone option, offset info and formatting" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: 換算核心 Converter

**Files:**
- Create: `src/TimeZoneCalc.Core/Converter.cs`
- Test: `tests/TimeZoneCalc.Core.Tests/ConverterTests.cs`

**Interfaces:**
- Consumes: `ZoneOption`、`ZoneOffsetInfo`（Task 2）
- Produces:
  - `enum AmbiguityChoice { First, Second }`
  - `enum ResolveKind { Ok, Invalid, Ambiguous }`
  - `readonly record struct ResolveResult(ResolveKind Kind, DateTimeOffset Instant)`；`ResolveResult.Invalid`
  - `readonly record struct ZonedTime(DateTime Wall, ZoneOffsetInfo Offset)`
  - `static class Converter { ResolveResult Resolve(ZoneOption zone, DateTime wall, AmbiguityChoice choice); ZonedTime ToZone(DateTimeOffset instant, ZoneOption zone); AmbiguityChoice ChoiceFor(DateTimeOffset instant, ZoneOption zone); }`

- [ ] **Step 1: 寫失敗的測試**

`tests/TimeZoneCalc.Core.Tests/ConverterTests.cs`：

```csharp
using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class ConverterTests
{
    private static DateTimeOffset Utc(int y, int mo, int d, int h, int mi, int s = 0) =>
        new(y, mo, d, h, mi, s, TimeSpan.Zero);

    [Fact]
    public void Utc_to_taipei_adds_eight_hours()
    {
        var r = Converter.Resolve(TestZones.Utc, TestZones.Wall(2026, 9, 24, 6, 32, 5), AmbiguityChoice.First);
        Assert.Equal(ResolveKind.Ok, r.Kind);
        var t = Converter.ToZone(r.Instant, TestZones.Taipei);
        Assert.Equal(TestZones.Wall(2026, 9, 24, 14, 32, 5), t.Wall);
        Assert.Equal(new ZoneOffsetInfo(TimeSpan.FromHours(8), false), t.Offset);
    }

    [Fact]
    public void Taipei_to_utc_subtracts_eight_hours_across_month()
    {
        var r = Converter.Resolve(TestZones.Taipei, TestZones.Wall(2026, 3, 1, 7, 0), AmbiguityChoice.First);
        Assert.Equal(ResolveKind.Ok, r.Kind);
        Assert.Equal(Utc(2026, 2, 28, 23, 0), r.Instant);
    }

    [Fact]
    public void Conversion_crosses_year_boundary()
    {
        var t = Converter.ToZone(Utc(2026, 12, 31, 20, 0), TestZones.Taipei);
        Assert.Equal(TestZones.Wall(2027, 1, 1, 4, 0), t.Wall);
    }

    [Fact]
    public void Fixed_offset_with_minutes_round_trips()
    {
        var t = Converter.ToZone(Utc(2026, 1, 1, 0, 0), TestZones.Kathmandu);
        Assert.Equal(TestZones.Wall(2026, 1, 1, 5, 45), t.Wall);
        var back = Converter.Resolve(TestZones.Kathmandu, TestZones.Wall(2026, 1, 1, 5, 45), AmbiguityChoice.First);
        Assert.Equal(Utc(2026, 1, 1, 0, 0), back.Instant);
    }

    [Fact]
    public void Spring_forward_gap_is_invalid() =>
        Assert.Equal(ResolveKind.Invalid,
            Converter.Resolve(TestZones.NewYork, TestZones.Wall(2026, 3, 8, 2, 30), AmbiguityChoice.First).Kind);

    [Theory]
    [InlineData(AmbiguityChoice.First, 5)]
    [InlineData(AmbiguityChoice.Second, 6)]
    public void Fall_back_overlap_is_ambiguous_and_choice_picks_occurrence(AmbiguityChoice choice, int utcHour)
    {
        var r = Converter.Resolve(TestZones.NewYork, TestZones.Wall(2026, 11, 1, 1, 30), choice);
        Assert.Equal(ResolveKind.Ambiguous, r.Kind);
        Assert.Equal(Utc(2026, 11, 1, utcHour, 30), r.Instant);
    }

    [Fact]
    public void Summer_time_in_new_york_is_utc_minus_four()
    {
        var r = Converter.Resolve(TestZones.NewYork, TestZones.Wall(2026, 7, 1, 8, 0), AmbiguityChoice.First);
        Assert.Equal(ResolveKind.Ok, r.Kind);
        Assert.Equal(Utc(2026, 7, 1, 12, 0), r.Instant);
    }

    [Theory]
    [InlineData(5, AmbiguityChoice.First)]
    [InlineData(6, AmbiguityChoice.Second)]
    [InlineData(12, AmbiguityChoice.First)]
    public void ChoiceFor_returns_choice_that_round_trips(int utcHour, AmbiguityChoice expected)
    {
        var instant = Utc(2026, 11, 1, utcHour, 30);
        var choice = Converter.ChoiceFor(instant, TestZones.NewYork);
        Assert.Equal(expected, choice);
        var wall = Converter.ToZone(instant, TestZones.NewYork).Wall;
        Assert.Equal(instant, Converter.Resolve(TestZones.NewYork, wall, choice).Instant);
    }

    [Fact]
    public void ChoiceFor_fixed_offset_is_first() =>
        Assert.Equal(AmbiguityChoice.First, Converter.ChoiceFor(Utc(2026, 11, 1, 6, 30), TestZones.Kathmandu));
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj --filter "FullyQualifiedName~ConverterTests"`
Expected: 編譯失敗，找不到 `Converter`。

- [ ] **Step 3: 實作**

`src/TimeZoneCalc.Core/Converter.cs`：

```csharp
namespace TimeZoneCalc.Core;

public enum AmbiguityChoice { First, Second }

public enum ResolveKind { Ok, Invalid, Ambiguous }

public readonly record struct ResolveResult(ResolveKind Kind, DateTimeOffset Instant)
{
    public static ResolveResult Invalid => new(ResolveKind.Invalid, default);
}

public readonly record struct ZonedTime(DateTime Wall, ZoneOffsetInfo Offset);

public static class Converter
{
    // 某時區的牆上時間 → 瞬間
    public static ResolveResult Resolve(ZoneOption zone, DateTime wall, AmbiguityChoice choice)
    {
        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        if (zone.City is not { } tz)
            return new(ResolveKind.Ok, new DateTimeOffset(wall, zone.FixedOffset));
        if (tz.IsInvalidTime(wall))
            return ResolveResult.Invalid;
        if (tz.IsAmbiguousTime(wall))
        {
            var offsets = tz.GetAmbiguousTimeOffsets(wall);
            // 偏移量越大，對應的瞬間越早，所以最大的偏移量是第一次出現
            var offset = choice == AmbiguityChoice.First ? offsets.Max() : offsets.Min();
            return new(ResolveKind.Ambiguous, new DateTimeOffset(wall, offset));
        }
        return new(ResolveKind.Ok, new DateTimeOffset(wall, tz.GetUtcOffset(wall)));
    }

    // 瞬間 → 某時區的牆上時間
    public static ZonedTime ToZone(DateTimeOffset instant, ZoneOption zone)
    {
        var info = zone.GetOffset(instant);
        return new(instant.ToOffset(info.Offset).DateTime, info);
    }

    // 讓 instant 在 zone 的牆上時間解析回同一個 instant 所需的選擇
    public static AmbiguityChoice ChoiceFor(DateTimeOffset instant, ZoneOption zone)
    {
        var wall = ToZone(instant, zone).Wall;
        var second = Resolve(zone, wall, AmbiguityChoice.Second);
        return second.Kind == ResolveKind.Ambiguous && second.Instant == instant
            ? AmbiguityChoice.Second
            : AmbiguityChoice.First;
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj`
Expected: 全部通過。

- [ ] **Step 5: Commit**

```
git add src\TimeZoneCalc.Core\Converter.cs tests\TimeZoneCalc.Core.Tests\ConverterTests.cs
git commit -m "feat(core): wall time and instant conversion with DST gaps and overlaps" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: 時區清單 ZoneCatalog

**Files:**
- Create: `src/TimeZoneCalc.Core/ZoneCatalog.cs`
- Test: `tests/TimeZoneCalc.Core.Tests/ZoneCatalogTests.cs`

**Interfaces:**
- Consumes: `ZoneOption`、`TimeFormat`（Task 2）
- Produces: `sealed class ZoneCatalog { const string TaipeiId = "Taipei Standard Time"; const string TaipeiName = "台北 (Asia/Taipei)"; ZoneCatalog(IReadOnlyCollection<TimeZoneInfo> systemZones); static ZoneCatalog CreateDefault(); ZoneOption Utc; ZoneOption Taipei; IReadOnlyList<ZoneOption> All; IReadOnlyList<ZoneOption> Filter(string query); }`

- [ ] **Step 1: 寫失敗的測試**

`tests/TimeZoneCalc.Core.Tests/ZoneCatalogTests.cs`：

```csharp
using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class ZoneCatalogTests
{
    private static readonly ZoneCatalog Catalog = ZoneCatalog.CreateDefault();

    [Fact]
    public void Utc_and_taipei_come_first()
    {
        Assert.Same(Catalog.Utc, Catalog.All[0]);
        Assert.Same(Catalog.Taipei, Catalog.All[1]);
        Assert.Equal("UTC", Catalog.Utc.DisplayName);
        Assert.Equal(TimeSpan.Zero, Catalog.Utc.FixedOffset);
        Assert.Equal("台北 (Asia/Taipei)", Catalog.Taipei.DisplayName);
        Assert.False(Catalog.Taipei.IsFixed);
        Assert.False(Catalog.Taipei.IsFallback);
    }

    [Fact]
    public void Fixed_offsets_follow_sorted_and_only_real_offsets()
    {
        var fixedOffsets = Catalog.All.Skip(2).TakeWhile(z => z.IsFixed).Select(z => z.FixedOffset).ToList();
        Assert.Equal(fixedOffsets.Order().ToList(), fixedOffsets);
        Assert.Equal(TimeSpan.FromHours(-12), fixedOffsets[0]);
        Assert.Equal(TimeSpan.FromHours(14), fixedOffsets[^1]);
        Assert.Contains(new TimeSpan(5, 30, 0), fixedOffsets);
        Assert.Contains(new TimeSpan(5, 45, 0), fixedOffsets);
        Assert.Contains(TimeSpan.FromHours(8), fixedOffsets);
        Assert.DoesNotContain(TimeSpan.Zero, fixedOffsets);
        Assert.DoesNotContain(new TimeSpan(5, 15, 0), fixedOffsets);
    }

    [Fact]
    public void Cities_come_after_fixed_offsets_without_duplicates_of_utc_or_taipei()
    {
        var rest = Catalog.All.Skip(2).ToList();
        var firstCity = rest.FindIndex(z => !z.IsFixed);
        Assert.True(firstCity > 0);
        Assert.All(rest.Skip(firstCity), z => Assert.False(z.IsFixed));

        var cities = Catalog.All.Where(z => !z.IsFixed).ToList();
        Assert.Single(cities, z => z.Id == ZoneCatalog.TaipeiId);
        Assert.DoesNotContain(cities, z => z.Id.StartsWith("UTC", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cities, z => z.Id == "Eastern Standard Time");
    }

    [Fact]
    public void Missing_taipei_falls_back_to_fixed_plus_eight()
    {
        var zones = TimeZoneInfo.GetSystemTimeZones().Where(z => z.Id != ZoneCatalog.TaipeiId).ToList();
        var catalog = new ZoneCatalog(zones);
        Assert.True(catalog.Taipei.IsFallback);
        Assert.True(catalog.Taipei.IsFixed);
        Assert.Equal(TimeSpan.FromHours(8), catalog.Taipei.FixedOffset);
        Assert.Same(catalog.Taipei, catalog.All[1]);
    }

    [Theory]
    [InlineData("tai")]
    [InlineData("TAIPEI")]
    [InlineData("台北")]
    public void Filter_finds_taipei_case_insensitive(string query) =>
        Assert.Contains(Catalog.Taipei, Catalog.Filter(query));

    [Fact]
    public void Filter_blank_returns_all() =>
        Assert.Equal(Catalog.All.Count, Catalog.Filter("  ").Count);

    [Fact]
    public void Filter_matches_fixed_offset_text() =>
        Assert.Contains(Catalog.Filter("+05:45"), z => z.IsFixed && z.FixedOffset == new TimeSpan(5, 45, 0));
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj --filter "FullyQualifiedName~ZoneCatalogTests"`
Expected: 編譯失敗，找不到 `ZoneCatalog`。

- [ ] **Step 3: 實作**

`src/TimeZoneCalc.Core/ZoneCatalog.cs`：

```csharp
namespace TimeZoneCalc.Core;

// 時區選單：UTC、台北、固定偏移量（由小到大）、其餘城市時區
public sealed class ZoneCatalog
{
    public const string TaipeiId = "Taipei Standard Time";
    public const string TaipeiName = "台北 (Asia/Taipei)";

    public ZoneCatalog(IReadOnlyCollection<TimeZoneInfo> systemZones)
    {
        Utc = ZoneOption.FromOffset(TimeSpan.Zero, "UTC", "UTC");

        var taipei = systemZones.FirstOrDefault(z => z.Id == TaipeiId);
        Taipei = taipei is null
            ? ZoneOption.FromOffset(TimeSpan.FromHours(8), "台北 (UTC+08:00 固定)", TaipeiId, isFallback: true)
            : ZoneOption.FromCity(taipei, TaipeiName);

        // 整點 -12～+14，加上系統時區實際用到的非整點偏移量（+05:30、+05:45…）
        var fixedOffsets = Enumerable.Range(-12, 27).Select(h => TimeSpan.FromHours(h))
            .Concat(systemZones.Select(z => z.BaseUtcOffset))
            .Where(o => o != TimeSpan.Zero)
            .Distinct()
            .Order()
            .Select(o => ZoneOption.FromOffset(o));

        // Windows 的 "UTC"、"UTC-11" 之類時區和固定偏移量重複，排除
        var cities = systemZones
            .Where(z => z.Id != TaipeiId && !z.Id.StartsWith("UTC", StringComparison.OrdinalIgnoreCase))
            .OrderBy(z => z.DisplayName, StringComparer.CurrentCulture)
            .Select(z => ZoneOption.FromCity(z));

        All = [Utc, Taipei, .. fixedOffsets, .. cities];
    }

    public static ZoneCatalog CreateDefault() => new(TimeZoneInfo.GetSystemTimeZones());

    public ZoneOption Utc { get; }
    public ZoneOption Taipei { get; }
    public IReadOnlyList<ZoneOption> All { get; }

    public IReadOnlyList<ZoneOption> Filter(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return All;
        return All.Where(z => z.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || z.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj`
Expected: 全部通過。

- [ ] **Step 5: Commit**

```
git add src\TimeZoneCalc.Core\ZoneCatalog.cs tests\TimeZoneCalc.Core.Tests\ZoneCatalogTests.cs
git commit -m "feat(core): zone catalog with UTC and Taipei first and fixed offsets" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: 數字由左往右填入 DigitEntry

**Files:**
- Create: `src/TimeZoneCalc.Core/DigitEntry.cs`
- Test: `tests/TimeZoneCalc.Core.Tests/DigitEntryTests.cs`

**Interfaces:**
- Consumes: 無
- Produces:
  - `enum EntryMode { Time, Date }`
  - `sealed class DigitEntry { const int MinYear = 1900; const int MaxYear = 2100; DigitEntry(EntryMode mode); EntryMode Mode; int MaxDigits; string Digits; bool IsFresh; bool TryPush(int digit); bool Backspace(); void Clear(); void MarkFresh(); void Load(TimeOnly time); void Load(DateOnly date); TimeOnly Time; DateOnly? Date; string Display; }`
  - 規則：`Time` 未填的位補 0；`Date` 未滿 8 位、日期不存在或年份超出 1900–2100 時為 `null`；`Display` 時間為 `HH:mm:ss`（缺位補 0），日期為 `yyyy-MM-dd`（缺位補 `_`）。

- [ ] **Step 1: 寫失敗的測試**

`tests/TimeZoneCalc.Core.Tests/DigitEntryTests.cs`：

```csharp
using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class DigitEntryTests
{
    private static DigitEntry Typed(EntryMode mode, string digits)
    {
        var e = new DigitEntry(mode);
        foreach (var c in digits)
            Assert.True(e.TryPush(c - '0'), $"應接受 {digits} 中的 {c}");
        return e;
    }

    [Fact]
    public void Six_digits_fill_hh_mm_ss()
    {
        var e = Typed(EntryMode.Time, "143205");
        Assert.Equal("14:32:05", e.Display);
        Assert.Equal(new TimeOnly(14, 32, 5), e.Time);
    }

    [Fact]
    public void Partial_time_pads_with_zero()
    {
        var e = Typed(EntryMode.Time, "1432");
        Assert.Equal("14:32:00", e.Display);
        Assert.Equal(new TimeOnly(14, 32, 0), e.Time);
    }

    [Fact]
    public void Empty_time_is_midnight() =>
        Assert.Equal("00:00:00", new DigitEntry(EntryMode.Time).Display);

    [Fact]
    public void Hour_19_is_allowed() =>
        Assert.Equal(new TimeOnly(19, 0), Typed(EntryMode.Time, "19").Time);

    [Theory]
    [InlineData("", 3)]
    [InlineData("2", 4)]
    [InlineData("23", 6)]
    [InlineData("2359", 6)]
    [InlineData("235959", 0)]
    public void Time_rejects_invalid_next_digit(string typed, int digit)
    {
        var e = Typed(EntryMode.Time, typed);
        Assert.False(e.TryPush(digit));
        Assert.Equal(typed, e.Digits);
    }

    [Fact]
    public void Backspace_removes_last_digit()
    {
        var e = Typed(EntryMode.Time, "1432");
        Assert.True(e.Backspace());
        Assert.Equal("143", e.Digits);
    }

    [Fact]
    public void Backspace_on_empty_returns_false() =>
        Assert.False(new DigitEntry(EntryMode.Time).Backspace());

    [Fact]
    public void Clear_empties()
    {
        var e = Typed(EntryMode.Time, "1432");
        e.Clear();
        Assert.Equal("", e.Digits);
    }

    [Fact]
    public void Load_then_first_digit_restarts()
    {
        var e = new DigitEntry(EntryMode.Time);
        e.Load(new TimeOnly(9, 5, 7));
        Assert.Equal("090507", e.Digits);
        Assert.True(e.IsFresh);
        Assert.True(e.TryPush(1));
        Assert.Equal("1", e.Digits);
        Assert.False(e.IsFresh);
    }

    [Fact]
    public void Backspace_after_load_edits_loaded_digits()
    {
        var e = new DigitEntry(EntryMode.Time);
        e.Load(new TimeOnly(9, 5, 7));
        Assert.True(e.Backspace());
        Assert.Equal("09050", e.Digits);
        Assert.False(e.IsFresh);
    }

    [Fact]
    public void MarkFresh_makes_next_digit_restart()
    {
        var e = Typed(EntryMode.Time, "14");
        e.MarkFresh();
        Assert.True(e.TryPush(0));
        Assert.Equal("0", e.Digits);
    }

    [Fact]
    public void Eight_digits_make_a_date()
    {
        var e = Typed(EntryMode.Date, "20260924");
        Assert.Equal(new DateOnly(2026, 9, 24), e.Date);
        Assert.Equal("2026-09-24", e.Display);
    }

    [Fact]
    public void Partial_date_is_null_and_shows_placeholders()
    {
        var e = Typed(EntryMode.Date, "202609");
        Assert.Null(e.Date);
        Assert.Equal("2026-09-__", e.Display);
    }

    [Theory]
    [InlineData("20260231")]
    [InlineData("20250229")]
    [InlineData("18991231")]
    [InlineData("21010101")]
    public void Impossible_or_out_of_range_dates_are_null(string digits) =>
        Assert.Null(Typed(EntryMode.Date, digits).Date);

    [Theory]
    [InlineData("20240229", 2024, 2, 29)]
    [InlineData("19000101", 1900, 1, 1)]
    [InlineData("21001231", 2100, 12, 31)]
    public void Valid_edge_dates(string digits, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), Typed(EntryMode.Date, digits).Date);

    [Theory]
    [InlineData("", 3)]
    [InlineData("2026", 2)]
    [InlineData("20260", 0)]
    [InlineData("20261", 3)]
    [InlineData("202609", 4)]
    [InlineData("2026090", 0)]
    [InlineData("2026093", 2)]
    [InlineData("20260924", 1)]
    public void Date_rejects_invalid_next_digit(string typed, int digit)
    {
        var e = Typed(EntryMode.Date, typed);
        Assert.False(e.TryPush(digit));
        Assert.Equal(typed, e.Digits);
    }

    [Fact] // Review Focus 3
    public void Loaded_out_of_range_date_displays_but_is_null()
    {
        var e = new DigitEntry(EntryMode.Date);
        e.Load(new DateOnly(2101, 1, 1));
        Assert.Equal("2101-01-01", e.Display);
        Assert.Null(e.Date);
    }

    [Fact]
    public void Loading_wrong_kind_throws()
    {
        Assert.Throws<InvalidOperationException>(() => new DigitEntry(EntryMode.Date).Load(new TimeOnly(1, 0)));
        Assert.Throws<InvalidOperationException>(() => new DigitEntry(EntryMode.Time).Load(new DateOnly(2026, 1, 1)));
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj --filter "FullyQualifiedName~DigitEntryTests"`
Expected: 編譯失敗，找不到 `DigitEntry`。

- [ ] **Step 3: 實作**

`src/TimeZoneCalc.Core/DigitEntry.cs`：

```csharp
using System.Globalization;

namespace TimeZoneCalc.Core;

public enum EntryMode { Time, Date }

// 像小算盤一樣由左往右填入數字：時間 HHmmss、日期 yyyyMMdd
public sealed class DigitEntry
{
    public const int MinYear = 1900;
    public const int MaxYear = 2100;

    private string _digits = "";

    public DigitEntry(EntryMode mode) => Mode = mode;

    public EntryMode Mode { get; }
    public int MaxDigits => Mode == EntryMode.Time ? 6 : 8;
    public string Digits => _digits;

    // 剛載入或剛切換過來：下一個數字會從頭重新填
    public bool IsFresh { get; private set; }

    public bool TryPush(int digit)
    {
        if (digit is < 0 or > 9)
            return false;
        var current = IsFresh ? "" : _digits;
        if (current.Length >= MaxDigits || !Allowed(current, digit))
            return false;
        _digits = current + (char)('0' + digit);
        IsFresh = false;
        return true;
    }

    public bool Backspace()
    {
        IsFresh = false;
        if (_digits.Length == 0)
            return false;
        _digits = _digits[..^1];
        return true;
    }

    public void Clear()
    {
        _digits = "";
        IsFresh = false;
    }

    public void MarkFresh() => IsFresh = true;

    public void Load(TimeOnly time) =>
        LoadDigits(EntryMode.Time, time.ToString("HHmmss", CultureInfo.InvariantCulture));

    public void Load(DateOnly date) =>
        LoadDigits(EntryMode.Date, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

    public TimeOnly Time
    {
        get
        {
            EnsureMode(EntryMode.Time);
            var d = _digits.PadRight(6, '0');
            return new TimeOnly(int.Parse(d[..2]), int.Parse(d[2..4]), int.Parse(d[4..]));
        }
    }

    public DateOnly? Date
    {
        get
        {
            EnsureMode(EntryMode.Date);
            if (_digits.Length < 8)
                return null;
            int y = int.Parse(_digits[..4]), m = int.Parse(_digits[4..6]), d = int.Parse(_digits[6..]);
            if (y < MinYear || y > MaxYear || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m))
                return null;
            return new DateOnly(y, m, d);
        }
    }

    public string Display
    {
        get
        {
            if (Mode == EntryMode.Time)
            {
                var t = _digits.PadRight(6, '0');
                return $"{t[..2]}:{t[2..4]}:{t[4..]}";
            }
            var d = _digits.PadRight(8, '_');
            return $"{d[..4]}-{d[4..6]}-{d[6..]}";
        }
    }

    private void LoadDigits(EntryMode mode, string digits)
    {
        EnsureMode(mode);
        _digits = digits;
        IsFresh = true;
    }

    private void EnsureMode(EntryMode mode)
    {
        if (Mode != mode)
            throw new InvalidOperationException($"這個輸入欄是 {Mode}，不是 {mode}");
    }

    // 只擋「這一位放下去一定不合法」的數字；日期是否存在等填滿 8 位再判斷
    private bool Allowed(string current, int digit)
    {
        var pos = current.Length;
        if (Mode == EntryMode.Time)
            return pos switch
            {
                0 => digit <= 2,
                1 => current[0] != '2' || digit <= 3,
                2 or 4 => digit <= 5,
                _ => true,
            };
        return pos switch
        {
            0 => digit is 1 or 2,
            4 => digit <= 1,
            5 => current[4] == '0' ? digit >= 1 : digit <= 2,
            6 => digit <= 3,
            7 => current[6] switch { '0' => digit >= 1, '3' => digit <= 1, _ => true },
            _ => true,
        };
    }
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj`
Expected: 全部通過。

- [ ] **Step 5: Commit**

```
git add src\TimeZoneCalc.Core\DigitEntry.cs tests\TimeZoneCalc.Core.Tests\DigitEntryTests.cs
git commit -m "feat(core): left-to-right digit entry for date and time" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: 計算器狀態 CalculatorState

**Files:**
- Create: `src/TimeZoneCalc.Core/CalculatorState.cs`
- Test: `tests/TimeZoneCalc.Core.Tests/CalculatorStateTests.cs`

**Interfaces:**
- Consumes: `ZoneCatalog`（Task 4）、`Converter`、`AmbiguityChoice`、`ResolveKind`（Task 3）、`DigitEntry`、`EntryMode`（Task 5）、`TimeFormat`（Task 2）
- Produces:
  - `enum InputStatus { Ok, IncompleteDate, InvalidDate, NonexistentTime, AmbiguousTime }`
  - `sealed class ZoneRow { ZoneOption Zone; string Text; string OffsetLabel; bool IsStale; bool CanCopy; }`（setter 為 internal）
  - `sealed class CalculatorState`：
    - `const int MaxRows = 5; const string Blank = "—";`
    - `CalculatorState(ZoneCatalog catalog, Func<DateTimeOffset> clock)`
    - `event EventHandler? Changed`
    - `IReadOnlyList<ZoneRow> Rows; int ActiveIndex; ZoneRow ActiveRow; EntryMode ActiveSegment; DigitEntry DateEntry; DigitEntry TimeEntry; AmbiguityChoice Ambiguity; InputStatus Status; bool CanAddRow; bool CanRemoveRow`
    - `void PushDigit(int digit); void Backspace(); void Clear(); void SetNow(); void SetDate(DateOnly date); void SetSegment(EntryMode segment); void ToggleSegment(); void SetActiveRow(int index); void MoveActive(int delta); void SetZone(int index, ZoneOption zone); void AddRow(); void RemoveRow(int index); void ToggleAmbiguity()`

- [ ] **Step 1: 寫失敗的測試**

`tests/TimeZoneCalc.Core.Tests/CalculatorStateTests.cs`：

```csharp
using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class CalculatorStateTests
{
    private static readonly ZoneCatalog Catalog = ZoneCatalog.CreateDefault();
    private static readonly ZoneOption NewYork = Catalog.All.First(z => z.Id == "Eastern Standard Time");
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 6, 32, 5, TimeSpan.Zero);

    private static CalculatorState Create() => new(Catalog, () => Now);

    private static void Type(CalculatorState s, string digits)
    {
        foreach (var c in digits)
            s.PushDigit(c - '0');
    }

    private static void Enter(CalculatorState s, string date, string time)
    {
        s.SetSegment(EntryMode.Date);
        Type(s, date);
        s.SetSegment(EntryMode.Time);
        Type(s, time);
    }

    [Fact]
    public void Starts_with_utc_and_taipei_at_now_with_utc_active()
    {
        var s = Create();
        Assert.Equal(new[] { Catalog.Utc, Catalog.Taipei }, s.Rows.Select(r => r.Zone));
        Assert.Equal(0, s.ActiveIndex);
        Assert.Equal(EntryMode.Time, s.ActiveSegment);
        Assert.Equal(InputStatus.Ok, s.Status);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
        Assert.Equal("2026-09-24 14:32:05", s.Rows[1].Text);
        Assert.Equal("UTC+08:00", s.Rows[1].OffsetLabel);
        Assert.True(s.Rows[1].CanCopy);
    }

    [Fact]
    public void Typing_time_restarts_and_converts_across_day()
    {
        var s = Create();
        Type(s, "2300");
        Assert.Equal("23:00:00", s.TimeEntry.Display);
        Assert.Equal("2026-09-25 07:00:00", s.Rows[1].Text);
    }

    [Fact]
    public void Typing_date_changes_date_and_keeps_time()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "20261231");
        Assert.Equal("2026-12-31 14:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Incomplete_date_marks_other_rows_stale_with_last_value()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "2026");
        Assert.Equal(InputStatus.IncompleteDate, s.Status);
        Assert.True(s.Rows[1].IsStale);
        Assert.Equal("2026-09-24 14:32:05", s.Rows[1].Text);
        Assert.False(s.Rows[0].CanCopy);
    }

    [Fact]
    public void Impossible_date_is_invalid()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "20260231");
        Assert.Equal(InputStatus.InvalidDate, s.Status);
        Assert.True(s.Rows[1].IsStale);
    }

    [Fact]
    public void Selecting_row_loads_its_value()
    {
        var s = Create();
        s.SetActiveRow(1);
        Assert.Equal(1, s.ActiveIndex);
        Assert.Equal("2026-09-24", s.DateEntry.Display);
        Assert.Equal("14:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
        Type(s, "08");
        Assert.Equal("2026-09-24 00:00:00", s.Rows[0].Text);
    }

    [Fact] // Review Focus 4
    public void Selecting_row_while_date_incomplete_loads_last_valid_instant()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "2026");
        s.SetActiveRow(1);
        Assert.Equal(InputStatus.Ok, s.Status);
        Assert.Equal("14:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
    }

    [Fact]
    public void MoveActive_clamps_to_rows()
    {
        var s = Create();
        s.MoveActive(-1);
        Assert.Equal(0, s.ActiveIndex);
        s.MoveActive(1);
        Assert.Equal(1, s.ActiveIndex);
        s.MoveActive(1);
        Assert.Equal(1, s.ActiveIndex);
    }

    [Fact]
    public void Clear_time_sets_midnight_and_clear_date_sets_today()
    {
        var s = Create();
        s.Clear();
        Assert.Equal("00:00:00", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 08:00:00", s.Rows[1].Text);
        s.SetSegment(EntryMode.Date);
        Type(s, "20200101");
        s.Clear();
        Assert.Equal("2026-09-24", s.DateEntry.Display);
    }

    [Fact]
    public void SetNow_restores_clock_time()
    {
        var s = Create();
        Type(s, "1200");
        s.SetNow();
        Assert.Equal("06:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24", s.DateEntry.Display);
    }

    [Fact]
    public void SetDate_from_calendar_keeps_time()
    {
        var s = Create();
        s.SetDate(new DateOnly(2026, 1, 1));
        Assert.Equal("2026-01-01 14:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Rows_are_limited_to_one_through_five()
    {
        var s = Create();
        for (var i = 0; i < 10; i++)
            s.AddRow();
        Assert.Equal(CalculatorState.MaxRows, s.Rows.Count);
        Assert.False(s.CanAddRow);
        Assert.Same(Catalog.Utc, s.Rows[4].Zone);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[4].Text);
        for (var i = 0; i < 10; i++)
            s.RemoveRow(0);
        Assert.Single(s.Rows);
        Assert.False(s.CanRemoveRow);
    }

    [Fact] // Review Focus 5
    public void Removing_active_row_activates_first_row_with_same_instant()
    {
        var s = Create();
        s.SetActiveRow(1);
        s.RemoveRow(1);
        Assert.Equal(0, s.ActiveIndex);
        Assert.Equal("06:32:05", s.TimeEntry.Display);
    }

    [Fact] // Review Focus 5
    public void Removing_row_before_active_keeps_same_active_row()
    {
        var s = Create();
        s.AddRow();
        s.SetZone(2, NewYork);
        s.SetActiveRow(2);
        s.RemoveRow(0);
        Assert.Equal(1, s.ActiveIndex);
        Assert.Same(NewYork, s.ActiveRow.Zone);
        Assert.Equal("02:32:05", s.TimeEntry.Display);
    }

    [Fact] // Review Focus 2
    public void Changing_other_row_zone_keeps_instant()
    {
        var s = Create();
        s.SetZone(1, NewYork);
        Assert.Equal("2026-09-24 02:32:05", s.Rows[1].Text);
        Assert.Equal("UTC-04:00 夏令", s.Rows[1].OffsetLabel);
    }

    [Fact] // Review Focus 2
    public void Changing_active_row_zone_keeps_typed_wall_time()
    {
        var s = Create();
        s.SetZone(0, Catalog.Taipei);
        Assert.Equal("06:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Nonexistent_time_blanks_other_rows()
    {
        var s = Create();
        s.SetZone(0, NewYork);
        Enter(s, "20260308", "023000");
        Assert.Equal(InputStatus.NonexistentTime, s.Status);
        Assert.Equal(CalculatorState.Blank, s.Rows[1].Text);
        Assert.False(s.Rows[1].CanCopy);
        Assert.False(s.Rows[0].CanCopy);
    }

    [Fact]
    public void Ambiguous_time_defaults_to_first_and_can_toggle()
    {
        var s = Create();
        s.SetZone(0, NewYork);
        Enter(s, "20261101", "013000");
        Assert.Equal(InputStatus.AmbiguousTime, s.Status);
        Assert.Equal(AmbiguityChoice.First, s.Ambiguity);
        Assert.Equal("2026-11-01 13:30:00", s.Rows[1].Text);

        s.ToggleAmbiguity();
        Assert.Equal(AmbiguityChoice.Second, s.Ambiguity);
        Assert.Equal("2026-11-01 14:30:00", s.Rows[1].Text);

        s.Backspace();
        Type(s, "0");
        Assert.Equal(AmbiguityChoice.First, s.Ambiguity);
        Assert.Equal("2026-11-01 13:30:00", s.Rows[1].Text);
    }

    [Fact] // Review Focus 1
    public void Selecting_row_showing_second_occurrence_keeps_instant()
    {
        var s = Create();
        Enter(s, "20261101", "063000");
        s.SetZone(1, NewYork);
        Assert.Equal("2026-11-01 01:30:00", s.Rows[1].Text);
        Assert.Equal("UTC-05:00", s.Rows[1].OffsetLabel);

        s.SetActiveRow(1);
        Assert.Equal(AmbiguityChoice.Second, s.Ambiguity);
        Assert.Equal(InputStatus.AmbiguousTime, s.Status);
        Assert.Equal("2026-11-01 06:30:00", s.Rows[0].Text);
    }

    [Fact] // Review Focus 3
    public void Selecting_row_whose_date_is_past_2100_reports_invalid_date()
    {
        var s = Create();
        Enter(s, "21001231", "230000");
        Assert.Equal("2101-01-01 07:00:00", s.Rows[1].Text);

        s.SetActiveRow(1);
        Assert.Equal(InputStatus.InvalidDate, s.Status);
        Assert.Equal("2101-01-01", s.DateEntry.Display);
        Assert.Equal("2100-12-31 23:00:00", s.Rows[0].Text);
    }

    [Fact]
    public void Changed_fires_on_accepted_input_only()
    {
        var s = Create();
        var count = 0;
        s.Changed += (_, _) => count++;
        s.PushDigit(9);   // 小時十位數不能是 9
        Assert.Equal(0, count);
        s.PushDigit(1);
        Assert.Equal(1, count);
    }
}
```

- [ ] **Step 2: 跑測試確認失敗**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj --filter "FullyQualifiedName~CalculatorStateTests"`
Expected: 編譯失敗，找不到 `CalculatorState`。

- [ ] **Step 3: 實作**

`src/TimeZoneCalc.Core/CalculatorState.cs`：

```csharp
namespace TimeZoneCalc.Core;

public enum InputStatus { Ok, IncompleteDate, InvalidDate, NonexistentTime, AmbiguousTime }

public sealed class ZoneRow
{
    internal ZoneRow(ZoneOption zone) => Zone = zone;

    public ZoneOption Zone { get; internal set; }
    public string Text { get; internal set; } = "";
    public string OffsetLabel { get; internal set; } = "";
    public bool IsStale { get; internal set; }
    public bool CanCopy { get; internal set; }
}

// 整個計算器的狀態；UI 只負責把它畫出來、把按鍵轉進來
public sealed class CalculatorState
{
    public const int MaxRows = 5;
    public const string Blank = "—";

    private readonly ZoneCatalog _catalog;
    private readonly Func<DateTimeOffset> _clock;
    private readonly List<ZoneRow> _rows;

    // 最後一次成功換算的瞬間；輸入暫時無效時，其他列顯示它（灰色）
    private DateTimeOffset? _lastInstant;

    public CalculatorState(ZoneCatalog catalog, Func<DateTimeOffset> clock)
    {
        _catalog = catalog;
        _clock = clock;
        _rows = [new ZoneRow(catalog.Utc), new ZoneRow(catalog.Taipei)];
        SetNow();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ZoneRow> Rows => _rows;
    public int ActiveIndex { get; private set; }
    public ZoneRow ActiveRow => _rows[ActiveIndex];
    public EntryMode ActiveSegment { get; private set; } = EntryMode.Time;
    public DigitEntry DateEntry { get; } = new(EntryMode.Date);
    public DigitEntry TimeEntry { get; } = new(EntryMode.Time);
    public AmbiguityChoice Ambiguity { get; private set; }
    public InputStatus Status { get; private set; }
    public bool CanAddRow => _rows.Count < MaxRows;
    public bool CanRemoveRow => _rows.Count > 1;

    private DigitEntry ActiveEntry => ActiveSegment == EntryMode.Date ? DateEntry : TimeEntry;

    public void PushDigit(int digit)
    {
        if (ActiveEntry.TryPush(digit))
            OnInputEdited();
    }

    public void Backspace()
    {
        if (ActiveEntry.Backspace())
            OnInputEdited();
    }

    public void Clear()
    {
        if (ActiveSegment == EntryMode.Time)
            TimeEntry.Clear();
        else
            DateEntry.Load(DateOnly.FromDateTime(Converter.ToZone(_clock(), ActiveRow.Zone).Wall));
        OnInputEdited();
    }

    public void SetNow() => LoadInstant(_clock());

    public void SetDate(DateOnly date)
    {
        DateEntry.Load(date);
        OnInputEdited();
    }

    public void SetSegment(EntryMode segment)
    {
        ActiveSegment = segment;
        ActiveEntry.MarkFresh();
        OnChanged();
    }

    public void ToggleSegment() =>
        SetSegment(ActiveSegment == EntryMode.Time ? EntryMode.Date : EntryMode.Time);

    public void SetActiveRow(int index)
    {
        if (index < 0 || index >= _rows.Count || index == ActiveIndex)
            return;
        ActiveIndex = index;
        ReloadActiveRow();
    }

    public void MoveActive(int delta) => SetActiveRow(Math.Clamp(ActiveIndex + delta, 0, _rows.Count - 1));

    // 改輸入列的時區：保留打好的牆上時間；改其他列：保留同一瞬間
    public void SetZone(int index, ZoneOption zone)
    {
        _rows[index].Zone = zone;
        if (index == ActiveIndex)
            Ambiguity = AmbiguityChoice.First;
        Recompute();
    }

    public void AddRow()
    {
        if (!CanAddRow)
            return;
        _rows.Add(new ZoneRow(_catalog.Utc));
        Recompute();
    }

    public void RemoveRow(int index)
    {
        if (!CanRemoveRow || index < 0 || index >= _rows.Count)
            return;
        var removedActive = index == ActiveIndex;
        _rows.RemoveAt(index);
        if (removedActive)
        {
            ActiveIndex = 0;
            ReloadActiveRow();
            return;
        }
        if (index < ActiveIndex)
            ActiveIndex--;
        Recompute();
    }

    public void ToggleAmbiguity()
    {
        if (Status != InputStatus.AmbiguousTime)
            return;
        Ambiguity = Ambiguity == AmbiguityChoice.First ? AmbiguityChoice.Second : AmbiguityChoice.First;
        Recompute();
    }

    private void ReloadActiveRow()
    {
        if (_lastInstant is { } instant)
            LoadInstant(instant);
        else
            Recompute();
    }

    private void LoadInstant(DateTimeOffset instant)
    {
        var zoned = Converter.ToZone(instant, ActiveRow.Zone);
        DateEntry.Load(DateOnly.FromDateTime(zoned.Wall));
        TimeEntry.Load(TimeOnly.FromDateTime(zoned.Wall));
        Ambiguity = Converter.ChoiceFor(instant, ActiveRow.Zone);
        Recompute();
    }

    private void OnInputEdited()
    {
        Ambiguity = AmbiguityChoice.First;
        Recompute();
    }

    private void Recompute()
    {
        var date = DateEntry.Date;
        if (date is null)
        {
            Status = DateEntry.Digits.Length < DateEntry.MaxDigits ? InputStatus.IncompleteDate : InputStatus.InvalidDate;
            ShowLastInstantAsStale();
            ActiveRow.Text = $"{DateEntry.Display} {TimeEntry.Display}";
            ActiveRow.OffsetLabel = "";
            ActiveRow.IsStale = false;
            ActiveRow.CanCopy = false;
            OnChanged();
            return;
        }

        var wall = date.Value.ToDateTime(TimeEntry.Time);
        var result = Converter.Resolve(ActiveRow.Zone, wall, Ambiguity);
        if (result.Kind == ResolveKind.Invalid)
        {
            Status = InputStatus.NonexistentTime;
            foreach (var row in _rows)
            {
                row.Text = Blank;
                row.OffsetLabel = "";
                row.IsStale = false;
                row.CanCopy = false;
            }
            ActiveRow.Text = TimeFormat.Wall(wall);
            OnChanged();
            return;
        }

        Status = result.Kind == ResolveKind.Ambiguous ? InputStatus.AmbiguousTime : InputStatus.Ok;
        _lastInstant = result.Instant;
        foreach (var row in _rows)
            Show(row, result.Instant, stale: false);
        OnChanged();
    }

    private void ShowLastInstantAsStale()
    {
        foreach (var row in _rows)
        {
            if (_lastInstant is { } last)
                Show(row, last, stale: true);
            else
                row.IsStale = true;
        }
    }

    private static void Show(ZoneRow row, DateTimeOffset instant, bool stale)
    {
        var zoned = Converter.ToZone(instant, row.Zone);
        row.Text = TimeFormat.Wall(zoned.Wall);
        row.OffsetLabel = zoned.Offset.Label;
        row.IsStale = stale;
        row.CanCopy = true;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 4: 跑測試確認通過**

Run: `dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj`
Expected: 全部通過。

- [ ] **Step 5: Commit**

```
git add src\TimeZoneCalc.Core\CalculatorState.cs tests\TimeZoneCalc.Core.Tests\CalculatorStateTests.cs
git commit -m "feat(core): calculator state with rows, active row and input status" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: 主畫面（大字區、時區列、數字鍵盤、月曆、狀態提示、複製）

**Files:**
- Modify: `src/TimeZoneCalc.App/TimeZoneCalc.App.csproj`（加 Core 參考）
- Replace: `src/TimeZoneCalc.App/MainWindow.xaml`、`MainWindow.xaml.cs`
- Create: `src/TimeZoneCalc.App/RowView.cs`、`src/TimeZoneCalc.App/Theme.cs`
- Create: `tools/capture-window.ps1`

**Interfaces:**
- Consumes: `CalculatorState`、`ZoneRow`、`InputStatus`、`ZoneCatalog`、`ZoneOption`、`DigitEntry`、`EntryMode`、`AmbiguityChoice`（Core 全部）
- Produces: `MainWindow` 內有 `Host`（`UserControl`，鍵盤焦點停在這裡）、`FocusHost()`、`CopyRow(int index)`；Task 8 會在 `Host` 加 `PreviewKeyDown`。

UI 不寫自動化測試；驗證方式是建置、啟動檢查、擷取畫面目視確認。

- [ ] **Step 1: App 參考 Core**

```
dotnet add src\TimeZoneCalc.App\TimeZoneCalc.App.csproj reference src\TimeZoneCalc.Core\TimeZoneCalc.Core.csproj
```

- [ ] **Step 2: 建立 `src/TimeZoneCalc.App/Theme.cs`**

```csharp
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TimeZoneCalc;

internal static class Theme
{
    public static readonly Brush Transparent = new SolidColorBrush(Colors.Transparent);

    public static Brush Get(string key) => (Brush)Application.Current.Resources[key];
}
```

- [ ] **Step 3: 建立 `src/TimeZoneCalc.App/RowView.cs`**

```csharp
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TimeZoneCalc.Core;

namespace TimeZoneCalc;

// 一列時區：藍條、時區選單、換算結果與偏移量、複製、刪除
internal sealed class RowView
{
    private const string FallbackNote = "找不到系統的台北時區，改用固定 UTC+08:00";

    private readonly Border _bar = new() { Width = 4, CornerRadius = new CornerRadius(2) };
    private readonly AutoSuggestBox _zoneBox = new() { Width = 220, VerticalAlignment = VerticalAlignment.Center, PlaceholderText = "搜尋時區" };
    private readonly TextBlock _value = new() { FontSize = 18, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
    private readonly TextBlock _offset = new() { FontSize = 12 };
    private readonly TextBlock _note = new() { FontSize = 12, Visibility = Visibility.Collapsed };
    private readonly Button _copy = new() { AllowFocusOnInteraction = false, VerticalAlignment = VerticalAlignment.Center, Content = new FontIcon { Glyph = "\uE8C8", FontSize = 14 } };
    private readonly Button _remove = new() { AllowFocusOnInteraction = false, VerticalAlignment = VerticalAlignment.Center, Content = new FontIcon { Glyph = "\uE711", FontSize = 12 } };
    private readonly DispatcherQueueTimer _flashTimer;
    private ZoneOption? _zone;
    private string? _zoneNote;

    public RowView(int index, ZoneCatalog catalog, CalculatorState state, Action<int> copy, Action returnFocus)
    {
        ToolTipService.SetToolTip(_copy, "複製");
        ToolTipService.SetToolTip(_remove, "刪除這一列");

        var sub = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        sub.Children.Add(_offset);
        sub.Children.Add(_note);
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(_value);
        info.Children.Add(sub);

        Root = new Grid { ColumnSpacing = 8, Padding = new Thickness(0, 4, 0, 4), Background = Theme.Transparent };
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        Place(_bar, 0);
        Place(_zoneBox, 1);
        Place(info, 2);
        Place(_copy, 3);
        Place(_remove, 4);

        // 點列的空白處 = 選為輸入列；點在時區選單或按鈕上不算
        Root.Tapped += (_, e) =>
        {
            if (IsInside(e.OriginalSource, _zoneBox) || IsInside(e.OriginalSource, _copy) || IsInside(e.OriginalSource, _remove))
                return;
            state.SetActiveRow(index);
            returnFocus();
        };

        _zoneBox.GotFocus += (_, _) =>
        {
            _zoneBox.Text = "";
            _zoneBox.ItemsSource = catalog.All;
            _zoneBox.IsSuggestionListOpen = true;
        };
        _zoneBox.TextChanged += (box, e) =>
        {
            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                box.ItemsSource = catalog.Filter(box.Text);
        };
        _zoneBox.QuerySubmitted += (box, e) =>
        {
            var chosen = e.ChosenSuggestion as ZoneOption ?? catalog.Filter(e.QueryText).FirstOrDefault();
            if (chosen is not null)
                state.SetZone(index, chosen);
            box.Text = state.Rows[index].Zone.DisplayName;
            returnFocus();
        };
        _zoneBox.LostFocus += (_, _) =>
        {
            if (_zone is not null)
                _zoneBox.Text = _zone.DisplayName;
        };

        _copy.Click += (_, _) => copy(index);
        _remove.Click += (_, _) =>
        {
            state.RemoveRow(index);
            returnFocus();
        };

        _flashTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _flashTimer.Interval = TimeSpan.FromSeconds(1.5);
        _flashTimer.IsRepeating = false;
        _flashTimer.Tick += (_, _) => ShowNote(_zoneNote);
    }

    public Grid Root { get; }

    public void Update(ZoneRow row, bool isActive, bool canRemove)
    {
        if (!ReferenceEquals(_zone, row.Zone))
        {
            _zone = row.Zone;
            _zoneBox.Text = row.Zone.DisplayName;
            ToolTipService.SetToolTip(_zoneBox, row.Zone.DisplayName);
        }
        _bar.Background = isActive ? Theme.Get("AccentFillColorDefaultBrush") : Theme.Transparent;
        _value.Text = row.Text;
        _value.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        _value.Foreground = Theme.Get(row.IsStale ? "TextFillColorDisabledBrush" : "TextFillColorPrimaryBrush");
        _offset.Text = row.OffsetLabel;
        _offset.Foreground = Theme.Get("TextFillColorSecondaryBrush");
        _zoneNote = row.Zone.IsFallback ? FallbackNote : null;
        if (!_flashTimer.IsRunning)
            ShowNote(_zoneNote);
        _remove.Visibility = canRemove ? Visibility.Visible : Visibility.Collapsed;
    }

    // 短暫顯示「已複製」之類的訊息
    public void Flash(string message)
    {
        ShowNote(message);
        _flashTimer.Stop();
        _flashTimer.Start();
    }

    private void ShowNote(string? text)
    {
        _note.Text = text ?? "";
        _note.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Place(FrameworkElement element, int column)
    {
        Grid.SetColumn(element, column);
        Root.Children.Add(element);
    }

    private static bool IsInside(object source, DependencyObject target)
    {
        for (var e = source as DependencyObject; e is not null; e = VisualTreeHelper.GetParent(e))
            if (ReferenceEquals(e, target))
                return true;
        return false;
    }
}
```

- [ ] **Step 4: 以完整版取代 `src/TimeZoneCalc.App/MainWindow.xaml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Window
    x:Class="TimeZoneCalc.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="時區計算器">

    <!-- Host 是可取得焦點的外框，讓鍵盤輸入不需要先點任何欄位 -->
    <UserControl x:Name="Host" IsTabStop="True" UseSystemFocusVisuals="False">
        <Grid x:Name="Root" Padding="12" RowSpacing="8">
            <Grid.Resources>
                <Style x:Key="KeyButton" TargetType="Button" BasedOn="{StaticResource DefaultButtonStyle}">
                    <Setter Property="HorizontalAlignment" Value="Stretch" />
                    <Setter Property="VerticalAlignment" Value="Stretch" />
                    <Setter Property="FontSize" Value="20" />
                    <Setter Property="AllowFocusOnInteraction" Value="False" />
                </Style>
            </Grid.Resources>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
                <RowDefinition Height="*" />
            </Grid.RowDefinitions>

            <!-- 大字區：📅 日期段 時間段 -->
            <Grid Grid.Row="0" ColumnSpacing="8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <Button x:Name="CalendarButton" VerticalAlignment="Center" AllowFocusOnInteraction="False" ToolTipService.ToolTip="選擇日期">
                    <FontIcon Glyph="&#xE787;" />
                    <Button.Flyout>
                        <Flyout x:Name="CalendarFlyout" Opening="CalendarFlyout_Opening">
                            <CalendarView x:Name="DatePickerCalendar" SelectionMode="Single" SelectedDatesChanged="Calendar_SelectedDatesChanged" />
                        </Flyout>
                    </Button.Flyout>
                </Button>
                <Border x:Name="DateSegment" Grid.Column="2" Padding="4,0" BorderThickness="0,0,0,3" Background="Transparent" Tapped="DateSegment_Tapped">
                    <TextBlock x:Name="DateText" FontSize="28" FontWeight="SemiBold" VerticalAlignment="Bottom" />
                </Border>
                <Border x:Name="TimeSegment" Grid.Column="3" Padding="4,0" BorderThickness="0,0,0,3" Background="Transparent" Tapped="TimeSegment_Tapped">
                    <TextBlock x:Name="TimeText" FontSize="40" FontWeight="SemiBold" />
                </Border>
            </Grid>

            <InfoBar x:Name="StatusBar" Grid.Row="1" IsClosable="False" IsOpen="False">
                <InfoBar.ActionButton>
                    <Button x:Name="AmbiguityButton" AllowFocusOnInteraction="False" Click="AmbiguityButton_Click" />
                </InfoBar.ActionButton>
            </InfoBar>

            <StackPanel x:Name="RowsPanel" Grid.Row="2" Spacing="2" />

            <HyperlinkButton x:Name="AddRowButton" Grid.Row="3" Content="＋ 新增時區" AllowFocusOnInteraction="False" Click="AddRow_Click" />

            <!-- 數字鍵盤 -->
            <Grid Grid.Row="4" RowSpacing="4" ColumnSpacing="4" MinHeight="260">
                <Grid.RowDefinitions>
                    <RowDefinition Height="*" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="*" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <Button Grid.Row="0" Grid.Column="0" Content="現在" Style="{StaticResource KeyButton}" Click="Now_Click" />
                <Button Grid.Row="0" Grid.Column="1" Content="C" Style="{StaticResource KeyButton}" Click="Clear_Click" />
                <Button Grid.Row="0" Grid.Column="2" Style="{StaticResource KeyButton}" Click="Back_Click">
                    <FontIcon Glyph="&#xE750;" />
                </Button>
                <Button Grid.Row="1" Grid.Column="0" Content="7" Tag="7" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="1" Grid.Column="1" Content="8" Tag="8" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="1" Grid.Column="2" Content="9" Tag="9" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="2" Grid.Column="0" Content="4" Tag="4" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="2" Grid.Column="1" Content="5" Tag="5" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="2" Grid.Column="2" Content="6" Tag="6" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="3" Grid.Column="0" Content="1" Tag="1" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="3" Grid.Column="1" Content="2" Tag="2" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="3" Grid.Column="2" Content="3" Tag="3" Style="{StaticResource KeyButton}" Click="Digit_Click" />
                <Button Grid.Row="4" Grid.Column="1" Content="0" Tag="0" Style="{StaticResource KeyButton}" Click="Digit_Click" />
            </Grid>
        </Grid>
    </UserControl>
</Window>
```

- [ ] **Step 5: 以完整版取代 `src/TimeZoneCalc.App/MainWindow.xaml.cs`**

```csharp
using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TimeZoneCalc.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace TimeZoneCalc;

public sealed partial class MainWindow : Window
{
    private readonly ZoneCatalog _catalog = ZoneCatalog.CreateDefault();
    private readonly CalculatorState _state;
    private readonly List<RowView> _rowViews = [];
    private bool _syncingCalendar;

    public MainWindow()
    {
        InitializeComponent();
        ResizeForDpi(480, 760);
        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop();

        DatePickerCalendar.MinDate = new DateTimeOffset(new DateTime(DigitEntry.MinYear, 1, 1));
        DatePickerCalendar.MaxDate = new DateTimeOffset(new DateTime(DigitEntry.MaxYear, 12, 31));

        _state = new CalculatorState(_catalog, () => DateTimeOffset.UtcNow);
        _state.Changed += (_, _) => Render();
        Host.Loaded += (_, _) => FocusHost();
        Render();
    }

    private void Render()
    {
        DateText.Text = _state.DateEntry.Display;
        TimeText.Text = _state.TimeEntry.Display;
        DateText.Foreground = Theme.Get(_state.Status == InputStatus.InvalidDate
            ? "SystemFillColorCriticalBrush"
            : "TextFillColorPrimaryBrush");
        var accent = Theme.Get("AccentFillColorDefaultBrush");
        DateSegment.BorderBrush = _state.ActiveSegment == EntryMode.Date ? accent : Theme.Transparent;
        TimeSegment.BorderBrush = _state.ActiveSegment == EntryMode.Time ? accent : Theme.Transparent;

        if (_rowViews.Count != _state.Rows.Count)
            RebuildRows();
        for (var i = 0; i < _rowViews.Count; i++)
            _rowViews[i].Update(_state.Rows[i], i == _state.ActiveIndex, _state.CanRemoveRow);
        AddRowButton.Visibility = _state.CanAddRow ? Visibility.Visible : Visibility.Collapsed;

        RenderStatus();
    }

    private void RebuildRows()
    {
        RowsPanel.Children.Clear();
        _rowViews.Clear();
        for (var i = 0; i < _state.Rows.Count; i++)
        {
            var view = new RowView(i, _catalog, _state, CopyRow, FocusHost);
            _rowViews.Add(view);
            RowsPanel.Children.Add(view.Root);
        }
    }

    private void RenderStatus()
    {
        AmbiguityButton.Visibility = Visibility.Collapsed;
        switch (_state.Status)
        {
            case InputStatus.InvalidDate:
                ShowStatus(InfoBarSeverity.Error, $"日期不存在或超出範圍（{DigitEntry.MinYear}–{DigitEntry.MaxYear}）");
                break;
            case InputStatus.NonexistentTime:
                ShowStatus(InfoBarSeverity.Warning, "此時間因夏令時間切換而不存在");
                break;
            case InputStatus.AmbiguousTime:
            {
                var first = _state.Ambiguity == AmbiguityChoice.First;
                ShowStatus(InfoBarSeverity.Informational, first
                    ? "此時間出現兩次，目前採用第一次出現（夏令時間）"
                    : "此時間出現兩次，目前採用第二次出現（標準時間）");
                AmbiguityButton.Content = first ? "改用第二次" : "改用第一次";
                AmbiguityButton.Visibility = Visibility.Visible;
                break;
            }
            default:
                StatusBar.IsOpen = false;
                break;
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    private void CopyRow(int index)
    {
        var row = _state.Rows[index];
        if (!row.CanCopy)
        {
            _rowViews[index].Flash("沒有可複製的時間");
            return;
        }
        try
        {
            var package = new DataPackage();
            package.SetText(row.Text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            _rowViews[index].Flash("已複製");
        }
        catch (Exception)
        {
            _rowViews[index].Flash("複製失敗");
        }
    }

    private void FocusHost() => Host.Focus(FocusState.Programmatic);

    private void Digit_Click(object sender, RoutedEventArgs e) =>
        _state.PushDigit(int.Parse((string)((Button)sender).Tag));

    private void Now_Click(object sender, RoutedEventArgs e) => _state.SetNow();

    private void Clear_Click(object sender, RoutedEventArgs e) => _state.Clear();

    private void Back_Click(object sender, RoutedEventArgs e) => _state.Backspace();

    private void AddRow_Click(object sender, RoutedEventArgs e) => _state.AddRow();

    private void AmbiguityButton_Click(object sender, RoutedEventArgs e) => _state.ToggleAmbiguity();

    private void DateSegment_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _state.SetSegment(EntryMode.Date);
        FocusHost();
    }

    private void TimeSegment_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _state.SetSegment(EntryMode.Time);
        FocusHost();
    }

    private void CalendarFlyout_Opening(object sender, object e)
    {
        _syncingCalendar = true;
        DatePickerCalendar.SelectedDates.Clear();
        if (_state.DateEntry.Date is { } date)
        {
            var value = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
            DatePickerCalendar.SelectedDates.Add(value);
            DatePickerCalendar.SetDisplayDate(value);
        }
        _syncingCalendar = false;
    }

    private void Calendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
    {
        if (_syncingCalendar || args.AddedDates.Count == 0)
            return;
        _state.SetDate(DateOnly.FromDateTime(args.AddedDates[0].Date));
        CalendarFlyout.Hide();
        FocusHost();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void ResizeForDpi(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }
}
```

- [ ] **Step 6: 建立 `tools/capture-window.ps1`（純 ASCII）**

```powershell
# Capture a window to PNG even when another window covers it (PrintWindow + PW_RENDERFULLCONTENT).
# Usage (PowerShell): powershell -NoProfile -ExecutionPolicy Bypass -File tools\capture-window.ps1 -ProcessId 1234 -OutFile shot.png
param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$OutFile
)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Capture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
}
"@
$p = Get-Process -Id $ProcessId
$p.Refresh()
$hwnd = $p.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { throw "Process $ProcessId has no main window yet" }
$r = New-Object Win32Capture+RECT
[void][Win32Capture]::GetWindowRect($hwnd, [ref]$r)
$bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[void][Win32Capture]::PrintWindow($hwnd, $hdc, 2)
$g.ReleaseHdc($hdc)
$g.Dispose()
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
```

- [ ] **Step 7: 建置**

Run: `dotnet build src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -p:Platform=x64`
Expected: 建置成功，0 個錯誤。

- [ ] **Step 8: 啟動並擷取畫面（PowerShell）**

```powershell
$exe = (Get-ChildItem src\TimeZoneCalc.App\bin -Recurse -Filter TimeZoneCalc.exe | Select-Object -First 1).FullName
$p = Start-Process -FilePath $exe -PassThru; Start-Sleep -Seconds 5; $p.HasExited
powershell -NoProfile -ExecutionPolicy Bypass -File tools\capture-window.ps1 -ProcessId $p.Id -OutFile "$env:TEMP\tzc-task7.png"
Stop-Process -Id $p.Id
```

Expected: `HasExited` 為 `False`。用 Read 工具打開 `%TEMP%\tzc-task7.png`，確認：
- 大字區顯示今天日期和目前 UTC 時間，時間段有藍色底線
- 兩列：UTC（左側藍條）、台北 (Asia/Taipei)，台北列下方顯示 `UTC+08:00`，時間比 UTC 多 8 小時
- 「＋ 新增時區」連結
- 鍵盤：現在、C、⌫、7–9、4–6、1–3、0

不符合就修正後重跑本步驟。

- [ ] **Step 9: 請使用者用滑鼠快速試用（需使用者）**

請使用者執行 Step 8 的 `$exe`，用滑鼠確認：點數字鍵會改時間、點台北列會切換藍條、📅 能選日期、⧉ 能複製、＋／✕ 能新增或刪除列、在時區框打 `new` 能找到紐約。**等使用者回報**，有問題先修。

- [ ] **Step 10: Commit**

```
git add src\TimeZoneCalc.App tools\capture-window.ps1
git commit -m "feat(app): calculator-style main window with zone rows, keypad and calendar" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: 實體鍵盤輸入

**Files:**
- Modify: `src/TimeZoneCalc.App/MainWindow.xaml`（`Host` 加 `PreviewKeyDown`）
- Modify: `src/TimeZoneCalc.App/MainWindow.xaml.cs`（新增 `Host_PreviewKeyDown`）

**Interfaces:**
- Consumes: `CalculatorState.PushDigit / Backspace / Clear / ToggleSegment / MoveActive / SetNow / ActiveIndex`、`MainWindow.CopyRow`（Task 7）
- Produces: 鍵盤對應表：`0`–`9`／數字鍵區 → 輸入；`Backspace` → ⌫；`Esc` → C；`Tab` → 切換日期／時間段；`↑`／`↓` → 換輸入列；`N` → 現在；`Ctrl+C` → 複製輸入列。焦點在文字框（時區篩選）時全部不攔截。

- [ ] **Step 1: 在 `MainWindow.xaml` 的 `Host` 加上事件**

把

```xml
    <UserControl x:Name="Host" IsTabStop="True" UseSystemFocusVisuals="False">
```

改成

```xml
    <UserControl x:Name="Host" IsTabStop="True" UseSystemFocusVisuals="False" PreviewKeyDown="Host_PreviewKeyDown">
```

- [ ] **Step 2: 在 `MainWindow.xaml.cs` 加 using 與處理函式**

檔案開頭 using 區加上：

```csharp
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;
```

在 `FocusHost()` 下方加入：

```csharp
    private void Host_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // 在時區篩選框打字時不攔截，讓文字框自己處理
        if (FocusManager.GetFocusedElement(Host.XamlRoot) is TextBox)
            return;

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        e.Handled = true;
        switch (e.Key)
        {
            case >= VirtualKey.Number0 and <= VirtualKey.Number9 when !ctrl:
                _state.PushDigit(e.Key - VirtualKey.Number0);
                break;
            case >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9:
                _state.PushDigit(e.Key - VirtualKey.NumberPad0);
                break;
            case VirtualKey.Back:
                _state.Backspace();
                break;
            case VirtualKey.Escape:
                _state.Clear();
                break;
            case VirtualKey.Tab:
                _state.ToggleSegment();
                break;
            case VirtualKey.Up:
                _state.MoveActive(-1);
                break;
            case VirtualKey.Down:
                _state.MoveActive(1);
                break;
            case VirtualKey.N when !ctrl:
                _state.SetNow();
                break;
            case VirtualKey.C when ctrl:
                CopyRow(_state.ActiveIndex);
                break;
            default:
                e.Handled = false;
                break;
        }
    }
```

- [ ] **Step 3: 建置**

Run: `dotnet build src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -p:Platform=x64`
Expected: 建置成功，0 個錯誤。

- [ ] **Step 4: 啟動檢查（PowerShell）**

```powershell
$exe = (Get-ChildItem src\TimeZoneCalc.App\bin -Recurse -Filter TimeZoneCalc.exe | Select-Object -First 1).FullName
$p = Start-Process -FilePath $exe -PassThru; Start-Sleep -Seconds 5; $p.HasExited; Stop-Process -Id $p.Id
```

Expected: `False`。

- [ ] **Step 5: 請使用者用鍵盤試用（需使用者）**

請使用者開啟程式後不點任何東西，直接依序試：打 `143205` → `Tab` → 打 `20261231` → `↓` → `Ctrl+C`（貼到記事本應為 `2026-12-31 22:32:05`）→ `Esc` → `N` → `Backspace`；再點時區框打 `tai`，確認數字與 `N` 會輸入到框裡、不會被計算器攔走。**等使用者回報**，有問題先修。

- [ ] **Step 6: Commit**

```
git add src\TimeZoneCalc.App\MainWindow.xaml src\TimeZoneCalc.App\MainWindow.xaml.cs
git commit -m "feat(app): physical keyboard input" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: 發佈腳本、README、手動檢查清單、最終驗證

**Files:**
- Create: `build/publish.cmd`
- Create: `README.md`
- Create: `docs/manual-test-checklist.md`

**Interfaces:**
- Consumes: 全部前面的 Task
- Produces: `publish\TimeZoneCalc\TimeZoneCalc.exe` 與 `publish\TimeZoneCalc-win-x64.zip`

- [ ] **Step 1: 建立 `build/publish.cmd`（純 ASCII）**

```bat
@echo off
REM Build the portable folder publish\TimeZoneCalc and zip it. Run from cmd.exe.
cd /d "%~dp0.."
dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj
if errorlevel 1 exit /b 1
if exist publish\TimeZoneCalc rmdir /s /q publish\TimeZoneCalc
dotnet publish src\TimeZoneCalc.App\TimeZoneCalc.App.csproj -c Release -p:Platform=x64 -o publish\TimeZoneCalc
if errorlevel 1 exit /b 1
powershell -NoProfile -Command "Compress-Archive -Path publish\TimeZoneCalc -DestinationPath publish\TimeZoneCalc-win-x64.zip -Force"
if errorlevel 1 exit /b 1
echo Done: publish\TimeZoneCalc-win-x64.zip
```

- [ ] **Step 2: 執行發佈並量大小**

Run: `build\publish.cmd`
Expected: 測試全過，最後印出 `Done: publish\TimeZoneCalc-win-x64.zip`。

PowerShell 量大小：

```powershell
"{0:N0} MB" -f ((Get-ChildItem publish\TimeZoneCalc -Recurse | Measure-Object Length -Sum).Sum / 1MB)
"{0:N0} MB" -f ((Get-Item publish\TimeZoneCalc-win-x64.zip).Length / 1MB)
```

記下兩個數字填進 README。

- [ ] **Step 3: 建立 `README.md`**

把 Step 2 量到的數字、Task 1 Step 3 記下的套件版本填進對應位置（下方 `約 N MB` 與版本欄位必須換成實際數字後才 commit）。

```markdown
# 時區計算器

查 log 時換算時間用：在任一時區列輸入日期時間，其他時區即時換算。外觀與操作仿 Windows 11 小算盤。

> 本文件中的指令以 **cmd.exe** 為準。

## 使用（免安裝）

1. 把 `TimeZoneCalc-win-x64.zip` 解壓到任意資料夾（例如桌面）。
2. 雙擊資料夾內的 `TimeZoneCalc.exe`。

- 支援 Windows 10 1809 以上、Windows 11（x64）。
- 不需要管理者權限，不需要先安裝 .NET 或 Windows App SDK。
- 解壓後資料夾約 N MB，zip 約 N MB。
- 程式崩潰時會寫 `%TEMP%\TimeZoneCalc-crash.txt`。

## 操作

| 按鍵 | 功能 |
|---|---|
| `0`–`9`（含數字鍵區） | 由左往右填入：時間 `HHmmss`、日期 `yyyyMMdd` |
| `Backspace` | 刪最後一位 |
| `Esc` | 清除（時間 → 00:00:00，日期 → 今天） |
| `Tab` | 切換日期段／時間段 |
| `↑` / `↓` | 換輸入列 |
| `N` | 現在 |
| `Ctrl+C` | 複製輸入列（`yyyy-MM-dd HH:mm:ss`） |

- 點任一列的空白處，那一列就成為輸入列（左側藍條）。
- 時區框可打字篩選，例如 `tai`、`new york`、`+05:45`。
- 夏令時間切換造成的「不存在時間」與「重複時間」會在上方提示。

## 建置

需要 .NET 9 SDK。

    build\publish.cmd

產出 `publish\TimeZoneCalc\` 與 `publish\TimeZoneCalc-win-x64.zip`。

只跑測試：

    dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj

| 套件 | 版本 |
|---|---|
| Microsoft.WindowsAppSDK | x.y.z |
| Microsoft.Windows.SDK.BuildTools | x.y.z |
```

- [ ] **Step 4: 建立 `docs/manual-test-checklist.md`**

```markdown
# 手動檢查清單

每次發佈前，用 `publish\TimeZoneCalc\TimeZoneCalc.exe` 逐項確認。

## 啟動
- [ ] 開啟即顯示兩列：UTC（藍條）、台北 (Asia/Taipei)
- [ ] 時間為現在的 UTC 時間，台北列多 8 小時，標示 `UTC+08:00`

## 輸入
- [ ] 不點任何東西直接打 `143205` → 大字 `14:32:05`
- [ ] 打 `1432` → `14:32:00`
- [ ] 小時十位數打 `3` 沒反應；`2` 之後打 `4` 沒反應
- [ ] `Tab` 切到日期段（底線移動），打 `20261231` → 日期更新
- [ ] 日期打 `20260231` → 日期標紅、上方紅色提示、其他列變灰但保留原值
- [ ] 日期只打 `2026` 就點台北列 → 載入上一個有效時間，不卡住
- [ ] `Backspace`、`Esc`、`N` 與畫面上 ⌫、C、現在 行為一致
- [ ] 📅 開月曆，選日期後月曆關閉、日期更新

## 時區列
- [ ] 點台北列 → 藍條移過去，大字顯示台北時間；再打數字，UTC 列跟著換算
- [ ] `↑`／`↓` 切換輸入列
- [ ] 點時區框打 `tai`、`new york`、`+05:45` 都找得到；打字時數字和 `N` 不會被計算器攔走
- [ ] 改其他列的時區 → 同一瞬間換算；改輸入列的時區 → 保留打好的時間
- [ ] ＋ 新增到 5 列後連結消失；✕ 刪到剩 1 列後 ✕ 消失
- [ ] 刪除輸入列 → 第一列變成輸入列，時間不變

## 夏令時間
- [ ] 輸入列設紐約，輸入 `20260308` `023000` → 黃色提示「不存在」，其他列 `—`
- [ ] 輸入列設紐約，輸入 `20261101` `013000` → 提示「出現兩次」，按「改用第二次」後其他列 +1 小時
- [ ] UTC 輸入 `20261101` `063000`，第二列設紐約（顯示 01:30 `UTC-05:00`），點紐約列 → UTC 列仍是 06:30

## 複製
- [ ] ⧉ 複製後顯示「已複製」，貼上為 `yyyy-MM-dd HH:mm:ss`
- [ ] `Ctrl+C` 複製輸入列
- [ ] 不存在的時間按 ⧉ → 顯示「沒有可複製的時間」

## 可攜性
- [ ] 在沒有開發工具的 Win10 或 Win11 電腦解壓 zip、雙擊執行，以上項目抽測正常
```

- [ ] **Step 5: 使用者跑完檢查清單（需使用者）**

請使用者照 `docs/manual-test-checklist.md` 逐項確認，包括最後一項「可攜性」（把 `publish\TimeZoneCalc-win-x64.zip` 帶到別台電腦）。**等使用者回報**；有失敗項目先修，修完重跑 `build\publish.cmd` 與相關項目。

- [ ] **Step 6: Commit**

```
git add build\publish.cmd README.md docs\manual-test-checklist.md
git commit -m "docs: publish script, README and manual test checklist" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
