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
