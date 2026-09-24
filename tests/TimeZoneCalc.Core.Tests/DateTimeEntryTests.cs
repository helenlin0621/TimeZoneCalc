using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class DateTimeEntryTests
{
    private static DateTimeEntry Loaded(int y = 2026, int mo = 9, int d = 24, int h = 14, int mi = 32, int s = 5)
    {
        var e = new DateTimeEntry();
        e.Load(new DateTime(y, mo, d, h, mi, s));
        return e;
    }

    private static void Type(DateTimeEntry e, string digits)
    {
        foreach (var c in digits)
            Assert.True(e.PushDigit(c - '0'));
    }

    [Fact]
    public void Load_shows_all_fields_and_starts_on_hour()
    {
        var e = Loaded();
        Assert.Equal("2026-09-24 14:32:05", e.Display);
        Assert.Equal(Field.Hour, e.Active);
        Assert.Equal(new DateTime(2026, 9, 24, 14, 32, 5), e.Wall);
    }

    [Fact]
    public void Typing_fourteen_digits_from_year_fills_every_field()
    {
        var e = Loaded();
        e.Select(Field.Year);
        Type(e, "20261231235958");
        Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 58), e.Wall);
        Assert.Equal(Field.Second, e.Active);
    }

    [Fact]
    public void Full_field_advances_to_next_field()
    {
        var e = Loaded();
        e.Select(Field.Month);
        Type(e, "03");
        Assert.Equal(Field.Day, e.Active);
    }

    [Fact]
    public void Filled_second_stays_and_next_digit_restarts_second()
    {
        var e = Loaded();
        e.Select(Field.Second);
        Type(e, "12");
        Assert.Equal(Field.Second, e.Active);
        Type(e, "3");
        Assert.Equal("3_", e.Text(Field.Second));
        Assert.Equal(3, e.Get(Field.Second));
    }

    [Fact]
    public void First_digit_after_selecting_restarts_that_field()
    {
        var e = Loaded();
        e.Select(Field.Minute);
        Type(e, "4");
        Assert.Equal("4_", e.Text(Field.Minute));
        Assert.Equal(4, e.Get(Field.Minute));
        Assert.Equal(Field.Minute, e.Active);
    }

    [Theory]
    [InlineData(Field.Hour, "30", 23)]
    [InlineData(Field.Hour, "99", 23)]
    [InlineData(Field.Minute, "75", 59)]
    [InlineData(Field.Second, "99", 59)]
    [InlineData(Field.Month, "15", 12)]
    [InlineData(Field.Month, "00", 1)]
    [InlineData(Field.Day, "00", 1)]
    public void Value_above_or_below_range_is_clamped(Field field, string digits, int expected)
    {
        var e = Loaded();
        e.Select(field);
        Type(e, digits);
        Assert.Equal(expected, e.Get(field));
    }

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2024, 2, 29)]
    [InlineData(2026, 6, 30)]
    [InlineData(2026, 7, 31)]
    public void Day_is_clamped_to_days_in_month(int year, int month, int expected)
    {
        var e = Loaded(year, month, 1);
        e.Select(Field.Day);
        Type(e, "31");
        Assert.Equal(expected, e.Get(Field.Day));
    }

    [Fact]
    public void Changing_month_clamps_existing_day()
    {
        var e = Loaded(2026, 3, 31);
        e.Select(Field.Month);
        Type(e, "02");
        Assert.Equal(28, e.Get(Field.Day));
    }

    [Fact]
    public void Changing_to_non_leap_year_clamps_feb_29()
    {
        var e = Loaded(2024, 2, 29);
        e.Select(Field.Year);
        Type(e, "2025");
        Assert.Equal(28, e.Get(Field.Day));
    }

    [Theory]
    [InlineData("1800", 1900)]
    [InlineData("2999", 2100)]
    public void Year_is_clamped_to_supported_range(string digits, int expected)
    {
        var e = Loaded();
        e.Select(Field.Year);
        Type(e, digits);
        Assert.Equal(expected, e.Get(Field.Year));
    }

    [Fact]
    public void Partial_year_is_incomplete()
    {
        var e = Loaded();
        e.Select(Field.Year);
        Type(e, "20");
        Assert.False(e.IsComplete);
        Assert.Null(e.Wall);
        Assert.Equal("20__", e.Text(Field.Year));
    }

    [Fact]
    public void Leaving_partial_year_keeps_previous_year()
    {
        var e = Loaded();
        e.Select(Field.Year);
        Type(e, "19");
        e.Select(Field.Month);
        Assert.Equal(2026, e.Get(Field.Year));
        Assert.True(e.IsComplete);
    }

    [Fact]
    public void Partial_two_digit_field_shows_underscore_and_applies_value()
    {
        var e = Loaded();
        e.Select(Field.Month);
        Type(e, "1");
        Assert.Equal("1_", e.Text(Field.Month));
        Assert.Equal(1, e.Get(Field.Month));
        e.Select(Field.Day);
        Assert.Equal("01", e.Text(Field.Month));
    }

    [Fact]
    public void Backspace_edits_current_value()
    {
        var e = Loaded();
        Assert.True(e.Backspace());
        Assert.Equal("1_", e.Text(Field.Hour));
        Assert.Equal(1, e.Get(Field.Hour));
        Assert.True(e.Backspace());
        Assert.False(e.Backspace());
    }

    [Fact]
    public void Clear_empties_active_field()
    {
        var e = Loaded();
        e.Clear();
        Assert.Equal("__", e.Text(Field.Hour));
        Assert.Equal(0, e.Get(Field.Hour));
        Assert.Equal("2026-09-24 __:32:05", e.Display);
    }

    [Fact]
    public void Next_and_previous_stop_at_the_ends()
    {
        var e = Loaded();
        e.Select(Field.Second);
        Assert.False(e.Next());
        e.Select(Field.Year);
        Assert.False(e.Previous());
        Assert.True(e.Next());
        Assert.Equal(Field.Month, e.Active);
    }

    [Fact]
    public void Rejects_non_digit()
    {
        var e = Loaded();
        Assert.False(e.PushDigit(10));
        Assert.False(e.PushDigit(-1));
    }

    [Fact] // Review Focus 3
    public void Loaded_year_past_2100_is_shown_as_is()
    {
        var e = Loaded(2101, 1, 1, 7, 0, 0);
        Assert.Equal("2101-01-01 07:00:00", e.Display);
        Assert.NotNull(e.Wall);
    }
}
