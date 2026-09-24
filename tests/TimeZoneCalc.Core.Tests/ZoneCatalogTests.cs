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
        Assert.Equal("UTC", Catalog.Utc.DisplayName);
        Assert.Equal(TimeSpan.Zero, Catalog.Utc.FixedOffset);
        Assert.Equal("台北 (Asia/Taipei)", Catalog.Taipei.DisplayName);
        Assert.False(Catalog.Taipei.IsFixed);
        Assert.False(Catalog.Taipei.IsFallback);
    }

    [Fact]
    public void Fixed_offsets_follow_sorted_and_only_real_offsets()
    {
        var fixedOffsets = Catalog.All.Skip(2).TakeWhile(z => z.IsFixed).Select(z => z.FixedOffset).ToList();
        Assert.Equal(fixedOffsets.Order().ToList(), fixedOffsets);
        Assert.Equal(TimeSpan.FromHours(-12), fixedOffsets[0]);
        Assert.Equal(TimeSpan.FromHours(14), fixedOffsets[^1]);
        Assert.Contains(new TimeSpan(5, 30, 0), fixedOffsets);
        Assert.Contains(new TimeSpan(5, 45, 0), fixedOffsets);
        Assert.Contains(TimeSpan.FromHours(8), fixedOffsets);
        Assert.DoesNotContain(TimeSpan.Zero, fixedOffsets);
        Assert.DoesNotContain(new TimeSpan(5, 15, 0), fixedOffsets);
    }

    [Fact]
    public void Cities_come_after_fixed_offsets_without_duplicates_of_utc_or_taipei()
    {
        var rest = Catalog.All.Skip(2).ToList();
        var firstCity = rest.FindIndex(z => !z.IsFixed);
        Assert.True(firstCity > 0);
        Assert.All(rest.Skip(firstCity), z => Assert.False(z.IsFixed));

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
        Assert.Same(catalog.Taipei, catalog.All[1]);
    }

    [Theory]
    [InlineData("tai")]
    [InlineData("TAIPEI")]
    [InlineData("台北")]
    public void Filter_finds_taipei_case_insensitive(string query) =>
        Assert.Contains(Catalog.Taipei, Catalog.Filter(query));

    [Fact]
    public void Filter_blank_returns_all() =>
        Assert.Equal(Catalog.All.Count, Catalog.Filter("  ").Count);

    [Fact]
    public void Filter_matches_fixed_offset_text() =>
        Assert.Contains(Catalog.Filter("+05:45"), z => z.IsFixed && z.FixedOffset == new TimeSpan(5, 45, 0));
}
