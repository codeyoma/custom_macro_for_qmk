using System.IO;
using System.Reflection;
using MacroTyper.Core.Update;

namespace MacroTyper.Update;

/// <summary>
/// 지금 도는 이 복사본이 무엇인지. 버전, 어느 exe 로 빌드되었는지, 스스로를 갈아끼울 수 있는지.
/// </summary>
internal static class AppIdentity
{
    /// <summary>교체 도중 밀려난 이전 버전. 다음 실행 때 지운다.</summary>
    public const string BackupSuffix = ".old";

    /// <summary>내려받는 중인 파일. 받다가 끊기면 남는다.</summary>
    public const string DownloadSuffix = ".update-download";

    private static readonly Assembly Self = Assembly.GetEntryAssembly() ?? typeof(AppIdentity).Assembly;

    public static Version Current { get; } =
        ReleaseFeed.Normalize(Self.GetName().Version ?? new Version(0, 0, 0, 0));

    /// <summary>
    /// 스스로를 갈아끼울 수 있는 형태인가.
    ///
    /// 릴리즈에 올라가는 것은 파일 하나짜리 exe 다. 그 경우에만 파일 하나를 바꿔치기하면 끝난다.
    /// 개발 중의 빌드는 dll 이 흩어져 있어서 exe 하나를 바꿔 봐야 소용이 없다.
    /// 파일 하나 배포본에서는 <see cref="Assembly.Location"/>이 빈 문자열이다.
    /// </summary>
#pragma warning disable IL3000 // 빈 문자열이 나오는 그 성질을 일부러 판별에 쓴다.
    public static bool IsSingleFileBuild { get; } =
        string.IsNullOrEmpty(Self.Location) && !string.IsNullOrEmpty(Environment.ProcessPath);
#pragma warning restore IL3000

    public static string ExecutablePath => Environment.ProcessPath ?? string.Empty;

    /// <summary>
    /// 지난 교체가 남긴 파일을 치운다.
    ///
    /// 이전 버전은 교체하는 그 순간에는 지울 수 없다. 그때 아직 돌고 있기 때문이다.
    /// 그래서 이름만 바꿔 두고, 다음 실행인 지금 지운다.
    ///
    /// 그런데 업데이트 직후에는 그 "이전 버전"이 아직 끝나는 중이다. 새 프로세스가 뜨는 사이에
    /// 종료를 마치지 못했으면 첫 시도가 실패한다. 그것으로 포기하면 69MB 짜리 파일이
    /// 다음 실행 때까지 exe 옆에 남는다. 그래서 몇 초 간격으로 몇 번 더 해 본다.
    ///
    /// 시작을 붙잡지 않도록 배경에서 돈다. 치우지 못해도 프로그램이 도는 데는 지장이 없다.
    /// </summary>
    public static void CleanUpLeftovers() => Task.Run(async () =>
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (TryRemoveLeftovers())
                return;

            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        }
    });

    /// <summary>남은 것이 없으면 <c>true</c>. 하나라도 못 지웠으면 <c>false</c>.</summary>
    private static bool TryRemoveLeftovers()
    {
        string executable = ExecutablePath;

        if (string.IsNullOrEmpty(executable))
            return true;

        string? directory = Path.GetDirectoryName(executable);

        if (string.IsNullOrEmpty(directory))
            return true;

        string stem = Path.GetFileName(executable);
        bool clear = true;

        foreach (string suffix in new[] { BackupSuffix, DownloadSuffix })
        {
            try
            {
                foreach (string leftover in Directory.EnumerateFiles(directory, stem + suffix + "*"))
                {
                    try
                    {
                        File.Delete(leftover);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        // 아직 잡혀 있다. 이것 하나 때문에 나머지까지 포기하지는 않는다.
                        clear = false;
                    }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                clear = false;
            }
        }

        return clear;
    }
}
