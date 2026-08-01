using MacroTyper.Core;

namespace MacroTyper.Tests;

public class SlotStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SlotStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "macrotyper-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "slots.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private SlotStore LoadedStore()
    {
        var store = new SlotStore(_path);
        store.Load();
        return store;
    }

    [Fact]
    public void Load_WithoutFile_YieldsFullSetOfEmptySlots()
    {
        var store = LoadedStore();

        Assert.Equal(MacroProtocol.SlotCount, store.Slots.Count);
        Assert.All(store.Slots, slot => Assert.True(slot.IsEmpty));
    }

    [Fact]
    public void Load_WithoutFile_AssignsSequentialIndexes()
    {
        var store = LoadedStore();

        for (int i = 0; i < MacroProtocol.SlotCount; i++)
            Assert.Equal(i, store.Slots[i].Index);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsSlotContent()
    {
        var store = LoadedStore();
        store.Set(new Slot(3, "주소", "서울특별시 강남구 테헤란로 123", true));
        store.Save();

        var reloaded = LoadedStore();

        Assert.Equal("주소", reloaded[3].Label);
        Assert.Equal("서울특별시 강남구 테헤란로 123", reloaded[3].Text);
        Assert.True(reloaded[3].AppendEnter);
    }

    [Fact]
    public void SaveThenLoad_PreservesLineBreaksAndUnicode()
    {
        var store = LoadedStore();
        store.Set(new Slot(0, "여러 줄", "첫 줄\n둘째 줄\t탭\r\n셋째 🙂", false));
        store.Save();

        var reloaded = LoadedStore();

        Assert.Equal("첫 줄\n둘째 줄\t탭\r\n셋째 🙂", reloaded[0].Text);
    }

    [Fact]
    public void Save_CreatesMissingParentDirectory()
    {
        string nested = Path.Combine(_dir, "sub", "dir", "slots.json");
        var store = new SlotStore(nested);
        store.Load();
        store.Set(new Slot(0, "a", "b", false));

        store.Save();

        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        var store = LoadedStore();
        store.Set(new Slot(0, "a", "b", false));
        store.Save();

        Assert.Equal(["slots.json"], Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Load_WithCorruptFile_YieldsEmptySlots()
    {
        File.WriteAllText(_path, "{ this is not valid json");

        var store = LoadedStore();

        Assert.Equal(MacroProtocol.SlotCount, store.Slots.Count);
        Assert.All(store.Slots, slot => Assert.True(slot.IsEmpty));
    }

    [Fact]
    public void Load_WithCorruptFile_KeepsOriginalContentInBackup()
    {
        const string original = "{ this is not valid json";
        File.WriteAllText(_path, original);

        LoadedStore();

        var backups = Directory.GetFiles(_dir, "slots.corrupt-*.json");
        Assert.Single(backups);
        Assert.Equal(original, File.ReadAllText(backups[0]));
    }

    [Fact]
    public void Load_WithFewerSlotsThanExpected_PadsRemainderWithEmptySlots()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [ { "index": 0, "label": "하나", "text": "본문", "appendEnter": false } ] }
            """);

        var store = LoadedStore();

        Assert.Equal(MacroProtocol.SlotCount, store.Slots.Count);
        Assert.Equal("하나", store[0].Label);
        Assert.True(store[1].IsEmpty);
        Assert.True(store[MacroProtocol.SlotCount - 1].IsEmpty);
    }

    [Fact]
    public void Load_WithMoreSlotsThanExpected_DiscardsOverflow()
    {
        var entries = Enumerable.Range(0, MacroProtocol.SlotCount + 5)
            .Select(i => $$"""{ "index": {{i}}, "label": "L{{i}}", "text": "T{{i}}", "appendEnter": false }""");
        File.WriteAllText(_path, $$"""{ "version": 1, "slots": [ {{string.Join(",", entries)}} ] }""");

        var store = LoadedStore();

        Assert.Equal(MacroProtocol.SlotCount, store.Slots.Count);
    }

    /// <summary>
    /// 파일 안의 index 값은 신뢰하지 않는다. 배열 위치가 진실이다.
    /// 손으로 편집된 파일 때문에 슬롯이 엉뚱한 키에 붙는 것을 막는다.
    /// </summary>
    [Fact]
    public void Load_WithMismatchedIndexField_RebuildsIndexFromPosition()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [
              { "index": 99, "label": "첫째", "text": "A", "appendEnter": false },
              { "index": 99, "label": "둘째", "text": "B", "appendEnter": false }
            ] }
            """);

        var store = LoadedStore();

        Assert.Equal(0, store[0].Index);
        Assert.Equal(1, store[1].Index);
        Assert.Equal("첫째", store[0].Label);
        Assert.Equal("둘째", store[1].Label);
    }

    [Fact]
    public void Set_ReplacesSlotAtItsOwnIndex()
    {
        var store = LoadedStore();

        store.Set(new Slot(10, "라벨", "본문", false));

        Assert.Equal("라벨", store[10].Label);
        Assert.True(store[9].IsEmpty);
        Assert.True(store[11].IsEmpty);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(MacroProtocol.SlotCount)]
    public void Set_WithIndexOutOfRange_Throws(int index)
    {
        var store = LoadedStore();

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Set(new Slot(index, "a", "b", false)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(MacroProtocol.SlotCount)]
    public void Indexer_WithIndexOutOfRange_Throws(int index)
    {
        var store = LoadedStore();

        Assert.Throws<ArgumentOutOfRangeException>(() => store[index]);
    }

    // --- 매크로패드를 돌려 놓고 쓸 때의 화면 방향 ---

    [Fact]
    public void Rotation_ByDefault_IsNone()
    {
        Assert.Equal(GridRotation.None, LoadedStore().Rotation);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsRotation()
    {
        var store = LoadedStore();
        store.Rotation = GridRotation.Clockwise90;
        store.Save();

        Assert.Equal(GridRotation.Clockwise90, LoadedStore().Rotation);
    }

    [Fact]
    public void Load_WithoutRotationField_FallsBackToNone()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [ { "index": 0, "label": "하나", "text": "본문", "appendEnter": false } ] }
            """);

        var store = LoadedStore();

        Assert.Equal(GridRotation.None, store.Rotation);
        Assert.Equal("하나", store[0].Label);
    }

    /// <summary>
    /// 손으로 편집된 파일에 엉뚱한 각도가 들어올 수 있다.
    /// 그대로 받아들이면 격자 크기 계산이 어긋나 화면이 깨진다.
    /// </summary>
    [Fact]
    public void Load_WithUnknownRotationValue_FallsBackToNone()
    {
        File.WriteAllText(_path, """
            { "version": 1, "rotation": 45, "slots": [] }
            """);

        Assert.Equal(GridRotation.None, LoadedStore().Rotation);
    }

    [Fact]
    public void Rotation_SetToUnknownValue_FallsBackToNone()
    {
        var store = LoadedStore();

        store.Rotation = (GridRotation)137;

        Assert.Equal(GridRotation.None, store.Rotation);
    }

    // --- 내보내기와 가져오기 ---

    [Fact]
    public void ExportTo_WritesFileThatCanBeLoadedBack()
    {
        string exported = Path.Combine(_dir, "backup.json");

        var store = LoadedStore();
        store.Set(new Slot(2, "주소", "서울특별시", true));
        store.Memo = "메모 내용";
        store.Rotation = GridRotation.Clockwise90;
        store.ExportTo(exported);

        var loaded = new SlotStore(exported);
        loaded.Load();

        Assert.Equal("주소", loaded[2].Label);
        Assert.True(loaded[2].AppendEnter);
        Assert.Equal("메모 내용", loaded.Memo);
        Assert.Equal(GridRotation.Clockwise90, loaded.Rotation);
    }

    /// <summary>내보내기는 원래 쓰던 파일을 건드리지 않는다.</summary>
    [Fact]
    public void ExportTo_DoesNotTouchTheWorkingFile()
    {
        var store = LoadedStore();
        store.Set(new Slot(0, "라벨", "본문", false));
        store.Save();

        store.Set(new Slot(0, "바뀐 라벨", "바뀐 본문", false));
        store.ExportTo(Path.Combine(_dir, "backup.json"));

        // 저장하지 않은 변경은 원래 파일에 반영되지 않아야 한다.
        Assert.Equal("라벨", LoadedStore()[0].Label);
    }

    [Fact]
    public void ExportTo_CreatesMissingDirectory()
    {
        string nested = Path.Combine(_dir, "some", "where", "backup.json");

        LoadedStore().ExportTo(nested);

        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void TryImportFrom_ReplacesCurrentSettings()
    {
        string source = Path.Combine(_dir, "source.json");
        var origin = new SlotStore(source);
        origin.Load();
        origin.Set(new Slot(5, "가져온 라벨", "가져온 본문", true));
        origin.Memo = "가져온 메모";
        origin.Save();

        var target = LoadedStore();
        bool ok = target.TryImportFrom(source);

        Assert.True(ok);
        Assert.Equal("가져온 라벨", target[5].Label);
        Assert.Equal("가져온 메모", target.Memo);
    }

    /// <summary>
    /// 가져온 내용은 원래 쓰던 파일에도 남아야 한다.
    /// 그러지 않으면 프로그램을 껐다 켰을 때 되돌아간다.
    /// </summary>
    [Fact]
    public void TryImportFrom_PersistsToWorkingFile()
    {
        string source = Path.Combine(_dir, "source.json");
        var origin = new SlotStore(source);
        origin.Load();
        origin.Set(new Slot(1, "옮겨온 것", "본문", false));
        origin.Save();

        LoadedStore().TryImportFrom(source);

        Assert.Equal("옮겨온 것", LoadedStore()[1].Label);
    }

    [Fact]
    public void TryImportFrom_MissingFile_ReturnsFalse()
    {
        Assert.False(LoadedStore().TryImportFrom(Path.Combine(_dir, "없는파일.json")));
    }

    /// <summary>가져오기에 실패했다고 쓰던 설정이 날아가면 안 된다.</summary>
    [Fact]
    public void TryImportFrom_CorruptFile_KeepsCurrentSettings()
    {
        string broken = Path.Combine(_dir, "broken.json");
        File.WriteAllText(broken, "{ 이건 json 이 아니다");

        var store = LoadedStore();
        store.Set(new Slot(0, "원래 라벨", "원래 본문", false));

        bool ok = store.TryImportFrom(broken);

        Assert.False(ok);
        Assert.Equal("원래 라벨", store[0].Label);
    }

    [Fact]
    public void TryImportFrom_CorruptFile_DoesNotBackUpTheSourceFile()
    {
        string broken = Path.Combine(_dir, "broken.json");
        File.WriteAllText(broken, "{ 이건 json 이 아니다");

        LoadedStore().TryImportFrom(broken);

        // 손상 백업은 '쓰던 파일'을 지킬 때만 만든다.
        // 남의 파일을 읽다 실패했다고 그 파일을 옮겨 버리면 안 된다.
        Assert.True(File.Exists(broken));
        Assert.Empty(Directory.GetFiles(_dir, "broken.corrupt-*.json"));
    }

    // --- 화면 아래에 늘 떠 있는 메모 ---

    [Fact]
    public void Memo_ByDefault_IsEmpty()
    {
        Assert.Equal(string.Empty, LoadedStore().Memo);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsMemo()
    {
        var store = LoadedStore();
        store.Memo = "회신 기한 금요일까지";
        store.Save();

        Assert.Equal("회신 기한 금요일까지", LoadedStore().Memo);
    }

    [Fact]
    public void SaveThenLoad_PreservesMemoLineBreaks()
    {
        var store = LoadedStore();
        store.Memo = "첫 줄\n둘째 줄\r\n셋째 줄";
        store.Save();

        Assert.Equal("첫 줄\n둘째 줄\r\n셋째 줄", LoadedStore().Memo);
    }

    [Fact]
    public void Load_WithoutMemoField_FallsBackToEmpty()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [] }
            """);

        Assert.Equal(string.Empty, LoadedStore().Memo);
    }

    /// <summary>메모를 지우면 빈 문자열이지 null 이 아니다. 화면 쪽에서 null 검사를 하지 않아도 되게 한다.</summary>
    [Fact]
    public void Memo_SetToNull_BecomesEmpty()
    {
        var store = LoadedStore();

        store.Memo = null!;

        Assert.Equal(string.Empty, store.Memo);
    }

    [Fact]
    public void Load_WithNullMemoInFile_FallsBackToEmpty()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [], "memo": null }
            """);

        Assert.Equal(string.Empty, LoadedStore().Memo);
    }

    // --- 새 버전 자동 확인 ---

    /// <summary>
    /// 기본값은 켬이다. 새 버전이 나온 줄 모르고 옛 버전을 계속 쓰는 쪽이
    /// 하루 한 번 GitHub 에 묻는 것보다 손해가 크다.
    /// </summary>
    [Fact]
    public void CheckForUpdates_ByDefault_IsOn()
    {
        Assert.True(LoadedStore().CheckForUpdates);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsCheckForUpdates()
    {
        var store = LoadedStore();
        store.CheckForUpdates = false;
        store.Save();

        Assert.False(LoadedStore().CheckForUpdates);
    }

    /// <summary>이 항목이 없던 시절의 파일은 켜 둔 것으로 읽는다.</summary>
    [Fact]
    public void Load_WithoutCheckForUpdatesField_FallsBackToOn()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [] }
            """);

        Assert.True(LoadedStore().CheckForUpdates);
    }

    // --- 치트시트를 여는 전역 단축키 ---

    [Fact]
    public void CheatHotkey_ByDefault_IsUnset()
    {
        Assert.False(LoadedStore().CheatHotkey.IsSet);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsHotkey()
    {
        var hotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20);

        var store = LoadedStore();
        store.CheatHotkey = hotkey;
        store.Save();

        Assert.Equal(hotkey, LoadedStore().CheatHotkey);
    }

    [Fact]
    public void Load_WithoutHotkeyField_FallsBackToUnset()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [] }
            """);

        Assert.False(LoadedStore().CheatHotkey.IsSet);
    }

    /// <summary>
    /// 보조 키 없는 단축키가 파일에 적혀 있으면 그 키가 시스템 전체에서 막힌다.
    /// 손으로 편집된 파일을 그대로 믿지 않는다.
    /// </summary>
    [Fact]
    public void CheatHotkey_SetWithoutModifier_IsRejected()
    {
        var store = LoadedStore();

        store.CheatHotkey = new Hotkey(HotkeyModifiers.None, 0x41);

        Assert.False(store.CheatHotkey.IsSet);
    }

    [Fact]
    public void Load_WithModifierlessHotkeyInFile_FallsBackToUnset()
    {
        File.WriteAllText(_path, """
            { "version": 1, "slots": [], "cheatHotkey": { "modifiers": 0, "virtualKey": 65 } }
            """);

        Assert.False(LoadedStore().CheatHotkey.IsSet);
    }

    [Fact]
    public void SaveThenLoad_KeepsRotationAndSlotsTogether()
    {
        var store = LoadedStore();
        store.Rotation = GridRotation.CounterClockwise90;
        store.Set(new Slot(7, "라벨", "본문", true));
        store.Save();

        var reloaded = LoadedStore();

        Assert.Equal(GridRotation.CounterClockwise90, reloaded.Rotation);
        Assert.Equal("라벨", reloaded[7].Label);
        Assert.True(reloaded[7].AppendEnter);
    }
}
