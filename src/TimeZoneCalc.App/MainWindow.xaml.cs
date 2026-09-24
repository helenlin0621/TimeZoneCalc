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
        ResizeForDpi(560, 760);
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
