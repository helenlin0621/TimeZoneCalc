using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TimeZoneCalc.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;
using Windows.UI.Core;

namespace TimeZoneCalc;

public sealed partial class MainWindow : Window
{
    private readonly ZoneCatalog _catalog = ZoneCatalog.CreateDefault();
    private readonly CalculatorState _state;
    private readonly List<RowView> _rowViews = [];

    public MainWindow()
    {
        InitializeComponent();
        ResizeForDpi(520, 720);
        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop();

        _state = new CalculatorState(_catalog, () => DateTimeOffset.UtcNow);
        SyncRowViews();
        _state.Changed += (_, _) => Render();
        Host.Loaded += (_, _) => FocusXamlIsland();
        Activated += (_, e) =>
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                FocusXamlIsland();
        };
        Render();
    }

    // 視窗被程式啟用（非滑鼠點擊）時，Win32 焦點會停在最外層視窗，
    // 鍵盤訊息送不進 XAML；把焦點交給 XAML 所在的子視窗
    private void FocusXamlIsland()
    {
        if (Host.XamlRoot is null)
            return;   // 還沒載入完成；Host.Loaded 會處理
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var bridge = FindWindowEx(hwnd, IntPtr.Zero, "Microsoft.UI.Content.DesktopChildSiteBridge", null);
        var site = bridge == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(bridge, IntPtr.Zero, "InputSiteWindowClass", null);
        if (site != IntPtr.Zero && GetFocus() == hwnd)
            SetFocus(site);
        FocusHost();
    }

    private void Render()
    {
        var added = SyncRowViews();
        for (var i = 0; i < _rowViews.Count; i++)
            _rowViews[i].Update(_state.Rows[i], i == _state.ActiveIndex, _state.ActiveField);
        AddRowButton.IsEnabled = _state.CanAddRow;
        RemoveRowButton.IsEnabled = _state.CanRemoveRow;
        if (added != 0)
            GrowWindow(added);
        RenderStatus();
    }

    // 列只會從最後面加減；回傳這次多了（正）或少了（負）幾列
    private int SyncRowViews()
    {
        var delta = _state.Rows.Count - _rowViews.Count;
        while (_rowViews.Count < _state.Rows.Count)
        {
            var view = new RowView(_rowViews.Count, _catalog, _state, FocusHost);
            _rowViews.Add(view);
            RowsPanel.Children.Add(view.Root);
        }
        while (_rowViews.Count > _state.Rows.Count)
        {
            _rowViews.RemoveAt(_rowViews.Count - 1);
            RowsPanel.Children.RemoveAt(RowsPanel.Children.Count - 1);
        }
        return delta;
    }

    // 每加減一列，視窗高度跟著加減一列的高度；最多長到螢幕可用高度
    private void GrowWindow(int rows)
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var rowHeight = (_rowViews[0].Root.ActualHeight > 0 ? _rowViews[0].Root.ActualHeight : 110) + RowsPanel.Spacing;
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var height = Math.Min(AppWindow.Size.Height + (int)(rows * rowHeight * scale), workArea.Height);
        AppWindow.Resize(new SizeInt32(AppWindow.Size.Width, Math.Max(height, (int)(720 * scale))));
        var position = AppWindow.Position;
        if (position.Y + height > workArea.Y + workArea.Height)
            AppWindow.Move(new PointInt32(position.X, Math.Max(workArea.Y, workArea.Y + workArea.Height - height)));
    }

    private void RenderStatus()
    {
        AmbiguityButton.Visibility = Visibility.Collapsed;
        switch (_state.Status)
        {
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

    // 沒有框選文字時 Ctrl+C 複製輸入列的完整時間
    private void CopyActiveRow()
    {
        var row = _state.ActiveRow;
        if (!row.CanCopy)
            return;
        try
        {
            var package = new DataPackage();
            package.SetText(row.Text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }
        catch (Exception)
        {
            ShowStatus(InfoBarSeverity.Warning, "複製失敗");
        }
    }

    private void FocusHost() => Host.Focus(FocusState.Programmatic);

    private void Host_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement(Host.XamlRoot);
        if (focused is TextBox)
            return;

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(CoreVirtualKeyStates.Down);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
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
            case VirtualKey.Tab when shift:
            case VirtualKey.Left:
                _state.PreviousField();
                break;
            case VirtualKey.Tab:
            case VirtualKey.Right:
                _state.NextField();
                break;
            case VirtualKey.Up:
                _state.MoveActive(-1);
                break;
            case VirtualKey.Down:
                _state.MoveActive(1);
                break;
            case VirtualKey.Add:
            case OemPlus when shift:
                _state.AddRow();
                break;
            case VirtualKey.Subtract:
            case OemMinus:
                _state.RemoveLastRow();
                break;
            case VirtualKey.N when !ctrl:
                _state.SetNow();
                break;
            case VirtualKey.C when ctrl:
                // 有框選文字就讓文字自己複製（小算盤模式）
                if (focused is RichTextBlock { SelectedText.Length: > 0 })
                    e.Handled = false;
                else
                    CopyActiveRow();
                break;
            default:
                e.Handled = false;
                break;
        }
    }

    private void Digit_Click(object sender, RoutedEventArgs e) =>
        _state.PushDigit(int.Parse((string)((Button)sender).Tag));

    private void Now_Click(object sender, RoutedEventArgs e) => _state.SetNow();

    private void Clear_Click(object sender, RoutedEventArgs e) => _state.Clear();

    private void Back_Click(object sender, RoutedEventArgs e) => _state.Backspace();

    private void AmbiguityButton_Click(object sender, RoutedEventArgs e) => _state.ToggleAmbiguity();

    private void AddRow_Click(object sender, RoutedEventArgs e) => _state.AddRow();

    private void RemoveRow_Click(object sender, RoutedEventArgs e) => _state.RemoveLastRow();

    // 主鍵盤上的 =/+ 與 -/_ 鍵
    private const VirtualKey OemPlus = (VirtualKey)0xBB;
    private const VirtualKey OemMinus = (VirtualKey)0xBD;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hwnd);

    private void ResizeForDpi(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(width * scale), (int)(height * scale)));
    }
}
