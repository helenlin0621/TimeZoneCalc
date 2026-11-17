namespace TimeZoneCalc.Core;

// 不存在／重複的時間不一定是夏令造成的：有些時區是直接改了標準偏移量（例如莫斯科 2014-10-26），
// 所以要看實際的偏移量與 IsDaylightSavingTime，不能一律寫「夏令」
public static class StatusText
{
    public static string Nonexistent(ZoneOption zone, DateTime wall)
    {
        if (zone.City is not { } tz)
            return "此時間不存在";
        // 把牆上時間當成 UTC，前後各 24 小時一定落在空檔的兩側（偏移量最多差 14 小時）
        var around = DateTime.SpecifyKind(wall, DateTimeKind.Utc);
        var before = Info(tz, new DateTimeOffset(around.AddDays(-1)));
        var after = Info(tz, new DateTimeOffset(around.AddDays(1)));
        return Nonexistent(before, after);
    }

    // 分開成純函式，因為「偏移量改了但不是夏令」的真實時區資料依 Windows 版本而異，測試要自己給旗標
    public static string Nonexistent(ZoneOffsetInfo before, ZoneOffsetInfo after) =>
        before.IsDaylight != after.IsDaylight
            ? "此時間因夏令時間切換而不存在"
            : $"此時間因時區偏移量調整而不存在（{before.Label} → {after.Label}）";

    public static string Ambiguous(ZoneOption zone, DateTime wall, AmbiguityChoice choice)
    {
        return zone.City is { } tz
            ? Ambiguous(ChosenOffset(tz, wall, choice), choice)
            : Ambiguous(null, choice);
    }

    public static string Ambiguous(ZoneOffsetInfo? chosen, AmbiguityChoice choice)
    {
        var which = choice == AmbiguityChoice.First ? "第一次" : "第二次";
        return chosen is { } info
            ? $"此時間出現兩次，目前採用{which}出現（{info.Label}）"
            : $"此時間出現兩次，目前採用{which}出現";
    }

    private static ZoneOffsetInfo ChosenOffset(TimeZoneInfo tz, DateTime wall, AmbiguityChoice choice)
    {
        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        var offsets = tz.GetAmbiguousTimeOffsets(wall);
        // 偏移量越大，對應的瞬間越早，所以最大的偏移量是第一次出現
        var offset = choice == AmbiguityChoice.First ? offsets.Max() : offsets.Min();
        return Info(tz, new DateTimeOffset(wall, offset));
    }

    private static ZoneOffsetInfo Info(TimeZoneInfo tz, DateTimeOffset instant) =>
        new(tz.GetUtcOffset(instant), tz.IsDaylightSavingTime(instant));
}
