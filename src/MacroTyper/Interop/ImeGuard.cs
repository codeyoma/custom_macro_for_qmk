namespace MacroTyper.Interop;

/// <summary>
/// 삽입하는 동안 대상 창의 IME를 잠시 닫아 두고, 끝나면 되돌린다.
///
/// 한글 IME가 켜진 채로 유니코드 입력을 주입하면 글자가 IME 조합 파이프라인에 섞여
/// 순서가 뒤바뀌거나 깨지는 사례가 보고되어 있다.
///
/// 다만 이건 확실한 해법이 아니라 최선의 시도다.
/// ImmGetContext 계열은 프로세스 경계를 넘지 못해서 다른 앱의 IME를 직접 확정시킬 수 없고,
/// 남은 경로인 WM_IME_CONTROL 은 Windows 11의 TSF 기반 새 IME에서 조용히 실패할 수 있다.
/// 그래서 실패해도 삽입은 그대로 진행한다. 막는 것보다 넣어 보는 편이 낫다.
/// </summary>
internal sealed class ImeGuard : IDisposable
{
    private const uint SendTimeoutMs = 200;

    private readonly nint _imeWindow;
    private readonly bool _wasOpen;

    private ImeGuard(nint imeWindow, bool wasOpen)
    {
        _imeWindow = imeWindow;
        _wasOpen = wasOpen;
    }

    /// <summary>대상 창의 IME가 열려 있으면 닫는다. 아무것도 못 해도 유효한 객체를 돌려준다.</summary>
    public static ImeGuard CloseFor(nint targetWindow)
    {
        if (targetWindow == 0)
            return new ImeGuard(0, wasOpen: false);

        nint imeWindow = NativeMethods.ImmGetDefaultIMEWnd(targetWindow);

        if (imeWindow == 0)
            return new ImeGuard(0, wasOpen: false);

        if (!TrySend(imeWindow, NativeMethods.ImcGetOpenStatus, 0, out nint status) || status == 0)
            return new ImeGuard(0, wasOpen: false);

        // 열려 있다. 닫아서 조합 중이던 글자를 확정시킨다.
        TrySend(imeWindow, NativeMethods.ImcSetOpenStatus, 0, out _);

        return new ImeGuard(imeWindow, wasOpen: true);
    }

    public void Dispose()
    {
        if (_imeWindow == 0 || !_wasOpen)
            return;

        TrySend(_imeWindow, NativeMethods.ImcSetOpenStatus, 1, out _);
    }

    private static bool TrySend(nint imeWindow, nint command, nint value, out nint result)
    {
        // 응답 없는 창에 걸려 삽입 전체가 멈추면 안 된다. 타임아웃을 두고 포기한다.
        nint sent = NativeMethods.SendMessageTimeout(
            imeWindow,
            NativeMethods.WmImeControl,
            command,
            value,
            NativeMethods.SmtoAbortIfHung,
            SendTimeoutMs,
            out result);

        return sent != 0;
    }
}
