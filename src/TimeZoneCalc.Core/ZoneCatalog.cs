namespace TimeZoneCalc.Core;

// 時區選單：UTC、台北、固定偏移量（由小到大）、其餘城市時區
public sealed class ZoneCatalog
{
    public const string TaipeiId = "Taipei Standard Time";
    public const string TaipeiName = "台北 (Asia/Taipei)";

    public ZoneCatalog(IReadOnlyCollection<TimeZoneInfo> systemZones)
    {
        Utc = ZoneOption.FromOffset(TimeSpan.Zero, "UTC", "UTC");

        var taipei = systemZones.FirstOrDefault(z => z.Id == TaipeiId);
        Taipei = taipei is null
            ? ZoneOption.FromOffset(TimeSpan.FromHours(8), "台北 (UTC+08:00 固定)", TaipeiId, isFallback: true)
            : ZoneOption.FromCity(taipei, TaipeiName);

        // 整點 -12～+14，加上系統時區實際用到的非整點偏移量（+05:30、+05:45…）
        var fixedOffsets = Enumerable.Range(-12, 27).Select(h => TimeSpan.FromHours(h))
            .Concat(systemZones.Select(z => z.BaseUtcOffset))
            .Where(o => o != TimeSpan.Zero)
            .Distinct()
            .Order()
            .Select(o => ZoneOption.FromOffset(o));

        // Windows 的 "UTC"、"UTC-11" 之類時區和固定偏移量重複，排除
        var cities = systemZones
            .Where(z => z.Id != TaipeiId && !z.Id.StartsWith("UTC", StringComparison.OrdinalIgnoreCase))
            .OrderBy(z => z.DisplayName, StringComparer.CurrentCulture)
            .Select(z => ZoneOption.FromCity(z));

        All = [Utc, Taipei, .. fixedOffsets, .. cities];
    }

    public static ZoneCatalog CreateDefault() => new(TimeZoneInfo.GetSystemTimeZones());

    public ZoneOption Utc { get; }
    public ZoneOption Taipei { get; }
    public IReadOnlyList<ZoneOption> All { get; }

    public IReadOnlyList<ZoneOption> Filter(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return All;
        return All.Where(z => z.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || z.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
