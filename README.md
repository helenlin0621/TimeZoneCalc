# 時區計算器

查 log 時換算時間用：在任一時區列輸入日期時間，其他時區即時換算。外觀與操作仿 Windows 11 小算盤。

> 本文件中的指令以 **cmd.exe** 為準。

## 使用（免安裝）

1. 把 `TimeZoneCalc-win-x64.zip` 解壓到任意資料夾（例如桌面）。
2. 雙擊資料夾內的 `TimeZoneCalc.exe`。

- 支援 Windows 10 1809 以上、Windows 11（x64）。
- 不需要管理者權限，不需要先安裝 .NET 或 Windows App SDK。
- 解壓後資料夾約 226 MB，zip 約 88 MB。
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

```
build\publish.cmd
```

產出 `publish\TimeZoneCalc\` 與 `publish\TimeZoneCalc-win-x64.zip`。

只跑測試：

```
dotnet test tests\TimeZoneCalc.Core.Tests\TimeZoneCalc.Core.Tests.csproj
```

| 套件 | 版本 |
|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 |

### 建置注意事項

- `EnableMsixTooling` 必須是 `true`。設成 `false` 時 `dotnet publish` 不會輸出 `TimeZoneCalc.pri`，發佈出來的 exe 一開啟就因 `XamlParseException` 崩潰。`WindowsPackageType=None` 讓它仍然是免安裝版，不會產生 MSIX。
- 壓 zip 用 `tar` 而不是 PowerShell 的 `Compress-Archive`：系統上有程序會鎖住發佈資料夾裡的某個 dll，`Compress-Archive` 因此失敗。

## 專案結構

| 路徑 | 內容 |
|---|---|
| `src\TimeZoneCalc.Core` | 所有邏輯（時區清單、換算、數字輸入、計算器狀態），不含 UI |
| `tests\TimeZoneCalc.Core.Tests` | Core 的 xUnit 測試 |
| `src\TimeZoneCalc.App` | WinUI 3 畫面 |
| `tools\capture-window.ps1` | 擷取視窗畫面（開發時目視檢查用，PowerShell） |
| `docs\manual-test-checklist.md` | 發佈前的手動檢查清單 |
