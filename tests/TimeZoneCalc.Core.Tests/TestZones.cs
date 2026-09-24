using TimeZoneCalc.Core;

namespace TimeZoneCalc.Core.Tests;

internal static class TestZones
{
    public static readonly ZoneOption Utc = ZoneOption.FromOffset(TimeSpan.Zero, "UTC", "UTC");
    public static readonly ZoneOption Taipei =
        ZoneOption.FromCity(TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time"), "台北 (Asia/Taipei)");
    public static readonly ZoneOption NewYork =
        ZoneOption.FromCity(TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
    public static readonly ZoneOption Kathmandu = ZoneOption.FromOffset(new TimeSpan(5, 45, 0));

    public static DateTime Wall(int y, int mo, int d, int h, int mi, int s = 0) =>
        new(y, mo, d, h, mi, s, DateTimeKind.Unspecified);
}
