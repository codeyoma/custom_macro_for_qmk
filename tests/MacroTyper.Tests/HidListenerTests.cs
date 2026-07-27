using MacroTyper.Core.Hid;

namespace MacroTyper.Tests;

/// <summary>
/// 실제 패킷 수신은 하드웨어가 있어야 확인되므로 여기서는 다루지 않는다.
/// 패킷 해석은 <see cref="MacroProtocolTests"/>가 이미 덮고 있고,
/// 여기서는 매크로패드가 없는 환경에서도 앱이 멀쩡히 뜨고 꺼지는지를 본다.
/// 이 테스트가 도는 맥에는 장치가 없으므로 그 상황이 그대로 재현된다.
/// </summary>
public class HidListenerTests
{
    private static HidListener CreateListener() =>
        // 존재할 리 없는 VID/PID. 혹시 개발 기계에 진짜 매크로패드가 꽂혀 있어도
        // 테스트가 그걸 붙잡지 않도록 한다.
        new(vendorId: 0x0000, productId: 0xFFFF);

    [Fact]
    public void IsConnected_BeforeStart_IsFalse()
    {
        using var listener = CreateListener();

        Assert.False(listener.IsConnected);
    }

    [Fact]
    public void StartThenStop_ReturnsPromptly()
    {
        using var listener = CreateListener();

        listener.Start();
        listener.Stop();

        Assert.False(listener.IsConnected);
    }

    /// <summary>
    /// 연결 여부는 단언하지 않는다.
    /// VID/PID 로 못 찾으면 usage 만으로 다시 찾도록 되어 있어서,
    /// 개발 기계에 진짜 매크로패드가 꽂혀 있으면 이 리스너도 그것을 잡는다.
    /// 여기서 보는 것은 "장치를 찾든 못 찾든 수신 루프가 조용히 돈다"는 것뿐이다.
    /// </summary>
    [Fact]
    public void Start_ThenLetTheLoopRun_DoesNotThrow()
    {
        using var listener = CreateListener();

        listener.Start();
        Thread.Sleep(100);
        listener.Stop();
    }

    [Fact]
    public void Start_CalledTwice_DoesNotThrow()
    {
        using var listener = CreateListener();

        listener.Start();
        listener.Start();
        listener.Stop();
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        using var listener = CreateListener();

        listener.Stop();
    }

    [Fact]
    public void Stop_CalledTwice_DoesNotThrow()
    {
        using var listener = CreateListener();

        listener.Start();
        listener.Stop();
        listener.Stop();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var listener = CreateListener();

        listener.Start();
        listener.Dispose();
        listener.Dispose();
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        var listener = CreateListener();
        listener.Dispose();

        Assert.Throws<ObjectDisposedException>(listener.Start);
    }

    /// <summary>재시작이 되어야 트레이 메뉴에서 "다시 연결"을 제공할 수 있다.</summary>
    [Fact]
    public void StartAfterStop_RunsAgain()
    {
        using var listener = CreateListener();

        listener.Start();
        listener.Stop();
        listener.Start();
        listener.Stop();

        Assert.False(listener.IsConnected);
    }
}
