using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MacroTyper.Core;
using MacroTyper.Interop;

namespace MacroTyper.Ui;

/// <summary>
/// 레이어 키를 누르고 있는 동안 뜨는 치트시트.
///
/// 이 창의 유일한 절대 조건은 포커스를 절대 받지 않는 것이다.
/// 포커스를 가져가는 순간 사용자가 글을 쓰던 창의 커서 위치가 날아가고,
/// 그러면 이 프로그램 전체가 무의미해진다.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int HotkeyId = 1;

    /// <summary>떠 있는 동안만 잡는 Esc. 닫는 데만 쓴다.</summary>
    private const int EscapeHotkeyId = 2;
    private const uint VirtualKeyEscape = 0x1B;

    private nint _handle;
    private GridRotation? _appliedRotation;
    private bool _hotkeyRegistered;
    private bool _escapeRegistered;

    /// <summary>
    /// 매크로패드의 레이어 키를 누르고 있어서 떠 있는가. 치트시트 키 칸에 "누르는 중"이 붙는다.
    /// 단축키로 열어 둔 것이면 거짓이다.
    /// </summary>
    public static readonly DependencyProperty IsHeldProperty =
        DependencyProperty.Register(nameof(IsHeld), typeof(bool), typeof(OverlayWindow), new PropertyMetadata(false));

    public bool IsHeld
    {
        get => (bool)GetValue(IsHeldProperty);
        set => SetValue(IsHeldProperty, value);
    }

    public OverlayWindow()
    {
        InitializeComponent();

        // 창을 미리 만들어 둔다. 첫 표시가 느리면 키를 눌렀는데 한 박자 늦게 뜬다.
        // 이 호출로 OnSourceInitialized 가 지금 일어나므로 확장 스타일도 여기서 붙는다.
        new WindowInteropHelper(this).EnsureHandle();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;

        ApplyExtendedStyles();

        HwndSource.FromHwnd(_handle)?.AddHook(WndProc);
    }

    /// <summary>
    /// 닫히지 않게 막는다. WPF Window 는 Close() 되면 HWND 가 파괴되어 다시 쓸 수 없다.
    /// 앱이 살아 있는 동안 이 인스턴스 하나를 계속 재사용한다.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    /// <summary>관리창에서 적은 메모를 아래에 깐다. 비어 있으면 자리째 접는다.</summary>
    public void UpdateMemo(string memo)
    {
        MemoText.Text = memo;
        MemoPanel.Visibility = string.IsNullOrWhiteSpace(memo) ? Visibility.Collapsed : Visibility.Visible;

        // 지난번에 내려 둔 자리에서 다시 열면 메모 중간이 보인다. 늘 처음부터 보여준다.
        MemoScroll.ScrollToTop();

        UpdateLayout();
    }

    /// <summary>
    /// 격자를 다시 그린다. 매크로패드를 돌려 놓았으면 화면도 같은 방향으로 눕는다.
    /// 슬롯 번호는 그대로고 늘어놓는 순서만 바뀐다.
    /// </summary>
    public void UpdateSlots(IReadOnlyList<Slot> slots, GridRotation rotation, KeyboardSide side)
    {
        MetaText.Text = $"{slots.Count(slot => !slot.IsEmpty)}개 슬롯";

        // 방향이 바뀔 때만 판을 갈아 끼운다.
        // 이 메서드는 레이어 키를 누를 때마다 불리는데, 매번 새 템플릿을 물리면
        // 격자 전체가 다시 만들어져 표시가 굼떠진다.
        if (_appliedRotation != rotation)
        {
            (int rows, int columns) = SlotGrid.SizeFor(rotation);
            Cells.ItemsPanel = SlotGridTemplate.For(rows, columns);
            _appliedRotation = rotation;
        }

        Cells.ItemsSource = SlotGrid.Cells(rotation, side)
            .Select(cell => OverlayCell.From(cell, slots))
            .ToArray();

        UpdateLayout();
    }

    /// <summary>
    /// <paramref name="anchorWindow"/> 가 있는 모니터 가운데에 띄운다.
    ///
    /// 배치를 먼저 하고 마지막에 보이게 하는 순서가 중요하다.
    /// 띄우고 나서 옮기면 이전 위치에 한 번 나타났다가 이동하는 게 눈에 보인다.
    /// </summary>
    /// <param name="held">매크로패드의 레이어 키를 누르고 있어서 뜨는 것인가. 단축키로 열면 거짓.</param>
    public void ShowOverlay(nint anchorWindow, bool held)
    {
        if (_handle == 0)
            return;

        IsHeld = held;
        CenterOnMonitorOf(anchorWindow);

        // 떠 있는 동안만 Esc 를 잡는다. 이 창은 포커스를 받지 않으므로 키 입력을 직접 받을 수 없다.
        // 다른 앱이 이미 Esc 를 잡고 있으면 안내도 뺀다. 안 되는 일을 된다고 적어 두지 않는다.
        if (!_escapeRegistered)
            _escapeRegistered = NativeMethods.RegisterHotKey(_handle, EscapeHotkeyId, 0, VirtualKeyEscape);

        EscHint.Visibility = _escapeRegistered ? Visibility.Visible : Visibility.Collapsed;

        if (!IsVisible)
        {
            // ShowActivated=false 라 ShowWindow(SW_SHOWNA) 로 나간다. 위치는 건드리지 않는다.
            Show();
        }
    }

    public void HideOverlay()
    {
        // 숨으면 Esc 를 곧바로 돌려준다. 붙잡고 있으면 다른 앱에서 Esc 가 먹지 않는다.
        if (_escapeRegistered)
        {
            NativeMethods.UnregisterHotKey(_handle, EscapeHotkeyId);
            _escapeRegistered = false;
        }

        if (IsVisible)
            Hide();
    }

    /// <summary>치트시트의 칸을 마우스로 눌렀을 때. 인자는 슬롯 인덱스(0부터).</summary>
    public event EventHandler<int>? SlotActivated;

    /// <summary>치트시트의 편집 버튼을 눌렀을 때.</summary>
    public event EventHandler? EditRequested;

    /// <summary>등록해 둔 전역 단축키가 눌렸을 때.</summary>
    public event EventHandler? HotkeyPressed;

    /// <summary>떠 있는 동안 Esc 가 눌렸을 때. 닫을지는 띄운 쪽이 정한다(단축키로 열어 둔 상태도 풀어야 한다).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// 치트시트를 여는 전역 단축키를 등록한다. 이전에 등록한 것은 풀린다.
    /// 다른 앱이 이미 쓰고 있는 조합이면 <c>false</c>를 돌려준다.
    ///
    /// 이 창의 핸들에 붙인다. 창이 숨어 있어도 핸들은 살아 있으므로 단축키는 계속 동작한다.
    /// </summary>
    public bool TryRegisterHotkey(Hotkey hotkey)
    {
        UnregisterHotkey();

        if (_handle == 0 || !hotkey.IsSet)
            return true;

        _hotkeyRegistered = NativeMethods.RegisterHotKey(
            _handle, HotkeyId, (uint)hotkey.Modifiers, hotkey.VirtualKey);

        return _hotkeyRegistered;
    }

    public void UnregisterHotkey()
    {
        if (_handle == 0 || !_hotkeyRegistered)
            return;

        NativeMethods.UnregisterHotKey(_handle, HotkeyId);
        _hotkeyRegistered = false;
    }

    private void OnCellClicked(object sender, MouseButtonEventArgs e)
    {
        // 치트시트 키 자리와 빈틈은 누를 것이 없다.
        if (sender is FrameworkElement { DataContext: OverlayCell { Kind: GridCellKind.Slot } cell })
            SlotActivated?.Invoke(this, cell.Number - 1);
    }

    private void OnEditClicked(object sender, MouseButtonEventArgs e)
    {
        EditRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CenterOnMonitorOf(nint anchorWindow)
    {
        nint reference = anchorWindow != 0 ? anchorWindow : _handle;

        nint monitor = NativeMethods.MonitorFromWindow(reference, NativeMethods.MonitorDefaultToNearest);
        var info = NativeMethods.MonitorInfo.Create();

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return;

        // 배율이 다른 모니터로 옮겨가면 WM_DPICHANGED 가 나면서 WPF 가 레이아웃을 다시 잡는다.
        // 그래서 먼저 대상 모니터로 옮겨 새 배율을 적용받은 뒤, 그때의 실제 크기로 가운데를 계산한다.
        // WPF 의 Left/Top 은 쓰지 않는다. 모니터마다 배율이 다르면 엉뚱한 모니터에 놓인다.
        NativeMethods.SetWindowPos(
            _handle, 0, info.Work.Left, info.Work.Top, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);

        // 판이 화면보다 크면 비율 그대로 줄인다. 작업 영역은 물리 픽셀이라 이 모니터의 배율로 나눈다.
        // 둘레의 투명한 그림자 여백은 빼고 판에만 준다.
        DpiScale dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        Scaler.MaxWidth = Math.Max(200, (info.Work.Width / dpi.DpiScaleX * 0.96) - 80);
        Scaler.MaxHeight = Math.Max(150, (info.Work.Height / dpi.DpiScaleY * 0.96) - 80);

        UpdateLayout();

        if (!NativeMethods.GetWindowRect(_handle, out NativeMethods.Rect bounds))
            return;

        int x = info.Work.Left + ((info.Work.Width - bounds.Width) / 2);
        int y = info.Work.Top + ((info.Work.Height - bounds.Height) / 2);

        NativeMethods.SetWindowPos(
            _handle, NativeMethods.HwndTopmost, x, y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    /// <summary>
    /// ShowActivated=false 만으로는 부족하다. 그건 처음 띄우는 순간에만 적용되고,
    /// 이미 떠 있는 창을 사용자가 클릭하면 그대로 활성화되어 원래 창의 포커스를 뺏는다.
    /// 창 스타일로 영구히 막아야 한다.
    /// </summary>
    private void ApplyExtendedStyles()
    {
        nint style = NativeMethods.GetWindowExStyle(_handle);

        style |= NativeMethods.WsExNoActivate   // 클릭해도 포그라운드가 되지 않는다
               | NativeMethods.WsExToolWindow;  // 작업 표시줄과 Alt+Tab 에서 빠진다

        // WS_EX_TRANSPARENT 는 일부러 걸지 않는다.
        // 그걸 걸면 클릭이 아래 창으로 통과해 버려서 칸을 눌러 삽입하거나
        // 편집 버튼을 누를 수가 없다.
        //
        // 대신 WS_EX_NOACTIVATE 와 WM_MOUSEACTIVATE 처리가 포커스를 지킨다.
        // 마우스는 받지만 활성화되지는 않으므로, 글을 쓰던 창이 포그라운드로 남고
        // 커서 위치도 그대로다.
        style &= ~NativeMethods.WsExTransparent;

        NativeMethods.SetWindowExStyle(_handle, style);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        // WS_EX_NOACTIVATE 가 놓치는 경로를 막는 마지막 빗장.
        if ((uint)msg == NativeMethods.WmMouseActivate)
        {
            handled = true;
            return NativeMethods.MaNoActivate;
        }

        if ((uint)msg == NativeMethods.WmHotkey && wParam == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        if ((uint)msg == NativeMethods.WmHotkey && wParam == EscapeHotkeyId)
        {
            handled = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        return 0;
    }
}
