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
}
