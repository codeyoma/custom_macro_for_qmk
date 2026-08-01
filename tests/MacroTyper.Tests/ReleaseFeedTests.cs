using MacroTyper.Core.Update;

namespace MacroTyper.Tests;

public class ReleaseFeedTests
{
    private const string Sample = """
        {
          "tag_name": "v0.2.0",
          "html_url": "https://github.com/codeyoma/custom_macro_for_qmk/releases/tag/v0.2.0",
          "draft": false,
          "prerelease": false,
          "assets": [
            {
              "name": "MacroTyper-win-x64.exe",
              "browser_download_url": "https://github.com/codeyoma/custom_macro_for_qmk/releases/download/v0.2.0/MacroTyper-win-x64.exe",
              "size": 612345
            },
            {
              "name": "MacroTyper-win-x64-standalone.exe",
              "browser_download_url": "https://github.com/codeyoma/custom_macro_for_qmk/releases/download/v0.2.0/MacroTyper-win-x64-standalone.exe",
              "size": 71000000
            }
          ]
        }
        """;

    [Fact]
    public void ParsesVersionFromTag()
    {
        AppRelease? release = ReleaseFeed.Parse(Sample);

        Assert.NotNull(release);
        Assert.Equal(new Version(0, 2, 0, 0), release.Version);
        Assert.Equal("v0.2.0", release.TagName);
    }

    [Fact]
    public void KeepsPageUrlForManualFallback()
    {
        AppRelease? release = ReleaseFeed.Parse(Sample);

        Assert.Equal(
            "https://github.com/codeyoma/custom_macro_for_qmk/releases/tag/v0.2.0",
            release!.PageUrl);
    }

    [Fact]
    public void ParsesAssets()
    {
        AppRelease? release = ReleaseFeed.Parse(Sample);

        Assert.Equal(2, release!.Assets.Count);

        ReleaseAsset lite = release.Assets[0];
        Assert.Equal("MacroTyper-win-x64.exe", lite.Name);
        Assert.Equal(612345, lite.SizeBytes);
        Assert.StartsWith("https://github.com/", lite.DownloadUrl);
    }

    [Theory]
    [InlineData("v1.2.3")]
    [InlineData("V1.2.3")]
    [InlineData("1.2.3")]
    public void AcceptsTagWithOrWithoutPrefix(string tag)
    {
        AppRelease? release = ReleaseFeed.Parse($$"""{"tag_name": "{{tag}}", "assets": []}""");

        Assert.Equal(new Version(1, 2, 3, 0), release!.Version);
    }

    /// <summary>
    /// Version 은 빠진 자리를 -1 로 둔다. 그대로 비교하면 0.2 가 0.2.0 보다 낮아진다.
    /// 태그를 어떻게 적었든 같은 버전이면 같아야 한다.
    /// </summary>
    [Fact]
    public void PadsMissingComponentsWithZero()
    {
        AppRelease? shortTag = ReleaseFeed.Parse("""{"tag_name": "v2.1", "assets": []}""");
        AppRelease? longTag = ReleaseFeed.Parse("""{"tag_name": "v2.1.0.0", "assets": []}""");

        Assert.Equal(longTag!.Version, shortTag!.Version);
    }

    [Theory]
    [InlineData("firmware-2026-07")]
    [InlineData("v0.2.0-beta")]
    [InlineData("")]
    public void RejectsTagThatIsNotAPlainVersion(string tag)
    {
        Assert.Null(ReleaseFeed.Parse($$"""{"tag_name": "{{tag}}", "assets": []}"""));
    }

    [Fact]
    public void RejectsMalformedJson()
    {
        Assert.Null(ReleaseFeed.Parse("이건 JSON 이 아니다"));
        Assert.Null(ReleaseFeed.Parse(""));
    }

    /// <summary>
    /// /releases/latest 는 원래 초안과 사전 배포판을 빼고 준다.
    /// 그래도 확인한다. 다 만들지 않은 릴리즈로 사용자를 끌고 갈 이유가 없다.
    /// </summary>
    [Theory]
    [InlineData("draft")]
    [InlineData("prerelease")]
    public void SkipsUnfinishedRelease(string flag)
    {
        string json = $$"""{"tag_name": "v9.0.0", "{{flag}}": true, "assets": []}""";

        Assert.Null(ReleaseFeed.Parse(json));
    }

    [Fact]
    public void SkipsAssetWithoutUsableUrl()
    {
        string json = """
            {
              "tag_name": "v0.2.0",
              "assets": [
                {"name": "빈줄", "browser_download_url": "", "size": 1},
                {"name": "쓸만한것", "browser_download_url": "https://github.com/a/b", "size": 2}
              ]
            }
            """;

        AppRelease? release = ReleaseFeed.Parse(json);

        Assert.Single(release!.Assets);
        Assert.Equal("쓸만한것", release.Assets[0].Name);
    }
}
