using MacroTyper.Core;

namespace MacroTyper.Tests;

public class FullWidthTests
{
    /// <summary>
    /// 한글 IME 를 전각 모드로 둔 채 "a, b" 를 치면 실제로 들어오는 값.
    /// Windows 11 Microsoft 한국어 IME 에서 잰 것이다.
    /// </summary>
    private const string TypedInFullWidthMode = "ａ，　ｂ";

    [Fact]
    public void ToHalfWidth_RestoresWhatWasTypedInFullWidthMode()
    {
        Assert.Equal("a, b", FullWidth.ToHalfWidth(TypedInFullWidthMode));
    }

    /// <summary>전각 한글 모드에서는 한글은 그대로 오고 콤마와 띄어쓰기만 전각으로 온다.</summary>
    [Fact]
    public void ToHalfWidth_LeavesHangulAndFixesPunctuationAround()
    {
        Assert.Equal("안녕, 하세요", FullWidth.ToHalfWidth("안녕，　하세요"));
    }

    /// <summary>ASCII 출력 가능 문자 94개 모두 전각 짝에서 정확히 되돌아와야 한다.</summary>
    [Fact]
    public void ToHalfWidth_MapsEveryFullWidthAsciiToItsPair()
    {
        for (char ascii = '!'; ascii <= '~'; ascii++)
        {
            char full = (char)(ascii + 0xFEE0);

            Assert.True(FullWidth.IsFullWidth(full), $"U+{(int)full:X4}");
            Assert.Equal(ascii, FullWidth.ToHalfWidth(full));
        }
    }

    [Fact]
    public void ToHalfWidth_TurnsIdeographicSpaceIntoSpace()
    {
        Assert.Equal(' ', FullWidth.ToHalfWidth('　'));
    }

    /// <summary>반각 짝이 하나로 정해지지 않는 전각 기호는 사용자가 고른 값으로 보고 그대로 둔다.</summary>
    [Theory]
    [InlineData('￦')] // ￦
    [InlineData('￥')] // ￥
    [InlineData('￠')] // ￠
    [InlineData('｟')] // ｟ — 범위 바로 바깥
    [InlineData('＀')] // 범위 바로 앞, 할당되지 않은 자리
    public void ToHalfWidth_LeavesSymbolsWithoutAnAsciiPair(char c)
    {
        Assert.False(FullWidth.IsFullWidth(c));
        Assert.Equal(c, FullWidth.ToHalfWidth(c));
    }

    [Theory]
    [InlineData("hello, world")]
    [InlineData("가나다 라마바")]
    [InlineData("ㅁㅠ")]
    [InlineData("漢字")]
    [InlineData("👍 이모지")]
    [InlineData("줄\r\n바꿈\t탭")]
    [InlineData("")]
    public void ToHalfWidth_LeavesTextWithoutFullWidthUntouched(string text)
    {
        Assert.Same(text, FullWidth.ToHalfWidth(text));
        Assert.Equal(0, FullWidth.Count(text));
    }

    [Fact]
    public void ToHalfWidth_KeepsLengthSoCaretPositionsStayValid()
    {
        string mixed = "ＡＢＣ　가나👍，ｘ";

        Assert.Equal(mixed.Length, FullWidth.ToHalfWidth(mixed).Length);
        Assert.Equal("ABC 가나👍,x", FullWidth.ToHalfWidth(mixed));
    }

    [Fact]
    public void Count_CountsOnlyCharactersWithAnAsciiPair()
    {
        Assert.Equal(4, FullWidth.Count(TypedInFullWidthMode));
        Assert.Equal(2, FullWidth.Count("안녕，　하세요￦"));
    }
}
