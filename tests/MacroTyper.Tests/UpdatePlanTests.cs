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

    private static AppRelease BothVariants(string tag) =>
        Release(tag, "MacroTyper-win-x64.exe", "MacroTyper-win-x64-standalone.exe");

    [Fact]
    public void OffersNewerVersion()
    {
        UpdateOffer? offer = UpdatePlan.Decide(new Version(0, 1, 0, 0), BothVariants("0.2.0"), AppVariant.Lite);

        Assert.NotNull(offer);
        Assert.Equal(new Version(0, 2, 0, 0), offer.Version);
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.0.9")]
    public void StaysQuietWhenNotNewer(string tag)
    {
        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), BothVariants(tag), AppVariant.Lite));
    }

    /// <summary>0.10 은 0.9 보다 높다. 문자열로 비교하면 뒤집힌다.</summary>
    [Fact]
    public void ComparesNumericallyNotAlphabetically()
    {
        Assert.NotNull(UpdatePlan.Decide(new Version(0, 9, 0, 0), BothVariants("0.10.0"), AppVariant.Lite));
    }

    [Fact]
    public void PicksTheAssetMatchingHowThisCopyWasBuilt()
    {
        UpdateOffer? lite = UpdatePlan.Decide(new Version(0, 1, 0, 0), BothVariants("0.2.0"), AppVariant.Lite);
        UpdateOffer? standalone = UpdatePlan.Decide(new Version(0, 1, 0, 0), BothVariants("0.2.0"), AppVariant.Standalone);

        Assert.Equal("MacroTyper-win-x64.exe", lite!.Asset.Name);
        Assert.Equal("MacroTyper-win-x64-standalone.exe", standalone!.Asset.Name);
    }

    /// <summary>
    /// lite 를 쓰는 사람에게 standalone 을 내려보내면 68MB 를 받게 된다.
    /// 이름이 정확히 맞는 것만 쓴다. 비슷한 것으로 대신하지 않는다.
    /// </summary>
    [Fact]
    public void StaysQuietWhenThisVariantIsMissingFromTheRelease()
    {
        AppRelease onlyStandalone = Release("0.2.0", "MacroTyper-win-x64-standalone.exe");

        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), onlyStandalone, AppVariant.Lite));
    }

    [Fact]
    public void StaysQuietWithoutARelease()
    {
        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), null, AppVariant.Lite));
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
            [new ReleaseAsset("MacroTyper-win-x64.exe", url, 1000)]);

        Assert.Null(UpdatePlan.Decide(new Version(0, 1, 0, 0), release, AppVariant.Lite));
    }

    [Theory]
    [InlineData("https://github.com/codeyoma/x/releases/download/v1/MacroTyper-win-x64.exe")]
    [InlineData("https://objects.githubusercontent.com/blah/MacroTyper-win-x64.exe")]
    public void AcceptsGitHubsOwnDownloadHosts(string url)
    {
        var release = new AppRelease(
            new Version(0, 2, 0, 0),
            "v0.2.0",
            "https://github.com/codeyoma/custom_macro_for_qmk/releases/latest",
            [new ReleaseAsset("MacroTyper-win-x64.exe", url, 1000)]);

        Assert.NotNull(UpdatePlan.Decide(new Version(0, 1, 0, 0), release, AppVariant.Lite));
    }
}
