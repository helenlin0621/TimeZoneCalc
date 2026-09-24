namespace TimeZoneCalc.Core;

public enum AmbiguityChoice { First, Second }

public enum ResolveKind { Ok, Invalid, Ambiguous }

public readonly record struct ResolveResult(ResolveKind Kind, DateTimeOffset Instant)
{
    public static ResolveResult Invalid => new(ResolveKind.Invalid, default);
}

public readonly record struct ZonedTime(DateTime Wall, ZoneOffsetInfo Offset);

public static class Converter
{
    // 某時區的牆上時間 → 瞬間
    public static ResolveResult Resolve(ZoneOption zone, DateTime wall, AmbiguityChoice choice)
    {
        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        if (zone.City is not { } tz)
            return new(ResolveKind.Ok, new DateTimeOffset(wall, zone.FixedOffset));
        if (tz.IsInvalidTime(wall))
            return ResolveResult.Invalid;
        if (tz.IsAmbiguousTime(wall))
        {
            var offsets = tz.GetAmbiguousTimeOffsets(wall);
            // 偏移量越大，對應的瞬間越早，所以最大的偏移量是第一次出現
            var offset = choice == AmbiguityChoice.First ? offsets.Max() : offsets.Min();
            return new(ResolveKind.Ambiguous, new DateTimeOffset(wall, offset));
        }
        return new(ResolveKind.Ok, new DateTimeOffset(wall, tz.GetUtcOffset(wall)));
    }

    // 瞬間 → 某時區的牆上時間
    public static ZonedTime ToZone(DateTimeOffset instant, ZoneOption zone)
    {
        var info = zone.GetOffset(instant);
        return new(instant.ToOffset(info.Offset).DateTime, info);
    }

    // 讓 instant 在 zone 的牆上時間解析回同一個 instant 所需的選擇
    public static AmbiguityChoice ChoiceFor(DateTimeOffset instant, ZoneOption zone)
    {
        var wall = ToZone(instant, zone).Wall;
        var second = Resolve(zone, wall, AmbiguityChoice.Second);
        return second.Kind == ResolveKind.Ambiguous && second.Instant == instant
            ? AmbiguityChoice.Second
            : AmbiguityChoice.First;
    }
}
