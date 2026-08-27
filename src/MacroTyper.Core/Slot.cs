namespace MacroTyper.Core;

/// <summary>슬롯을 눌렀을 때 무엇을 할 것인가.</summary>
public enum SlotAction
{
    /// <summary>등록한 문장을 타이핑해 넣는다.</summary>
    Text = 0,

    /// <summary>등록한 키 조합을 그대로 눌러 준다. 예: Ctrl+Shift+T.</summary>
    Shortcut = 1,
}

/// <summary>
/// 물리 키 하나에 대응하는 등록 내용.
/// </summary>
/// <param name="Index">0 이상 <see cref="MacroProtocol.SlotCount"/> 미만. 물리 키와 1:1.</param>
/// <param name="Label">치트시트에 표시할 짧은 이름. 내용만으로는 24개를 구분할 수 없어서 따로 둔다.</param>
/// <param name="Text">타이핑될 문장. <see cref="SlotAction.Text"/>일 때만 쓴다. 개행을 포함할 수 있다.</param>
/// <param name="AppendEnter">문장 삽입 직후 Enter를 한 번 더 보낼지.</param>
/// <param name="Action">문장을 넣을지 키 조합을 누를지.</param>
/// <param name="Shortcut">
/// 보낼 키 조합. <see cref="SlotAction.Shortcut"/>일 때만 쓴다.
/// 전역 단축키와 같은 자료 구조를 쓰지만 뜻이 다르다. 저쪽은 "누르면 반응할 조합"이고
/// 이쪽은 "눌러 줄 조합"이다. 그래서 보조 키 없는 단일 키(F5 등)도 그대로 받는다.
/// </param>
public sealed record Slot(
    int Index,
    string Label,
    string Text,
    bool AppendEnter,
    SlotAction Action = SlotAction.Text,
    Hotkey Shortcut = default)
{
    /// <summary>보낼 것이 없는 슬롯. 키를 눌러도 아무 일도 일어나지 않는다.</summary>
    public bool IsEmpty => Action == SlotAction.Shortcut
        ? !Shortcut.IsSet
        : string.IsNullOrEmpty(Text);

    /// <summary>치트시트에 보여줄 문구. 라벨이 비어 있으면 내용으로 대신한다.</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Label))
                return Label;

            if (IsEmpty)
                return string.Empty;

            if (Action == SlotAction.Shortcut)
                return Shortcut.Describe();

            int lineEnd = Text.IndexOfAny(['\r', '\n']);
            return lineEnd < 0 ? Text : Text[..lineEnd];
        }
    }

    /// <summary>
    /// 라벨 밑에 흐리게 보여줄 내용.
    ///
    /// 문장은 개행과 탭을 공백으로 눕힌다. 좁은 칸에서 줄이 끊기면 오히려 읽기 어렵다.
    /// 라벨이 없을 때는 비워 둔다. 그 경우 <see cref="DisplayName"/>이 이미 내용을 보여주므로
    /// 같은 것이 두 줄로 겹친다.
    /// </summary>
    public string Preview
    {
        get
        {
            if (IsEmpty || string.IsNullOrWhiteSpace(Label))
                return string.Empty;

            if (Action == SlotAction.Shortcut)
                return Shortcut.Describe();

            // 빈 항목을 버리면 연속된 줄바꿈이 공백 하나로 합쳐진다.
            return string.Join(
                    ' ',
                    Text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();
        }
    }

    /// <summary>비어 있는 슬롯을 만든다.</summary>
    public static Slot Empty(int index) => new(index, string.Empty, string.Empty, false);
}
