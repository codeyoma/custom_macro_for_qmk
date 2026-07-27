using MacroTyper.Core;

namespace MacroTyper.Ui;

/// <summary>
/// 격자 한 칸. 관리창과 치트시트가 함께 쓴다.
///
/// 문장 슬롯만 담는 게 아니다. 매크로패드의 맨 아랫줄이 한 칸 튀어나와 있어서
/// 치트시트 키 자리와 그 위의 빈틈도 같이 그려야 실물과 모양이 맞는다.
/// </summary>
/// <param name="Preview">라벨 밑에 흐리게 깔 문장.</param>
/// <param name="AppendEnter">오른쪽 아래 점으로 표시한다.</param>
public sealed record OverlayCell(
    GridCellKind Kind,
    int Number,
    string Label,
    string Preview,
    bool IsEmpty,
    bool AppendEnter)
{
    public static OverlayCell From(GridCell cell, IReadOnlyList<Slot> slots) => cell.Kind switch
    {
        GridCellKind.Slot => FromSlot(slots[cell.SlotIndex]),
        GridCellKind.CheatKey => CheatKey,
        _ => Blank,
    };

    private static OverlayCell FromSlot(Slot slot) => new(
        GridCellKind.Slot,
        slot.Index + 1,
        slot.DisplayName,
        slot.Preview,
        slot.IsEmpty,
        slot.AppendEnter);

    /// <summary>지금 누르고 있는 그 키. 문장이 없고 번호도 없다.</summary>
    private static OverlayCell CheatKey { get; } =
        new(GridCellKind.CheatKey, 0, "치트시트", string.Empty, false, false);

    /// <summary>키가 없는 자리. 칸은 차지하되 보이지 않는다.</summary>
    private static OverlayCell Blank { get; } =
        new(GridCellKind.Blank, 0, string.Empty, string.Empty, true, false);
}
