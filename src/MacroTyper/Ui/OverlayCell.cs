using MacroTyper.Core;

namespace MacroTyper.Ui;

/// <summary>
/// 격자 한 칸. 관리창과 치트시트가 함께 쓴다.
///
/// 문장 슬롯만 담는 게 아니다. 매크로패드의 맨 아랫줄이 한 칸 튀어나와 있어서
/// 치트시트 키 자리와 그 위의 빈틈도 같이 그려야 실물과 모양이 맞는다.
/// </summary>
/// <param name="Preview">라벨 밑에 흐리게 깔 문장.</param>
/// <param name="AppendEnter">Enter 키캡으로 표시한다.</param>
/// <param name="IsShortcut">
/// 글을 넣는 대신 키를 눌러 주는 칸. 문장 대신 키캡을 늘어놓는다.
/// 누르기 전에 "이건 글이 아니라 키가 나간다"를 알 수 있어야 한다.
/// </param>
/// <param name="Keys">키 조합 칸에서 키캡 하나하나에 적을 이름. 예: Ctrl, T.</param>
/// <param name="HasFullWidth">전각 영문·기호·띄어쓰기가 섞인 문장. 관리창에서 표시해 찾기 쉽게 한다.</param>
/// <param name="IsSelected">관리창에서 지금 편집 중인 칸.</param>
public sealed record OverlayCell(
    GridCellKind Kind,
    int Number,
    string Label,
    string Preview,
    bool IsEmpty,
    bool AppendEnter,
    bool IsShortcut,
    IReadOnlyList<string> Keys,
    bool HasFullWidth,
    bool IsSelected = false)
{
    public static OverlayCell From(GridCell cell, IReadOnlyList<Slot> slots) => cell.Kind switch
    {
        GridCellKind.Slot => FromSlot(slots[cell.SlotIndex]),
        GridCellKind.CheatKey => CheatKey,
        _ => Blank,
    };

    private static OverlayCell FromSlot(Slot slot)
    {
        // 아직 조합을 안 넣은 칸에까지 키캡을 달면 설정된 것처럼 보인다.
        bool isShortcut = slot.Action == SlotAction.Shortcut && !slot.IsEmpty;

        // 라벨 없는 키 조합 칸은 DisplayName 이 곧 조합이라 키캡과 같은 것이 두 번 나온다. 키캡만 남긴다.
        string label = isShortcut && string.IsNullOrWhiteSpace(slot.Label) ? string.Empty : slot.DisplayName;

        return new(
            GridCellKind.Slot,
            slot.Index + 1,
            label,
            isShortcut ? string.Empty : slot.Preview,
            slot.IsEmpty,
            // 키 조합 칸에서는 Enter 가 나가지 않는다. TextInjector 가 그 값을 보지 않는다.
            // 문장 모드에서 켜 둔 채 단축키로 바꾸면 값은 남으므로 여기서 걸러야 한다.
            // 남겨 두면 없는 동작을 있다고 말하는 셈이 된다.
            slot.AppendEnter && slot.Action != SlotAction.Shortcut,
            isShortcut,
            isShortcut ? slot.Shortcut.Describe().Split(" + ") : [],
            slot.Action == SlotAction.Text && FullWidth.Count(slot.Text) > 0);
    }

    /// <summary>지금 누르고 있는 그 키. 문장이 없고 번호도 없다.</summary>
    private static OverlayCell CheatKey { get; } =
        new(GridCellKind.CheatKey, 0, "치트시트", string.Empty, false, false, false, [], false);

    /// <summary>키가 없는 자리. 칸은 차지하되 보이지 않는다.</summary>
    private static OverlayCell Blank { get; } =
        new(GridCellKind.Blank, 0, string.Empty, string.Empty, true, false, false, [], false);
}
