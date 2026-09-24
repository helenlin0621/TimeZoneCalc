using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class ConverterTests
{
    private static DateTimeOffset Utc(int y, int mo, int d, int h, int mi, int s = 0) =>
        new(y, mo, d, h, mi, s, TimeSpan.Zero);

    [Fact]
    public void Utc_to_taipei_adds_eight_hours()
    {
        var r = Converter.Resolve(TestZones.Utc, TestZones.Wall(2026, 9, 24, 6, 32, 5), AmbiguityChoice.First);
        Assert.Equal(ResolveKind.Ok, r.Kind);
        var t = Converter.ToZone(r.Instant, TestZones.Taipei);
        Assert.Equal(TestZones.Wall(2026, 9, 24, 14, 32, 5), t.Wall);
        Assert.Equal(new ZoneOffsetInfo(TimeSpan.FromHours(8), false), t.Offset);
    }

    [Fact]
    public void Taipei_to_utc_subtracts_eight_hours_across_month()
    {
        var r = Converter.Resolve(TestZones.Taipei, TestZones.Wall(2026, 3, 1, 7, 0), AmbiguityChoice.First);
        Assert.Equal(ResolveKind.Ok, r.Kind);
        Assert.Equal(Utc(2026, 2, 28, 23, 0), r.Instant);
    }

    [Fact]
    public void Conversion_crosses_year_boundary()
    {
        var t = Converter.ToZone(Utc(2026, 12, 31, 20, 0), TestZones.Taipei);
        Assert.Equal(TestZones.Wall(2027, 1, 1, 4, 0), t.Wall);
    }

    [Fact]
    public void Fixed_offset_with_minutes_round_trips()
    {
        var t = Converter.ToZone(Utc(2026, 1, 1, 0, 0), TestZones.Kathmandu);
        Assert.Equal(TestZones.Wall(2026, 1, 1, 5, 45), t.Wall);
        var back = Converter.Resolve(TestZones.Kathmandu, TestZones.Wall(2026, 1, 1, 5, 45), AmbiguityChoice.First);
        Assert.Equal(Utc(2026, 1, 1, 0, 0), back.Instant);
    }

    [Fact]
    public void Spring_forward_gap_is_invalid() =>
        Assert.Equal(ResolveKind.Invalid,
            Converter.Resolve(TestZones.NewYork, TestZones.Wall(2026, 3, 8, 2, 30), AmbiguityChoice.First).Kind);

    [Theory]
    [InlineData(AmbiguityChoice.First, 5)]
    [InlineData(AmbiguityChoice.Second, 6)]
    public void Fall_back_overlap_is_ambiguous_and_choice_picks_occurrence(AmbiguityChoice choice, int utcHour)
    {
        var r = Converter.Resolve(TestZones.NewYork, TestZones.Wall(2026, 11, 1, 1, 30), choice);
        Assert.Equal(ResolveKind.Ambiguous, r.Kind);
        Assert.Equal(Utc(2026, 11, 1, utcHour, 30), r.Instant);
    }

    [Fact]
    public void Summer_time_in_new_york_is_utc_minus_four()
    {
        var r = Converter.Resolve(TestZones.NewYork, TestZones.Wall(2026, 7, 1, 8, 0), AmbiguityChoice.First);
        Assert.Equal(ResolveKind.Ok, r.Kind);
        Assert.Equal(Utc(2026, 7, 1, 12, 0), r.Instant);
    }

    [Theory]
    [InlineData(5, AmbiguityChoice.First)]
    [InlineData(6, AmbiguityChoice.Second)]
    [InlineData(12, AmbiguityChoice.First)]
    public void ChoiceFor_returns_choice_that_round_trips(int utcHour, AmbiguityChoice expected)
    {
        var instant = Utc(2026, 11, 1, utcHour, 30);
        var choice = Converter.ChoiceFor(instant, TestZones.NewYork);
        Assert.Equal(expected, choice);
        var wall = Converter.ToZone(instant, TestZones.NewYork).Wall;
        Assert.Equal(instant, Converter.Resolve(TestZones.NewYork, wall, choice).Instant);
    }

    [Fact]
    public void ChoiceFor_fixed_offset_is_first() =>
        Assert.Equal(AmbiguityChoice.First, Converter.ChoiceFor(Utc(2026, 11, 1, 6, 30), TestZones.Kathmandu));
}
