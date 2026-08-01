namespace MacroTyper.Core.Update;

/// <param name="SizeBytes">얼마나 받는지 미리 알려주기 위한 것. 69MB 는 그냥 넘길 크기가 아니다.</param>
public sealed record ReleaseAsset(string Name, string DownloadUrl, long SizeBytes);

/// <param name="PageUrl">자동 교체가 막혔을 때 사람이 직접 갈 곳.</param>
public sealed record AppRelease(
    Version Version,
    string TagName,
    string PageUrl,
    IReadOnlyList<ReleaseAsset> Assets);

/// <summary>지금 받을 수 있는 새 버전 하나. 이게 없으면 알릴 것도 없다.</summary>
public sealed record UpdateOffer(Version Version, ReleaseAsset Asset, string PageUrl);
