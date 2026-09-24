using System.Globalization;

namespace TimeZoneCalc.Core;

public enum EntryMode { Time, Date }

// 像小算盤一樣由左往右填入數字：時間 HHmmss、日期 yyyyMMdd
public sealed class DigitEntry
{
    public const int MinYear = 1900;
    public const int MaxYear = 2100;

    private string _digits = "";

    public DigitEntry(EntryMode mode) => Mode = mode;

    public EntryMode Mode { get; }
    public int MaxDigits => Mode == EntryMode.Time ? 6 : 8;
    public string Digits => _digits;

    // 剛載入或剛切換過來：下一個數字會從頭重新填
    public bool IsFresh { get; private set; }

    public bool TryPush(int digit)
    {
        if (digit is < 0 or > 9)
            return false;
        var current = IsFresh ? "" : _digits;
        if (current.Length >= MaxDigits || !Allowed(current, digit))
            return false;
        _digits = current + (char)('0' + digit);
        IsFresh = false;
        return true;
    }

    public bool Backspace()
    {
        IsFresh = false;
        if (_digits.Length == 0)
            return false;
        _digits = _digits[..^1];
        return true;
    }

    public void Clear()
    {
        _digits = "";
        IsFresh = false;
    }

    public void MarkFresh() => IsFresh = true;

    public void Load(TimeOnly time) =>
        LoadDigits(EntryMode.Time, time.ToString("HHmmss", CultureInfo.InvariantCulture));

    public void Load(DateOnly date) =>
        LoadDigits(EntryMode.Date, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

    public TimeOnly Time
    {
        get
        {
            EnsureMode(EntryMode.Time);
            var d = _digits.PadRight(6, '0');
            return new TimeOnly(int.Parse(d[..2]), int.Parse(d[2..4]), int.Parse(d[4..]));
        }
    }

    public DateOnly? Date
    {
        get
        {
            EnsureMode(EntryMode.Date);
            if (_digits.Length < 8)
                return null;
            int y = int.Parse(_digits[..4]), m = int.Parse(_digits[4..6]), d = int.Parse(_digits[6..]);
            if (y < MinYear || y > MaxYear || m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(y, m))
                return null;
            return new DateOnly(y, m, d);
        }
    }

    public string Display
    {
        get
        {
            if (Mode == EntryMode.Time)
            {
                var t = _digits.PadRight(6, '0');
                return $"{t[..2]}:{t[2..4]}:{t[4..]}";
            }
            var d = _digits.PadRight(8, '_');
            return $"{d[..4]}-{d[4..6]}-{d[6..]}";
        }
    }

    private void LoadDigits(EntryMode mode, string digits)
    {
        EnsureMode(mode);
        _digits = digits;
        IsFresh = true;
    }

    private void EnsureMode(EntryMode mode)
    {
        if (Mode != mode)
            throw new InvalidOperationException($"這個輸入欄是 {Mode}，不是 {mode}");
    }

    // 只擋「這一位放下去一定不合法」的數字；日期是否存在等填滿 8 位再判斷
    private bool Allowed(string current, int digit)
    {
        var pos = current.Length;
        if (Mode == EntryMode.Time)
            return pos switch
            {
                0 => digit <= 2,
                1 => current[0] != '2' || digit <= 3,
                2 or 4 => digit <= 5,
                _ => true,
            };
        return pos switch
        {
            0 => digit is 1 or 2,
            4 => digit <= 1,
            5 => current[4] == '0' ? digit >= 1 : digit <= 2,
            6 => digit <= 3,
            7 => current[6] switch { '0' => digit >= 1, '3' => digit <= 1, _ => true },
            _ => true,
        };
    }
}
