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
}
