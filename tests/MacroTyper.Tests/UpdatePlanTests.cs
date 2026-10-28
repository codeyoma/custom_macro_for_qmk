using MacroTyper.Core.Update;

namespace MacroTyper.Tests;

public class UpdatePlanTests
{
    private static AppRelease Release(string tag, params string[] assetNames) => new(
        ReleaseFeed.Normalize(Version.Parse(tag)),
        "v" + tag,
        "https://github.com/codeyoma/custom_macro_for_qmk/releases/latest",
        assetNames
            .Select(n => new ReleaseAsset(n, $"https://github.com/codeyoma/custom_macro_for_qmk/releases/download/x/{n}", 1000))
            .ToArray());

    /// <summary>실제 릴리즈에는 exe 말고 펌웨어와 인증서도 함께 올라간다.</summary>
    private static AppRelease FullRelease(string tag) => Release(
        tag, "helix_pico_eunsun.hex", UpdatePlan.AssetName, "codeyoma-publisher.crt");

    [Fact]
    public void OffersNewerVersion()
    {
        UpdateOffer? offer = UpdatePlan.Decide(new Version(0, 1, 0, 0), FullRelease("0.2.0"));

        Assert.NotNull(offer);
        Assert.Equal(new Version(0, 2, 0, 0), offer.Version);
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.0.9")]
    public void StaysQuietWhenNotNewer(string tag)
    {
        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), FullRelease(tag)));
    }

    /// <summary>0.10 은 0.9 보다 높다. 문자열로 비교하면 뒤집힌다.</summary>
    [Fact]
    public void ComparesNumericallyNotAlphabetically()
    {
        Assert.NotNull(UpdatePlan.Decide(new Version(0, 9, 0, 0), FullRelease("0.10.0")));
    }

    /// <summary>
    /// exe 가 아닌 것을 exe 자리에 놓지 않는다.
    /// 릴리즈에 펌웨어 hex 와 인증서가 같이 있으므로 이름이 정확히 맞아야 한다.
    /// </summary>
    [Fact]
    public void PicksTheExeAndNothingElse()
    {
        UpdateOffer? offer = UpdatePlan.Decide(new Version(0, 1, 0, 0), FullRelease("0.2.0"));

        Assert.Equal(UpdatePlan.AssetName, offer!.Asset.Name);
    }

    /// <summary>
    /// exe 가 빠진 릴리즈에는 반응하지 않는다. 펌웨어만 올린 릴리즈가 있을 수 있다.
    /// </summary>
    [Fact]
    public void StaysQuietWhenTheExeIsMissingFromTheRelease()
    {
        AppRelease firmwareOnly = Release("0.2.0", "helix_pico_eunsun.hex");

        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), firmwareOnly));
    }

    [Fact]
    public void StaysQuietWithoutARelease()
    {
        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), null));
    }

    /// <summary>
    /// 내려받을 주소는 응답이 시키는 대로 따라가지 않는다.
    /// 답이 바뀌어도 GitHub 밖으로는 나가지 않아야 한다.
    /// </summary>
    [Theory]
    [InlineData("http://github.com/a/b.exe")]
    [InlineData("https://github.com.attacker.net/a/b.exe")]
    [InlineData("https://example.com/b.exe")]
    [InlineData("file:///C:/b.exe")]
    public void RefusesAssetHostedOutsideGitHub(string url)
    {
        var release = new AppRelease(
            new Version(0, 2, 0, 0),
            "v0.2.0",
            "https://github.com/codeyoma/custom_macro_for_qmk/releases/latest",
            [new ReleaseAsset(UpdatePlan.AssetName, url, 1000)]);

        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), release));
    }

    [Theory]
    [InlineData("https://github.com/codeyoma/x/releases/download/v1/MacroTyper-win-x64-standalone.exe")]
    [InlineData("https://objects.githubusercontent.com/blah/MacroTyper-win-x64-standalone.exe")]
    public void AcceptsGitHubsOwnDownloadHosts(string url)
    {
        var release = new AppRelease(
            new Version(0, 2, 0, 0),
            "v0.2.0",
            "https://github.com/codeyoma/custom_macro_for_qmk/releases/latest",
            [new ReleaseAsset(UpdatePlan.AssetName, url, 1000)]);

        Assert.NotNull(UpdatePlan.Decide(new Version(0, 1, 0, 0), release));
    }

    /// <summary>
    /// 파일 이름은 호환성의 일부다. 돌고 있는 예전 버전들이 이 이름을 찾으므로,
    /// 바꾸는 순간 그들은 새 버전이 나와도 영영 알아채지 못한다.
    /// 이 테스트가 깨지면 릴리즈 자산 이름도 같이 바꿀 것인지 먼저 답해야 한다.
    /// </summary>
    [Fact]
    public void AssetNameIsFrozen()
    {
        Assert.Equal("MacroTyper-win-x64-standalone.exe", UpdatePlan.AssetName);
    }

    /// <summary>
    /// 발행자 인증서(codeyoma-publisher.crt)의 지문. 이 값이 어긋나면 서명 없이 나간 버전을 깐 사람은
    /// 서명된 다음 버전을 영영 자동으로 받지 못한다. v0.2.4 exe 의 서명에서 읽은 값과 같다.
    /// </summary>
    [Fact]
    public void PublisherThumbprintIsFrozen()
    {
        Assert.Equal(
            "08BC97F9DA766A65D8524AE798391CF9B6965E248C58555B30B88DA0C2482DF2",
            UpdatePlan.PublisherThumbprint);
    }

    /// <summary>GetCertHashString 과 같은 형식이어야 문자열 비교가 맞는다. SHA256 은 16진수 64자다.</summary>
    [Fact]
    public void PublisherThumbprintIsUppercaseSha256Hex()
    {
        Assert.Matches("^[0-9A-F]{64}$", UpdatePlan.PublisherThumbprint);
    }

    [Fact]
    public void ExpectedThumbprint_UsesRunningSignatureWhenThereIsOne()
    {
        Assert.Equal("ABCD", UpdatePlan.ExpectedThumbprint("ABCD"));
    }

    /// <summary>서명 없이 나간 버전도 서명된 다음 버전을 받을 수 있어야 한다.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExpectedThumbprint_FallsBackToPublisherWhenRunningExeIsUnsigned(string? running)
    {
        Assert.Equal(UpdatePlan.PublisherThumbprint, UpdatePlan.ExpectedThumbprint(running));
    }
}
