namespace MacroTyper.Core;

/// <summary>매크로패드를 책상에 놓은 방향. 시계 방향으로 잰다.</summary>
public enum GridRotation
{
    /// <summary>가로. 4행 7열.</summary>
    None = 0,

    /// <summary>시계 방향 90도. 세로로 세운 상태. 7행 4열.</summary>
    Clockwise90 = 90,

    /// <summary>뒤집음. 4행 7열.</summary>
    Half = 180,

    /// <summary>반시계 방향 90도. 7행 4열.</summary>
    CounterClockwise90 = 270,
}

/// <summary>격자 한 자리에 무엇이 놓이는가.</summary>
public enum GridCellKind
{
    /// <summary>키가 없는 자리. 맨 아랫줄만 한 칸 튀어나와 있어서 생기는 빈틈이다.</summary>
    Blank,

    /// <summary>치트시트 레이어 키. 문장이 없고 누르면 이 화면이 뜬다.</summary>
    CheatKey,

    /// <summary>문장 슬롯.</summary>
    Slot,
}

/// <summary>격자 한 자리.</summary>
/// <param name="SlotIndex">
/// <see cref="GridCellKind.Slot"/>일 때만 의미가 있다. 나머지는 -1.
/// </param>
public readonly record struct GridCell(GridCellKind Kind, int SlotIndex)
{
    public static GridCell Blank { get; } = new(GridCellKind.Blank, -1);
    public static GridCell CheatKey { get; } = new(GridCellKind.CheatKey, -1);

    public static GridCell Slot(int index) => new(GridCellKind.Slot, index);
}

/// <summary>
/// 매크로패드의 물리 배치를 화면 격자로 옮긴다.
///
/// 실물은 맨 아랫줄만 한 칸 더 왼쪽으로 튀어나와 있다. 그 자리가 치트시트 키다.
/// 위 세 줄의 같은 열은 키가 없는 빈틈이다.
///
///        [1] [2] [3] [4] [5] [6]
///        [7] [8] [9] [10][11][12]
///        [13][14][15][16][17][18]
///   [치트][19][20][21][22][23][24]
///
/// 매크로패드를 돌려 놓으면 화면 격자도 같이 돌아야 눈에 보이는 자리와 손이 맞는다.
/// 슬롯 번호 자체는 절대 바뀌지 않는다. 키보드가 보내는 인덱스는 그대로고,
/// 화면에 어떤 순서로 늘어놓을지만 달라진다.
/// </summary>
public static class SlotGrid
{
    /// <summary>돌리지 않았을 때의 격자. 맨 왼쪽 열을 포함한 크기다.</summary>
    public const int BaseRows = 4;
    public const int BaseColumns = 7;

    /// <summary>한 줄에 놓이는 문장 슬롯 수. 맨 왼쪽 열은 여기에 들어가지 않는다.</summary>
    public const int SlotsPerRow = 6;

    public static bool IsQuarterTurn(GridRotation rotation) =>
        rotation is GridRotation.Clockwise90 or GridRotation.CounterClockwise90;

    /// <summary>회전 후 격자 크기. 90도와 270도에서는 행과 열이 뒤바뀐다.</summary>
    public static (int Rows, int Columns) SizeFor(GridRotation rotation) =>
        IsQuarterTurn(rotation) ? (BaseColumns, BaseRows) : (BaseRows, BaseColumns);

    /// <summary>
    /// 화면 왼쪽 위부터 차례로 놓을 자리들. 길이는 항상 행×열이고,
    /// 그 안에 문장 슬롯 24개가 정확히 한 번씩 들어 있다.
    /// </summary>
    public static IReadOnlyList<GridCell> Cells(GridRotation rotation)
    {
        (int rows, int columns) = SizeFor(rotation);
        var cells = new GridCell[rows * columns];

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                cells[(row * columns) + column] = CellAt(rotation, row, column);
            }
        }

        return cells;
    }

    /// <summary>회전된 격자의 (row, column) 자리에 놓일 것.</summary>
    public static GridCell CellAt(GridRotation rotation, int row, int column)
    {
        // 화면 좌표를 원래 배치의 좌표로 되돌린 뒤 무엇이 놓이는지 본다.
        (int baseRow, int baseColumn) = rotation switch
        {
            GridRotation.Clockwise90 => (BaseRows - 1 - column, row),
            GridRotation.Half => (BaseRows - 1 - row, BaseColumns - 1 - column),
            GridRotation.CounterClockwise90 => (column, BaseColumns - 1 - row),
            _ => (row, column),
        };

        return PhysicalCellAt(baseRow, baseColumn);
    }

    /// <summary>돌리지 않은 실물 배치에서 (row, column) 자리에 놓인 것.</summary>
    private static GridCell PhysicalCellAt(int row, int column)
    {
        if (column == 0)
        {
            // 맨 왼쪽 열은 아랫줄에만 키가 있다. 위 세 줄은 빈틈이다.
            return row == BaseRows - 1 ? GridCell.CheatKey : GridCell.Blank;
        }

        return GridCell.Slot((row * SlotsPerRow) + (column - 1));
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
