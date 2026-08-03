using System.Windows;
using System.Windows.Media;

namespace MacroTyper.Ui;

/// <summary>
/// 창 없는 트레이 앱에서 대화 상자를 띄운다.
///
/// <see cref="MessageBox"/>를 소유자 없이 부르면 안 된다. 그러면 WPF 가 이 스레드의
/// 활성 창을 대신 소유자로 삼는데, 이 앱에서 그건 화면에 없는 치트시트 창이다.
/// 그 창은 숨어 있고 <c>WS_EX_NOACTIVATE</c>라 활성화될 수 없다.
/// 게다가 트레이 앱은 포그라운드가 아니어서 Windows 의 포그라운드 잠금에도 걸린다.
///
/// 어느 쪽이든 결과는 같다. 대화 상자가 만들어지자마자 사용자가 쓰던 창 뒤로 밀려서,
/// 잠깐 번쩍이고 사라진 것처럼 보인다. 사실은 뒤에 살아 있고 앱은 그 대답을 기다린다.
///
/// 그래서 화면 한가운데에 보이지 않는 창을 잠깐 세워 소유자로 준다.
/// Topmost 라서 포커스를 얻지 못하더라도 최소한 앞에는 선다.
/// </summary>
internal static class TrayDialog
{
    public static MessageBoxResult Show(
        string text,
        string caption,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.Information)
    {
        Window anchor = CreateAnchor();

        anchor.Show();
        anchor.Activate();

        try
        {
            return MessageBox.Show(anchor, text, caption, buttons, icon);
        }
        finally
        {
            // ShutdownMode 가 OnExplicitShutdown 이라 이 창을 닫아도 앱은 살아 있다.
            anchor.Close();
        }
    }

    /// <summary>
    /// 눈에 보이지 않는 소유자 창.
    ///
    /// 화면 한가운데여야 한다. MessageBox 는 소유자 창을 기준으로 가운데에 뜨므로,
    /// 소유자를 화면 밖에 두면 대화 상자도 화면 밖에 뜬다.
    /// </summary>
    private static Window CreateAnchor() => new()
    {
        Width = 1,
        Height = 1,
        WindowStartupLocation = WindowStartupLocation.CenterScreen,
        WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false,
        AllowsTransparency = true,
        Background = Brushes.Transparent,
        Topmost = true,
    };
}
