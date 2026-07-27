using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MacroTyper.Core;
using MacroTyper.Interop;

namespace MacroTyper.Ui;

/// <summary>슬롯 24개를 보고 고치는 창.</summary>
public partial class ManagerWindow : Window
{
    private const int TestCountdownSeconds = 3;

    private readonly SlotStore _store;
    private readonly TextInjector _injector;
    private readonly Action _slotsChanged;

    private int _selectedIndex = -1;
    private DispatcherTimer? _countdown;
    private int _secondsLeft;
    private GridRotation? _appliedRotation;

    public ManagerWindow(SlotStore store, TextInjector injector, Action slotsChanged)
    {
        InitializeComponent();

        _store = store;
        _injector = injector;
        _slotsChanged = slotsChanged;

        RefreshGrid();
    }

    /// <summary>
    /// 닫기 버튼으로는 종료하지 않는다. 이 앱은 트레이에 상주하는 게 정상 상태다.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        StopCountdown();
        Hide();
    }

    public void SetConnectionState(bool connected)
    {
        StatusText.Text = connected ? "매크로패드 연결됨" : "매크로패드 없음";
        StatusText.Foreground = new SolidColorBrush(connected ? Color.FromRgb(0x0F, 0x6E, 0x56) : Color.FromRgb(0xA3, 0x2D, 0x2D));
        StatusBadge.Background = new SolidColorBrush(connected ? Color.FromRgb(0xE1, 0xF5, 0xEE) : Color.FromRgb(0xFC, 0xEB, 0xEB));
    }

    public void RefreshGrid()
    {
        GridRotation rotation = _store.Rotation;

        if (_appliedRotation != rotation)
        {
            (int rows, int columns) = SlotGrid.SizeFor(rotation);
            SlotCells.ItemsPanel = SlotGridTemplate.For(rows, columns);
            _appliedRotation = rotation;
        }

        SlotCells.ItemsSource = SlotGrid.Cells(rotation)
            .Select(cell => OverlayCell.From(cell, _store.Slots))
            .ToArray();

        RotateButton.Content = $"방향 {(int)rotation}°";
    }

    /// <summary>
    /// 매크로패드를 돌려 놓았을 때 격자도 같이 돌린다.
    /// 슬롯 번호와 등록해 둔 문장은 그대로다. 화면에 늘어놓는 순서만 바뀐다.
    /// </summary>
    private void OnRotate(object sender, RoutedEventArgs e)
    {
        _store.Rotation = SlotGrid.Next(_store.Rotation);

        try
        {
            _store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 저장에 실패해도 이번 실행 동안은 바뀐 방향으로 보여 준다.
            HintText.Text = "방향을 저장하지 못했습니다";
        }

        RefreshGrid();
        _slotsChanged();
    }

    private void OnSlotClicked(object sender, RoutedEventArgs e)
    {
        // 치트시트 키 자리와 빈틈은 고칠 내용이 없다.
        if (sender is Button { DataContext: OverlayCell { Kind: GridCellKind.Slot } cell })
            SelectSlot(cell.Number - 1);
    }

    private void SelectSlot(int index)
    {
        _selectedIndex = index;
        Slot slot = _store[index];

        EditingHeader.Text = $"슬롯 {index + 1} 편집";
        LabelBox.Text = slot.Label;
        TextBoxContent.Text = slot.Text;
        AppendEnterBox.IsChecked = slot.AppendEnter;

        LabelBox.IsEnabled = true;
        TextBoxContent.IsEnabled = true;
        AppendEnterBox.IsEnabled = true;
        SaveButton.IsEnabled = true;
        TestButton.IsEnabled = true;

        HintText.Text = string.Empty;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0)
            return;

        _store.Set(new Slot(
            _selectedIndex,
            LabelBox.Text.Trim(),
            TextBoxContent.Text,
            AppendEnterBox.IsChecked == true));

        try
        {
            _store.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HintText.Text = "저장 실패: 파일에 쓸 수 없습니다";
            return;
        }

        RefreshGrid();
        _slotsChanged();

        HintText.Text = "저장했습니다";
    }

    /// <summary>
    /// 저장한 문장이 실제로 잘 들어가는지 확인한다.
    /// 카운트다운 동안 사용자가 넣어 볼 창을 클릭하면, 시간이 다 됐을 때 그 창에 들어간다.
    /// 한글 IME나 특정 앱에서 깨지는지를 실사용 전에 여기서 잡는다.
    /// </summary>
    private void OnTestInject(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0)
            return;

        if (_countdown is not null)
        {
            StopCountdown();
            return;
        }

        _secondsLeft = TestCountdownSeconds;
        TestButton.Content = "취소";
        HintText.Text = $"{_secondsLeft}초 뒤에 넣습니다. 넣어 볼 창을 클릭하세요";

        _countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdown.Tick += OnCountdownTick;
        _countdown.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _secondsLeft--;

        if (_secondsLeft > 0)
        {
            HintText.Text = $"{_secondsLeft}초 뒤에 넣습니다. 넣어 볼 창을 클릭하세요";
            return;
        }

        StopCountdown();

        InjectionOutcome outcome = _injector.Inject(
            TextBoxContent.Text,
            AppendEnterBox.IsChecked == true);

        HintText.Text = Describe(outcome);
    }

    private void StopCountdown()
    {
        if (_countdown is null)
            return;

        _countdown.Stop();
        _countdown.Tick -= OnCountdownTick;
        _countdown = null;

        TestButton.Content = "테스트 삽입";
    }

    private static string Describe(InjectionOutcome outcome) => outcome switch
    {
        InjectionOutcome.Success => "넣었습니다",
        InjectionOutcome.NothingToInject => "내용이 비어 있습니다",
        InjectionOutcome.TargetIsSelf => "다른 창을 클릭한 뒤 다시 해보세요",
        InjectionOutcome.BlockedByElevation => "대상 창이 관리자 권한입니다. 트레이 메뉴에서 관리자 권한으로 재시작하세요",
        InjectionOutcome.Incomplete => "일부만 들어갔습니다. 문장이 너무 길 수 있습니다",
        _ => string.Empty,
    };
}
