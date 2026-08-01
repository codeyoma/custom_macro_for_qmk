using System.IO;
using System.Net.Http;
using MacroTyper.Core.Update;

namespace MacroTyper.Update;

internal enum UpdateOutcome
{
    /// <summary>새 exe 가 제자리에 놓였다. 다시 시작하면 된다.</summary>
    Ready,

    /// <summary>파일 하나짜리 배포본이 아니다. 개발 중의 빌드에서만 나온다.</summary>
    Unsupported,

    /// <summary>내려받지 못했다.</summary>
    DownloadFailed,

    /// <summary>내려받은 파일이 우리가 서명한 것이 아니다.</summary>
    SignatureMismatch,

    /// <summary>exe 자리에 쓸 수 없다. 보통 권한 문제다.</summary>
    CannotReplace,
}

internal sealed record UpdateResult(UpdateOutcome Outcome, string Message)
{
    public bool IsReady => Outcome == UpdateOutcome.Ready;
}

/// <summary>
/// 새 exe 를 받아 지금 exe 자리에 놓는다. 다시 시작하는 것은 부르는 쪽의 일이다.
///
/// 돌고 있는 exe 는 덮어쓸 수 없지만 이름은 바꿀 수 있다. 그래서 지금 것을 옆으로 밀어 두고
/// 새것을 그 자리에 놓는다. 밀어 둔 것은 다음 실행 때 <see cref="AppIdentity.CleanUpLeftovers"/>가 지운다.
/// </summary>
internal static class UpdateInstaller
{
    public static async Task<UpdateResult> InstallAsync(
        UpdateOffer offer,
        UpdateSource source,
        IProgress<double>? progress = null,
        CancellationToken cancellation = default)
    {
        if (!AppIdentity.IsSingleFileBuild)
            return new UpdateResult(UpdateOutcome.Unsupported, "개발 중의 빌드는 스스로 교체하지 않습니다.");

        string executable = AppIdentity.ExecutablePath;
        string download = executable + AppIdentity.DownloadSuffix;

        // 68MB 를 다 받고 나서 권한이 없다는 것을 알면 그 시간이 통째로 버려진다.
        if (!CanWriteBeside(executable))
        {
            return new UpdateResult(
                UpdateOutcome.CannotReplace,
                "프로그램이 놓인 폴더에 쓸 수 없습니다. exe 를 문서 폴더 같은 곳으로 옮긴 뒤 다시 시도하세요.");
        }

        try
        {
            await source.DownloadAsync(offer.Asset, download, progress, cancellation).ConfigureAwait(false);
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException or UnauthorizedAccessException)
        {
            Discard(download);
            return new UpdateResult(UpdateOutcome.DownloadFailed, "내려받지 못했습니다. 잠시 뒤에 다시 시도하세요.");
        }

        UpdateResult? rejected = Verify(executable, download);

        if (rejected is not null)
        {
            Discard(download);
            return rejected;
        }

        return Swap(executable, download);
    }

    /// <summary>
    /// 받은 파일이 지금 도는 exe 와 같은 키로 서명되었는지 본다. 문제가 없으면 <c>null</c>.
    ///
    /// 두 가지를 따로 확인한다. 서명이 파일 내용과 맞는지(WinVerifyTrust), 그리고
    /// 서명한 인증서가 우리 것인지(지문). 하나만으로는 부족하다.
    /// 서명 블록을 그대로 둔 채 내용만 바꿔치기할 수 있고, 아무 인증서로나 새로 서명할 수도 있다.
    /// </summary>
    private static UpdateResult? Verify(string executable, string download)
    {
        string? expected = Authenticode.Thumbprint(executable);

        if (expected is null)
        {
            return new UpdateResult(
                UpdateOutcome.SignatureMismatch,
                "지금 실행 중인 파일에 서명이 없어 새 파일과 대조할 수 없습니다.");
        }

        SignatureState state = Authenticode.Check(download);

        // Valid 만 받으면 안 된다. 자체 서명이라 인증서를 신뢰할 수 있는 루트에 넣지 않은 PC 에서는
        // 발급자 신뢰가 늘 실패한다. 발급자는 지문으로 따로 확인하므로,
        // 여기서는 "서명이 내용과 맞는가"만 본다.
        if (state is not (SignatureState.Valid or SignatureState.UntrustedPublisher))
        {
            return new UpdateResult(
                UpdateOutcome.SignatureMismatch,
                "내려받은 파일의 서명을 확인할 수 없습니다.");
        }

        string? actual = Authenticode.Thumbprint(download);

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateResult(
                UpdateOutcome.SignatureMismatch,
                "내려받은 파일이 지금 프로그램과 다른 인증서로 서명되어 있습니다.");
        }

        return null;
    }

    private static UpdateResult Swap(string executable, string download)
    {
        string backup = executable + AppIdentity.BackupSuffix;

        // 지난 교체가 남긴 것이 아직 잡혀 있을 수 있다. 그러면 이름을 비켜 준다.
        if (File.Exists(backup) && !TryDelete(backup))
            backup = $"{executable}{AppIdentity.BackupSuffix}-{Environment.TickCount64}";

        try
        {
            File.Move(executable, backup);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Discard(download);
            return new UpdateResult(UpdateOutcome.CannotReplace, "지금 프로그램 파일을 옮길 수 없습니다.");
        }

        try
        {
            File.Move(download, executable);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 여기서 멈추면 exe 가 사라진 자리만 남는다. 반드시 되돌린다.
            TryMoveBack(backup, executable);
            Discard(download);
            return new UpdateResult(UpdateOutcome.CannotReplace, "새 파일을 제자리에 놓지 못했습니다.");
        }

        return new UpdateResult(UpdateOutcome.Ready, string.Empty);
    }

    private static bool CanWriteBeside(string executable)
    {
        string? directory = Path.GetDirectoryName(executable);

        if (string.IsNullOrEmpty(directory))
            return false;

        string probe = Path.Combine(directory, Path.GetFileName(executable) + ".update-probe");

        try
        {
            using var _ = new FileStream(
                probe, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Discard(string path) => TryDelete(path);

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryMoveBack(string backup, string executable)
    {
        try
        {
            File.Move(backup, executable);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 되돌리는 것마저 실패했다. 사용자가 직접 .old 를 원래 이름으로 바꿔야 한다.
        }
    }
}
