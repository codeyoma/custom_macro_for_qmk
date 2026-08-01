namespace MacroTyper.Core.Update;

/// <summary>
/// 이 복사본이 어떻게 빌드되었는지. 릴리즈에 올라간 exe 가 둘이라 하나를 골라야 한다.
/// </summary>
public enum AppVariant
{
    /// <summary>.NET 런타임이 따로 깔려 있어야 도는 작은 exe.</summary>
    Lite,

    /// <summary>런타임을 품고 있어 그냥 도는 큰 exe.</summary>
    Standalone,
}

/// <param name="SizeBytes">얼마나 받는지 미리 알려주기 위한 것. 68MB 와 0.6MB 는 다른 결정이다.</param>
public sealed record ReleaseAsset(string Name, string DownloadUrl, long SizeBytes);

/// <param name="PageUrl">자동 교체가 막혔을 때 사람이 직접 갈 곳.</param>
public sealed record AppRelease(
    Version Version,
    string TagName,
    string PageUrl,
    IReadOnlyList<ReleaseAsset> Assets);

/// <summary>지금 받을 수 있는 새 버전 하나. 이게 없으면 알릴 것도 없다.</summary>
public sealed record UpdateOffer(Version Version, ReleaseAsset Asset, string PageUrl);
