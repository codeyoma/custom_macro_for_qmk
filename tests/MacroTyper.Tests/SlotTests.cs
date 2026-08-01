using MacroTyper.Core;

namespace MacroTyper.Tests;

public class SlotTests
{
    [Fact]
    public void Empty_ProducesSlotWithGivenIndexAndNoContent()
    {
        var slot = Slot.Empty(5);

        Assert.Equal(5, slot.Index);
        Assert.Equal(string.Empty, slot.Label);
        Assert.Equal(string.Empty, slot.Text);
        Assert.False(slot.AppendEnter);
    }

    [Fact]
    public void IsEmpty_WithoutText_ReturnsTrue()
    {
        Assert.True(Slot.Empty(0).IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithLabelButNoText_ReturnsTrue()
    {
        var slot = new Slot(0, "이름만 있음", string.Empty, false);

        Assert.True(slot.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WithText_ReturnsFalse()
    {
        var slot = new Slot(0, string.Empty, "안녕하세요", false);

        Assert.False(slot.IsEmpty);
    }

    [Fact]
    public void DisplayName_WithLabel_ReturnsLabel()
    {
        var slot = new Slot(0, "주소", "서울특별시 강남구", false);

        Assert.Equal("주소", slot.DisplayName);
    }

    [Fact]
    public void DisplayName_WithoutLabel_ReturnsFirstLineOfText()
    {
        var slot = new Slot(0, string.Empty, "서울특별시 강남구\n(우) 06234", false);

        Assert.Equal("서울특별시 강남구", slot.DisplayName);
    }

    [Fact]
    public void DisplayName_WithoutLabelAndCrLfText_ReturnsFirstLineWithoutLineBreak()
    {
        var slot = new Slot(0, string.Empty, "첫 줄\r\n둘째 줄", false);

        Assert.Equal("첫 줄", slot.DisplayName);
    }

    [Fact]
    public void DisplayName_WithWhitespaceLabel_FallsBackToText()
    {
        var slot = new Slot(0, "   ", "실제 문장", false);

        Assert.Equal("실제 문장", slot.DisplayName);
    }

    [Fact]
    public void DisplayName_OfEmptySlot_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, Slot.Empty(0).DisplayName);
    }

    // --- 키 조합을 보내는 슬롯 ---

    private static readonly Hotkey CtrlC = new(HotkeyModifiers.Control, 0x43);

    [Fact]
    public void Action_ByDefault_IsText()
    {
        Assert.Equal(SlotAction.Text, Slot.Empty(0).Action);
    }

    [Fact]
    public void IsEmpty_ShortcutSlotWithoutChord_ReturnsTrue()
    {
        var slot = new Slot(0, "라벨", string.Empty, false, SlotAction.Shortcut, Hotkey.None);

        Assert.True(slot.IsEmpty);
    }

    /// <summary>키 조합 슬롯은 문장이 비어 있어도 보낼 것이 있으면 빈 슬롯이 아니다.</summary>
    [Fact]
    public void IsEmpty_ShortcutSlotWithChord_ReturnsFalse()
    {
        var slot = new Slot(0, "복사", string.Empty, false, SlotAction.Shortcut, CtrlC);

        Assert.False(slot.IsEmpty);
    }

    /// <summary>반대로 문장 슬롯은 키 조합이 들어 있어도 문장이 비면 빈 슬롯이다.</summary>
    [Fact]
    public void IsEmpty_TextSlotWithChordButNoText_ReturnsTrue()
    {
        var slot = new Slot(0, "라벨", string.Empty, false, SlotAction.Text, CtrlC);

        Assert.True(slot.IsEmpty);
    }

    [Fact]
    public void DisplayName_ShortcutSlotWithLabel_ReturnsLabel()
    {
        var slot = new Slot(0, "복사", string.Empty, false, SlotAction.Shortcut, CtrlC);

        Assert.Equal("복사", slot.DisplayName);
    }

    /// <summary>라벨이 없으면 무엇을 보내는지라도 보여준다.</summary>
    [Fact]
    public void DisplayName_ShortcutSlotWithoutLabel_ShowsChord()
    {
        var slot = new Slot(0, string.Empty, string.Empty, false, SlotAction.Shortcut, CtrlC);

        Assert.Equal("Ctrl + C", slot.DisplayName);
    }

    [Fact]
    public void Preview_ShortcutSlotWithLabel_ShowsChord()
    {
        var slot = new Slot(0, "복사", string.Empty, false, SlotAction.Shortcut, CtrlC);

        Assert.Equal("Ctrl + C", slot.Preview);
    }

    /// <summary>라벨이 없으면 제목 자리에 이미 조합이 나오므로 아래는 비운다.</summary>
    [Fact]
    public void Preview_ShortcutSlotWithoutLabel_IsEmpty()
    {
        var slot = new Slot(0, string.Empty, string.Empty, false, SlotAction.Shortcut, CtrlC);

        Assert.Equal(string.Empty, slot.Preview);
    }

    [Fact]
    public void Preview_EmptyShortcutSlot_IsEmpty()
    {
        var slot = new Slot(0, "라벨", string.Empty, false, SlotAction.Shortcut, Hotkey.None);

        Assert.Equal(string.Empty, slot.Preview);
    }

    // --- 관리창에서 라벨 밑에 흐리게 보여줄 미리보기 ---

    [Fact]
    public void Preview_OfEmptySlot_IsEmpty()
    {
        Assert.Equal(string.Empty, Slot.Empty(0).Preview);
    }

    /// <summary>
    /// 라벨이 없으면 DisplayName 이 이미 문장을 보여준다.
    /// 미리보기까지 같은 내용을 내면 두 줄이 겹쳐 보인다.
    /// </summary>
    [Fact]
    public void Preview_WithoutLabel_IsEmptyToAvoidDuplicatingDisplayName()
    {
        var slot = new Slot(0, string.Empty, "라벨 없는 문장", false);

        Assert.Equal(string.Empty, slot.Preview);
    }

    [Fact]
    public void Preview_WithWhitespaceLabel_IsEmpty()
    {
        var slot = new Slot(0, "   ", "문장", false);

        Assert.Equal(string.Empty, slot.Preview);
    }

    [Fact]
    public void Preview_WithLabel_ReturnsText()
    {
        var slot = new Slot(0, "주소", "서울특별시 강남구 테헤란로 123", false);

        Assert.Equal("서울특별시 강남구 테헤란로 123", slot.Preview);
    }

    /// <summary>좁은 칸에서 줄이 끊기면 오히려 읽기 어렵다. 한 줄로 눕힌다.</summary>
    [Fact]
    public void Preview_FlattensLineBreaksIntoSpaces()
    {
        var slot = new Slot(0, "주소", "서울특별시 강남구\n테헤란로 123\r\n4층", false);

        Assert.Equal("서울특별시 강남구 테헤란로 123 4층", slot.Preview);
    }

    [Fact]
    public void Preview_FlattensTabsIntoSpaces()
    {
        var slot = new Slot(0, "표", "이름\t홍길동", false);

        Assert.Equal("이름 홍길동", slot.Preview);
    }

    [Fact]
    public void Preview_CollapsesConsecutiveLineBreaks()
    {
        var slot = new Slot(0, "문단", "첫 문단\n\n둘째 문단", false);

        Assert.Equal("첫 문단 둘째 문단", slot.Preview);
    }

    [Fact]
    public void Preview_TrimsSurroundingWhitespace()
    {
        var slot = new Slot(0, "라벨", "\n  본문  \n", false);

        Assert.Equal("본문", slot.Preview);
    }
}
