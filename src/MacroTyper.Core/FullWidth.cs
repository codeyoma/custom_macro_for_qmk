namespace MacroTyper.Core;

/// <summary>
/// 전각 영문·숫자·기호·띄어쓰기를 찾아 반각으로 되돌린다.
///
/// 한글 IME 가 전각 모드(Shift+Space 로 켜지고 꺼진다)인 채로 치면 a 가 ａ(U+FF41),
/// 콤마가 ，(U+FF0C), 띄어쓰기가 　(U+3000) 로 들어간다. 한글은 그대로라 치는 사람은 알아채기 어렵다.
/// 그렇게 저장된 문장은 우리가 저장된 값 그대로 보내므로, 받는 앱에서 글꼴이 다르고 간격이 넓은 글자로 나온다.
///
/// ASCII 와 짝이 하나로 정해지는 것만 다룬다. U+FF01..U+FF5E 는 U+0021..U+007E 로, U+3000 은 U+0020 으로.
/// 그 밖의 전각 기호(￦ ￥ ￠ 등)는 반각 짝이 하나로 정해지지 않아 건드리지 않는다.
/// 바꾸어도 글자 수가 그대로라 커서 위치를 다시 계산할 필요가 없다.
/// </summary>
public static class FullWidth
{
    private const char First = '！';           // ！
    private const char Last = '～';            // ～
    private const char IdeographicSpace = '　';

    /// <summary>전각과 반각 사이의 거리. ！(U+FF01) 에서 !(U+0021) 를 뺀 값이다.</summary>
    private const int Offset = First - '!';

    /// <summary>반각 짝이 있는 전각 글자인가.</summary>
    public static bool IsFullWidth(char c) => c is >= First and <= Last or IdeographicSpace;

    /// <summary>반각 짝이 있는 전각 글자가 몇 개인가.</summary>
    public static int Count(string text)
    {
        int count = 0;

        foreach (char c in text)
        {
            if (IsFullWidth(c))
                count++;
        }

        return count;
    }

    /// <summary>반각 짝이 있는 전각 글자를 모두 반각으로 바꾼다. 바꿀 것이 없으면 받은 문자열을 그대로 돌려준다.</summary>
    public static string ToHalfWidth(string text)
    {
        if (Count(text) == 0)
            return text;

        return string.Create(text.Length, text, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
                span[i] = ToHalfWidth(source[i]);
        });
    }

    public static char ToHalfWidth(char c) => c switch
    {
        IdeographicSpace => ' ',
        >= First and <= Last => (char)(c - Offset),
        _ => c,
    };
}
