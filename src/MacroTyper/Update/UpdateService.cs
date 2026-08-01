using System.Windows.Threading;
using MacroTyper.Core.Update;

namespace MacroTyper.Update;

/// <summary>
/// 새 버전이 나왔는지 이따금 물어본다.
///
/// 확인은 켜고 끌 수 있다. 이 프로그램이 하는 일 가운데 유일하게 바깥으로 나가는 통신이라,
/// 원하지 않는 사람은 끌 수 있어야 한다.
/// </summary>
internal sealed class UpdateService : IDisposable
{
    public const string Repository = "codeyoma/custom_macro_for_qmk";
    public const string ReleasesPageUrl = $"https://github.com/{Repository}/releases/latest";

    /// <summary>시작하자마자 묻지 않는다. 켜지는 순간이 제일 바쁘다.</summary>
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly UpdateSource _source = new(Repository);
    private readonly DispatcherTimer _timer;

    /// <summary>이번 실행에서 이미 알린 버전. 하루에 한 번씩 같은 말을 반복하지 않는다.</summary>
    private Version? _announced;

    public UpdateService()
    {
        _timer = new DispatcherTimer { Interval = FirstDelay };
        _timer.Tick += OnTick;
    }

    /// <summary>자동 확인에서 처음 발견했을 때만 올라온다. 사용자가 직접 누른 확인은 반환값으로 받는다.</summary>
    public event EventHandler<UpdateOffer>? UpdateFound;

    /// <summary>지금 받을 수 있는 새 버전. 없으면 <c>null</c>.</summary>
    public UpdateOffer? Pending { get; private set; }

    public UpdateSource Source => _source;

    public bool Enabled
    {
        get => _timer.IsEnabled;
        set
        {
            if (value)
                _timer.Start();
            else
                _timer.Stop();
        }
    }

    /// <summary>
    /// 한 번 물어본다. 확인을 꺼 두었더라도 부르면 확인한다.
    /// 사용자가 직접 누른 것을 설정 때문에 무시할 이유는 없다.
    /// </summary>
    public async Task<UpdateOffer?> CheckAsync(CancellationToken cancellation = default)
    {
        AppRelease? release = await _source.FetchLatestAsync(cancellation).ConfigureAwait(true);

        Pending = UpdatePlan.Decide(AppIdentity.Current, release);

        return Pending;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        // 첫 확인이 끝났으니 이제부터는 하루 간격이다.
        _timer.Interval = Interval;

        UpdateOffer? offer = await CheckAsync().ConfigureAwait(true);

        if (offer is null || offer.Version == _announced)
            return;

        _announced = offer.Version;
        UpdateFound?.Invoke(this, offer);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _source.Dispose();
    }
}
