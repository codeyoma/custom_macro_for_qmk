namespace MacroTyper.Core.Update;

/// <summary>
/// 받아온 릴리즈를 보고 "지금 알릴 것이 있는가"를 판단한다. 여기는 네트워크도 파일도 건드리지 않는다.
/// </summary>
public static class UpdatePlan
{
    /// <summary>
    /// 릴리즈에 올리는 파일 이름. 여기와 릴리즈가 어긋나면 업데이트가 조용히 멈춘다.
    /// 이름을 바꾸려면 이전 버전들이 이미 이 이름을 찾고 있다는 것을 기억해야 한다.
    /// </summary>
    public static string AssetNameFor(AppVariant variant) => variant switch
    {
        AppVariant.Standalone => "MacroTyper-win-x64-standalone.exe",
        _ => "MacroTyper-win-x64.exe",
    };

    public static UpdateOffer? Decide(Version current, AppRelease? release, AppVariant variant)
    {
        if (release is null)
            return null;

        if (release.Version <= ReleaseFeed.Normalize(current))
            return null;

        string wanted = AssetNameFor(variant);

        // 이름이 정확히 맞는 것만 쓴다. lite 를 쓰는 사람에게 standalone 을 내려보내면
        // 0.6MB 를 받던 사람이 68MB 를 받게 된다.
        ReleaseAsset? asset = release.Assets
            .FirstOrDefault(a => string.Equals(a.Name, wanted, StringComparison.OrdinalIgnoreCase));

        if (asset is null || !IsTrustedDownloadUrl(asset.DownloadUrl))
            return null;

        return new UpdateOffer(release.Version, asset, release.PageUrl);
    }

    /// <summary>
    /// 응답이 알려준 주소를 그대로 따라가지 않는다.
    ///
    /// 이 주소로 받은 파일은 곧 지금 도는 exe 를 대신하게 된다. 응답이 어떤 이유로든 바뀌었을 때
    /// 아무 데서나 내려받는 일만은 없어야 한다. 서명 대조가 뒤에 한 겹 더 있지만,
    /// 애초에 남의 서버에 접속조차 하지 않는 편이 낫다.
    /// </summary>
    private static bool IsTrustedDownloadUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttps)
            return false;

        string host = uri.Host;

        return host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }
}
