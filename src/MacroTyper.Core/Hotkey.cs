namespace MacroTyper.Core;

/// <summary>
/// 전역 단축키의 보조 키. 값은 Win32 <c>RegisterHotKey</c>의 <c>fsModifiers</c>와 같다.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
}

/// <summary>
/// 치트시트를 여는 전역 단축키.
///
/// 매크로패드의 레이어 키와는 별개다. 그쪽은 누르고 있는 동안 떠 있지만
/// 이건 한 번 눌러 열고 다시 눌러 닫는다. Windows 가 단축키를 알려줄 때
/// 눌림만 전하고 뗌은 전하지 않기 때문이다.
/// </summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    /// <summary>설정하지 않은 상태.</summary>
    public static Hotkey None => default;

    /// <summary>쓸 수 있는 조합인가. 보조 키만으로는 등록할 수 없다.</summary>
    public bool IsSet => VirtualKey != 0;

    /// <summary>
    /// 보조 키 없이 등록하면 그 키를 다른 앱이 영영 못 쓰게 된다.
    /// A 하나를 단축키로 잡으면 어디서도 A 를 칠 수 없다.
    /// </summary>
    public bool HasModifier => Modifiers != HotkeyModifiers.None;

    /// <summary>사람이 읽을 수 있는 형태. 예: <c>Ctrl + Alt + Space</c>.</summary>
    public string Describe()
    {
        if (!IsSet)
            return string.Empty;

        var parts = new List<string>(5);

        // 순서를 고정한다. 누른 순서에 따라 표기가 흔들리면 설정을 신뢰할 수 없다.
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");

        parts.Add(NameOf(VirtualKey));

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// 가상 키 코드의 이름. 흔히 쓰는 것만 이름을 주고 나머지는 16진수로 보여준다.
    /// 이름을 몰라도 무엇이 잡혔는지는 알 수 있어야 한다.
    /// </summary>
    private static string NameOf(uint virtualKey) => virtualKey switch
    {
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),          // 0-9
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),          // A-Z
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",                // F1-F24
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x14 => "CapsLock",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2C => "PrintScreen",
        0x2D => "Insert",
        0x2E => "Delete",
        _ => $"0x{virtualKey:X2}",
    };
}
