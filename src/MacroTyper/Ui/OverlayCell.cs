using MacroTyper.Core;

namespace MacroTyper.Ui;

/// <summary>슬롯 한 칸. 화면에 필요한 것만 담는다. 관리창과 치트시트가 함께 쓴다.</summary>
/// <param name="AppendEnter">관리창에서 점으로 표시한다. 치트시트는 쓰지 않는다.</param>
public sealed record OverlayCell(int Number, string Label, bool IsEmpty, bool AppendEnter)
{
    public static OverlayCell From(Slot slot) =>
        new(slot.Index + 1, slot.DisplayName, slot.IsEmpty, slot.AppendEnter);
}
