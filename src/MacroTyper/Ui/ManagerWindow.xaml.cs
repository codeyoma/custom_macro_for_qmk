using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MacroTyper.Core;
using MacroTyper.Interop;
using Microsoft.Win32;

namespace MacroTyper.Ui;

/// <summary>슬롯 24개를 보고 고치는 창.</summary>
public partial class ManagerWindow : Window
{
    private const int TestCountdownSeconds = 3;

    private readonly SlotStore _store;
    private readonly TextInjector _injector;
    private readonly Action _slotsChanged;
    private readonly Func<Hotkey, bool> _applyHotkey;

    private bool _capturingHotkey;

    /// <summary>마지막으로 파일에 쓴 메모. 바뀐 게 없으면 저장하지 않는다.</summary>
    private string _savedMemo = string.Empty;

    private int _selectedIndex = -1;
    private DispatcherTimer? _countdown;
    private int _secondsLeft;
    private GridRotation? _appliedRotation;

    public ManagerWindow(
        SlotStore store,
        TextInjector injector,
        Action slotsChanged,
        Func<Hotkey, bool> applyHotkey)
    {
        InitializeComponent();

        _store = store;
        _injector = injector;
        _slotsChanged = slotsChanged;
        _applyHotkey = applyHotkey;

        RefreshGrid();
        RefreshHotkeyButton();

        MemoBox.Text = _store.Memo;
    }

    // --- 설정 백업 ---

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
            return;

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "설정 내보내기",
            FileName = $"macrotyper-{DateTime.Now:yyyyMMdd}.json",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
            DefaultExt = ".json",
        };

        if (dialog.ShowDialog(this) != true)
            return;

        // 메모는 포커스가 빠져야 저장되므로, 내보내기 전에 지금 내용을 확실히 반영한다.
        _store.Memo = MemoBox.Text;

        try
        {
            _store.ExportTo(dialog.FileName);
            HintText.Text = "내보냈습니다";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HintText.Text = "내보내지 못했습니다. 다른 위치를 골라보세요";
        }
    }

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "설정 가져오기",
            Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
            return;

        // 지금 설정을 통째로 덮어쓴다. 되돌릴 수 없으므로 한 번 묻는다.
        MessageBoxResult answer = MessageBox.Show(
            this,
            "지금 등록된 문장과 설정을 가져온 내용으로 모두 바꿉니다. 계속할까요?",
            "설정 가져오기",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.OK)
            return;

        if (!_store.TryImportFrom(dialog.FileName))
        {
            HintText.Text = "읽을 수 없는 파일입니다. 설정은 그대로 두었습니다";
            return;
        }

        _appliedRotation = null;
        MemoBox.Text = _store.Memo;
        _savedMemo = _store.Memo;

        RefreshGrid();
        RefreshHotkeyButton();
        _applyHotkey(_store.CheatHotkey);
        _slotsChanged();

        ClearSelection();
        HintText.Text = "가져왔습니다";
    }

    private void OnOpenSettingsFolder(object sender, RoutedEventArgs e)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacroTyper");

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            HintText.Text = folder;
        }
    }

    /// <summary>가져오기로 내용이 통째로 바뀌면 편집 중이던 슬롯은 의미가 없어진다.</summary>
    private void ClearSelection()
    {
        _selectedIndex = -1;

        EditingHeader.Text = "슬롯을 선택하세요";
        LabelBox.Text = string.Empty;
        TextBoxContent.Text = string.Empty;
        AppendEnterBox.IsChecked = false;

        LabelBox.IsEnabled = false;
        TextBoxContent.IsEnabled = false;
        AppendEnterBox.IsEnabled = false;
        SaveButton.IsEnabled = false;
        TestButton.IsEnabled = false;
    }

    // --- 늘 떠 있는 메모 ---

    /// <summary>
    /// 치트시트에는 곧바로 반영하고, 파일 저장은 입력이 끝난 뒤로 미룬다.
    /// 한 글자마다 파일을 쓰면 디스크를 계속 두드리게 된다.
    /// </summary>
    private void OnMemoChanged(object sender, TextChangedEventArgs e)
    {
        _store.Memo = MemoBox.Text;
        _slotsChanged();
    }

    private void OnMemoCommitted(object sender, RoutedEventArgs e) => SaveMemo();

    private void SaveMemo()
    {
        if (_store.Memo == _savedMemo)
            return;

        try
        {
            _store.Save();
            _savedMemo = _store.Memo;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HintText.Text = "메모를 저장하지 못했습니다";
        }
    }

    /// <summary>
    /// 닫기 버튼으로는 종료하지 않는다. 이 앱은 트레이에 상주하는 게 정상 상태다.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        StopCountdown();

        // 메모를 적다가 창을 닫아도 잃지 않게 한다.
        SaveMemo();

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

    // --- 치트시트를 여는 전역 단축키 ---

    private void RefreshHotkeyButton()
    {
        HotkeyButton.Content = _store.CheatHotkey.IsSet
            ? _store.CheatHotkey.Describe()
            : "단축키 없음";
    }

    private void OnHotkeyButtonClicked(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey)
        {
            StopCapturingHotkey();
            return;
        }

        _capturingHotkey = true;
        HotkeyButton.Content = "키를 누르세요";
        HintText.Text = "Ctrl · Alt · Shift · Win 중 하나를 함께 눌러야 합니다. Esc 로 취소, Delete 로 해제";
    }

    private void StopCapturingHotkey()
    {
        _capturingHotkey = false;
        RefreshHotkeyButton();
    }

    /// <summary>
    /// 단축키를 잡는 동안에는 이 창의 모든 키 입력을 가로챈다.
    /// 그러지 않으면 Ctrl+A 같은 조합이 편집 상자로 새어 들어간다.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_capturingHotkey)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;

        // Alt 조합은 SystemKey 로 온다.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            StopCapturingHotkey();
            HintText.Text = string.Empty;
            return;
        }

        if (key is Key.Delete or Key.Back)
        {
            ApplyHotkey(Hotkey.None);
            return;
        }

        // 보조 키만 눌린 상태는 아직 조합이 완성되지 않은 것이다. 계속 기다린다.
        if (IsModifierKey(key))
            return;

        HotkeyModifiers modifiers = ToHotkeyModifiers(Keyboard.Modifiers);

        if (modifiers == HotkeyModifiers.None)
        {
            HintText.Text = "보조 키 없이 등록하면 그 키를 다른 앱에서 쓸 수 없게 됩니다";
            return;
        }

        ApplyHotkey(new Hotkey(modifiers, (uint)KeyInterop.VirtualKeyFromKey(key)));
    }

    private void ApplyHotkey(Hotkey hotkey)
    {
        _capturingHotkey = false;

        if (!_applyHotkey(hotkey))
        {
            RefreshHotkeyButton();
            HintText.Text = "다른 프로그램이 이미 쓰는 조합입니다. 다른 키로 해보세요";
            return;
        }

        RefreshHotkeyButton();
        HintText.Text = hotkey.IsSet ? "단축키를 등록했습니다" : "단축키를 해제했습니다";
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys keys)
    {
        HotkeyModifiers result = HotkeyModifiers.None;

        if (keys.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (keys.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (keys.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (keys.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Windows;

        return result;
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
