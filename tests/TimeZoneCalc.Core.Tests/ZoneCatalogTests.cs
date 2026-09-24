using TimeZoneCalc.Core;
using Xunit;

namespace TimeZoneCalc.Core.Tests;

public class ZoneCatalogTests
{
    private static readonly ZoneCatalog Catalog = ZoneCatalog.CreateDefault();

    [Fact]
    public void Utc_and_taipei_come_first()
    {
        Assert.Same(Catalog.Utc, Catalog.All[0]);
        Assert.Same(Catalog.Taipei, Catalog.All[1]);
        Assert.Equal("UTC (UTC+00:00)", Catalog.Utc.DisplayName);
        Assert.Equal(TimeSpan.Zero, Catalog.Utc.FixedOffset);
        Assert.Equal("台北 (UTC+08:00)", Catalog.Taipei.DisplayName);
        Assert.False(Catalog.Taipei.IsFixed);
        Assert.False(Catalog.Taipei.IsFallback);
    }

    [Fact]
    public void No_fixed_offsets_after_utc_and_cities_sorted_by_standard_offset()
    {
        var rest = Catalog.All.Skip(2).ToList();
        Assert.All(rest, z => Assert.False(z.IsFixed));
        var offsets = rest.Select(z => z.City!.BaseUtcOffset).ToList();
        Assert.Equal(offsets.Order().ToList(), offsets);
    }

    [Fact]
    public void Every_label_is_name_then_standard_offset()
    {
        Assert.All(Catalog.All, z => Assert.Matches(@"^[^(\s].* \(UTC[+-]\d\d:\d\d\)$", z.DisplayName));
        var eastern = Catalog.All.First(z => z.Id == "Eastern Standard Time");
        Assert.EndsWith(" (UTC-05:00)", eastern.DisplayName);
    }

    [Fact]
    public void Cities_have_no_duplicates_of_utc_or_taipei()
    {
        var cities = Catalog.All.Where(z => !z.IsFixed).ToList();
        Assert.Single(cities, z => z.Id == ZoneCatalog.TaipeiId);
        Assert.DoesNotContain(cities, z => z.Id.StartsWith("UTC", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cities, z => z.Id == "Eastern Standard Time");
    }

    [Fact]
    public void Missing_taipei_falls_back_to_fixed_plus_eight()
    {
        var zones = TimeZoneInfo.GetSystemTimeZones().Where(z => z.Id != ZoneCatalog.TaipeiId).ToList();
        var catalog = new ZoneCatalog(zones);
        Assert.True(catalog.Taipei.IsFallback);
        Assert.True(catalog.Taipei.IsFixed);
        Assert.Equal(TimeSpan.FromHours(8), catalog.Taipei.FixedOffset);
        Assert.Equal("台北 (UTC+08:00)", catalog.Taipei.DisplayName);
        Assert.Same(catalog.Taipei, catalog.All[1]);
    }

    [Theory]
    [InlineData("tai")]
    [InlineData("TAIPEI")]
    [InlineData("台北")]
    public void Filter_finds_taipei_case_insensitive(string query) =>
        Assert.Contains(Catalog.Taipei, Catalog.Filter(query));

    [Theory]
    [InlineData("new york", "Eastern Standard Time")]
    [InlineData("紐約", "Eastern Standard Time")]
    [InlineData("los angeles", "Pacific Standard Time")]
    [InlineData("tokyo", "Tokyo Standard Time")]
    [InlineData("東京", "Tokyo Standard Time")]
    [InlineData("london", "GMT Standard Time")]
    [InlineData("berlin", "W. Europe Standard Time")]
    public void Filter_finds_common_cities_by_alias(string query, string zoneId) =>
        Assert.Contains(Catalog.Filter(query), z => z.Id == zoneId);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("zzz-no-such-zone")]
    public void Match_blank_or_unknown_query_is_null(string query) =>
        Assert.Null(Catalog.Match(query));

    [Fact]
    public void Match_returns_first_filtered_zone() =>
        Assert.Same(Catalog.Taipei, Catalog.Match("tai"));

    [Fact]
    public void Filter_blank_returns_all() =>
        Assert.Equal(Catalog.All.Count, Catalog.Filter("  ").Count);

    [Fact]
    public void Filter_matches_offset_text() =>
        Assert.Contains(Catalog.Filter("+05:45"), z => z.City?.BaseUtcOffset == new TimeSpan(5, 45, 0));
}
