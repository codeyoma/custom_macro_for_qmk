using MacroTyper.Core;

namespace MacroTyper.Tests;

public class MacroProtocolTests
{
    /// <summary>선두 바이트만 지정해 32바이트 패킷을 만든다. 나머지는 0 패딩.</summary>
    private static byte[] Packet(params byte[] head)
    {
        var packet = new byte[MacroProtocol.PacketSize];
        head.CopyTo(packet, 0);
        return packet;
    }

    [Fact]
    public void Parse_PasteCommand_ReturnsPasteWithSlotIndex()
    {
        var result = MacroProtocol.Parse(Packet(MacroProtocol.Magic, MacroProtocol.CmdPaste, 7));

        Assert.Equal(new MacroEvent.Paste(7), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public void Parse_PasteCommand_AcceptsBoundarySlotIndexes(byte index)
    {
        var result = MacroProtocol.Parse(Packet(MacroProtocol.Magic, MacroProtocol.CmdPaste, index));

        Assert.Equal(new MacroEvent.Paste(index), result);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(255)]
    public void Parse_PasteCommandWithOutOfRangeSlot_ReturnsNull(byte index)
    {
        var result = MacroProtocol.Parse(Packet(MacroProtocol.Magic, MacroProtocol.CmdPaste, index));

        Assert.Null(result);
    }

    [Fact]
    public void Parse_OverlayShowCommand_ReturnsOverlayShowWithLayer()
    {
        var result = MacroProtocol.Parse(Packet(MacroProtocol.Magic, MacroProtocol.CmdOverlayShow, 3));

        Assert.Equal(new MacroEvent.OverlayShow(3), result);
    }

    [Fact]
    public void Parse_OverlayHideCommand_ReturnsOverlayHide()
    {
        var result = MacroProtocol.Parse(Packet(MacroProtocol.Magic, MacroProtocol.CmdOverlayHide));

        Assert.Equal(new MacroEvent.OverlayHide(), result);
    }

    [Fact]
    public void Parse_PongCommand_ReturnsPong()
    {
        var result = MacroProtocol.Parse(Packet(MacroProtocol.Magic, MacroProtocol.CmdPong));

        Assert.Equal(new MacroEvent.Pong(), result);
    }

    [Fact]
    public void Parse_WrongMagic_ReturnsNull()
    {
        var result = MacroProtocol.Parse(Packet(0xFF, MacroProtocol.CmdPaste, 0));

        Assert.Null(result);
    }

    [Fact]
    public void Parse_UnknownCommand_ReturnsNull()
    {
        var result = MacroProtocol.Parse(Packet(MacroProtocol.Magic, 0x7E, 0));

        Assert.Null(result);
    }

    [Fact]
    public void Parse_TooShortPacket_ReturnsNull()
    {
        var result = MacroProtocol.Parse(new byte[] { MacroProtocol.Magic, MacroProtocol.CmdPaste });

        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyPacket_ReturnsNull()
    {
        var result = MacroProtocol.Parse(ReadOnlySpan<byte>.Empty);

        Assert.Null(result);
    }

    /// <summary>
    /// Windows HID 스택은 report id 바이트를 앞에 붙여 33바이트로 넘겨주는 경우가 있다.
    /// QMK Raw HID는 numbered report를 쓰지 않으므로 그 바이트는 0이다.
    /// 수신 계층이 어느 쪽으로 넘기든 같게 해석되어야 한다.
    /// </summary>
    [Fact]
    public void Parse_PacketPrefixedWithZeroReportId_ParsesSameAsUnprefixed()
    {
        var prefixed = new byte[MacroProtocol.PacketSize + 1];
        prefixed[0] = 0x00;
        prefixed[1] = MacroProtocol.Magic;
        prefixed[2] = MacroProtocol.CmdPaste;
        prefixed[3] = 11;

        var result = MacroProtocol.Parse(prefixed);

        Assert.Equal(new MacroEvent.Paste(11), result);
    }

    [Fact]
    public void BuildPing_ProducesPacketOfProtocolSize()
    {
        var packet = MacroProtocol.BuildPing();

        Assert.Equal(MacroProtocol.PacketSize, packet.Length);
    }

    [Fact]
    public void BuildPing_StartsWithMagicAndPingCommand()
    {
        var packet = MacroProtocol.BuildPing();

        Assert.Equal(MacroProtocol.Magic, packet[0]);
        Assert.Equal(MacroProtocol.CmdPing, packet[1]);
    }

    [Fact]
    public void BuildPing_PadsRemainderWithZeros()
    {
        var packet = MacroProtocol.BuildPing();

        Assert.All(packet[2..], b => Assert.Equal(0, b));
    }
}
