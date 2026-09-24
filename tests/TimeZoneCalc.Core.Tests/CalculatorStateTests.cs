using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class CalculatorStateTests
{
    private static readonly ZoneCatalog Catalog = ZoneCatalog.CreateDefault();
    private static readonly ZoneOption NewYork = Catalog.All.First(z => z.Id == "Eastern Standard Time");
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 6, 32, 5, TimeSpan.Zero);

    private static CalculatorState Create() => new(Catalog, () => Now);

    private static void Type(CalculatorState s, string digits)
    {
        foreach (var c in digits)
            s.PushDigit(c - '0');
    }

    private static void Enter(CalculatorState s, string date, string time)
    {
        s.SetSegment(EntryMode.Date);
        Type(s, date);
        s.SetSegment(EntryMode.Time);
        Type(s, time);
    }

    [Fact]
    public void Starts_with_utc_and_taipei_at_now_with_utc_active()
    {
        var s = Create();
        Assert.Equal(new[] { Catalog.Utc, Catalog.Taipei }, s.Rows.Select(r => r.Zone));
        Assert.Equal(0, s.ActiveIndex);
        Assert.Equal(EntryMode.Time, s.ActiveSegment);
        Assert.Equal(InputStatus.Ok, s.Status);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
        Assert.Equal("2026-09-24 14:32:05", s.Rows[1].Text);
        Assert.Equal("UTC+08:00", s.Rows[1].OffsetLabel);
        Assert.True(s.Rows[1].CanCopy);
    }

    [Fact]
    public void Typing_time_restarts_and_converts_across_day()
    {
        var s = Create();
        Type(s, "2300");
        Assert.Equal("23:00:00", s.TimeEntry.Display);
        Assert.Equal("2026-09-25 07:00:00", s.Rows[1].Text);
    }

    [Fact]
    public void Typing_date_changes_date_and_keeps_time()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "20261231");
        Assert.Equal("2026-12-31 14:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Incomplete_date_marks_other_rows_stale_with_last_value()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "2026");
        Assert.Equal(InputStatus.IncompleteDate, s.Status);
        Assert.True(s.Rows[1].IsStale);
        Assert.Equal("2026-09-24 14:32:05", s.Rows[1].Text);
        Assert.False(s.Rows[0].CanCopy);
        Assert.False(s.Rows[1].CanCopy);   // 灰色的舊值不能被當成目前結果複製
    }

    [Fact]
    public void Impossible_date_is_invalid()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "20260231");
        Assert.Equal(InputStatus.InvalidDate, s.Status);
        Assert.True(s.Rows[1].IsStale);
    }

    [Fact]
    public void Selecting_row_loads_its_value()
    {
        var s = Create();
        s.SetActiveRow(1);
        Assert.Equal(1, s.ActiveIndex);
        Assert.Equal("2026-09-24", s.DateEntry.Display);
        Assert.Equal("14:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
        Type(s, "08");
        Assert.Equal("2026-09-24 00:00:00", s.Rows[0].Text);
    }

    [Fact] // Review Focus 4
    public void Selecting_row_while_date_incomplete_loads_last_valid_instant()
    {
        var s = Create();
        s.SetSegment(EntryMode.Date);
        Type(s, "2026");
        s.SetActiveRow(1);
        Assert.Equal(InputStatus.Ok, s.Status);
        Assert.Equal("14:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
    }

    [Fact]
    public void MoveActive_clamps_to_rows()
    {
        var s = Create();
        s.MoveActive(-1);
        Assert.Equal(0, s.ActiveIndex);
        s.MoveActive(1);
        Assert.Equal(1, s.ActiveIndex);
        s.MoveActive(1);
        Assert.Equal(1, s.ActiveIndex);
    }

    [Fact]
    public void Clear_time_sets_midnight_and_clear_date_sets_today()
    {
        var s = Create();
        s.Clear();
        Assert.Equal("00:00:00", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 08:00:00", s.Rows[1].Text);
        s.SetSegment(EntryMode.Date);
        Type(s, "20200101");
        s.Clear();
        Assert.Equal("2026-09-24", s.DateEntry.Display);
    }

    [Fact]
    public void SetNow_restores_clock_time()
    {
        var s = Create();
        Type(s, "1200");
        s.SetNow();
        Assert.Equal("06:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24", s.DateEntry.Display);
    }

    [Fact]
    public void SetDate_from_calendar_keeps_time()
    {
        var s = Create();
        s.SetDate(new DateOnly(2026, 1, 1));
        Assert.Equal("2026-01-01 14:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Rows_are_limited_to_one_through_five()
    {
        var s = Create();
        for (var i = 0; i < 10; i++)
            s.AddRow();
        Assert.Equal(CalculatorState.MaxRows, s.Rows.Count);
        Assert.False(s.CanAddRow);
        Assert.Same(Catalog.Utc, s.Rows[4].Zone);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[4].Text);
        for (var i = 0; i < 10; i++)
            s.RemoveRow(0);
        Assert.Single(s.Rows);
        Assert.False(s.CanRemoveRow);
    }

    [Fact] // Review Focus 5
    public void Removing_active_row_activates_first_row_with_same_instant()
    {
        var s = Create();
        s.SetActiveRow(1);
        s.RemoveRow(1);
        Assert.Equal(0, s.ActiveIndex);
        Assert.Equal("06:32:05", s.TimeEntry.Display);
    }

    [Fact] // Review Focus 5
    public void Removing_row_before_active_keeps_same_active_row()
    {
        var s = Create();
        s.AddRow();
        s.SetZone(2, NewYork);
        s.SetActiveRow(2);
        s.RemoveRow(0);
        Assert.Equal(1, s.ActiveIndex);
        Assert.Same(NewYork, s.ActiveRow.Zone);
        Assert.Equal("02:32:05", s.TimeEntry.Display);
    }

    [Fact] // Review Focus 2
    public void Changing_other_row_zone_keeps_instant()
    {
        var s = Create();
        s.SetZone(1, NewYork);
        Assert.Equal("2026-09-24 02:32:05", s.Rows[1].Text);
        Assert.Equal("UTC-04:00 夏令", s.Rows[1].OffsetLabel);
    }

    [Fact] // Review Focus 2
    public void Changing_active_row_zone_keeps_typed_wall_time()
    {
        var s = Create();
        s.SetZone(0, Catalog.Taipei);
        Assert.Equal("06:32:05", s.TimeEntry.Display);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Nonexistent_time_blanks_other_rows()
    {
        var s = Create();
        s.SetZone(0, NewYork);
        Enter(s, "20260308", "023000");
        Assert.Equal(InputStatus.NonexistentTime, s.Status);
        Assert.Equal(CalculatorState.Blank, s.Rows[1].Text);
        Assert.False(s.Rows[1].CanCopy);
        Assert.False(s.Rows[0].CanCopy);
    }

    [Fact]
    public void Ambiguous_time_defaults_to_first_and_can_toggle()
    {
        var s = Create();
        s.SetZone(0, NewYork);
        Enter(s, "20261101", "013000");
        Assert.Equal(InputStatus.AmbiguousTime, s.Status);
        Assert.Equal(AmbiguityChoice.First, s.Ambiguity);
        Assert.Equal("2026-11-01 13:30:00", s.Rows[1].Text);

        s.ToggleAmbiguity();
        Assert.Equal(AmbiguityChoice.Second, s.Ambiguity);
        Assert.Equal("2026-11-01 14:30:00", s.Rows[1].Text);

        s.Backspace();
        Type(s, "0");
        Assert.Equal(AmbiguityChoice.First, s.Ambiguity);
        Assert.Equal("2026-11-01 13:30:00", s.Rows[1].Text);
    }

    [Fact] // Review Focus 1
    public void Selecting_row_showing_second_occurrence_keeps_instant()
    {
        var s = Create();
        Enter(s, "20261101", "063000");
        s.SetZone(1, NewYork);
        Assert.Equal("2026-11-01 01:30:00", s.Rows[1].Text);
        Assert.Equal("UTC-05:00", s.Rows[1].OffsetLabel);

        s.SetActiveRow(1);
        Assert.Equal(AmbiguityChoice.Second, s.Ambiguity);
        Assert.Equal(InputStatus.AmbiguousTime, s.Status);
        Assert.Equal("2026-11-01 06:30:00", s.Rows[0].Text);
    }

    [Fact] // Review Focus 3
    public void Selecting_row_whose_date_is_past_2100_reports_invalid_date()
    {
        var s = Create();
        Enter(s, "21001231", "230000");
        Assert.Equal("2101-01-01 07:00:00", s.Rows[1].Text);

        s.SetActiveRow(1);
        Assert.Equal(InputStatus.InvalidDate, s.Status);
        Assert.Equal("2101-01-01", s.DateEntry.Display);
        Assert.Equal("2100-12-31 23:00:00", s.Rows[0].Text);
    }

    [Fact]
    public void Changed_fires_on_accepted_input_only()
    {
        var s = Create();
        var count = 0;
        s.Changed += (_, _) => count++;
        s.PushDigit(9);   // 小時十位數不能是 9
        Assert.Equal(0, count);
        s.PushDigit(1);
        Assert.Equal(1, count);
    }
}
