using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class DigitEntryTests
{
    private static DigitEntry Typed(EntryMode mode, string digits)
    {
        var e = new DigitEntry(mode);
        foreach (var c in digits)
            Assert.True(e.TryPush(c - '0'), $"應接受 {digits} 中的 {c}");
        return e;
    }

    [Fact]
    public void Six_digits_fill_hh_mm_ss()
    {
        var e = Typed(EntryMode.Time, "143205");
        Assert.Equal("14:32:05", e.Display);
        Assert.Equal(new TimeOnly(14, 32, 5), e.Time);
    }

    [Fact]
    public void Partial_time_pads_with_zero()
    {
        var e = Typed(EntryMode.Time, "1432");
        Assert.Equal("14:32:00", e.Display);
        Assert.Equal(new TimeOnly(14, 32, 0), e.Time);
    }

    [Fact]
    public void Empty_time_is_midnight() =>
        Assert.Equal("00:00:00", new DigitEntry(EntryMode.Time).Display);

    [Fact]
    public void Hour_19_is_allowed() =>
        Assert.Equal(new TimeOnly(19, 0), Typed(EntryMode.Time, "19").Time);

    [Theory]
    [InlineData("", 3)]
    [InlineData("2", 4)]
    [InlineData("23", 6)]
    [InlineData("2359", 6)]
    [InlineData("235959", 0)]
    public void Time_rejects_invalid_next_digit(string typed, int digit)
    {
        var e = Typed(EntryMode.Time, typed);
        Assert.False(e.TryPush(digit));
        Assert.Equal(typed, e.Digits);
    }

    [Fact]
    public void Backspace_removes_last_digit()
    {
        var e = Typed(EntryMode.Time, "1432");
        Assert.True(e.Backspace());
        Assert.Equal("143", e.Digits);
    }

    [Fact]
    public void Backspace_on_empty_returns_false() =>
        Assert.False(new DigitEntry(EntryMode.Time).Backspace());

    [Fact]
    public void Clear_empties()
    {
        var e = Typed(EntryMode.Time, "1432");
        e.Clear();
        Assert.Equal("", e.Digits);
    }

    [Fact]
    public void Load_then_first_digit_restarts()
    {
        var e = new DigitEntry(EntryMode.Time);
        e.Load(new TimeOnly(9, 5, 7));
        Assert.Equal("090507", e.Digits);
        Assert.True(e.IsFresh);
        Assert.True(e.TryPush(1));
        Assert.Equal("1", e.Digits);
        Assert.False(e.IsFresh);
    }

    [Fact]
    public void Backspace_after_load_edits_loaded_digits()
    {
        var e = new DigitEntry(EntryMode.Time);
        e.Load(new TimeOnly(9, 5, 7));
        Assert.True(e.Backspace());
        Assert.Equal("09050", e.Digits);
        Assert.False(e.IsFresh);
    }

    [Fact]
    public void MarkFresh_makes_next_digit_restart()
    {
        var e = Typed(EntryMode.Time, "14");
        e.MarkFresh();
        Assert.True(e.TryPush(0));
        Assert.Equal("0", e.Digits);
    }

    [Fact]
    public void Eight_digits_make_a_date()
    {
        var e = Typed(EntryMode.Date, "20260924");
        Assert.Equal(new DateOnly(2026, 9, 24), e.Date);
        Assert.Equal("2026-09-24", e.Display);
    }

    [Fact]
    public void Partial_date_is_null_and_shows_placeholders()
    {
        var e = Typed(EntryMode.Date, "202609");
        Assert.Null(e.Date);
        Assert.Equal("2026-09-__", e.Display);
    }

    [Theory]
    [InlineData("20260231")]
    [InlineData("20250229")]
    [InlineData("18991231")]
    [InlineData("21010101")]
    public void Impossible_or_out_of_range_dates_are_null(string digits) =>
        Assert.Null(Typed(EntryMode.Date, digits).Date);

    [Theory]
    [InlineData("20240229", 2024, 2, 29)]
    [InlineData("19000101", 1900, 1, 1)]
    [InlineData("21001231", 2100, 12, 31)]
    public void Valid_edge_dates(string digits, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), Typed(EntryMode.Date, digits).Date);

    [Theory]
    [InlineData("", 3)]
    [InlineData("2026", 2)]
    [InlineData("20260", 0)]
    [InlineData("20261", 3)]
    [InlineData("202609", 4)]
    [InlineData("2026090", 0)]
    [InlineData("2026093", 2)]
    [InlineData("20260924", 1)]
    public void Date_rejects_invalid_next_digit(string typed, int digit)
    {
        var e = Typed(EntryMode.Date, typed);
        Assert.False(e.TryPush(digit));
        Assert.Equal(typed, e.Digits);
    }

    [Fact] // Review Focus 3
    public void Loaded_out_of_range_date_displays_but_is_null()
    {
        var e = new DigitEntry(EntryMode.Date);
        e.Load(new DateOnly(2101, 1, 1));
        Assert.Equal("2101-01-01", e.Display);
        Assert.Null(e.Date);
    }

    [Fact]
    public void Loading_wrong_kind_throws()
    {
        Assert.Throws<InvalidOperationException>(() => new DigitEntry(EntryMode.Date).Load(new TimeOnly(1, 0)));
        Assert.Throws<InvalidOperationException>(() => new DigitEntry(EntryMode.Time).Load(new DateOnly(2026, 1, 1)));
    }
}
