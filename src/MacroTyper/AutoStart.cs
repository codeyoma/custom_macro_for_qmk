using System.IO;
using Microsoft.Win32;

namespace MacroTyper;

/// <summary>
/// 로그온할 때 자동으로 뜨게 한다.
///
/// 레지스트리 Run 키를 쓴다. 이 앱은 일반 권한으로 실행되므로 HKCU 로 충분하고,
/// 관리자 권한이 필요 없어 UAC 프롬프트도 뜨지 않는다.
/// (앱을 관리자 권한으로 올리면 Run 키로는 무인 자동 시작이 불가능해지고
///  부팅할 때마다 UAC 창이 뜬다. 그래서 평상시에는 일반 권한을 유지한다.)
/// </summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MacroTyper";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is not null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
        }
    }

    public static void Toggle()
    {
        if (IsEnabled)
            Disable();
        else
            Enable();
    }

    private static void Enable()
    {
        string? executable = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executable))
            return;

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(ValueName, $"\"{executable}\"");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // 등록에 실패해도 앱은 계속 돈다. 수동으로 실행하면 된다.
        }
    }

    private static void Disable()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
    }
}
