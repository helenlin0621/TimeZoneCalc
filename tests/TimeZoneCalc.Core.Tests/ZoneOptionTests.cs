using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class ZoneOptionTests
{
    [Fact]
    public void Fixed_offset_never_changes_and_is_not_daylight()
    {
        var info = TestZones.Kathmandu.GetOffset(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(new ZoneOffsetInfo(new TimeSpan(5, 45, 0), false), info);
        Assert.Equal("UTC+05:45", TestZones.Kathmandu.DisplayName);
        Assert.Equal("fixed:UTC+05:45", TestZones.Kathmandu.Id);
        Assert.True(TestZones.Kathmandu.IsFixed);
    }

    [Fact]
    public void City_offset_follows_daylight_saving()
    {
        var summer = TestZones.NewYork.GetOffset(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var winter = TestZones.NewYork.GetOffset(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(new ZoneOffsetInfo(TimeSpan.FromHours(-4), true), summer);
        Assert.Equal(new ZoneOffsetInfo(TimeSpan.FromHours(-5), false), winter);
        Assert.Equal("UTC-04:00 夏令", summer.Label);
        Assert.Equal("UTC-05:00", winter.Label);
        Assert.False(TestZones.NewYork.IsFixed);
    }
}
