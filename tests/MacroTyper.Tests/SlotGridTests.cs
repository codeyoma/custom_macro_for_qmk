using MacroTyper.Core;

namespace MacroTyper.Tests;

public class SlotGridTests
{
    /// <summary>회전된 격자에서 화면 (row, column) 자리에 오는 슬롯 번호(1부터).</summary>
    private static int NumberAt(GridRotation rotation, int row, int column) =>
        SlotGrid.SlotAt(rotation, row, column) + 1;

    [Theory]
    [InlineData(GridRotation.None, 4, 6)]
    [InlineData(GridRotation.Half, 4, 6)]
    [InlineData(GridRotation.Clockwise90, 6, 4)]
    [InlineData(GridRotation.CounterClockwise90, 6, 4)]
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
    /// 어떤 방향이든 24개 슬롯이 정확히 한 번씩 나와야 한다.
    /// 하나라도 빠지면 그 문장은 화면에서 영영 볼 수 없게 된다.
    /// </summary>
    [Theory]
    [InlineData(GridRotation.None)]
    [InlineData(GridRotation.Clockwise90)]
    [InlineData(GridRotation.Half)]
    [InlineData(GridRotation.CounterClockwise90)]
    public void Order_ContainsEverySlotExactlyOnce(GridRotation rotation)
    {
        var order = SlotGrid.Order(rotation);

        Assert.Equal(MacroProtocol.SlotCount, order.Count);
        Assert.Equal(Enumerable.Range(0, MacroProtocol.SlotCount), order.Order());
    }

    [Fact]
    public void Order_WithoutRotation_IsPlainSequence()
    {
        Assert.Equal(Enumerable.Range(0, MacroProtocol.SlotCount), SlotGrid.Order(GridRotation.None));
    }

    /// <summary>돌리지 않으면 왼쪽 위가 1번, 오른쪽 아래가 24번이다.</summary>
    [Fact]
    public void SlotAt_WithoutRotation_MatchesPhysicalLayout()
    {
        Assert.Equal(1, NumberAt(GridRotation.None, 0, 0));
        Assert.Equal(6, NumberAt(GridRotation.None, 0, 5));
        Assert.Equal(19, NumberAt(GridRotation.None, 3, 0));
        Assert.Equal(24, NumberAt(GridRotation.None, 3, 5));
    }

    /// <summary>
    /// 키보드를 시계 방향으로 세우면 왼쪽 위 키(1번)가 오른쪽 위로 간다.
    /// 화면도 같이 돌아야 눈에 보이는 자리와 손이 맞는다.
    /// </summary>
    [Fact]
    public void SlotAt_Clockwise90_MovesTopLeftToTopRight()
    {
        Assert.Equal(1, NumberAt(GridRotation.Clockwise90, 0, 3));
    }

    [Fact]
    public void SlotAt_Clockwise90_MovesBottomLeftToTopLeft()
    {
        // 원래 왼쪽 아래(19번)가 시계 방향 회전 후 왼쪽 위로 온다.
        Assert.Equal(19, NumberAt(GridRotation.Clockwise90, 0, 0));
    }

    [Fact]
    public void SlotAt_Clockwise90_MovesTopRightToBottomRight()
    {
        Assert.Equal(6, NumberAt(GridRotation.Clockwise90, 5, 3));
    }

    [Fact]
    public void SlotAt_Half_MirrorsBothAxes()
    {
        Assert.Equal(24, NumberAt(GridRotation.Half, 0, 0));
        Assert.Equal(19, NumberAt(GridRotation.Half, 0, 5));
        Assert.Equal(6, NumberAt(GridRotation.Half, 3, 0));
        Assert.Equal(1, NumberAt(GridRotation.Half, 3, 5));
    }

    /// <summary>반시계로 세우면 오른쪽 위 키(6번)가 왼쪽 위로 온다.</summary>
    [Fact]
    public void SlotAt_CounterClockwise90_MovesTopRightToTopLeft()
    {
        Assert.Equal(6, NumberAt(GridRotation.CounterClockwise90, 0, 0));
    }

    [Fact]
    public void SlotAt_CounterClockwise90_MovesTopLeftToBottomLeft()
    {
        Assert.Equal(1, NumberAt(GridRotation.CounterClockwise90, 5, 0));
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

    /// <summary>
    /// 두 번 돌린 것은 180도와 같아야 한다. 회전 변환이 서로 어긋나지 않는지 본다.
    /// </summary>
    [Fact]
    public void SlotAt_TwoQuarterTurns_EqualsHalfTurn()
    {
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 6; column++)
            {
                // 시계 90도로 옮겨간 자리를 다시 시계 90도 돌리면 180도 자리와 같다.
                int viaHalf = SlotGrid.SlotAt(GridRotation.Half, row, column);

                // (row, column) 을 180도 돌린 좌표는 (3-row, 5-column) 이다.
                int direct = SlotGrid.SlotAt(GridRotation.None, 3 - row, 5 - column);

                Assert.Equal(direct, viaHalf);
            }
        }
    }

    /// <summary>시계 90도와 반시계 90도는 서로 반대여야 한다.</summary>
    [Fact]
    public void SlotAt_ClockwiseAndCounterClockwise_AreOpposites()
    {
        for (int row = 0; row < 6; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                int clockwise = SlotGrid.SlotAt(GridRotation.Clockwise90, row, column);
                int counter = SlotGrid.SlotAt(GridRotation.CounterClockwise90, 5 - row, 3 - column);

                Assert.Equal(clockwise, counter);
            }
        }
    }
}
