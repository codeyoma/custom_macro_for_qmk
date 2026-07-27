using MacroTyper.Core;

namespace MacroTyper.Tests;

public class SlotGridTests
{
    /// <summary>회전된 격자의 그 자리에 놓인 슬롯 번호(1부터). 슬롯이 아니면 실패한다.</summary>
    private static int NumberAt(GridRotation rotation, int row, int column)
    {
        GridCell cell = SlotGrid.CellAt(rotation, row, column);

        Assert.Equal(GridCellKind.Slot, cell.Kind);
        return cell.SlotIndex + 1;
    }

    private static GridCellKind KindAt(GridRotation rotation, int row, int column) =>
        SlotGrid.CellAt(rotation, row, column).Kind;

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
    [InlineData(GridRotation.None)]
    [InlineData(GridRotation.Clockwise90)]
    [InlineData(GridRotation.Half)]
    [InlineData(GridRotation.CounterClockwise90)]
    public void Cells_ContainEverySlotExactlyOnce(GridRotation rotation)
    {
        var slots = SlotGrid.Cells(rotation)
            .Where(c => c.Kind == GridCellKind.Slot)
            .Select(c => c.SlotIndex)
            .Order();

        Assert.Equal(Enumerable.Range(0, MacroProtocol.SlotCount), slots);
    }

    /// <summary>치트시트 키는 어느 방향에서든 딱 하나다.</summary>
    [Theory]
    [InlineData(GridRotation.None)]
    [InlineData(GridRotation.Clockwise90)]
    [InlineData(GridRotation.Half)]
    [InlineData(GridRotation.CounterClockwise90)]
    public void Cells_ContainExactlyOneCheatKey(GridRotation rotation)
    {
        Assert.Single(SlotGrid.Cells(rotation), c => c.Kind == GridCellKind.CheatKey);
    }

    /// <summary>맨 아랫줄만 튀어나와 있으므로 위 세 줄에 빈틈이 셋 생긴다.</summary>
    [Theory]
    [InlineData(GridRotation.None)]
    [InlineData(GridRotation.Clockwise90)]
    [InlineData(GridRotation.Half)]
    [InlineData(GridRotation.CounterClockwise90)]
    public void Cells_ContainThreeBlanks(GridRotation rotation)
    {
        Assert.Equal(3, SlotGrid.Cells(rotation).Count(c => c.Kind == GridCellKind.Blank));
    }

    [Theory]
    [InlineData(GridRotation.None)]
    [InlineData(GridRotation.Clockwise90)]
    [InlineData(GridRotation.Half)]
    [InlineData(GridRotation.CounterClockwise90)]
    public void Cells_FillTheWholeGrid(GridRotation rotation)
    {
        (int rows, int columns) = SlotGrid.SizeFor(rotation);

        Assert.Equal(rows * columns, SlotGrid.Cells(rotation).Count);
    }

    // --- 돌리지 않은 실물 배치 ---

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
    [Fact]
    public void CellAt_ClockwiseAndCounterClockwise_AreOpposites()
    {
        (int rows, int columns) = SlotGrid.SizeFor(GridRotation.Clockwise90);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                GridCell clockwise = SlotGrid.CellAt(GridRotation.Clockwise90, row, column);
                GridCell counter = SlotGrid.CellAt(
                    GridRotation.CounterClockwise90, rows - 1 - row, columns - 1 - column);

                Assert.Equal(clockwise, counter);
            }
        }
    }
}
