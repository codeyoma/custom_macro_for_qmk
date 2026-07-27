using System.Runtime.InteropServices;

namespace MacroTyper.Interop;

/// <summary>
/// 프로세스의 무결성 수준을 읽는다.
///
/// SendInput은 자기보다 높은 무결성 수준의 창에 입력을 넣지 못하는데,
/// 반환값도 GetLastError도 그 사실을 알려주지 않는다. 그냥 아무 일도 일어나지 않는다.
/// 사용자가 "왜 안 되지"로 시간을 버리지 않도록 미리 확인해서 이유를 말해 준다.
/// </summary>
internal static class ProcessIntegrity
{
    /// <summary>무결성 수준 RID. 값이 클수록 높은 권한이다. 알 수 없으면 <c>null</c>.</summary>
    public static int? GetLevel(uint processId)
    {
        nint process = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation, inheritHandle: false, processId);

        if (process == 0)
            return null;

        try
        {
            if (!NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery, out nint token))
                return null;

            try
            {
                return ReadIntegrityRid(token);
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(process);
        }
    }

    /// <summary>현재 프로세스의 무결성 수준.</summary>
    public static int? GetOwnLevel() => GetLevel((uint)Environment.ProcessId);

    private static int? ReadIntegrityRid(nint token)
    {
        // 첫 호출은 필요한 크기만 알아내려고 일부러 실패시킨다.
        NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, 0, 0, out int needed);

        if (needed <= 0)
            return null;

        nint buffer = Marshal.AllocHGlobal(needed);

        try
        {
            if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, buffer, needed, out _))
                return null;

            var label = Marshal.PtrToStructure<NativeMethods.TokenMandatoryLabel>(buffer);

            nint countPointer = NativeMethods.GetSidSubAuthorityCount(label.Label.Sid);
            if (countPointer == 0)
                return null;

            byte count = Marshal.ReadByte(countPointer);
            if (count == 0)
                return null;

            // 무결성 수준은 SID의 마지막 sub-authority에 들어 있다.
            nint ridPointer = NativeMethods.GetSidSubAuthority(label.Label.Sid, (uint)(count - 1));

            return ridPointer == 0 ? null : Marshal.ReadInt32(ridPointer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
