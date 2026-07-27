using MacroTyper.Core;

namespace MacroTyper.Ui;

/// <summary>슬롯 한 칸. 화면에 필요한 것만 담는다. 관리창과 치트시트가 함께 쓴다.</summary>
/// <param name="Preview">라벨 밑에 흐리게 깔 문장. 관리창에서만 쓴다. 치트시트는 칸이 좁다.</param>
/// <param name="AppendEnter">양쪽 다 오른쪽 아래 점으로 표시한다.</param>
public sealed record OverlayCell(int Number, string Label, string Preview, bool IsEmpty, bool AppendEnter)
{
    public static OverlayCell From(Slot slot) =>
        new(slot.Index + 1, slot.DisplayName, slot.Preview, slot.IsEmpty, slot.AppendEnter);
}
