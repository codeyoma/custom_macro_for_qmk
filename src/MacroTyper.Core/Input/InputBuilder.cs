namespace MacroTyper.Core.Input;

/// <summary>
/// 문장을 <c>SendInput</c>에 넘길 <see cref="NativeInput"/> 배열로 바꾼다.
///
/// 순수 함수다. OS를 부르지 않으므로 까다로운 규칙(개행, 서로게이트 페어, 제어 문자)을
/// 전부 여기에 모아 두고 테스트로 고정한다.
/// </summary>
public static class InputBuilder
{
    /// <summary>
    /// 문장을 키 입력 이벤트 배열로 바꾼다. 문자 하나당 누름과 뗌 두 개가 나온다.
    /// </summary>
    /// <param name="text">삽입할 문장.</param>
    /// <param name="appendEnter">맨 끝에 Enter를 한 번 더 붙일지.</param>
    public static NativeInput[] Build(string text, bool appendEnter = false)
    {
        var inputs = new List<NativeInput>(text.Length * 2 + 2);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            switch (c)
            {
                case '\r':
                    // \r\n 은 줄바꿈 하나다. Enter를 두 번 보내면 빈 줄이 생긴다.
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    AddVirtualKey(inputs, VirtualKeys.Return);
                    break;

                case '\n':
                    AddVirtualKey(inputs, VirtualKeys.Return);
                    break;

                case '\t':
                    AddVirtualKey(inputs, VirtualKeys.Tab);
                    break;

                default:
                    // 나머지 제어 문자는 어떤 앱에서도 의미가 없고 예상 밖 동작을 부를 수 있어 버린다.
                    // 서로게이트는 IsControl이 false라 여기로 와서 코드 유닛 그대로 실려 나간다.
                    if (!char.IsControl(c))
                        AddUnicodeUnit(inputs, c);
                    break;
            }
        }

        if (appendEnter)
            AddVirtualKey(inputs, VirtualKeys.Return);

        return inputs.ToArray();
    }

    /// <summary>
    /// UTF-16 코드 유닛 하나를 그대로 실어 보낸다.
    /// BMP 밖 문자는 상위/하위 서로게이트가 각각 별도 이벤트로 나가고, 대상 앱이 다시 합친다.
    /// </summary>
    private static void AddUnicodeUnit(List<NativeInput> inputs, char unit)
    {
        inputs.Add(Keyboard(virtualKey: 0, scan: unit, KeyboardInput.FlagUnicode));
        inputs.Add(Keyboard(virtualKey: 0, scan: unit, KeyboardInput.FlagUnicode | KeyboardInput.FlagKeyUp));
    }

    /// <summary>
    /// 진짜 키를 누른다. 개행과 탭은 유니코드로 보내면 대부분의 앱이 무시하기 때문에
    /// 이 경로로만 실제 동작이 난다.
    /// </summary>
    private static void AddVirtualKey(List<NativeInput> inputs, ushort virtualKey)
    {
        ushort scan = VirtualKeys.ScanCodeOf(virtualKey);

        inputs.Add(Keyboard(virtualKey, scan, flags: 0));
        inputs.Add(Keyboard(virtualKey, scan, KeyboardInput.FlagKeyUp));
    }

    private static NativeInput Keyboard(ushort virtualKey, ushort scan, uint flags) => new()
    {
        Type = NativeInput.TypeKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Scan = scan,
                Flags = flags,
                Time = 0,
                ExtraInfo = 0,
            },
        },
    };
}
