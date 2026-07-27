using System.Runtime.InteropServices;
using MacroTyper.Core.Input;

namespace MacroTyper.Interop;

/// <summary>
/// 이 프로그램이 쓰는 Win32 호출 모음.
/// </summary>
internal static class NativeMethods
{
    // --- 입력 주입 ---

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint count, NativeInput[] inputs, int size);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    // --- 무결성 수준 확인 (UIPI 차단 사전 감지) ---

    internal const uint ProcessQueryLimitedInformation = 0x1000;
    internal const uint TokenQuery = 0x0008;
    internal const int TokenIntegrityLevel = 25;

    // 대상 창이 우리보다 높은 무결성 수준이면 SendInput이 조용히 무시된다.
    internal const int SecurityMandatoryMediumRid = 0x2000;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformation(
        nint tokenHandle, int tokenInformationClass, nint tokenInformation, int length, out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern nint GetSidSubAuthority(nint sid, uint index);

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern nint GetSidSubAuthorityCount(nint sid);

    [StructLayout(LayoutKind.Sequential)]
    internal struct TokenMandatoryLabel
    {
        public SidAndAttributes Label;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SidAndAttributes
    {
        public nint Sid;
        public uint Attributes;
    }

    // --- IME ---

    internal const uint WmImeControl = 0x0283;
    internal const nint ImcGetOpenStatus = 0x0005;
    internal const nint ImcSetOpenStatus = 0x0006;
    internal const uint SmtoAbortIfHung = 0x0002;

    [DllImport("imm32.dll")]
    internal static extern nint ImmGetDefaultIMEWnd(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SendMessageTimeout(
        nint hWnd, uint msg, nint wParam, nint lParam, uint flags, uint timeoutMs, out nint result);

    // --- 전역 단축키 ---

    internal const uint WmHotkey = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(nint hWnd, int id);

    // --- 창 스타일 (오버레이) ---

    internal const int GwlExStyle = -20;

    internal const int WsExTransparent = 0x00000020;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExNoActivate = 0x08000000;

    internal const uint WmMouseActivate = 0x0021;
    internal const nint MaNoActivate = 3;

    /// <summary>
    /// 32비트 프로세스의 user32.dll에는 SetWindowLongPtrW export가 없다.
    /// 무조건 Ptr 버전을 부르면 32비트에서 EntryPointNotFoundException이 난다.
    /// </summary>
    internal static nint SetWindowExStyle(nint hWnd, nint style) =>
        nint.Size == 8
            ? SetWindowLongPtr(hWnd, GwlExStyle, style)
            : SetWindowLong(hWnd, GwlExStyle, (int)style);

    internal static nint GetWindowExStyle(nint hWnd) =>
        nint.Size == 8
            ? GetWindowLongPtr(hWnd, GwlExStyle)
            : GetWindowLong(hWnd, GwlExStyle);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int index, nint newLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int index, int newLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int index);

    // --- 모니터와 창 배치 (오버레이) ---

    internal const uint MonitorDefaultToNearest = 0x00000002;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpShowWindow = 0x0040;

    internal static readonly nint HwndTopmost = -1;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint hWnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint hWnd, out Rect rect);

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint hWnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;

        public static MonitorInfo Create() => new() { Size = Marshal.SizeOf<MonitorInfo>() };
    }
}
