using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using MacroTyper.Core;
using MacroTyper.Core.Hid;
using MacroTyper.Interop;
using MacroTyper.Ui;

namespace MacroTyper;

/// <summary>
/// 조각들을 배선하는 곳. 이 클래스 말고는 서로를 모른다.
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstance;
    private SlotStore _store = null!;
    private TextInjector _injector = null!;
    private HidListener _listener = null!;
    private OverlayWindow _overlay = null!;
    private ManagerWindow? _manager;
    private TaskbarIcon? _tray;

    /// <summary>단축키로 열어 둔 상태. 레이어 키를 떼도 닫히지 않는다.</summary>
    private bool _overlayPinned;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        if (!TryClaimSingleInstance())
        {
            Shutdown();
            return;
        }

        _store = SlotStore.OpenDefault();
        _store.Load();

        _injector = new TextInjector();

        _overlay = new OverlayWindow();
        _overlay.UpdateSlots(_store.Slots, _store.Rotation);

        // 치트시트를 보다가 마우스로 바로 넣거나 고치러 갈 수 있게 한다.
        // 오버레이는 포커스를 받지 않으므로 칸을 눌러도 글을 쓰던 창이 포그라운드로 남는다.
        _overlay.SlotActivated += OnSlotActivated;
        _overlay.EditRequested += (_, _) => OpenManager();
        _overlay.HotkeyPressed += (_, _) => ToggleOverlay();

        _overlay.TryRegisterHotkey(_store.CheatHotkey);

        CreateTrayIcon();

        _listener = new HidListener();
        _listener.EventReceived += OnMacroEvent;
        _listener.ConnectionChanged += OnConnectionChanged;
        _listener.Start();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _listener?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
    }

    /// <summary>
    /// 두 개가 동시에 돌면 키 하나에 문장이 두 번 들어간다.
    /// 세션 안에서만 유일하면 되므로 Local\ 을 쓴다.
    /// </summary>
    private bool TryClaimSingleInstance()
    {
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\MacroTyper.SingleInstance", out bool isFirst);

        if (isFirst)
            return true;

        _singleInstance.Dispose();
        _singleInstance = null;

        MessageBox.Show(
            "이미 실행 중입니다. 작업 표시줄 트레이를 확인하세요.",
            "문장 매크로",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        return false;
    }

    // --- 매크로패드에서 온 신호 ---

    private void OnMacroEvent(object? sender, MacroEvent macroEvent)
    {
        // 이 핸들러는 HID 수신 스레드에서 불린다.
        switch (macroEvent)
        {
            case MacroEvent.Paste paste:
                // 삽입은 UI를 건드리지 않는다. 오히려 UI 스레드에서 하면
                // 조각 사이 대기 때문에 화면이 잠깐 멈춘다.
                InjectSlot(paste.SlotIndex);
                break;

            case MacroEvent.OverlayShow:
                Dispatcher.BeginInvoke(ShowOverlay);
                break;

            case MacroEvent.OverlayHide:
                // 단축키로 열어 둔 상태라면 레이어 키를 뗐다고 닫지 않는다.
                Dispatcher.BeginInvoke(() =>
                {
                    if (!_overlayPinned)
                        _overlay.HideOverlay();
                });
                break;

            case MacroEvent.Pong:
                break;
        }
    }

    private void InjectSlot(int index)
    {
        if (index < 0 || index >= MacroProtocol.SlotCount)
            return;

        Slot slot = _store[index];

        if (slot.IsEmpty)
            return;

        InjectionOutcome outcome = _injector.Inject(slot.Text, slot.AppendEnter);

        if (outcome is InjectionOutcome.Success or InjectionOutcome.NothingToInject)
            return;

        Dispatcher.BeginInvoke(() => NotifyFailure(slot, outcome));
    }

    private void NotifyFailure(Slot slot, InjectionOutcome outcome)
    {
        string message = outcome switch
        {
            InjectionOutcome.BlockedByElevation =>
                "대상 창이 관리자 권한이라 입력이 막혔습니다. 트레이 메뉴에서 관리자 권한으로 재시작하세요.",
            InjectionOutcome.TargetIsSelf =>
                "글을 넣을 창을 먼저 클릭하세요.",
            InjectionOutcome.Incomplete =>
                "문장이 일부만 들어갔습니다. 대상 앱이 입력을 따라오지 못했을 수 있습니다.",
            _ => "문장을 넣지 못했습니다.",
        };

        _tray?.ShowBalloonTip($"{slot.Index + 1}번 삽입 실패", message, BalloonIcon.Warning);
    }

    /// <summary>
    /// 단축키로 치트시트를 열고 닫는다.
    ///
    /// 레이어 키와 달리 토글이다. Windows 는 단축키가 눌린 것만 알려주고
    /// 언제 떼는지는 알려주지 않아서 "누르고 있는 동안"을 만들 수 없다.
    /// </summary>
    private void ToggleOverlay()
    {
        if (_overlayPinned)
        {
            _overlayPinned = false;
            _overlay.HideOverlay();
            return;
        }

        _overlayPinned = true;
        ShowOverlay();
    }

    private void OnSlotActivated(object? sender, int index)
    {
        // 마우스로 골랐으면 볼 일이 끝난 것이다. 단축키로 열어 뒀더라도 닫는다.
        if (_overlayPinned)
        {
            _overlayPinned = false;
            _overlay.HideOverlay();
        }

        Task.Run(() => InjectSlot(index));
    }

    /// <summary>
    /// 단축키를 등록하고 저장한다. 다른 앱이 이미 쓰는 조합이면 <c>false</c>.
    /// 등록에 실패하면 저장하지 않는다. 다음 실행 때 또 실패할 설정을 남길 이유가 없다.
    /// </summary>
    private bool ApplyHotkey(Hotkey hotkey)
    {
        if (!_overlay.TryRegisterHotkey(hotkey))
        {
            // 실패했으면 쓰던 것으로 되돌린다.
            _overlay.TryRegisterHotkey(_store.CheatHotkey);
            return false;
        }

        _store.CheatHotkey = hotkey;

        try
        {
            _store.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 저장에 실패해도 이번 실행 동안은 동작한다.
        }

        return true;
    }

    private void ShowOverlay()
    {
        // 오버레이는 포커스를 받지 않으므로, 지금 포그라운드인 창이 곧 사용자가 작업 중인 창이다.
        nint foreground = NativeMethods.GetForegroundWindow();

        _overlay.UpdateSlots(_store.Slots, _store.Rotation);
        _overlay.ShowOverlay(foreground);
    }

    private void OnConnectionChanged(object? sender, bool connected)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_tray is not null)
            {
                _tray.IconSource = (ImageSource)FindResource(connected ? "TrayConnected" : "TrayDisconnected");
                _tray.ToolTipText = connected ? "문장 매크로 — 연결됨" : "문장 매크로 — 매크로패드 없음";
            }

            _manager?.SetConnectionState(connected);
        });
    }

    // --- 트레이 ---

    private void CreateTrayIcon()
    {
        _tray = new TaskbarIcon
        {
            IconSource = (ImageSource)FindResource("TrayDisconnected"),
            ToolTipText = "문장 매크로 — 매크로패드 없음",
            ContextMenu = BuildTrayMenu(),
            MenuActivation = PopupActivationMode.RightClick,
        };

        _tray.TrayMouseDoubleClick += (_, _) => OpenManager();
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();

        var open = new MenuItem { Header = "문장 관리" };
        open.Click += (_, _) => OpenManager();
        menu.Items.Add(open);

        menu.Items.Add(new Separator());

        var autoStart = new MenuItem { Header = "로그온할 때 자동 실행", IsCheckable = true, IsChecked = AutoStart.IsEnabled };
        autoStart.Click += (_, _) =>
        {
            AutoStart.Toggle();
            autoStart.IsChecked = AutoStart.IsEnabled;
        };
        menu.Items.Add(autoStart);

        var elevate = new MenuItem { Header = "관리자 권한으로 재시작" };
        elevate.Click += (_, _) => RestartElevated();
        menu.Items.Add(elevate);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "종료" };
        quit.Click += (_, _) => Shutdown();
        menu.Items.Add(quit);

        return menu;
    }

    private void OpenManager()
    {
        if (_manager is null)
        {
            _manager = new ManagerWindow(
                _store,
                _injector,
                () => _overlay.UpdateSlots(_store.Slots, _store.Rotation),
                ApplyHotkey);
            _manager.SetConnectionState(_listener.IsConnected);
        }

        _manager.RefreshGrid();
        _manager.Show();

        if (_manager.WindowState == WindowState.Minimized)
            _manager.WindowState = WindowState.Normal;

        _manager.Activate();
    }

    /// <summary>
    /// 관리자 권한으로 뜬 창에는 일반 권한에서 입력을 넣을 수 없다.
    /// 그럴 때만 쓰라고 열어 둔 길이다. 평상시에는 일반 권한이 낫다.
    /// </summary>
    private void RestartElevated()
    {
        string? executable = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executable))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas" });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // 사용자가 UAC 를 취소했다. 그대로 계속 쓴다.
            return;
        }

        Shutdown();
    }
}
