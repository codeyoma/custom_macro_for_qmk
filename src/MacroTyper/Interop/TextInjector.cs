using System.Runtime.InteropServices;
using MacroTyper.Core.Input;

namespace MacroTyper.Interop;

/// <summary>삽입 시도의 결과. 실패하면 왜 실패했는지 사용자에게 말해 줄 수 있어야 한다.</summary>
public enum InjectionOutcome
{
    Success,

    /// <summary>보낼 내용이 없었다. 빈 슬롯을 눌렀을 때.</summary>
    NothingToInject,

    /// <summary>포커스가 우리 창에 있었다. 그대로 넣으면 우리 입력란에 글이 들어간다.</summary>
    TargetIsSelf,

    /// <summary>대상 창이 우리보다 높은 권한이다. 넣어도 조용히 무시된다.</summary>
    BlockedByElevation,

    /// <summary>일부만 들어갔다. 대상 앱이 입력을 따라오지 못했을 때.</summary>
    Incomplete,
}

/// <summary>
/// 문장을 현재 포커스된 창에 타이핑해 넣는다. 클립보드는 건드리지 않는다.
/// </summary>
public sealed class TextInjector
{
    // 조각 사이 숨 돌릴 틈. 이게 없으면 대상 앱이 입력을 따라오지 못해 조용히 흘린다.
    private const int ChunkDelayMs = 5;

    public InjectionOutcome Inject(string text, bool appendEnter)
    {
        if (string.IsNullOrEmpty(text) && !appendEnter)
            return InjectionOutcome.NothingToInject;

        nint target = NativeMethods.GetForegroundWindow();

        if (target == 0)
            return InjectionOutcome.TargetIsSelf;

        NativeMethods.GetWindowThreadProcessId(target, out uint targetProcessId);

        if (targetProcessId == (uint)Environment.ProcessId)
            return InjectionOutcome.TargetIsSelf;

        if (IsTargetHigherIntegrity(targetProcessId))
            return InjectionOutcome.BlockedByElevation;

        using var _ = ImeGuard.CloseFor(target);

        IReadOnlyList<string> chunks = TextChunker.Split(text);

        if (chunks.Count == 0)
            return Send(InputBuilder.Build(string.Empty, appendEnter))
                ? InjectionOutcome.Success
                : InjectionOutcome.Incomplete;

        for (int i = 0; i < chunks.Count; i++)
        {
            bool isLast = i == chunks.Count - 1;

            if (!Send(InputBuilder.Build(chunks[i], appendEnter && isLast)))
                return InjectionOutcome.Incomplete;

            if (!isLast)
                Thread.Sleep(ChunkDelayMs);
        }

        return InjectionOutcome.Success;
    }

    private static bool Send(NativeInput[] inputs)
    {
        if (inputs.Length == 0)
            return true;

        // cbSize를 하드코딩하면 SendInput이 그냥 실패한다.
        // 구조체 크기는 x64에서 40, x86에서 28로 다르다.
        uint sent = NativeMethods.SendInput(
            (uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());

        return sent == inputs.Length;
    }

    /// <summary>
    /// 대상이 우리보다 높은 권한이면 SendInput이 아무 말 없이 무시된다.
    /// 어느 한쪽이라도 확인이 안 되면 막지 않고 그냥 시도한다.
    /// 확실하지 않은 이유로 기능을 잠그는 것보다 낫다.
    /// </summary>
    private static bool IsTargetHigherIntegrity(uint targetProcessId)
    {
        int? targetLevel = ProcessIntegrity.GetLevel(targetProcessId);
        int? ownLevel = ProcessIntegrity.GetOwnLevel();

        if (targetLevel is null || ownLevel is null)
            return false;

        return targetLevel > ownLevel;
    }
}
