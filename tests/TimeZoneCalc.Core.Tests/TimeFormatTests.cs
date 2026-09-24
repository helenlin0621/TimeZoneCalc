using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class TimeFormatTests
{
    [Fact]
    public void Wall_formats_as_yyyy_MM_dd_HH_mm_ss() =>
        Assert.Equal("2026-09-24 14:32:05", TimeFormat.Wall(new DateTime(2026, 9, 24, 14, 32, 5)));

    [Theory]
    [InlineData(8, 0, "UTC+08:00")]
    [InlineData(-4, 0, "UTC-04:00")]
    [InlineData(0, 0, "UTC+00:00")]
    [InlineData(5, 45, "UTC+05:45")]
    [InlineData(-9, -30, "UTC-09:30")]
    public void Offset_formats_sign_hours_minutes(int hours, int minutes, string expected) =>
        Assert.Equal(expected, TimeFormat.Offset(new TimeSpan(hours, minutes, 0)));
}
