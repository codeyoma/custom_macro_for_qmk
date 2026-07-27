using HidSharp;
using HidSharp.Reports;

namespace MacroTyper.Core.Hid;

/// <summary>
/// 매크로패드의 Raw HID 인터페이스를 찾아 열고, 패킷을 받아 이벤트로 흘려보낸다.
/// 장치가 없거나 뽑혀도 죽지 않고 계속 다시 찾는다.
///
/// 이 클래스는 슬롯도 문장도 삽입 방법도 모른다. HID에서 이벤트까지가 전부다.
/// </summary>
public sealed class HidListener : IDisposable
{
    /// <summary>QMK Raw HID의 extended usage: (UsagePage 0xFF60 &lt;&lt; 16) | Usage 0x61.</summary>
    public const uint RawHidUsage = 0xFF600061;

    /// <summary>Helix Pico (Yushakobo).</summary>
    public const int DefaultVendorId = 0x3265;
    public const int DefaultProductId = 0x0001;

    // 읽기를 무한 대기로 두면 종료할 때 스레드가 영영 깨어나지 않는다.
    // HidSharp의 Read는 CancellationToken을 받지 않기 때문에
    // 짧은 타임아웃으로 깨어나 종료 신호를 확인하는 방식이 유일하게 확실하다.
    private const int ReadTimeoutMs = 300;
    private const int ReconnectDelayMs = 1000;

    private readonly int _vendorId;
    private readonly int _productId;
    private readonly object _gate = new();

    private CancellationTokenSource? _cancellation;
    private Thread? _worker;
    private volatile bool _connected;
    private bool _disposed;

    public HidListener(int vendorId = DefaultVendorId, int productId = DefaultProductId)
    {
        _vendorId = vendorId;
        _productId = productId;
    }

    /// <summary>패킷이 해석되었을 때. 백그라운드 스레드에서 발생하므로 UI 갱신은 마샬링해야 한다.</summary>
    public event EventHandler<MacroEvent>? EventReceived;

    /// <summary>연결 상태가 바뀌었을 때. 백그라운드 스레드에서 발생한다.</summary>
    public event EventHandler<bool>? ConnectionChanged;

    public bool IsConnected => _connected;

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_worker is not null)
                return;

            _cancellation = new CancellationTokenSource();
            CancellationToken token = _cancellation.Token;

            _worker = new Thread(() => Run(token))
            {
                IsBackground = true,
                Name = "MacroTyper HID",
            };
            _worker.Start();
        }
    }

    public void Stop()
    {
        Thread? worker;
        CancellationTokenSource? cancellation;

        lock (_gate)
        {
            worker = _worker;
            cancellation = _cancellation;
            _worker = null;
            _cancellation = null;
        }

        if (worker is null)
            return;

        cancellation?.Cancel();

        // 읽기 타임아웃이 300ms라 그 안에 깨어난다. 넉넉하게 기다린다.
        worker.Join(TimeSpan.FromSeconds(3));
        cancellation?.Dispose();

        SetConnected(false);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        Stop();
    }

    private void Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HidDevice? device = FindRawHidInterface();

            if (device is null)
            {
                SetConnected(false);
                token.WaitHandle.WaitOne(ReconnectDelayMs);
                continue;
            }

            try
            {
                ReadUntilDisconnected(device, token);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or UnauthorizedAccessException)
            {
                // 장치가 뽑혔다. 다시 찾으러 간다.
            }

            SetConnected(false);

            if (!token.IsCancellationRequested)
                token.WaitHandle.WaitOne(ReconnectDelayMs);
        }
    }

    private void ReadUntilDisconnected(HidDevice device, CancellationToken token)
    {
        using HidStream stream = device.Open();
        stream.ReadTimeout = ReadTimeoutMs;

        // 길이를 32로 하드코딩하면 안 된다. Windows는 report id 바이트를 앞에 붙여
        // 33바이트로 보고하는데, 32만 요청하면 마지막 1바이트가 조용히 잘린다.
        byte[] buffer = new byte[device.GetMaxInputReportLength()];

        SetConnected(true);

        while (!token.IsCancellationRequested)
        {
            int read;

            try
            {
                read = stream.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                // 종료 신호를 확인하러 깨어난 것뿐이다.
                continue;
            }

            if (read <= 0)
                continue;

            MacroEvent? macroEvent = MacroProtocol.Parse(buffer.AsSpan(0, read));

            if (macroEvent is not null)
                EventReceived?.Invoke(this, macroEvent);
        }
    }

    /// <summary>
    /// 키보드는 HID 인터페이스를 여러 개 노출한다. VID/PID만으로는 어느 것이
    /// Raw HID인지 알 수 없어서 리포트 디스크립터의 usage까지 확인해야 한다.
    /// </summary>
    private HidDevice? FindRawHidInterface()
    {
        foreach (HidDevice device in DeviceList.Local.GetHidDevices(_vendorId, _productId))
        {
            try
            {
                ReportDescriptor descriptor = device.GetReportDescriptor();

                foreach (DeviceItem item in descriptor.DeviceItems)
                {
                    if (item.Usages.ContainsValue(RawHidUsage))
                        return device;
                }
            }
            catch (Exception e) when (e is NotSupportedException or IOException or UnauthorizedAccessException)
            {
                // Windows는 원본 디스크립터가 아니라 재구성본을 주는데 실패하는 장치가 있다.
                // 그 장치 하나 때문에 탐색 전체가 멈추면 안 된다.
            }
        }

        return null;
    }

    private void SetConnected(bool value)
    {
        if (_connected == value)
            return;

        _connected = value;
        ConnectionChanged?.Invoke(this, value);
    }
}
