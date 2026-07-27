namespace MacroTyper.Core;

/// <summary>
/// 매크로패드가 Raw HID로 보낸 신호를 해석한 결과.
/// 이 타입은 HID나 Windows API를 전혀 모른다.
/// </summary>
public abstract record MacroEvent
{
    private MacroEvent() { }

    /// <summary>슬롯의 문장을 활성 창에 삽입하라는 요청.</summary>
    /// <param name="SlotIndex">0 이상 <see cref="MacroProtocol.SlotCount"/> 미만.</param>
    public sealed record Paste(int SlotIndex) : MacroEvent;

    /// <summary>치트시트 오버레이를 표시하라는 요청.</summary>
    /// <param name="Layer">오버레이를 띄운 QMK 레이어 번호.</param>
    public sealed record OverlayShow(int Layer) : MacroEvent;

    /// <summary>치트시트 오버레이를 숨기라는 요청.</summary>
    public sealed record OverlayHide : MacroEvent;

    /// <summary>핑에 대한 키보드의 응답. 연결 확인용.</summary>
    public sealed record Pong : MacroEvent;
}
