using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class StatusTextTests
{
    private static ZoneOption Zone(string id) => ZoneOption.FromCity(TimeZoneInfo.FindSystemTimeZoneById(id));

    [Fact]
    public void Dst_gap_says_daylight_saving()
    {
        var text = StatusText.Nonexistent(TestZones.NewYork, TestZones.Wall(2026, 3, 8, 2, 30));
        Assert.Equal("此時間因夏令時間切換而不存在", text);
    }

    [Fact]
    public void Dst_overlap_labels_both_occurrences_with_daylight_only_on_the_dst_one()
    {
        var wall = TestZones.Wall(2026, 11, 1, 1, 30);
        Assert.Equal("此時間出現兩次，目前採用第一次出現（UTC-04:00 夏令）",
            StatusText.Ambiguous(TestZones.NewYork, wall, AmbiguityChoice.First));
        Assert.Equal("此時間出現兩次，目前採用第二次出現（UTC-05:00）",
            StatusText.Ambiguous(TestZones.NewYork, wall, AmbiguityChoice.Second));
    }

    // 莫斯科 2014-10-26 這類「改標準偏移量」的情況：旗標都不是夏令
    [Fact]
    public void Standard_offset_change_is_not_called_daylight_saving()
    {
        var plus4 = new ZoneOffsetInfo(TimeSpan.FromHours(4), false);
        var plus3 = new ZoneOffsetInfo(TimeSpan.FromHours(3), false);
        Assert.Equal("此時間出現兩次，目前採用第一次出現（UTC+04:00）", StatusText.Ambiguous(plus4, AmbiguityChoice.First));
        Assert.Equal("此時間出現兩次，目前採用第二次出現（UTC+03:00）", StatusText.Ambiguous(plus3, AmbiguityChoice.Second));
    }

    [Fact]
    public void Standard_offset_gap_shows_offsets_instead_of_daylight_saving()
    {
        var text = StatusText.Nonexistent(new ZoneOffsetInfo(TimeSpan.FromHours(3), false),
                                          new ZoneOffsetInfo(TimeSpan.FromHours(4), false));
        Assert.Equal("此時間因時區偏移量調整而不存在（UTC+03:00 → UTC+04:00）", text);
    }

    [Fact]
    public void Real_zone_status_follows_daylight_flags_not_assumptions()
    {
        var moscow = Zone("Russian Standard Time");
        var wall = TestZones.Wall(2014, 10, 26, 1, 30);
        Assert.True(moscow.City!.IsAmbiguousTime(wall));
        foreach (var choice in new[] { AmbiguityChoice.First, AmbiguityChoice.Second })
        {
            var offset = choice == AmbiguityChoice.First ? TimeSpan.FromHours(4) : TimeSpan.FromHours(3);
            var daylight = moscow.City.IsDaylightSavingTime(new DateTimeOffset(wall, offset));
            Assert.Equal(daylight, StatusText.Ambiguous(moscow, wall, choice).Contains("夏令"));
        }
    }
}
