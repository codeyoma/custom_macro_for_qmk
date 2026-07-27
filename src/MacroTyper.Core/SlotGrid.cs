namespace MacroTyper.Core;

/// <summary>매크로패드를 책상에 놓은 방향. 시계 방향으로 잰다.</summary>
public enum GridRotation
{
    /// <summary>가로. 4행 6열.</summary>
    None = 0,

    /// <summary>시계 방향 90도. 세로로 세운 상태. 6행 4열.</summary>
    Clockwise90 = 90,

    /// <summary>뒤집음. 4행 6열.</summary>
    Half = 180,

    /// <summary>반시계 방향 90도. 6행 4열.</summary>
    CounterClockwise90 = 270,
}

/// <summary>
/// 매크로패드를 돌려 놓고 쓸 때, 화면 격자도 같이 돌려야 눈에 보이는 자리와 손이 맞는다.
///
/// 슬롯 번호 자체는 절대 바뀌지 않는다. 키보드가 보내는 인덱스는 그대로고,
/// 화면에 어떤 순서로 늘어놓을지만 달라진다.
/// 회전을 바꿔도 등록해 둔 문장이 다른 키로 옮겨가지 않는다.
/// </summary>
public static class SlotGrid
{
    /// <summary>돌리지 않았을 때의 격자. 매크로패드의 물리 배치와 같다.</summary>
    public const int BaseRows = 4;
    public const int BaseColumns = 6;

    public static bool IsQuarterTurn(GridRotation rotation) =>
        rotation is GridRotation.Clockwise90 or GridRotation.CounterClockwise90;

    /// <summary>회전 후 격자 크기. 90도와 270도에서는 행과 열이 뒤바뀐다.</summary>
    public static (int Rows, int Columns) SizeFor(GridRotation rotation) =>
        IsQuarterTurn(rotation) ? (BaseColumns, BaseRows) : (BaseRows, BaseColumns);

    /// <summary>
    /// 화면 왼쪽 위부터 차례로 놓을 슬롯 인덱스. 길이는 항상 24이고 중복이 없다.
    /// </summary>
    public static IReadOnlyList<int> Order(GridRotation rotation)
    {
        (int rows, int columns) = SizeFor(rotation);
        var order = new int[rows * columns];

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                order[(row * columns) + column] = SlotAt(rotation, row, column);
            }
        }

        return order;
    }

    /// <summary>회전된 격자의 (row, column) 자리에 놓일 슬롯 인덱스.</summary>
    public static int SlotAt(GridRotation rotation, int row, int column)
    {
        // 화면 좌표를 원래 배치의 좌표로 되돌린 뒤 인덱스를 계산한다.
        (int baseRow, int baseColumn) = rotation switch
        {
            GridRotation.Clockwise90 => (BaseRows - 1 - column, row),
            GridRotation.Half => (BaseRows - 1 - row, BaseColumns - 1 - column),
            GridRotation.CounterClockwise90 => (column, BaseColumns - 1 - row),
            _ => (row, column),
        };

        return (baseRow * BaseColumns) + baseColumn;
    }

    /// <summary>다음 90도. 계속 누르면 한 바퀴 돈다.</summary>
    public static GridRotation Next(GridRotation rotation) => rotation switch
    {
        GridRotation.None => GridRotation.Clockwise90,
        GridRotation.Clockwise90 => GridRotation.Half,
        GridRotation.Half => GridRotation.CounterClockwise90,
        _ => GridRotation.None,
    };
}
