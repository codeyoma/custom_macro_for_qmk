using System.Net.Http.Headers;

namespace MacroTyper.Core.Update;

/// <summary>
/// GitHub 에 새 릴리즈가 있는지 묻고, 있으면 파일을 받아 온다.
/// 판단은 <see cref="UpdatePlan"/>이 하고 여기는 옮기기만 한다.
/// </summary>
public sealed class UpdateSource : IDisposable
{
    /// <summary>GitHub API 는 User-Agent 없는 요청을 403 으로 막는다.</summary>
    private const string UserAgent = "MacroTyper";

    private readonly HttpClient _http;
    private readonly string _repository;

    public UpdateSource(string repository, HttpMessageHandler? handler = null)
    {
        _repository = repository;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);

        // 확인은 배경 작업이다. 응답이 없으면 조용히 포기하는 편이 매달려 있는 것보다 낫다.
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    /// <summary>
    /// 가장 최근 정식 릴리즈. 못 받아오면 <c>null</c>이다.
    ///
    /// 실패를 예외로 올리지 않는다. 인터넷이 끊겼거나 GitHub 이 잠깐 느린 것은
    /// 사용자에게 알릴 일이 아니다. 다음 확인 때 다시 물으면 된다.
    /// </summary>
    public async Task<AppRelease?> FetchLatestAsync(CancellationToken cancellation = default)
    {
        try
        {
            string url = $"https://api.github.com/repos/{_repository}/releases/latest";
            using HttpResponseMessage response = await _http.GetAsync(url, cancellation).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            string body = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            return ReleaseFeed.Parse(body);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// 파일을 <paramref name="destination"/>에 받는다. 실패하면 예외가 올라간다.
    /// 이건 사용자가 직접 누른 일이라 조용히 넘길 수 없다.
    /// </summary>
    /// <param name="progress">0 에서 1 사이. 길이를 모르면 호출되지 않는다.</param>
    public async Task DownloadAsync(
        ReleaseAsset asset,
        string destination,
        IProgress<double>? progress = null,
        CancellationToken cancellation = default)
    {
        using HttpResponseMessage response = await _http
            .GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellation)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        // 헤더가 없으면 릴리즈가 알려준 크기를 쓴다. 둘 다 없으면 진행률을 접는다.
        long total = response.Content.Headers.ContentLength ?? asset.SizeBytes;

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        await using var target = new FileStream(
            destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        byte[] buffer = new byte[81920];
        long copied = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);

            copied += read;

            if (total > 0)
                progress?.Report(Math.Min(1.0, (double)copied / total));
        }
    }

    public void Dispose() => _http.Dispose();
}
