using MacroTyper.Core;

namespace MacroTyper.Ui;

/// <summary>치트시트 한 칸. 화면에 필요한 것만 담는다.</summary>
public sealed record OverlayCell(int Number, string Label, bool IsEmpty)
{
    public static OverlayCell From(Slot slot) =>
        new(slot.Index + 1, slot.DisplayName, slot.IsEmpty);
}
