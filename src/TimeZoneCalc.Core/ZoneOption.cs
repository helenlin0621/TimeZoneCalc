namespace TimeZoneCalc.Core;

// 一個可選的時區：城市（依日期套用夏令時間）或固定偏移量
public sealed class ZoneOption
{
    private ZoneOption(string id, string displayName, TimeZoneInfo? city, TimeSpan fixedOffset, bool isFallback)
    {
        Id = id;
        DisplayName = displayName;
        City = city;
        FixedOffset = fixedOffset;
        IsFallback = isFallback;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public TimeZoneInfo? City { get; }
    public TimeSpan FixedOffset { get; }
    public bool IsFixed => City is null;
    public bool IsFallback { get; }

    public static ZoneOption FromCity(TimeZoneInfo tz, string? displayName = null) =>
        new(tz.Id, displayName ?? tz.DisplayName, tz, TimeSpan.Zero, false);

    public static ZoneOption FromOffset(TimeSpan offset, string? displayName = null, string? id = null, bool isFallback = false) =>
        new(id ?? "fixed:" + TimeFormat.Offset(offset), displayName ?? TimeFormat.Offset(offset), null, offset, isFallback);

    public ZoneOffsetInfo GetOffset(DateTimeOffset instant) => City is null
        ? new ZoneOffsetInfo(FixedOffset, false)
        : new ZoneOffsetInfo(City.GetUtcOffset(instant), City.IsDaylightSavingTime(instant));

    public override string ToString() => DisplayName;
}
