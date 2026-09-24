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
    private readonly AutoSuggestBox _zoneBox = new() { Width = 180, VerticalAlignment = VerticalAlignment.Center, PlaceholderText = "搜尋時區" };
    private readonly TextBlock _value = new() { FontSize = 18, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
    private readonly TextBlock _offset = new() { FontSize = 12 };
    private readonly TextBlock _note = new() { FontSize = 12, Visibility = Visibility.Collapsed };
    private readonly Button _copy = new() { AllowFocusOnInteraction = false, VerticalAlignment = VerticalAlignment.Center, Content = new FontIcon { Glyph = "", FontSize = 14 } };
    private readonly Button _remove = new() { AllowFocusOnInteraction = false, VerticalAlignment = VerticalAlignment.Center, Content = new FontIcon { Glyph = "", FontSize = 12 } };
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
