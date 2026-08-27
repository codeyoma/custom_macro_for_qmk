using MacroTyper.Core;

namespace MacroTyper.Tests;

public class SlotShortcutCaptureTests
{
    private const uint VkBackspace = 0x08;
    private const uint VkEscape = 0x1B;
    private const uint VkDelete = 0x2E;

    /// <summary>
    /// Backspace 는 슬롯이 보내 줄 실제 키다. 등록 화면에서 설정 삭제 명령으로
    /// 가로채면 매크로패드에 Backspace 를 배정할 수 없다.
    /// </summary>
    [Fact]
    public void Decide_Backspace_AssignsShortcut()
    {
        var shortcut = new Hotkey(HotkeyModifiers.None, VkBackspace);

        SlotShortcutCapture result = SlotShortcutCapture.Decide(shortcut);

        Assert.Equal(SlotShortcutCaptureAction.Assign, result.Action);
        Assert.Equal(shortcut, result.Shortcut);
    }

    [Fact]
    public void Decide_Delete_ClearsShortcut()
    {
        SlotShortcutCapture result = SlotShortcutCapture.Decide(
            new Hotkey(HotkeyModifiers.None, VkDelete));

        Assert.Equal(SlotShortcutCaptureAction.Clear, result.Action);
        Assert.Equal(Hotkey.None, result.Shortcut);
    }

    [Fact]
    public void Decide_Escape_CancelsCapture()
    {
        SlotShortcutCapture result = SlotShortcutCapture.Decide(
            new Hotkey(HotkeyModifiers.None, VkEscape));

        Assert.Equal(SlotShortcutCaptureAction.Cancel, result.Action);
        Assert.Equal(Hotkey.None, result.Shortcut);
    }
}
