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

    // 從年開始連續打 14 位：yyyyMMddHHmmss
    private static void Enter(CalculatorState s, string yyyyMMddHHmmss)
    {
        s.SelectField(Field.Year);
        Type(s, yyyyMMddHHmmss);
    }

    [Fact]
    public void Starts_with_utc_and_taipei_at_now_with_utc_hour_active()
    {
        var s = Create();
        Assert.Equal(new[] { Catalog.Utc, Catalog.Taipei }, s.Rows.Select(r => r.Zone));
        Assert.Equal(0, s.ActiveIndex);
        Assert.Equal(Field.Hour, s.ActiveField);
        Assert.Equal(InputStatus.Ok, s.Status);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
        Assert.Equal("2026-09-24 14:32:05", s.Rows[1].Text);
        Assert.Equal("UTC+08:00", s.Rows[1].OffsetLabel);
        Assert.True(s.Rows[1].CanCopy);
    }

    [Fact]
    public void Typing_hour_and_minute_converts_across_day()
    {
        var s = Create();
        Type(s, "2300");
        Assert.Equal("2026-09-24 23:00:05", s.Rows[0].Text);
        Assert.Equal("2026-09-25 07:00:05", s.Rows[1].Text);
        Assert.Equal(Field.Second, s.ActiveField);
    }

    [Fact]
    public void Typing_full_datetime_from_year()
    {
        var s = Create();
        Enter(s, "20261231143205");
        Assert.Equal("2026-12-31 22:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Clicking_a_field_then_typing_edits_only_that_field()
    {
        var s = Create();
        s.SelectField(Field.Month);
        Type(s, "12");
        Assert.Equal("2026-12-24 06:32:05", s.Rows[0].Text);
        Assert.Equal(Field.Day, s.ActiveField);
    }

    [Theory]
    [InlineData(Field.Hour, "30", "2026-09-24 23:32:05")]
    [InlineData(Field.Minute, "75", "2026-09-24 06:59:05")]
    [InlineData(Field.Month, "15", "2026-12-24 06:32:05")]
    public void Values_above_max_are_clamped(Field field, string digits, string expected)
    {
        var s = Create();
        s.SelectField(field);
        Type(s, digits);
        Assert.Equal(expected, s.Rows[0].Text);
    }

    [Fact]
    public void Day_clamps_to_month_length()
    {
        var s = Create();
        Enter(s, "20260231");
        Assert.Equal("2026-02-28 06:32:05", s.Rows[0].Text);
        Assert.Equal(InputStatus.Ok, s.Status);
    }

    [Fact]
    public void Incomplete_year_marks_other_rows_stale_with_last_value()
    {
        var s = Create();
        s.SelectField(Field.Year);
        Type(s, "202");
        Assert.Equal(InputStatus.IncompleteDate, s.Status);
        Assert.Equal("202_-09-24 06:32:05", s.Rows[0].Text);
        Assert.True(s.Rows[1].IsStale);
        Assert.Equal("2026-09-24 14:32:05", s.Rows[1].Text);
        Assert.False(s.Rows[0].CanCopy);
        Assert.False(s.Rows[1].CanCopy);   // 灰色的舊值不能被當成目前結果複製
    }

    [Fact]
    public void Partial_field_shows_underscore_and_is_not_copyable()
    {
        var s = Create();
        s.SelectField(Field.Minute);
        Type(s, "4");
        Assert.Equal("2026-09-24 06:4_:05", s.Rows[0].Text);
        Assert.False(s.Rows[0].CanCopy);
        Assert.Equal("2026-09-24 14:04:05", s.Rows[1].Text);
    }

    [Fact]
    public void Next_and_previous_field()
    {
        var s = Create();
        s.NextField();
        Assert.Equal(Field.Minute, s.ActiveField);
        s.PreviousField();
        s.PreviousField();
        Assert.Equal(Field.Day, s.ActiveField);
    }

    [Fact]
    public void Selecting_row_loads_its_value_and_keeps_field()
    {
        var s = Create();
        s.SetActiveRow(1);
        Assert.Equal(1, s.ActiveIndex);
        Assert.Equal("2026-09-24 14:32:05", s.Entry.Display);
        Assert.Equal(Field.Hour, s.ActiveField);
        Type(s, "08");
        Assert.Equal("2026-09-24 00:32:05", s.Rows[0].Text);
    }

    [Fact] // Review Focus 4
    public void Selecting_row_while_year_incomplete_loads_last_valid_instant()
    {
        var s = Create();
        s.SelectField(Field.Year);
        Type(s, "202");
        s.SetActiveRow(1);
        Assert.Equal(InputStatus.Ok, s.Status);
        Assert.Equal("2026-09-24 14:32:05", s.Entry.Display);
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
    public void Clear_empties_active_field()
    {
        var s = Create();
        s.Clear();
        Assert.Equal("2026-09-24 __:32:05", s.Rows[0].Text);
        Assert.Equal("2026-09-24 08:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void SetNow_restores_clock_time()
    {
        var s = Create();
        Type(s, "12");
        s.SetNow();
        Assert.Equal("2026-09-24 06:32:05", s.Entry.Display);
    }

    [Fact]
    public void Editing_second_row_converts_back_to_first()
    {
        // 使用者的例子：UTC 00:00 ↔ 台北 08:00；把台北改成 16:00，UTC 變 08:00
        var s = Create();
        Enter(s, "20260924000000");
        Assert.Equal("2026-09-24 08:00:00", s.Rows[1].Text);
        s.SetActiveRow(1);
        s.SelectField(Field.Hour);
        Type(s, "160000");
        Assert.Equal("2026-09-24 08:00:00", s.Rows[0].Text);
        Assert.Equal(2, s.Rows.Count);
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
        Assert.Equal("2026-09-24 06:32:05", s.Rows[0].Text);
        Assert.Equal("2026-09-24 06:32:05", s.Rows[1].Text);
    }

    [Fact]
    public void Nonexistent_time_blanks_other_rows()
    {
        var s = Create();
        s.SetZone(0, NewYork);
        Enter(s, "20260308023000");
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
        Enter(s, "20261101013000");
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
        Enter(s, "20261101063000");
        s.SetZone(1, NewYork);
        Assert.Equal("2026-11-01 01:30:00", s.Rows[1].Text);
        Assert.Equal("UTC-05:00", s.Rows[1].OffsetLabel);

        s.SetActiveRow(1);
        Assert.Equal(AmbiguityChoice.Second, s.Ambiguity);
        Assert.Equal(InputStatus.AmbiguousTime, s.Status);
        Assert.Equal("2026-11-01 06:30:00", s.Rows[0].Text);
    }

    [Fact] // Review Focus 3
    public void Selecting_row_whose_date_is_past_2100_keeps_the_value()
    {
        var s = Create();
        Enter(s, "21001231230000");
        Assert.Equal("2101-01-01 07:00:00", s.Rows[1].Text);

        s.SetActiveRow(1);
        Assert.Equal(InputStatus.Ok, s.Status);
        Assert.Equal("2101-01-01 07:00:00", s.Entry.Display);
        Assert.Equal("2100-12-31 23:00:00", s.Rows[0].Text);
    }

    [Fact]
    public void Changed_fires_on_accepted_input_only()
    {
        var s = Create();
        var count = 0;
        s.Changed += (_, _) => count++;
        s.PushDigit(10);
        Assert.Equal(0, count);
        s.PushDigit(1);
        Assert.Equal(1, count);
    }
}
