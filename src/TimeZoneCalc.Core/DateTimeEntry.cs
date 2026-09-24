using System.Globalization;

namespace TimeZoneCalc.Core;

public enum Field { Year, Month, Day, Hour, Minute, Second }

// 年月日時分秒六個欄位：點哪一欄就輸入哪一欄，打滿自動跳下一欄；超過上限直接帶入上限
public sealed class DateTimeEntry
{
    public const int MinYear = 1900;
    public const int MaxYear = 2100;

    private readonly int[] _values = [2000, 1, 1, 0, 0, 0];

    // 正在輸入的欄位裡已打的數字；_editing 為 false 時顯示欄位的值
    private string _buffer = "";
    private bool _editing;

    public Field Active { get; private set; } = Field.Hour;

    public static int Width(Field field) => field == Field.Year ? 4 : 2;

    public int Get(Field field) => _values[(int)field];

    // 年只打了一部分時，日期還不完整
    public bool IsComplete => !(_editing && Active == Field.Year && _buffer.Length < 4);

    public DateTime? Wall => IsComplete
        ? new DateTime(_values[0], _values[1], _values[2], _values[3], _values[4], _values[5])
        : null;

    public string Text(Field field) => _editing && field == Active
        ? _buffer.PadRight(Width(field), '_')
        : _values[(int)field].ToString(field == Field.Year ? "0000" : "00", CultureInfo.InvariantCulture);

    public string Display =>
        $"{Text(Field.Year)}-{Text(Field.Month)}-{Text(Field.Day)} {Text(Field.Hour)}:{Text(Field.Minute)}:{Text(Field.Second)}";

    public void Load(DateTime wall)
    {
        _values[0] = wall.Year;
        _values[1] = wall.Month;
        _values[2] = wall.Day;
        _values[3] = wall.Hour;
        _values[4] = wall.Minute;
        _values[5] = wall.Second;
        EndEdit();
    }

    public void Select(Field field)
    {
        EndEdit();
        Active = field;
    }

    public bool Next()
    {
        if (Active == Field.Second)
            return false;
        Select(Active + 1);
        return true;
    }

    public bool Previous()
    {
        if (Active == Field.Year)
            return false;
        Select(Active - 1);
        return true;
    }

    public bool PushDigit(int digit)
    {
        if (digit is < 0 or > 9)
            return false;
        if (!_editing)
        {
            _buffer = "";
            _editing = true;
        }
        _buffer += (char)('0' + digit);
        Apply();
        if (_buffer.Length == Width(Active))
        {
            if (Active == Field.Second)
                EndEdit();
            else
                Select(Active + 1);
        }
        return true;
    }

    public bool Backspace()
    {
        if (!_editing)
        {
            _buffer = Text(Active);
            _editing = true;
        }
        if (_buffer.Length == 0)
            return false;
        _buffer = _buffer[..^1];
        Apply();
        return true;
    }

    public void Clear()
    {
        _buffer = "";
        _editing = true;
        Apply();
    }

    // 年要打滿 4 位才生效（離開時沒打滿就維持原值）；其他欄位每打一位就套用
    private void Apply()
    {
        if (Active == Field.Year)
        {
            if (_buffer.Length == 4)
                SetValue(Field.Year, int.Parse(_buffer, CultureInfo.InvariantCulture));
            return;
        }
        SetValue(Active, _buffer.Length == 0 ? 0 : int.Parse(_buffer, CultureInfo.InvariantCulture));
    }

    private void EndEdit()
    {
        _editing = false;
        _buffer = "";
    }

    private void SetValue(Field field, int value)
    {
        var (min, max) = field switch
        {
            Field.Year => (MinYear, MaxYear),
            Field.Month => (1, 12),
            Field.Day => (1, DateTime.DaysInMonth(_values[0], _values[1])),
            Field.Hour => (0, 23),
            _ => (0, 59),
        };
        _values[(int)field] = Math.Clamp(value, min, max);
        // 改了年或月，原本的日可能超過當月天數
        _values[2] = Math.Min(_values[2], DateTime.DaysInMonth(_values[0], _values[1]));
    }
}
