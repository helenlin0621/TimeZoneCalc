using System.Runtime.InteropServices;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
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
    private bool _syncingCalendar;

    public MainWindow()
    {
        InitializeComponent();
        ResizeForDpi(560, 760);
        if (MicaController.IsSupported())
            SystemBackdrop = new MicaBackdrop();

        DatePickerCalendar.MinDate = new DateTimeOffset(new DateTime(DigitEntry.MinYear, 1, 1));
        DatePickerCalendar.MaxDate = new DateTimeOffset(new DateTime(DigitEntry.MaxYear, 12, 31));

        _state = new CalculatorState(_catalog, () => DateTimeOffset.UtcNow);
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
