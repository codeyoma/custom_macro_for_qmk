namespace MacroTyper.Core.Input;

/// <summary>
/// 긴 문장을 한 번에 보내면 대상 앱이 입력을 놓친다(실측 사례: 1000자 중 434자만 도착).
/// 그래서 나눠서 보내야 하는데, 나누는 위치를 잘못 잡으면 이모지가 깨진다.
///
/// SendInput의 순서 보장은 '한 번의 호출 안'에서만 유효하다. 서로게이트 상위와 하위가
/// 서로 다른 호출로 갈라지면 그 사이에 다른 입력이 끼어들어 글자가 깨질 수 있다.
/// 그래서 자르는 위치를 문자열 단계에서 미리 안전하게 정한다.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// 한 번에 보낼 문자 수. 문자 하나가 입력 이벤트 두 개가 되므로 이 값의 두 배가 실제 이벤트 수다.
    /// 안전한 상한은 앱마다 다르다. 보수적으로 잡았다.
    /// </summary>
    public const int DefaultMaxCharsPerChunk = 50;

    /// <summary>
    /// 문장을 안전하게 나눈다. 이어 붙이면 반드시 원본과 같고, 어느 조각도
    /// 서로게이트 페어를 가로지르지 않는다.
    /// </summary>
    public static IReadOnlyList<string> Split(string text, int maxChars = DefaultMaxCharsPerChunk)
    {
        // 서로게이트 페어는 두 칸을 차지한다. 한 칸짜리 조각으로는 절대 담을 수 없다.
        if (maxChars < 2)
            throw new ArgumentOutOfRangeException(
                nameof(maxChars), maxChars, "조각 크기는 최소 2여야 합니다. 서로게이트 페어가 두 칸을 차지합니다.");

        var chunks = new List<string>();

        int start = 0;
        while (start < text.Length)
        {
            int length = Math.Min(maxChars, text.Length - start);

            // 조각의 마지막 칸이 서로게이트 상위면 짝이 다음 조각으로 넘어가 버린다. 한 칸 물러선다.
            if (start + length < text.Length && char.IsHighSurrogate(text[start + length - 1]))
                length--;

            chunks.Add(text.Substring(start, length));
            start += length;
        }

        return chunks;
    }
}
