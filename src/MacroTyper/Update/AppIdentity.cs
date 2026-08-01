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
    /// 어느 exe 를 받아야 하는지.
    ///
    /// 빌드할 때 <c>SelfContained</c> 값을 어셈블리에 새겨 둔다. 실행 중에 알아내려 하면
    /// 런타임이 품어져 있는지를 확실히 구분할 방법이 없다.
    /// </summary>
    public static AppVariant Variant { get; } =
        Self.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SelfContained")?.Value?
            .Equals("true", StringComparison.OrdinalIgnoreCase) == true
            ? AppVariant.Standalone
            : AppVariant.Lite;

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
    /// </summary>
    public static void CleanUpLeftovers()
    {
        string executable = ExecutablePath;

        if (string.IsNullOrEmpty(executable))
            return;

        string? directory = Path.GetDirectoryName(executable);

        if (string.IsNullOrEmpty(directory))
            return;

        string stem = Path.GetFileName(executable);

        foreach (string suffix in new[] { BackupSuffix, DownloadSuffix })
        {
            try
            {
                foreach (string leftover in Directory.EnumerateFiles(directory, stem + suffix + "*"))
                    File.Delete(leftover);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // 아직 잡혀 있거나 권한이 없다. 다음에 다시 해 본다.
                // 남은 파일 하나 때문에 시작을 막을 이유는 없다.
            }
        }
    }
}
