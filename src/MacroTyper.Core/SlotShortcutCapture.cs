namespace MacroTyper.Core;

/// <summary>슬롯 키 조합 등록 중에 누른 키를 어떻게 처리할지.</summary>
public enum SlotShortcutCaptureAction
{
    Assign,
    Clear,
    Cancel,
}

/// <summary>
/// 슬롯 키 조합 등록 규칙. Delete 는 기존 조합을 지우고 Esc 는 등록을 취소한다.
/// 그 밖의 키는 Backspace 를 포함해 슬롯이 실제로 보내 줄 키로 등록한다.
/// </summary>
public readonly record struct SlotShortcutCapture(
    SlotShortcutCaptureAction Action,
    Hotkey Shortcut)
{
    private const uint VkEscape = 0x1B;
    private const uint VkDelete = 0x2E;

    public static SlotShortcutCapture Decide(Hotkey shortcut) => shortcut.VirtualKey switch
    {
        VkEscape => new(SlotShortcutCaptureAction.Cancel, Hotkey.None),
        VkDelete => new(SlotShortcutCaptureAction.Clear, Hotkey.None),
        _ => new(SlotShortcutCaptureAction.Assign, shortcut),
    };
}
