using MacroTyper.Core;

namespace MacroTyper.Tests;

public class HotkeyTests
{
    private const uint VkA = 0x41;
    private const uint VkSpace = 0x20;
    private const uint VkF9 = 0x78;
    private const uint VkOem1 = 0xBA; // ;:

    [Fact]
    public void None_IsNotSet()
    {
        Assert.False(Hotkey.None.IsSet);
    }

    [Fact]
    public void None_DescribesAsEmpty()
    {
        Assert.Equal(string.Empty, Hotkey.None.Describe());
    }

    /// <summary>보조 키만 눌린 상태는 단축키가 아니다.</summary>
    [Fact]
    public void WithoutMainKey_IsNotSet()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0);

        Assert.False(hotkey.IsSet);
    }

    [Fact]
    public void WithMainKey_IsSet()
    {
        Assert.True(new Hotkey(HotkeyModifiers.Control, VkA).IsSet);
    }

    /// <summary>
    /// 보조 키 없는 단축키는 그 키를 어디서도 못 쓰게 만든다.
    /// A 를 단축키로 잡으면 어느 앱에서도 A 가 입력되지 않는다.
    /// </summary>
    [Fact]
    public void WithoutModifier_HasModifierIsFalse()
    {
        Assert.False(new Hotkey(HotkeyModifiers.None, VkA).HasModifier);
    }

    [Fact]
    public void WithModifier_HasModifierIsTrue()
    {
        Assert.True(new Hotkey(HotkeyModifiers.Alt, VkA).HasModifier);
    }

    [Fact]
    public void Describe_SingleModifierAndLetter()
    {
        Assert.Equal("Ctrl + A", new Hotkey(HotkeyModifiers.Control, VkA).Describe());
    }

    /// <summary>보조 키 순서는 항상 같아야 한다. 눌린 순서에 따라 표기가 흔들리면 안 된다.</summary>
    [Fact]
    public void Describe_OrdersModifiersConsistently()
    {
        var all = new Hotkey(
            HotkeyModifiers.Shift | HotkeyModifiers.Windows | HotkeyModifiers.Alt | HotkeyModifiers.Control,
            VkA);

        Assert.Equal("Ctrl + Alt + Shift + Win + A", all.Describe());
    }

    [Fact]
    public void Describe_NamesSpaceKey()
    {
        Assert.Equal("Ctrl + Alt + Space", new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkSpace).Describe());
    }

    [Fact]
    public void Describe_NamesFunctionKey()
    {
        Assert.Equal("Alt + F9", new Hotkey(HotkeyModifiers.Alt, VkF9).Describe());
    }

    [Theory]
    [InlineData(0x30, "0")]
    [InlineData(0x39, "9")]
    [InlineData(0x5A, "Z")]
    public void Describe_NamesDigitsAndLetters(uint virtualKey, string expected)
    {
        Assert.Equal($"Ctrl + {expected}", new Hotkey(HotkeyModifiers.Control, virtualKey).Describe());
    }

    /// <summary>이름을 모르는 키라도 무엇이 잡혔는지는 보여줘야 한다.</summary>
    [Fact]
    public void Describe_FallsBackToHexForUnknownKey()
    {
        string text = new Hotkey(HotkeyModifiers.Control, VkOem1).Describe();

        Assert.Equal("Ctrl + 0xBA", text);
    }

    [Fact]
    public void Describe_WithoutModifier_ShowsKeyAlone()
    {
        Assert.Equal("F9", new Hotkey(HotkeyModifiers.None, VkF9).Describe());
    }

    /// <summary>같은 조합은 같은 값이어야 저장하고 비교할 수 있다.</summary>
    [Fact]
    public void Equality_ComparesByValue()
    {
        var left = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkSpace);
        var right = new Hotkey(HotkeyModifiers.Alt | HotkeyModifiers.Control, VkSpace);

        Assert.Equal(left, right);
    }
}
