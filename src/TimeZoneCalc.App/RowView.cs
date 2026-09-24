using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using TimeZoneCalc.Core;
using Windows.System;
using Windows.UI.Text;

namespace TimeZoneCalc;

// 一列時區：藍條、時區按鈕（點開搜尋面板）、可直接輸入與框選的日期時間、實際偏移量
internal sealed class RowView
{
    private const string FallbackNote = "找不到系統的台北時區，改用固定 UTC+08:00";

    private readonly Border _bar = new() { Width = 4, CornerRadius = new CornerRadius(2) };
    private readonly TextBlock _zoneLabel = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _zoneButton;
    private readonly Flyout _flyout = new() { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft };
    private readonly TextBox _search = new() { PlaceholderText = "搜尋時區，例如 tai、紐約、+07:00" };
    private readonly ListView _list = new() { Height = 320, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.None };
    private readonly RichTextBlock _value = new() { FontSize = 30, FontWeight = FontWeights.SemiBold, FontFamily = new FontFamily("Cascadia Mono, Consolas") };
    private readonly Run _date = new();
    private readonly Run _time = new();
    private readonly TextBlock _offset = new() { FontSize = 12 };
    private readonly TextBlock _note = new() { FontSize = 12, Visibility = Visibility.Collapsed };
    private ZoneOption? _zone;

    public RowView(int index, ZoneCatalog catalog, CalculatorState state, Action returnFocus)
    {
        // 時區按鈕：本身不拿焦點，點開後在面板裡搜尋
        var buttonContent = new Grid { ColumnSpacing = 8 };
        buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var chevron = new FontIcon { Glyph = "", FontSize = 12 };
        Grid.SetColumn(chevron, 1);
        buttonContent.Children.Add(_zoneLabel);
        buttonContent.Children.Add(chevron);
        _zoneButton = new Button
        {
            Content = buttonContent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            AllowFocusOnInteraction = false,
            Flyout = _flyout,
        };

        var panel = new StackPanel { Width = 400, Spacing = 8 };
        panel.Children.Add(_search);
        panel.Children.Add(_list);
        _flyout.Content = panel;
        _flyout.Opened += (_, _) =>
        {
            _search.Text = "";
            _list.ItemsSource = catalog.All;
            _search.Focus(FocusState.Programmatic);
        };
        _flyout.Closed += (_, _) => returnFocus();
        _search.TextChanged += (_, _) => _list.ItemsSource = catalog.Filter(_search.Text);
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                if (catalog.Match(_search.Text) is { } zone)
                    Choose(zone);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down && _list.ContainerFromIndex(0) is ListViewItem first)
            {
                first.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };
        _list.ItemClick += (_, e) => Choose((ZoneOption)e.ClickedItem);

        void Choose(ZoneOption zone)
        {
            state.SetZone(index, zone);
            _flyout.Hide();
        }

        // 日期時間：點日期或時間就在這一列輸入；拖曳可框選後 Ctrl+C 複製
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(_date);
        paragraph.Inlines.Add(new Run { Text = " " });
        paragraph.Inlines.Add(_time);
        _value.Blocks.Add(paragraph);
        _value.Tapped += (_, e) =>
        {
            var pos = _value.GetPositionFromPoint(e.GetPosition(_value));
            var segment = pos is not null && pos.Offset < _time.ContentStart.Offset ? EntryMode.Date : EntryMode.Time;
            state.SetActiveRow(index);
            state.SetSegment(segment);
            e.Handled = true;
        };

        var sub = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        sub.Children.Add(_offset);
        sub.Children.Add(_note);

        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(_zoneButton);
        content.Children.Add(_value);
        content.Children.Add(sub);

        Root = new Grid { ColumnSpacing = 12, Background = Theme.Transparent };
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        // 內容欄最多 460 寬、靠左；視窗放大時時區按鈕不會跑到中間
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 460 });
        Grid.SetColumn(content, 1);
        Root.Children.Add(_bar);
        Root.Children.Add(content);

        // 點列的空白處也會選為輸入列
        Root.Tapped += (_, _) =>
        {
            state.SetActiveRow(index);
            returnFocus();
        };
    }

    public Grid Root { get; }

    public void Update(ZoneRow row, bool isActive, EntryMode segment, bool dateInvalid)
    {
        if (!ReferenceEquals(_zone, row.Zone))
        {
            _zone = row.Zone;
            _zoneLabel.Text = row.Zone.DisplayName;
            ToolTipService.SetToolTip(_zoneButton, row.Zone.DisplayName);
        }
        _bar.Background = isActive ? Theme.Get("AccentFillColorDefaultBrush") : Theme.Transparent;

        var parts = row.Text.Split(' ', 2);
        _date.Text = parts[0];
        _time.Text = parts.Length > 1 ? parts[1] : "";

        var normal = Theme.Get(row.IsStale ? "TextFillColorDisabledBrush" : "TextFillColorPrimaryBrush");
        var accent = Theme.Get("AccentTextFillColorPrimaryBrush");
        _value.Foreground = normal;
        _date.Foreground = isActive && dateInvalid ? Theme.Get("SystemFillColorCriticalBrush")
            : isActive && segment == EntryMode.Date ? accent : normal;
        _time.Foreground = isActive && segment == EntryMode.Time ? accent : normal;
        _date.TextDecorations = isActive && segment == EntryMode.Date ? TextDecorations.Underline : TextDecorations.None;
        _time.TextDecorations = isActive && segment == EntryMode.Time ? TextDecorations.Underline : TextDecorations.None;

        _offset.Text = row.OffsetLabel;
        _offset.Foreground = Theme.Get("TextFillColorSecondaryBrush");
        _note.Text = row.Zone.IsFallback ? FallbackNote : "";
        _note.Visibility = row.Zone.IsFallback ? Visibility.Visible : Visibility.Collapsed;
    }
}
