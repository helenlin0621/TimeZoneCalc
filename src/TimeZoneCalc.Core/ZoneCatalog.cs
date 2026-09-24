using System.Text.RegularExpressions;

namespace TimeZoneCalc.Core;

// 時區選單：UTC、台北，其餘城市時區依標準偏移量由小到大
public sealed class ZoneCatalog
{
    public const string TaipeiId = "Taipei Standard Time";
    public const string TaipeiName = "台北 (UTC+08:00)";

    // Windows 時區名稱多半不含城市名（例如美東是 "Eastern Time (US & Canada)"），補上常用城市供搜尋
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["Eastern Standard Time"] = "New York 紐約 Toronto 多倫多",
        ["Central Standard Time"] = "Chicago 芝加哥 Dallas Houston",
        ["Mountain Standard Time"] = "Denver 丹佛",
        ["Pacific Standard Time"] = "Los Angeles 洛杉磯 San Francisco 舊金山 Seattle 西雅圖",
        ["GMT Standard Time"] = "London 倫敦 Dublin",
        ["W. Europe Standard Time"] = "Berlin 柏林 Amsterdam 阿姆斯特丹 Rome 羅馬",
        ["Romance Standard Time"] = "Paris 巴黎 Madrid 馬德里 Brussels",
        ["Tokyo Standard Time"] = "Tokyo 東京 Osaka 大阪",
        ["Korea Standard Time"] = "Seoul 首爾",
        ["China Standard Time"] = "Beijing 北京 Shanghai 上海 Hong Kong 香港",
        ["Singapore Standard Time"] = "Singapore 新加坡 Kuala Lumpur 吉隆坡",
        ["SE Asia Standard Time"] = "Bangkok 曼谷 Hanoi 河內 Jakarta 雅加達",
        ["India Standard Time"] = "India 印度 Mumbai New Delhi",
        ["AUS Eastern Standard Time"] = "Sydney 雪梨 Melbourne 墨爾本",
    };

    public ZoneCatalog(IReadOnlyCollection<TimeZoneInfo> systemZones)
    {
        Utc = ZoneOption.FromOffset(TimeSpan.Zero, Label("UTC", TimeSpan.Zero), "UTC");

        var taipei = systemZones.FirstOrDefault(z => z.Id == TaipeiId);
        Taipei = taipei is null
            ? ZoneOption.FromOffset(TimeSpan.FromHours(8), TaipeiName, TaipeiId, isFallback: true)
            : ZoneOption.FromCity(taipei, TaipeiName);

        // Windows 的 "UTC"、"UTC-11" 之類不是城市，排除
        var cities = systemZones
            .Where(z => z.Id != TaipeiId && !z.Id.StartsWith("UTC", StringComparison.OrdinalIgnoreCase))
            .Select(z => ZoneOption.FromCity(z, Label(CityName(z), z.BaseUtcOffset)))
            .OrderBy(z => z.City!.BaseUtcOffset)
            .ThenBy(z => z.DisplayName, StringComparer.CurrentCulture);

        All = [Utc, Taipei, .. cities];
    }

    // 統一格式「名稱 (UTC±hh:mm)」，括號內為標準（非夏令）偏移量
    private static string Label(string name, TimeSpan standardOffset) => $"{name} ({TimeFormat.Offset(standardOffset)})";

    // Windows 顯示名稱是 "(UTC+07:00) 克拉斯諾亞爾斯克"，去掉前面的偏移量
    private static string CityName(TimeZoneInfo zone)
    {
        var name = Regex.Replace(zone.DisplayName, @"^\(UTC[^)]*\)\s*", "").Trim();
        return name.Length == 0 ? zone.Id : name;
    }

    public static ZoneCatalog CreateDefault() => new(TimeZoneInfo.GetSystemTimeZones());

    public ZoneOption Utc { get; }
    public ZoneOption Taipei { get; }
    public IReadOnlyList<ZoneOption> All { get; }

    // 使用者在時區框按 Enter 時要換成的時區；空白或找不到就是 null（維持原時區）
    public ZoneOption? Match(string query) =>
        string.IsNullOrWhiteSpace(query) ? null : Filter(query).FirstOrDefault();

    public IReadOnlyList<ZoneOption> Filter(string query)
    {
        query = query.Trim();
        if (query.Length == 0)
            return All;
        return All.Where(z => z.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || z.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || (!z.IsFixed && Aliases.TryGetValue(z.Id, out var alias)
                               && alias.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
    }
}
