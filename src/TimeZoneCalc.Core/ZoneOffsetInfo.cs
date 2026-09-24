namespace TimeZoneCalc.Core;

public readonly record struct ZoneOffsetInfo(TimeSpan Offset, bool IsDaylight)
{
    public string Label => TimeFormat.Offset(Offset) + (IsDaylight ? " 夏令" : "");
}
