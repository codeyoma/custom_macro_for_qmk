using System.Runtime.InteropServices;

namespace MacroTyper.Core.Input;

/// <summary>
/// Win32 <c>INPUT</c> 구조체. <c>SendInput</c>에 넘길 배열의 원소다.
///
/// 이 타입들은 Windows에서만 의미가 있지만 순수 데이터라 어디서든 컴파일된다.
/// 덕분에 "문자열을 어떤 입력 이벤트로 바꿀 것인가"를 Windows 없이 테스트할 수 있다.
/// 실제 <c>SendInput</c> 호출만 Windows 전용 프로젝트에 있다.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NativeInput
{
    public uint Type;
    public InputUnion Data;

    public const uint TypeKeyboard = 1;
}

[StructLayout(LayoutKind.Explicit)]
public struct InputUnion
{
    [FieldOffset(0)] public MouseInput Mouse;
    [FieldOffset(0)] public KeyboardInput Keyboard;
    [FieldOffset(0)] public HardwareInput Hardware;
}

[StructLayout(LayoutKind.Sequential)]
public struct KeyboardInput
{
    /// <summary>가상 키 코드. 유니코드 입력에서는 0이어야 한다.</summary>
    public ushort VirtualKey;

    /// <summary>유니코드 입력에서는 UTF-16 코드 유닛이 들어간다.</summary>
    public ushort Scan;

    public uint Flags;
    public uint Time;
    public nint ExtraInfo;

    public const uint FlagExtendedKey = 0x0001;
    public const uint FlagKeyUp = 0x0002;
    public const uint FlagUnicode = 0x0004;
    public const uint FlagScanCode = 0x0008;
}

[StructLayout(LayoutKind.Sequential)]
public struct MouseInput
{
    public int X;
    public int Y;
    public uint Data;
    public uint Flags;
    public uint Time;
    public nint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct HardwareInput
{
    public uint Msg;
    public ushort ParamL;
    public ushort ParamH;
}

/// <summary>이 프로그램이 쓰는 가상 키 코드.</summary>
public static class VirtualKeys
{
    public const ushort Return = 0x0D;
    public const ushort Tab = 0x09;

    public const ushort Shift = 0x10;
    public const ushort Control = 0x11;
    public const ushort Alt = 0x12;
    public const ushort LeftWindows = 0x5B;

    /// <summary>
    /// 가상 키에 대응하는 스캔코드(Set 1). 가상 키만 보내면 조용히 무시하고
    /// 스캔코드를 직접 읽는 앱이 있어서 함께 실어 보낸다.
    /// 물리 키 위치를 가리키는 값이라 키보드 레이아웃과 무관하다.
    /// </summary>
    public static ushort ScanCodeOf(ushort virtualKey) => virtualKey switch
    {
        Return => 0x1C,
        Tab => 0x0F,
        Shift => 0x2A,
        Control => 0x1D,
        Alt => 0x38,
        _ => 0,
    };
}
