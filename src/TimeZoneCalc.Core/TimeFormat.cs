using System.Globalization;

namespace TimeZoneCalc.Core;

public static class TimeFormat
{
    public static string Wall(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Offset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return $"UTC{sign}{abs.Hours:00}:{abs.Minutes:00}";
    }
}
