namespace MacroTyper.Core;

/// <summary>
/// QMK Raw HID 32바이트 패킷과 <see cref="MacroEvent"/> 사이의 변환.
/// 순수 함수만 담는다. I/O도 상태도 없다.
/// </summary>
public static class MacroProtocol
{
    /// <summary>패킷 첫 바이트. 다른 Raw HID 트래픽(VIA 등)과 구분한다.</summary>
    public const byte Magic = 0xAB;

    /// <summary>QMK RAW_EPSIZE. 패킷은 항상 이 크기로 오간다.</summary>
    public const int PacketSize = 32;

    /// <summary>등록 가능한 문장 개수. Helix Pico 한쪽의 물리 키 수와 같다.</summary>
    public const int SlotCount = 24;

    public const byte CmdPaste = 0x01;
    public const byte CmdOverlayShow = 0x02;
    public const byte CmdOverlayHide = 0x03;
    public const byte CmdPing = 0x10;
    public const byte CmdPong = 0x11;

    /// <summary>
    /// 수신 패킷을 이벤트로 해석한다. 해석할 수 없으면 <c>null</c>을 반환한다.
    /// 알 수 없는 패킷은 예외가 아니라 <c>null</c>이다. 다른 도구의 트래픽이 섞여 들어올 수 있고,
    /// 그때마다 수신 루프가 죽으면 안 되기 때문이다.
    /// </summary>
    public static MacroEvent? Parse(ReadOnlySpan<byte> packet)
    {
        // Windows HID 스택은 report id 바이트를 앞에 붙여 넘겨주기도 한다.
        // QMK Raw HID는 numbered report를 쓰지 않으므로 그 바이트는 0이다. 있으면 벗겨낸다.
        if (packet.Length > 1 && packet[0] == 0x00 && packet[1] == Magic)
            packet = packet[1..];

        if (packet.Length < 3 || packet[0] != Magic)
            return null;

        byte arg = packet[2];

        return packet[1] switch
        {
            CmdPaste => arg < SlotCount ? new MacroEvent.Paste(arg) : null,
            CmdOverlayShow => new MacroEvent.OverlayShow(arg),
            CmdOverlayHide => new MacroEvent.OverlayHide(),
            CmdPong => new MacroEvent.Pong(),
            _ => null,
        };
    }

    /// <summary>키보드에 보낼 핑 패킷을 만든다.</summary>
    public static byte[] BuildPing()
    {
        var packet = new byte[PacketSize];
        packet[0] = Magic;
        packet[1] = CmdPing;
        return packet;
    }
}
