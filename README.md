# 時區計算器

查 log 時換算時間用：兩列時區互相換算，在任一列輸入日期時間，另一列即時顯示同一時刻。外觀與操作仿 Windows 11 小算盤的「程式設計人員」模式。

> 本文件中的指令以 **cmd.exe** 為準。

## 使用（免安裝）

提供兩種版本，功能完全相同，擇一使用：

| 版本 | 檔案 | 大小 | 說明 |
|---|---|---|---|
| **單一 exe**（建議） | `TimeZoneCalc.exe` | 約 219 MB（zip 約 83 MB） | 只有一個檔案，複製到任何位置雙擊即可。第一次執行時會把內含的執行環境解壓到 `%TEMP%\.net\TimeZoneCalc`（約 210 MB）；清掉暫存資料夾也沒關係，下次會再解壓 |
| **資料夾版** | `TimeZoneCalc` 資料夾（約 515 個檔案） | 約 226 MB（zip 約 88 MB） | 整個資料夾一起帶走，雙擊裡面的 `TimeZoneCalc.exe`。不需要解壓到暫存資料夾；公司政策禁止從 `%TEMP%` 執行程式時改用這個 |

- 支援 Windows 10 1809 以上、Windows 11（x64）。
- 不需要管理者權限，不需要先安裝 .NET 或 Windows App SDK。
- 程式沒有數位簽章，第一次執行若出現「Windows 已保護您的電腦」，按「其他資訊」→「仍要執行」。
- 程式崩潰時會寫 `%TEMP%\TimeZoneCalc-crash.txt`。

## 操作

| 按鍵 | 功能 |
|---|---|
| `0`–`9`（含數字鍵區） | 輸入目前欄位；打滿自動跳下一欄（從年開始連打 `20260924143205` 可一次填完） |
| `Backspace` | 刪目前欄位最後一位 |
| `Esc` | 清空目前欄位 |
| `Tab` / `→` | 下一欄 |
| `Shift+Tab` / `←` | 上一欄 |
| `↑` / `↓` | 換輸入列 |
| `+` / `−` | 在最後面加一列 / 移除最後一列（2–5 列） |
| `N` | 現在 |
| `Ctrl+C` | 有框選文字時複製框選內容；沒有時複製輸入列（`yyyy-MM-dd HH:mm:ss`） |

- 點某一列的年、月、日、時、分或秒，那一列就成為輸入列（左側藍條），並切到那一欄（底線）。
- 超過上限直接帶入上限：月 12、日依當月天數（2 月看閏年）、時 23、分秒 59。
- 用滑鼠拖曳框選日期時間文字，再按 `Ctrl+C` 複製。
- 點時區按鈕打開搜尋面板，可打字篩選，例如 `tai`、`紐約`、`new york`、`+07:00`；按 Enter 或點選即套用，Esc 取消。
- 夏令時間切換造成的「不存在時間」與「重複時間」會在上方提示。

## 建置

需要 .NET 9 SDK。

```
build\publish.cmd
```

一次產出兩種版本：

| 版本 | 位置 | 傳檔用 zip |
|---|---|---|
| 單一 exe | `publish\single\TimeZoneCalc.exe` | `publish\TimeZoneCalc-single-win-x64.zip` |
| 資料夾版 | `publish\folder\TimeZoneCalc\` | `publish\TimeZoneCalc-folder-win-x64.zip` |

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
