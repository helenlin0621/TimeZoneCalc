using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
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
    // 年月日時分秒各一個 Run，中間夾分隔符號
    private readonly Run[] _fields = [new(), new(), new(), new(), new(), new()];
    private readonly Run[] _separators = [new(), new(), new(), new(), new()];
    private static readonly string[] SeparatorText = ["-", "-", " ", ":", ":"];
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

        // 日期時間：點年月日時分秒任一欄就在這一列輸入那一欄；拖曳可框選後 Ctrl+C 複製
        var paragraph = new Paragraph();
        for (var i = 0; i < _fields.Length; i++)
        {
            paragraph.Inlines.Add(_fields[i]);
            if (i < _separators.Length)
                paragraph.Inlines.Add(_separators[i]);
        }
        _value.Blocks.Add(paragraph);
        // 可框選的 RichTextBlock 會自己把點擊標成已處理，一般的 Tapped += 收不到，
        // 要用 handledEventsToo 才拿得到
        _value.AddHandler(UIElement.TappedEvent, new TappedEventHandler((_, e) =>
        {
            state.SetActiveRow(index);
            state.SelectField(FieldAtX(e.GetPosition(_value).X));
            e.Handled = true;
        }), handledEventsToo: true);

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

    // 點在某欄或它後面的分隔符號上，都算那一欄
    private Field FieldAtX(double x)
    {
        var field = Field.Year;
        for (var i = 1; i < _fields.Length; i++)
            if (x >= FieldStartX(_fields[i]))
                field = (Field)i;
        return field;
    }

    private static double FieldStartX(Run run) => run.ContentStart.GetCharacterRect(LogicalDirection.Forward).X;

    public void Update(ZoneRow row, bool isActive, Field activeField)
    {
        if (!ReferenceEquals(_zone, row.Zone))
        {
            _zone = row.Zone;
            _zoneLabel.Text = row.Zone.DisplayName;
            ToolTipService.SetToolTip(_zoneButton, row.Zone.DisplayName);
        }
        _bar.Background = isActive ? Theme.Get("AccentFillColorDefaultBrush") : Theme.Transparent;

        // Text 是固定格式 "yyyy-MM-dd HH:mm:ss"（輸入中可能含 "_"）；不存在的時間是 "—"
        var blank = row.Text == CalculatorState.Blank;
        int[] starts = [0, 5, 8, 11, 14, 17];
        for (var i = 0; i < _fields.Length; i++)
            _fields[i].Text = blank ? (i == 0 ? row.Text : "") : row.Text.Substring(starts[i], i == 0 ? 4 : 2);
        for (var i = 0; i < _separators.Length; i++)
            _separators[i].Text = blank ? "" : SeparatorText[i];

        var normal = Theme.Get(row.IsStale ? "TextFillColorDisabledBrush" : "TextFillColorPrimaryBrush");
        var accent = Theme.Get("AccentTextFillColorPrimaryBrush");
        _value.Foreground = normal;
        for (var i = 0; i < _fields.Length; i++)
        {
            var current = isActive && (Field)i == activeField;
            _fields[i].Foreground = current ? accent : normal;
            _fields[i].TextDecorations = current ? TextDecorations.Underline : TextDecorations.None;
        }

        _offset.Text = row.OffsetLabel;
        _offset.Foreground = Theme.Get("TextFillColorSecondaryBrush");
        _note.Text = row.Zone.IsFallback ? FallbackNote : "";
        _note.Visibility = row.Zone.IsFallback ? Visibility.Visible : Visibility.Collapsed;
    }
}
