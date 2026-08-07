using MacroTyper.Core;
using MacroTyper.Core.Input;

namespace MacroTyper.Tests;

public class InputBuilderTests
{
    private static bool IsKeyUp(NativeInput input) =>
        (input.Data.Keyboard.Flags & KeyboardInput.FlagKeyUp) != 0;

    private static bool IsUnicode(NativeInput input) =>
        (input.Data.Keyboard.Flags & KeyboardInput.FlagUnicode) != 0;

    /// <summary>누름 이벤트에 실린 코드 유닛만 이어 붙인다. 실제로 나가는 글자다.</summary>
    private static string Emitted(string text) => new(
        InputBuilder.Build(text)
            .Where(i => !IsKeyUp(i) && IsUnicode(i))
            .Select(i => (char)i.Data.Keyboard.Scan)
            .ToArray());

    [Fact]
    public void Build_EmptyText_ProducesNoInput()
    {
        Assert.Empty(InputBuilder.Build(string.Empty));
    }

    /// <summary>
    /// 등록한 그대로 나가야 한다. 한 글자도 늘거나 줄거나 바뀌지 않는다.
    ///
    /// 삽입된 글에 공백이 하나 더 생기고 마침표가 전각으로 커진다는 보고가 있었다.
    /// 그런 변형이 여기서 나지 않는다는 것을 못박아 둔다. 이 테스트가 통과하는 한
    /// 원인은 이 아래(IME 또는 대상 앱)이거나, 저장된 문장 자체가 이미 그렇다.
    /// </summary>
    [Theory]
    [InlineData("안녕하세요. 확인 부탁드립니다.")]
    [InlineData("a b  c")]
    [InlineData(". , ! ? : ; ' \" ( ) - _ / \\")]
    [InlineData("  앞뒤 공백  ")]
    public void Build_PassesTextThroughUnchanged(string text)
    {
        Assert.Equal(text, Emitted(text));
    }

    /// <summary>
    /// 전각과 반각은 서로 다른 문자다. 우리가 바꾸지 않으므로 넣은 쪽이 그대로 나온다.
    /// 전각이 나온다면 저장된 문장이 이미 전각이었거나 대상 쪽에서 바뀐 것이다.
    /// </summary>
    [Fact]
    public void Build_DoesNotConvertBetweenHalfAndFullWidth()
    {
        Assert.Equal("a.b", Emitted("a.b"));
        Assert.Equal("ａ．ｂ", Emitted("ａ．ｂ"));
        Assert.Equal("한 칸", Emitted("한 칸"));
        Assert.Equal("한　칸", Emitted("한　칸"));
    }

    [Fact]
    public void Build_SingleCharacter_ProducesKeyDownThenKeyUp()
    {
        var inputs = InputBuilder.Build("A");

        Assert.Equal(2, inputs.Length);
        Assert.False(IsKeyUp(inputs[0]));
        Assert.True(IsKeyUp(inputs[1]));
    }

    [Fact]
    public void Build_SingleCharacter_SendsCodeUnitAsScanWithNoVirtualKey()
    {
        var inputs = InputBuilder.Build("A");

        Assert.All(inputs, i => Assert.True(IsUnicode(i)));
        Assert.All(inputs, i => Assert.Equal(0, i.Data.Keyboard.VirtualKey));
        Assert.All(inputs, i => Assert.Equal('A', i.Data.Keyboard.Scan));
    }

    [Fact]
    public void Build_AllInputs_AreKeyboardType()
    {
        var inputs = InputBuilder.Build("가나다\n라");

        Assert.All(inputs, i => Assert.Equal(NativeInput.TypeKeyboard, i.Type));
    }

    [Fact]
    public void Build_Hangul_SendsCodePointAsScan()
    {
        var inputs = InputBuilder.Build("가");

        Assert.Equal(2, inputs.Length);
        Assert.Equal(0xAC00, inputs[0].Data.Keyboard.Scan);
    }

    [Fact]
    public void Build_MultipleCharacters_ProducesPairPerCharacterInOrder()
    {
        var inputs = InputBuilder.Build("AB");

        Assert.Equal(4, inputs.Length);
        Assert.Equal('A', inputs[0].Data.Keyboard.Scan);
        Assert.Equal('A', inputs[1].Data.Keyboard.Scan);
        Assert.Equal('B', inputs[2].Data.Keyboard.Scan);
        Assert.Equal('B', inputs[3].Data.Keyboard.Scan);
    }

    /// <summary>
    /// 개행을 유니코드 코드 유닛으로 보내면 대부분의 앱이 무시한다.
    /// Enter는 가상 키로 눌러야 실제 줄바꿈이 된다.
    /// </summary>
    [Fact]
    public void Build_LineFeed_SendsReturnAsVirtualKeyNotUnicode()
    {
        var inputs = InputBuilder.Build("\n");

        Assert.Equal(2, inputs.Length);
        Assert.All(inputs, i => Assert.False(IsUnicode(i)));
        Assert.All(inputs, i => Assert.Equal(VirtualKeys.Return, i.Data.Keyboard.VirtualKey));
    }

    [Fact]
    public void Build_CarriageReturnLineFeed_ProducesSingleReturn()
    {
        var inputs = InputBuilder.Build("\r\n");

        Assert.Equal(2, inputs.Length);
        Assert.Equal(VirtualKeys.Return, inputs[0].Data.Keyboard.VirtualKey);
    }

    [Fact]
    public void Build_LoneCarriageReturn_ProducesSingleReturn()
    {
        var inputs = InputBuilder.Build("\r");

        Assert.Equal(2, inputs.Length);
        Assert.Equal(VirtualKeys.Return, inputs[0].Data.Keyboard.VirtualKey);
    }

    [Fact]
    public void Build_TextAroundLineBreak_KeepsOrder()
    {
        var inputs = InputBuilder.Build("가\n나");

        Assert.Equal(6, inputs.Length);
        Assert.Equal(0xAC00, inputs[0].Data.Keyboard.Scan);
        Assert.Equal(VirtualKeys.Return, inputs[2].Data.Keyboard.VirtualKey);
        Assert.Equal(0xB098, inputs[4].Data.Keyboard.Scan);
    }

    /// <summary>탭도 제어 문자라 유니코드로는 들어가지 않는다.</summary>
    [Fact]
    public void Build_Tab_SendsTabAsVirtualKey()
    {
        var inputs = InputBuilder.Build("\t");

        Assert.Equal(2, inputs.Length);
        Assert.All(inputs, i => Assert.False(IsUnicode(i)));
        Assert.All(inputs, i => Assert.Equal(VirtualKeys.Tab, i.Data.Keyboard.VirtualKey));
    }

    /// <summary>
    /// BMP 밖 문자는 UTF-16 서로게이트 페어 두 개로 나뉜다.
    /// 각 코드 유닛을 별도 이벤트로 보내야 조합되어 한 글자로 들어간다.
    /// </summary>
    [Fact]
    public void Build_SurrogatePair_SendsBothCodeUnitsSeparately()
    {
        var inputs = InputBuilder.Build("🙂");

        Assert.Equal(4, inputs.Length);
        Assert.Equal(0xD83D, inputs[0].Data.Keyboard.Scan);
        Assert.Equal(0xD83D, inputs[1].Data.Keyboard.Scan);
        Assert.Equal(0xDE42, inputs[2].Data.Keyboard.Scan);
        Assert.Equal(0xDE42, inputs[3].Data.Keyboard.Scan);
    }

    [Fact]
    public void Build_SurrogatePair_MarksEveryUnitAsUnicode()
    {
        var inputs = InputBuilder.Build("🙂");

        Assert.All(inputs, i => Assert.True(IsUnicode(i)));
    }

    /// <summary>
    /// 널 문자나 벨 같은 나머지 제어 문자는 어떤 앱에서도 의미가 없고
    /// 예상치 못한 동작을 부를 수 있어 버린다.
    /// </summary>
    [Theory]
    [InlineData("\0")]
    [InlineData("\b")]
    [InlineData("")]
    public void Build_OtherControlCharacters_AreSkipped(string text)
    {
        Assert.Empty(InputBuilder.Build(text));
    }

    /// <summary>
    /// 가상 키를 보낼 때 스캔코드를 함께 실어야 스캔코드를 직접 읽는 앱에서도 먹는다.
    /// 가상 키만 보내면 조용히 무시하는 앱이 있다.
    /// </summary>
    [Theory]
    [InlineData("\n", 0x1C)]
    [InlineData("\r\n", 0x1C)]
    [InlineData("\t", 0x0F)]
    public void Build_VirtualKeyCharacters_CarryMatchingScanCode(string text, int expectedScan)
    {
        var inputs = InputBuilder.Build(text);

        Assert.All(inputs, i => Assert.Equal(expectedScan, i.Data.Keyboard.Scan));
    }

    [Fact]
    public void Build_UnicodeCharacters_LeaveVirtualKeyAtZero()
    {
        var inputs = InputBuilder.Build("가A");

        Assert.All(inputs, i => Assert.Equal(0, i.Data.Keyboard.VirtualKey));
    }

    [Fact]
    public void Build_WithAppendEnter_AddsReturnAtEnd()
    {
        var inputs = InputBuilder.Build("A", appendEnter: true);

        Assert.Equal(4, inputs.Length);
        Assert.Equal('A', inputs[0].Data.Keyboard.Scan);
        Assert.Equal(VirtualKeys.Return, inputs[2].Data.Keyboard.VirtualKey);
        Assert.True(IsKeyUp(inputs[3]));
    }

    // --- 키 조합 눌러 주기 ---

    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkAlt = 0x12;
    private const ushort VkWin = 0x5B;
    private const uint VkC = 0x43;
    private const uint VkF5 = 0x74;

    [Fact]
    public void BuildShortcut_Unset_ProducesNothing()
    {
        Assert.Empty(InputBuilder.BuildShortcut(Hotkey.None));
    }

    [Fact]
    public void BuildShortcut_SingleKeyWithoutModifier_PressesAndReleasesIt()
    {
        var inputs = InputBuilder.BuildShortcut(new Hotkey(HotkeyModifiers.None, VkF5));

        Assert.Equal(2, inputs.Length);
        Assert.Equal(VkF5, inputs[0].Data.Keyboard.VirtualKey);
        Assert.False(IsKeyUp(inputs[0]));
        Assert.True(IsKeyUp(inputs[1]));
    }

    /// <summary>
    /// 보조 키는 먼저 누르고 나중에 뗀다. 순서가 어긋나면 조합이 아니라
    /// 낱개 키를 따로 누른 것이 된다.
    /// </summary>
    [Fact]
    public void BuildShortcut_WrapsMainKeyInsideModifier()
    {
        var inputs = InputBuilder.BuildShortcut(new Hotkey(HotkeyModifiers.Control, VkC));

        Assert.Equal(4, inputs.Length);

        Assert.Equal(VkControl, inputs[0].Data.Keyboard.VirtualKey);
        Assert.False(IsKeyUp(inputs[0]));

        Assert.Equal(VkC, inputs[1].Data.Keyboard.VirtualKey);
        Assert.False(IsKeyUp(inputs[1]));

        Assert.Equal(VkC, inputs[2].Data.Keyboard.VirtualKey);
        Assert.True(IsKeyUp(inputs[2]));

        Assert.Equal(VkControl, inputs[3].Data.Keyboard.VirtualKey);
        Assert.True(IsKeyUp(inputs[3]));
    }

    /// <summary>보조 키가 여럿이면 누른 역순으로 뗀다.</summary>
    [Fact]
    public void BuildShortcut_ReleasesModifiersInReverseOrder()
    {
        var inputs = InputBuilder.BuildShortcut(
            new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, VkC));

        Assert.Equal(6, inputs.Length);

        Assert.Equal(VkControl, inputs[0].Data.Keyboard.VirtualKey);
        Assert.Equal(VkShift, inputs[1].Data.Keyboard.VirtualKey);
        Assert.Equal(VkC, inputs[2].Data.Keyboard.VirtualKey);
        Assert.Equal(VkC, inputs[3].Data.Keyboard.VirtualKey);
        Assert.Equal(VkShift, inputs[4].Data.Keyboard.VirtualKey);
        Assert.Equal(VkControl, inputs[5].Data.Keyboard.VirtualKey);
    }

    [Fact]
    public void BuildShortcut_WithAllModifiers_PressesEachOnce()
    {
        var all = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Windows;

        var inputs = InputBuilder.BuildShortcut(new Hotkey(all, VkC));

        Assert.Equal(10, inputs.Length);
        Assert.Equal(VkControl, inputs[0].Data.Keyboard.VirtualKey);
        Assert.Equal(VkAlt, inputs[1].Data.Keyboard.VirtualKey);
        Assert.Equal(VkShift, inputs[2].Data.Keyboard.VirtualKey);
        Assert.Equal(VkWin, inputs[3].Data.Keyboard.VirtualKey);
    }

    /// <summary>조합을 보낼 때는 유니코드 경로를 쓰지 않는다. 진짜 키를 눌러야 앱이 단축키로 받는다.</summary>
    [Fact]
    public void BuildShortcut_NeverUsesUnicodeFlag()
    {
        var inputs = InputBuilder.BuildShortcut(
            new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, VkC));

        Assert.All(inputs, i => Assert.False(IsUnicode(i)));
        Assert.All(inputs, i => Assert.Equal(NativeInput.TypeKeyboard, i.Type));
    }

    [Fact]
    public void Build_WithAppendEnterAndEmptyText_ProducesOnlyReturn()
    {
        var inputs = InputBuilder.Build(string.Empty, appendEnter: true);

        Assert.Equal(2, inputs.Length);
        Assert.Equal(VirtualKeys.Return, inputs[0].Data.Keyboard.VirtualKey);
    }
}
