using MacroTyper.Core.Input;

namespace MacroTyper.Tests;

public class TextChunkerTests
{
    [Fact]
    public void Split_EmptyText_ProducesNoChunks()
    {
        Assert.Empty(TextChunker.Split(string.Empty, 10));
    }

    [Fact]
    public void Split_TextShorterThanLimit_ProducesSingleChunk()
    {
        var chunks = TextChunker.Split("안녕하세요", 10);

        Assert.Equal(["안녕하세요"], chunks);
    }

    [Fact]
    public void Split_TextExactlyAtLimit_ProducesSingleChunk()
    {
        var chunks = TextChunker.Split("abcde", 5);

        Assert.Single(chunks);
    }

    [Fact]
    public void Split_TextOneOverLimit_ProducesTwoChunks()
    {
        var chunks = TextChunker.Split("abcdef", 5);

        Assert.Equal(["abcde", "f"], chunks);
    }

    [Fact]
    public void Split_LongText_KeepsEveryChunkWithinLimit()
    {
        string text = new('가', 237);

        var chunks = TextChunker.Split(text, 50);

        Assert.All(chunks, c => Assert.InRange(c.Length, 1, 50));
    }

    /// <summary>어떤 경우에도 잘라 붙이면 원본이 나와야 한다. 한 글자도 잃거나 더해서는 안 된다.</summary>
    [Theory]
    [InlineData("짧은 문장", 50)]
    [InlineData("주소: 서울특별시 강남구 테헤란로 123, 4층\n(우) 06234\t담당자 홍길동", 7)]
    [InlineData("이모지 섞임 🙂🙃😀 그리고 한글 그리고 english 123", 5)]
    [InlineData("🙂🙂🙂🙂🙂🙂🙂🙂", 2)]
    [InlineData("a🙂b🙂c🙂d", 3)]
    public void Split_ThenConcatenate_RestoresOriginal(string text, int maxChars)
    {
        var chunks = TextChunker.Split(text, maxChars);

        Assert.Equal(text, string.Concat(chunks));
    }

    /// <summary>
    /// 조각 하나만 떼어 봐도 그 자체로 온전한 UTF-16이어야 한다.
    /// 짝 잃은 서로게이트가 남으면 그 조각을 보낼 때 글자가 깨진다.
    /// </summary>
    [Theory]
    [InlineData("a🙂b🙂c🙂d", 3)]
    [InlineData("이모지 섞임 🙂🙃😀 그리고 한글", 5)]
    [InlineData("🙂🙂🙂🙂", 2)]
    [InlineData("ab🙂cd🙂ef", 4)]
    public void Split_NeverLeavesOrphanSurrogate(string text, int maxChars)
    {
        var chunks = TextChunker.Split(text, maxChars);

        foreach (string chunk in chunks)
        {
            Assert.False(char.IsHighSurrogate(chunk[^1]), $"조각 '{chunk}' 끝에 짝 잃은 상위 서로게이트가 남았다.");
            Assert.False(char.IsLowSurrogate(chunk[0]), $"조각 '{chunk}' 앞에 짝 잃은 하위 서로게이트가 남았다.");
        }
    }

    [Fact]
    public void Split_SurrogatePairStraddlingBoundary_MovesWholePairToNextChunk()
    {
        // a b c [상위][하위] d — 4칸 제한이면 4번째 칸이 상위 서로게이트다.
        var chunks = TextChunker.Split("abc🙂d", 4);

        Assert.Equal(["abc", "🙂d"], chunks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Split_WithChunkSizeTooSmallForSurrogatePair_Throws(int maxChars)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextChunker.Split("아무 문장", maxChars));
    }

    [Fact]
    public void Split_LongTextOfSurrogatePairsOnly_ProducesEvenLengthChunks()
    {
        string text = string.Concat(Enumerable.Repeat("🙂", 40));

        var chunks = TextChunker.Split(text, 7);

        Assert.All(chunks, c => Assert.Equal(0, c.Length % 2));
    }
}
