using System.Windows;
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
    private nint _handle;

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

    public void UpdateSlots(IReadOnlyList<Slot> slots)
    {
        Cells.ItemsSource = slots.Select(OverlayCell.From).ToArray();
        UpdateLayout();
    }

    /// <summary>
    /// <paramref name="anchorWindow"/> 가 있는 모니터 가운데에 띄운다.
    ///
    /// 배치를 먼저 하고 마지막에 보이게 하는 순서가 중요하다.
    /// 띄우고 나서 옮기면 이전 위치에 한 번 나타났다가 이동하는 게 눈에 보인다.
    /// </summary>
    public void ShowOverlay(nint anchorWindow)
    {
        if (_handle == 0)
            return;

        CenterOnMonitorOf(anchorWindow);

        if (!IsVisible)
        {
            // ShowActivated=false 라 ShowWindow(SW_SHOWNA) 로 나간다. 위치는 건드리지 않는다.
            Show();
        }
    }

    public void HideOverlay()
    {
        if (IsVisible)
            Hide();
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
               | NativeMethods.WsExTransparent  // 클릭이 아래 창으로 통과한다
               | NativeMethods.WsExToolWindow;  // 작업 표시줄과 Alt+Tab 에서 빠진다

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

        return 0;
    }
}
