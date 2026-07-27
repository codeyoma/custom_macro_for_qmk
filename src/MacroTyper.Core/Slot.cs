namespace MacroTyper.Core;

/// <summary>
/// 물리 키 하나에 대응하는 등록 문장.
/// </summary>
/// <param name="Index">0 이상 <see cref="MacroProtocol.SlotCount"/> 미만. 물리 키와 1:1.</param>
/// <param name="Label">치트시트에 표시할 짧은 이름. 문장 앞부분으로는 24개를 구분할 수 없어서 따로 둔다.</param>
/// <param name="Text">실제로 타이핑될 전체 문장. 개행을 포함할 수 있다.</param>
/// <param name="AppendEnter">삽입 직후 Enter를 한 번 더 보낼지.</param>
public sealed record Slot(int Index, string Label, string Text, bool AppendEnter)
{
    /// <summary>삽입할 내용이 없는 슬롯. 키를 눌러도 아무 일도 일어나지 않는다.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Text);

    /// <summary>치트시트에 보여줄 문구. 라벨이 비어 있으면 문장 첫 줄로 대신한다.</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Label))
                return Label;

            if (IsEmpty)
                return string.Empty;

            int lineEnd = Text.IndexOfAny(['\r', '\n']);
            return lineEnd < 0 ? Text : Text[..lineEnd];
        }
    }

    /// <summary>비어 있는 슬롯을 만든다.</summary>
    public static Slot Empty(int index) => new(index, string.Empty, string.Empty, false);
}
