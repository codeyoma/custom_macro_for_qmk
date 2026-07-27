using HidSharp;
using HidSharp.Reports;
using MacroTyper.Core;
using MacroTyper.Core.Hid;

// 매크로패드가 실제로 어떤 HID 인터페이스를 내놓는지 확인하고,
// Raw HID 로 패킷이 오는지 직접 받아 본다.
//
// 이 도구는 Windows 전용이 아니다. HidSharp 가 크로스 플랫폼이라
// 맥에서도 그대로 돌아가므로, Windows 로 넘어가기 전에 펌웨어부터 검증할 수 있다.
//
//   dotnet run --project tools/HidProbe             HID 장치 나열
//   dotnet run --project tools/HidProbe -- all      전부 나열(필터 없이)
//   dotnet run --project tools/HidProbe -- listen   Raw HID 수신 대기

bool listen = args.Contains("listen");
bool showAll = args.Contains("all");

Console.WriteLine("=== 연결된 HID 장치 ===\n");

HidDevice[] devices = DeviceList.Local.GetHidDevices().ToArray();
Console.WriteLine($"전체 {devices.Length}개\n");

HidDevice? rawHid = null;

foreach (HidDevice device in devices)
{
    string name;
    try
    {
        name = device.GetFriendlyName();
    }
    catch (Exception)
    {
        name = "(이름 없음)";
    }

    bool interesting = showAll
                    || name.Contains("helix", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("keyboard", StringComparison.OrdinalIgnoreCase)
                    || device.VendorID == HidListener.DefaultVendorId;

    if (!interesting)
        continue;

    Console.WriteLine($"{name}");
    Console.WriteLine($"  VID/PID : 0x{device.VendorID:X4} / 0x{device.ProductID:X4}   ({device.VendorID} / {device.ProductID})");
    Console.WriteLine($"  경로     : {device.DevicePath}");

    try
    {
        Console.WriteLine($"  리포트   : in={device.GetMaxInputReportLength()} out={device.GetMaxOutputReportLength()}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"  리포트   : 확인 실패 ({e.GetType().Name})");
    }

    try
    {
        ReportDescriptor descriptor = device.GetReportDescriptor();

        foreach (DeviceItem item in descriptor.DeviceItems)
        {
            string usages = string.Join(", ", item.Usages.GetAllValues().Select(u => $"0x{u:X8}"));
            Console.WriteLine($"  usage    : {usages}");

            if (item.Usages.ContainsValue(HidListener.RawHidUsage))
            {
                Console.WriteLine($"  >>> Raw HID 인터페이스 (0x{HidListener.RawHidUsage:X8})");
                rawHid = device;
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine($"  usage    : 디스크립터 읽기 실패 ({e.GetType().Name}: {e.Message})");
    }

    Console.WriteLine();
}

if (rawHid is null)
{
    Console.WriteLine("Raw HID 인터페이스(usage 0xFF600061)를 찾지 못했다.");
    Console.WriteLine("펌웨어에 RAW_ENABLE = yes 가 들어갔는지 확인할 것.");

    if (!showAll)
        Console.WriteLine("\n전체 장치를 보려면: dotnet run --project tools/HidProbe -- all");

    return 1;
}

if (!listen)
{
    Console.WriteLine("Raw HID 인터페이스를 찾았다. 패킷을 받아 보려면:");
    Console.WriteLine("  dotnet run --project tools/HidProbe -- listen");
    return 0;
}

Console.WriteLine("=== 수신 대기 중. 매크로패드 키를 눌러 보세요. Ctrl+C 로 종료 ===\n");

using var listener = new HidListener(rawHid.VendorID, rawHid.ProductID);

listener.ConnectionChanged += (_, connected) =>
    Console.WriteLine(connected ? "[연결됨]" : "[끊김]");

listener.EventReceived += (_, macroEvent) =>
{
    string description = macroEvent switch
    {
        MacroEvent.Paste paste => $"문장 삽입 요청 — {paste.SlotIndex + 1}번 슬롯",
        MacroEvent.OverlayShow show => $"치트시트 표시 — 레이어 {show.Layer}",
        MacroEvent.OverlayHide => "치트시트 숨김",
        MacroEvent.Pong => "핑 응답",
        _ => macroEvent.ToString() ?? "?",
    };

    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {description}");
};

listener.Start();

var quit = new ManualResetEventSlim();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quit.Set();
};

quit.Wait();
listener.Stop();

return 0;
