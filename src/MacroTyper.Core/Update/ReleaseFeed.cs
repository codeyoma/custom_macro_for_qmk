using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroTyper.Core.Update;

/// <summary>
/// GitHub 릴리즈 응답을 읽는다. 응답 형식을 아는 유일한 조각이다.
///
/// 읽을 수 없으면 예외 대신 <c>null</c>이다. 업데이트 확인은 배경에서 도는 부수적인 일이고,
/// GitHub 이 응답을 바꾸거나 잠깐 이상한 것을 돌려준다고 프로그램이 흔들려서는 안 된다.
/// </summary>
public static class ReleaseFeed
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static AppRelease? Parse(string json)
    {
        Payload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<Payload>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload is null || payload.Draft || payload.Prerelease)
            return null;

        if (!TryParseTag(payload.TagName, out Version? version))
            return null;

        ReleaseAsset[] assets = (payload.Assets ?? [])
            .Where(a => !string.IsNullOrEmpty(a.Name) && !string.IsNullOrEmpty(a.DownloadUrl))
            .Select(a => new ReleaseAsset(a.Name!, a.DownloadUrl!, a.Size))
            .ToArray();

        return new AppRelease(version, payload.TagName!, payload.PageUrl ?? string.Empty, assets);
    }

    /// <summary>
    /// 태그에서 버전을 읽는다. 앞의 <c>v</c>는 있어도 되고 없어도 된다.
    ///
    /// 그 밖의 꾸밈은 받지 않는다. <c>v0.2.0-beta</c> 같은 것을 억지로 <c>0.2.0</c>으로 읽으면
    /// 사전 배포판이 정식판인 척 내려가고, <c>firmware-2026-07</c> 같은 태그는 아예 버전이 아니다.
    /// </summary>
    private static bool TryParseTag(
        string? tag,
        [NotNullWhen(true)] out Version? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        string text = tag.Trim();

        if (text[0] is 'v' or 'V')
            text = text[1..];

        if (!Version.TryParse(text, out Version? parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    /// <summary>
    /// 네 자리로 맞춘다.
    ///
    /// <see cref="Version"/>은 적히지 않은 자리를 0 이 아니라 -1 로 둔다.
    /// 그래서 <c>0.2</c>가 <c>0.2.0</c>보다 낮다고 나온다. 태그를 몇 자리로 적었느냐로
    /// 업데이트 여부가 갈리면 안 되므로, 비교하기 전에 항상 여기를 거친다.
    /// </summary>
    public static Version Normalize(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(version.Build, 0),
        Math.Max(version.Revision, 0));

    private sealed class Payload
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? PageUrl { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public Asset[]? Assets { get; set; }
    }

    private sealed class Asset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
