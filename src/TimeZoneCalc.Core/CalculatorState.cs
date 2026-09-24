namespace TimeZoneCalc.Core;

public enum InputStatus { Ok, IncompleteDate, NonexistentTime, AmbiguousTime }

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
    public const string Blank = "—";

    private readonly Func<DateTimeOffset> _clock;

    // 固定兩列，互相換算（像小算盤的 HEX／DEC）
    private readonly List<ZoneRow> _rows;

    // 最後一次成功換算的瞬間；輸入暫時無效時，其他列顯示它（灰色）
    private DateTimeOffset? _lastInstant;

    public CalculatorState(ZoneCatalog catalog, Func<DateTimeOffset> clock)
    {
        _clock = clock;
        _rows = [new ZoneRow(catalog.Utc), new ZoneRow(catalog.Taipei)];
        SetNow();
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ZoneRow> Rows => _rows;
    public int ActiveIndex { get; private set; }
    public ZoneRow ActiveRow => _rows[ActiveIndex];
    public DateTimeEntry Entry { get; } = new();
    public Field ActiveField => Entry.Active;
    public AmbiguityChoice Ambiguity { get; private set; }
    public InputStatus Status { get; private set; }

    public void PushDigit(int digit)
    {
        if (Entry.PushDigit(digit))
            OnInputEdited();
    }

    public void Backspace()
    {
        if (Entry.Backspace())
            OnInputEdited();
    }

    // 清空目前這一欄
    public void Clear()
    {
        Entry.Clear();
        OnInputEdited();
    }

    public void SetNow() => LoadInstant(_clock());

    // 離開欄位時未打滿的年會還原，所以要重算
    public void SelectField(Field field)
    {
        Entry.Select(field);
        Recompute();
    }

    public void NextField()
    {
        if (Entry.Next())
            Recompute();
    }

    public void PreviousField()
    {
        if (Entry.Previous())
            Recompute();
    }

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
        Entry.Load(zoned.Wall);
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
        if (Entry.Wall is not { } wall)
        {
            Status = InputStatus.IncompleteDate;
            ShowLastInstantAsStale();
            ActiveRow.Text = Entry.Display;
            ActiveRow.OffsetLabel = "";
            ActiveRow.IsStale = false;
            ActiveRow.CanCopy = false;
            OnChanged();
            return;
        }

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
            ActiveRow.Text = Entry.Display;
            OnChanged();
            return;
        }

        Status = result.Kind == ResolveKind.Ambiguous ? InputStatus.AmbiguousTime : InputStatus.Ok;
        _lastInstant = result.Instant;
        foreach (var row in _rows)
            Show(row, result.Instant, stale: false);
        // 輸入列顯示正在打的內容（例如 "4_"）；還沒打完的欄位不能複製
        ActiveRow.Text = Entry.Display;
        ActiveRow.CanCopy = !Entry.Display.Contains('_');
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
        row.CanCopy = !stale;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
