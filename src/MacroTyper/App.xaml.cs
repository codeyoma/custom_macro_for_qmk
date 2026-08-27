using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;
using MacroTyper.Core;
using MacroTyper.Core.Hid;
using MacroTyper.Core.Update;
using MacroTyper.Interop;
using MacroTyper.Ui;
using MacroTyper.Update;

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
    private UpdateService? _updates;
    private MenuItem? _updateMenuItem;

    /// <summary>단축키로 열어 둔 상태. 레이어 키를 떼도 닫히지 않는다.</summary>
    private bool _overlayPinned;

    /// <summary>지금 떠 있는 풍선을 눌렀을 때 업데이트를 시작해도 되는지.</summary>
    private bool _balloonOffersUpdate;

    /// <summary>교체가 진행 중. 두 번 누르면 같은 파일을 두 번 받는다.</summary>
    private bool _updating;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        if (!TryClaimSingleInstance())
        {
            Shutdown();
            return;
        }

        // 지난 업데이트가 밀어 둔 이전 버전을 치운다. 그때는 아직 돌고 있어서 지울 수 없었다.
        AppIdentity.CleanUpLeftovers();

        _store = SlotStore.OpenDefault();
        _store.Load();

        _injector = new TextInjector();

        _overlay = new OverlayWindow();
        RefreshOverlay();

        // 치트시트를 보다가 마우스로 바로 넣거나 고치러 갈 수 있게 한다.
        // 오버레이는 포커스를 받지 않으므로 칸을 눌러도 글을 쓰던 창이 포그라운드로 남는다.
        _overlay.SlotActivated += OnSlotActivated;
        _overlay.EditRequested += (_, _) => OpenManager();
        _overlay.HotkeyPressed += (_, _) => ToggleOverlay();

        _overlay.TryRegisterHotkey(_store.CheatHotkey);

        CreateTrayIcon();
        StartUpdateChecks();

        _listener = new HidListener();
        _listener.EventReceived += OnMacroEvent;
        _listener.ConnectionChanged += OnConnectionChanged;
        _listener.Start();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _listener?.Dispose();
        _updates?.Dispose();
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

        TrayDialog.Show("이미 실행 중입니다. 작업 표시줄 트레이를 확인하세요.", "문장 매크로");

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

        _injector.Send(slot);
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

    /// <summary>치트시트에 보이는 것들을 한꺼번에 다시 그린다.</summary>
    private void RefreshOverlay()
    {
        _overlay.UpdateSlots(_store.Slots, _store.Rotation);
        _overlay.UpdateMemo(_store.Memo);
    }

    private void ShowOverlay()
    {
        // 오버레이는 포커스를 받지 않으므로, 지금 포그라운드인 창이 곧 사용자가 작업 중인 창이다.
        nint foreground = NativeMethods.GetForegroundWindow();

        RefreshOverlay();
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

        // 풍선은 몇 초 뒤에 사라진다. 그때 자리를 비웠던 사람도 트레이 메뉴에서 다시 찾을 수 있어야 한다.
        _tray.TrayBalloonTipClicked += (_, _) =>
        {
            if (_balloonOffersUpdate && _updates?.Pending is { } offer)
                StartUpdate(offer);
        };
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();

        // 새 버전을 찾기 전에는 보이지 않는다.
        _updateMenuItem = new MenuItem { Header = "새 버전 받기", Visibility = Visibility.Collapsed, FontWeight = FontWeights.Bold };
        _updateMenuItem.Click += (_, _) =>
        {
            if (_updates?.Pending is { } offer)
                StartUpdate(offer);
        };
        menu.Items.Add(_updateMenuItem);

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

        var autoCheck = new MenuItem
        {
            Header = "새 버전 자동 확인",
            IsCheckable = true,
            IsChecked = _store.CheckForUpdates,
        };
        autoCheck.Click += (_, _) => SetUpdateChecking(autoCheck.IsChecked);
        menu.Items.Add(autoCheck);

        var checkNow = new MenuItem { Header = "지금 새 버전 확인" };
        checkNow.Click += (_, _) => CheckForUpdatesNow();
        menu.Items.Add(checkNow);

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
                RefreshOverlay,
                ApplyHotkey);
            _manager.SetConnectionState(_listener.IsConnected);
        }

        _manager.RefreshGrid();
        _manager.Show();

        if (_manager.WindowState == WindowState.Minimized)
            _manager.WindowState = WindowState.Normal;

        BringToFront(_manager);
    }

    /// <summary>
    /// 창을 확실히 앞으로 끌어온다.
    ///
    /// <see cref="Window.Activate"/>만으로는 모자란다. 트레이 앱은 포그라운드가 아니고,
    /// Windows 는 포그라운드가 아닌 프로세스가 포커스를 가져가는 것을 막는다.
    /// 그러면 창은 열리되 쓰던 창 뒤에 서서, 아무 일도 안 일어난 것처럼 보인다.
    ///
    /// Topmost 를 켰다 끄면 포커스와 무관하게 z-order 만 맨 앞으로 옮길 수 있다.
    /// 계속 켜 두지는 않는다. 관리창이 다른 창 위에 영원히 붙어 있을 이유는 없다.
    /// </summary>
    private static void BringToFront(Window window)
    {
        window.Activate();

        window.Topmost = true;
        window.Topmost = false;

        window.Focus();
    }

    // --- 새 버전 ---

    private void StartUpdateChecks()
    {
        _updates = new UpdateService();
        _updates.UpdateFound += (_, offer) => AnnounceUpdate(offer);

        // 개발 중의 빌드는 스스로 갈아끼울 수 없다. 알려 봐야 할 수 있는 일이 없다.
        _updates.Enabled = _store.CheckForUpdates && AppIdentity.IsSingleFileBuild;
    }

    private void SetUpdateChecking(bool enabled)
    {
        _store.CheckForUpdates = enabled;

        if (_updates is not null)
            _updates.Enabled = enabled && AppIdentity.IsSingleFileBuild;

        try
        {
            _store.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 저장에 실패해도 이번 실행 동안은 설정대로 동작한다.
        }
    }

    private void AnnounceUpdate(UpdateOffer offer)
    {
        if (_updateMenuItem is not null)
        {
            _updateMenuItem.Header = $"새 버전 {Describe(offer.Version)} 받기";
            _updateMenuItem.Visibility = Visibility.Visible;
        }

        _balloonOffersUpdate = true;
        _tray?.ShowBalloonTip(
            $"새 버전 {Describe(offer.Version)}",
            "눌러서 업데이트합니다. 등록해 둔 문장은 그대로 남습니다.",
            BalloonIcon.Info);
    }

    private async void CheckForUpdatesNow()
    {
        if (_updates is null)
            return;

        UpdateOffer? offer = await _updates.CheckAsync();

        if (offer is null)
        {
            TrayDialog.Show($"최신 버전입니다. (현재 {Describe(AppIdentity.Current)})", "문장 매크로");
            return;
        }

        AnnounceUpdate(offer);
        StartUpdate(offer);
    }

    /// <summary>
    /// 새 exe 를 받아 지금 것을 대신하게 하고 다시 시작한다.
    ///
    /// 사용자가 명시적으로 누른 뒤에만 시작한다. 프로그램이 스스로 바뀌는 일을
    /// 모르는 사이에 해서는 안 된다.
    /// </summary>
    private async void StartUpdate(UpdateOffer offer)
    {
        if (_updating || _updates is null)
            return;

        MessageBoxResult answer = TrayDialog.Show(
            $"""
            새 버전이 나왔습니다.

                지금    {Describe(AppIdentity.Current)}
                새것    {Describe(offer.Version)}

            {DescribeDownload(offer.Asset.SizeBytes)} 지금 프로그램을 대신하고 다시 시작합니다.
            등록해 둔 문장과 메모, 단축키는 그대로 남습니다.
            """,
            "문장 매크로 업데이트",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.OK)
            return;

        _updating = true;
        string tooltip = _tray?.ToolTipText ?? "문장 매크로";

        _balloonOffersUpdate = false;
        _tray?.ShowBalloonTip(
            "업데이트를 내려받는 중",
            "다 받으면 저절로 다시 시작합니다. 그때까지 그대로 쓰셔도 됩니다.",
            BalloonIcon.Info);

        // 진행률은 퍼센트가 바뀔 때만 반영한다. 조각마다 갱신하면 65MB 를 받는 동안
        // 트레이 아이콘에 수백 번 알림을 보내게 된다.
        int lastPercent = -1;

        var progress = new Progress<double>(fraction =>
        {
            int percent = (int)(fraction * 100);

            if (percent == lastPercent || _tray is null)
                return;

            lastPercent = percent;
            _tray.ToolTipText = $"문장 매크로 — 업데이트 내려받는 중 {percent}%";
        });

        UpdateResult result = await UpdateInstaller.InstallAsync(offer, _updates.Source, progress);

        if (result.IsReady)
        {
            RestartForUpdate();
            return;
        }

        _updating = false;

        if (_tray is not null)
            _tray.ToolTipText = tooltip;

        OfferManualDownload(offer, result);
    }

    /// <summary>
    /// 자동 교체가 막혔을 때. 여기서 끝내면 사용자는 새 버전을 받을 길을 잃는다.
    /// </summary>
    private void OfferManualDownload(UpdateOffer offer, UpdateResult result)
    {
        MessageBoxResult answer = TrayDialog.Show(
            $"""
            업데이트하지 못했습니다.

            {result.Message}

            받는 곳을 브라우저로 열까요?
            """,
            "문장 매크로 업데이트",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        string page = string.IsNullOrEmpty(offer.PageUrl) ? UpdateService.ReleasesPageUrl : offer.PageUrl;

        try
        {
            Process.Start(new ProcessStartInfo(page) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // 브라우저가 없다. 여기서 더 해 줄 것이 없다.
        }
    }

    /// <summary>
    /// 새 exe 로 다시 시작한다.
    ///
    /// 먼저 자리를 비운다. 뮤텍스를 쥔 채로 새 프로세스를 띄우면 그쪽이 "이미 실행 중"으로
    /// 죽어 버리고, HID 장치도 놓아주어야 새 프로세스가 매크로패드를 열 수 있다.
    /// </summary>
    private void RestartForUpdate()
    {
        string executable = AppIdentity.ExecutablePath;

        _listener?.Dispose();
        _updates?.Dispose();
        _updates = null;
        _tray?.Dispose();
        _tray = null;

        ReleaseSingleInstance();

        try
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            TrayDialog.Show("교체는 끝났습니다. 프로그램을 직접 다시 실행해 주세요.", "문장 매크로");
        }

        Shutdown();
    }

    private void ReleaseSingleInstance()
    {
        if (_singleInstance is null)
            return;

        try
        {
            _singleInstance.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // 이 스레드가 쥐고 있지 않았다. 프로세스가 끝나면 어차피 풀린다.
        }

        _singleInstance.Dispose();
        _singleInstance = null;
    }

    /// <summary>끝자리 0 은 접는다. 0.2.0.0 보다 0.2.0 이 읽기 쉽다.</summary>
    private static string Describe(Version version) =>
        version.Revision > 0 ? version.ToString(4) : version.ToString(3);

    /// <summary>조사까지 함께 만든다. 크기를 모를 때도 문장이 어색해지지 않아야 한다.</summary>
    private static string DescribeDownload(long bytes) => bytes switch
    {
        <= 0 => "새 파일을 내려받아",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#}KB를 내려받아",
        _ => $"{bytes / (1024.0 * 1024.0):0.#}MB를 내려받아",
    };

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
