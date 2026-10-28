using MacroTyper.Core;

namespace MacroTyper.Tests;

public class SlotGridTests
{
    /// <summary>회전된 격자의 그 자리에 놓인 슬롯 번호(1부터). 슬롯이 아니면 실패한다.</summary>
    private static int NumberAt(GridRotation rotation, int row, int column) =>
        NumberAt(rotation, KeyboardSide.Right, row, column);

    private static int NumberAt(GridRotation rotation, KeyboardSide side, int row, int column)
    {
        GridCell cell = SlotGrid.CellAt(rotation, side, row, column);

        Assert.Equal(GridCellKind.Slot, cell.Kind);
        return cell.SlotIndex + 1;
    }

    private static GridCellKind KindAt(GridRotation rotation, int row, int column) =>
        KindAt(rotation, KeyboardSide.Right, row, column);

    private static GridCellKind KindAt(GridRotation rotation, KeyboardSide side, int row, int column) =>
        SlotGrid.CellAt(rotation, side, row, column).Kind;

    /// <summary>네 방향과 두 반쪽의 모든 조합. 어느 조합이든 지켜야 하는 성질을 확인할 때 쓴다.</summary>
    public static TheoryData<GridRotation, KeyboardSide> AllLayouts()
    {
        var data = new TheoryData<GridRotation, KeyboardSide>();

        foreach (GridRotation rotation in Enum.GetValues<GridRotation>())
        {
            foreach (KeyboardSide side in Enum.GetValues<KeyboardSide>())
                data.Add(rotation, side);
        }

        return data;
    }

    [Theory]
    [InlineData(GridRotation.None, 4, 7)]
    [InlineData(GridRotation.Half, 4, 7)]
    [InlineData(GridRotation.Clockwise90, 7, 4)]
    [InlineData(GridRotation.CounterClockwise90, 7, 4)]
    public void SizeFor_SwapsRowsAndColumnsOnQuarterTurns(GridRotation rotation, int rows, int columns)
    {
        Assert.Equal((rows, columns), SlotGrid.SizeFor(rotation));
    }

    [Theory]
    [InlineData(GridRotation.None, false)]
    [InlineData(GridRotation.Half, false)]
    [InlineData(GridRotation.Clockwise90, true)]
    [InlineData(GridRotation.CounterClockwise90, true)]
    public void IsQuarterTurn_IdentifiesSidewaysOrientations(GridRotation rotation, bool expected)
    {
        Assert.Equal(expected, SlotGrid.IsQuarterTurn(rotation));
    }

    /// <summary>
    /// 어떤 방향이든 문장 24개가 정확히 한 번씩 나와야 한다.
    /// 하나라도 빠지면 그 문장은 화면에서 영영 볼 수 없게 된다.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllLayouts))]
    public void Cells_ContainEverySlotExactlyOnce(GridRotation rotation, KeyboardSide side)
    {
        var slots = SlotGrid.Cells(rotation, side)
            .Where(c => c.Kind == GridCellKind.Slot)
            .Select(c => c.SlotIndex)
            .Order();

        Assert.Equal(Enumerable.Range(0, MacroProtocol.SlotCount), slots);
    }

    /// <summary>치트시트 키는 어느 방향, 어느 반쪽에서든 딱 하나다.</summary>
    [Theory]
    [MemberData(nameof(AllLayouts))]
    public void Cells_ContainExactlyOneCheatKey(GridRotation rotation, KeyboardSide side)
    {
        Assert.Single(SlotGrid.Cells(rotation, side), c => c.Kind == GridCellKind.CheatKey);
    }

    /// <summary>맨 아랫줄만 튀어나와 있으므로 위 세 줄에 빈틈이 셋 생긴다.</summary>
    [Theory]
    [MemberData(nameof(AllLayouts))]
    public void Cells_ContainThreeBlanks(GridRotation rotation, KeyboardSide side)
    {
        Assert.Equal(3, SlotGrid.Cells(rotation, side).Count(c => c.Kind == GridCellKind.Blank));
    }

    [Theory]
    [MemberData(nameof(AllLayouts))]
    public void Cells_FillTheWholeGrid(GridRotation rotation, KeyboardSide side)
    {
        (int rows, int columns) = SlotGrid.SizeFor(rotation);

        Assert.Equal(rows * columns, SlotGrid.Cells(rotation, side).Count);
    }

    // --- 돌리지 않은 실물 배치: 오른쪽 반쪽 ---

    [Fact]
    public void CellAt_WithoutRotation_PutsCheatKeyAtBottomLeft()
    {
        Assert.Equal(GridCellKind.CheatKey, KindAt(GridRotation.None, 3, 0));
    }

    [Fact]
    public void CellAt_WithoutRotation_LeavesLeftColumnBlankAboveCheatKey()
    {
        Assert.Equal(GridCellKind.Blank, KindAt(GridRotation.None, 0, 0));
        Assert.Equal(GridCellKind.Blank, KindAt(GridRotation.None, 1, 0));
        Assert.Equal(GridCellKind.Blank, KindAt(GridRotation.None, 2, 0));
    }

    /// <summary>슬롯은 왼쪽 열을 건너뛰고 두 번째 열부터 시작한다.</summary>
    [Fact]
    public void CellAt_WithoutRotation_StartsSlotsAtSecondColumn()
    {
        Assert.Equal(1, NumberAt(GridRotation.None, 0, 1));
        Assert.Equal(6, NumberAt(GridRotation.None, 0, 6));
        Assert.Equal(19, NumberAt(GridRotation.None, 3, 1));
        Assert.Equal(24, NumberAt(GridRotation.None, 3, 6));
    }

    // --- 돌린 배치 ---

    /// <summary>
    /// 시계 방향으로 세우면 왼쪽 아래 모서리가 왼쪽 위로 온다.
    /// 치트시트 키가 거기 있으므로 세운 상태에서도 왼쪽 위 구석에 남는다.
    /// </summary>
    [Fact]
    public void CellAt_Clockwise90_MovesCheatKeyToTopLeft()
    {
        Assert.Equal(GridCellKind.CheatKey, KindAt(GridRotation.Clockwise90, 0, 0));
    }

    [Fact]
    public void CellAt_Clockwise90_MovesFirstSlotToSecondRowRightEdge()
    {
        // 원래 (0,1) 자리의 1번이 시계 방향 회전 후 (1,3) 으로 간다.
        Assert.Equal(1, NumberAt(GridRotation.Clockwise90, 1, 3));
    }

    [Fact]
    public void CellAt_Half_MovesCheatKeyToTopRight()
    {
        Assert.Equal(GridCellKind.CheatKey, KindAt(GridRotation.Half, 0, 6));
    }

    [Fact]
    public void CellAt_Half_MirrorsSlots()
    {
        Assert.Equal(24, NumberAt(GridRotation.Half, 0, 0));
        Assert.Equal(1, NumberAt(GridRotation.Half, 3, 5));
    }

    [Fact]
    public void CellAt_CounterClockwise90_MovesCheatKeyToBottomRight()
    {
        Assert.Equal(GridCellKind.CheatKey, KindAt(GridRotation.CounterClockwise90, 6, 3));
    }

    /// <summary>90도를 네 번 돌리면 제자리로 돌아온다.</summary>
    [Fact]
    public void Next_CyclesThroughAllFourOrientations()
    {
        var seen = new List<GridRotation>();
        GridRotation current = GridRotation.None;

        for (int i = 0; i < 4; i++)
        {
            current = SlotGrid.Next(current);
            seen.Add(current);
        }

        Assert.Equal(
            [GridRotation.Clockwise90, GridRotation.Half, GridRotation.CounterClockwise90, GridRotation.None],
            seen);
    }

    /// <summary>시계 90도와 반시계 90도는 서로 반대여야 한다.</summary>
    [Theory]
    [InlineData(KeyboardSide.Right)]
    [InlineData(KeyboardSide.Left)]
    public void CellAt_ClockwiseAndCounterClockwise_AreOpposites(KeyboardSide side)
    {
        (int rows, int columns) = SlotGrid.SizeFor(GridRotation.Clockwise90);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                GridCell clockwise = SlotGrid.CellAt(GridRotation.Clockwise90, side, row, column);
                GridCell counter = SlotGrid.CellAt(
                    GridRotation.CounterClockwise90, side, rows - 1 - row, columns - 1 - column);

                Assert.Equal(clockwise, counter);
            }
        }
    }

    // --- 왼쪽 반쪽 ---

    /// <summary>왼쪽 반쪽은 맨 아랫줄이 오른쪽으로 튀어나와 있다. 그 자리가 치트시트 키다.</summary>
    [Fact]
    public void CellAt_LeftSide_PutsCheatKeyAtBottomRight()
    {
        Assert.Equal(GridCellKind.CheatKey, KindAt(GridRotation.None, KeyboardSide.Left, 3, 6));
    }

    [Fact]
    public void CellAt_LeftSide_LeavesRightColumnBlankAboveCheatKey()
    {
        Assert.Equal(GridCellKind.Blank, KindAt(GridRotation.None, KeyboardSide.Left, 0, 6));
        Assert.Equal(GridCellKind.Blank, KindAt(GridRotation.None, KeyboardSide.Left, 1, 6));
        Assert.Equal(GridCellKind.Blank, KindAt(GridRotation.None, KeyboardSide.Left, 2, 6));
    }

    /// <summary>
    /// 번호는 오른쪽 반쪽과 똑같이 왼쪽 위부터 매긴다.
    /// 19번은 13번 바로 아래다. 튀어나온 칸 때문에 한 칸씩 밀리지 않는다.
    /// </summary>
    [Fact]
    public void CellAt_LeftSide_StartsSlotsAtFirstColumn()
    {
        Assert.Equal(1, NumberAt(GridRotation.None, KeyboardSide.Left, 0, 0));
        Assert.Equal(6, NumberAt(GridRotation.None, KeyboardSide.Left, 0, 5));
        Assert.Equal(13, NumberAt(GridRotation.None, KeyboardSide.Left, 2, 0));
        Assert.Equal(19, NumberAt(GridRotation.None, KeyboardSide.Left, 3, 0));
        Assert.Equal(24, NumberAt(GridRotation.None, KeyboardSide.Left, 3, 5));
    }

    /// <summary>두 반쪽은 같은 기판을 뒤집은 것이라 키가 있는 자리의 모양이 서로 거울상이다.</summary>
    [Fact]
    public void CellAt_LeftSide_MirrorsShapeOfRightSide()
    {
        for (int row = 0; row < SlotGrid.BaseRows; row++)
        {
            for (int column = 0; column < SlotGrid.BaseColumns; column++)
            {
                Assert.Equal(
                    KindAt(GridRotation.None, KeyboardSide.Right, row, SlotGrid.BaseColumns - 1 - column),
                    KindAt(GridRotation.None, KeyboardSide.Left, row, column));
            }
        }
    }

    /// <summary>시계 방향으로 세우면 오른쪽 아래 모서리가 왼쪽 아래로 온다.</summary>
    [Fact]
    public void CellAt_LeftSide_Clockwise90_MovesCheatKeyToBottomLeft()
    {
        Assert.Equal(GridCellKind.CheatKey, KindAt(GridRotation.Clockwise90, KeyboardSide.Left, 6, 0));
    }

    [Fact]
    public void CellAt_LeftSide_Half_MovesCheatKeyToTopLeft()
    {
        Assert.Equal(GridCellKind.CheatKey, KindAt(GridRotation.Half, KeyboardSide.Left, 0, 0));
        Assert.Equal(24, NumberAt(GridRotation.Half, KeyboardSide.Left, 0, 1));
    }

    [Fact]
    public void Other_TogglesBetweenSides()
    {
        Assert.Equal(KeyboardSide.Left, SlotGrid.Other(KeyboardSide.Right));
        Assert.Equal(KeyboardSide.Right, SlotGrid.Other(KeyboardSide.Left));
    }
}
