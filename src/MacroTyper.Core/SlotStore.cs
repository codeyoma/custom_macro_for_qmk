using System.Text.Encodings.Web;
using System.Text.Json;

namespace MacroTyper.Core;

/// <summary>
/// 슬롯 24개를 JSON 파일로 읽고 쓴다. 파일 포맷을 아는 유일한 조각이다.
/// </summary>
public sealed class SlotStore
{
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        // 사용자가 파일을 직접 열어볼 수 있게 한글을 \uXXXX로 이스케이프하지 않는다.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _filePath;
    private Slot[] _slots = CreateEmptySet();
    private GridRotation _rotation = GridRotation.None;
    private Hotkey _cheatHotkey = Hotkey.None;

    public SlotStore(string filePath) => _filePath = filePath;

    /// <summary>
    /// 매크로패드를 돌려 놓았을 때의 화면 방향.
    /// 화면에 늘어놓는 순서만 바꾼다. 슬롯 번호와 문장은 그대로다.
    ///
    /// 정의되지 않은 각도는 받지 않는다. 그대로 두면 격자 크기 계산이 어긋나 화면이 깨진다.
    /// </summary>
    public GridRotation Rotation
    {
        get => _rotation;
        set => _rotation = Enum.IsDefined(value) ? value : GridRotation.None;
    }

    /// <summary>
    /// 치트시트를 여는 전역 단축키. 매크로패드의 레이어 키와는 별개의 길이다.
    ///
    /// 보조 키 없는 조합은 받지 않는다. 그런 걸 등록하면 그 키가 시스템 전체에서 막혀
    /// 어느 앱에서도 입력할 수 없게 된다.
    /// </summary>
    public Hotkey CheatHotkey
    {
        get => _cheatHotkey;
        set => _cheatHotkey = value.IsSet && value.HasModifier ? value : Hotkey.None;
    }

    /// <summary>%APPDATA%\MacroTyper\slots.json 을 사용하는 기본 저장소.</summary>
    public static SlotStore OpenDefault()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacroTyper");

        return new SlotStore(Path.Combine(dir, "slots.json"));
    }

    /// <summary>항상 <see cref="MacroProtocol.SlotCount"/>개. 인덱스가 위치와 일치한다.</summary>
    public IReadOnlyList<Slot> Slots => _slots;

    public Slot this[int index]
    {
        get
        {
            ThrowIfOutOfRange(index, nameof(index));
            return _slots[index];
        }
    }

    /// <summary>
    /// 파일에서 읽는다. 파일이 없으면 빈 슬롯으로 시작한다.
    /// 파일이 손상되어 읽을 수 없으면 원본을 백업해 두고 빈 슬롯으로 시작한다.
    /// 사용자 데이터를 조용히 지우지 않기 위해서다.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _slots = CreateEmptySet();
            return;
        }

        SlotFile? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SlotFile>(File.ReadAllText(_filePath), JsonOptions);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            BackupCorruptFile();
            _slots = CreateEmptySet();
            return;
        }

        _slots = parsed?.Slots is { } entries ? Normalize(entries) : CreateEmptySet();

        // 속성 setter 를 거쳐야 파일에 적힌 엉뚱한 값이 걸러진다.
        Rotation = parsed?.Rotation ?? GridRotation.None;
        CheatHotkey = parsed?.CheatHotkey ?? Hotkey.None;
    }

    /// <summary>임시 파일에 쓴 뒤 교체한다. 저장 중 중단되어도 기존 파일이 남는다.</summary>
    public void Save()
    {
        string? dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var payload = new SlotFile(
            FormatVersion,
            _rotation,
            _cheatHotkey,
            _slots.Select(s => new SlotEntry(s.Index, s.Label, s.Text, s.AppendEnter)).ToArray());

        string temp = _filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(payload, JsonOptions));
        File.Move(temp, _filePath, overwrite: true);
    }

    /// <summary>슬롯 하나를 갈아끼운다. <paramref name="slot"/>의 인덱스 위치에 놓인다.</summary>
    public void Set(Slot slot)
    {
        ThrowIfOutOfRange(slot.Index, nameof(slot));
        _slots[slot.Index] = slot;
    }

    private static Slot[] CreateEmptySet() =>
        Enumerable.Range(0, MacroProtocol.SlotCount).Select(Slot.Empty).ToArray();

    /// <summary>
    /// 파일 안의 index 값은 신뢰하지 않는다. 배열 위치가 진실이다.
    /// 손으로 편집된 파일 때문에 슬롯이 엉뚱한 키에 붙는 것을 막는다.
    /// </summary>
    private static Slot[] Normalize(IReadOnlyList<SlotEntry> entries)
    {
        var result = CreateEmptySet();
        int count = Math.Min(entries.Count, MacroProtocol.SlotCount);

        for (int i = 0; i < count; i++)
        {
            SlotEntry entry = entries[i];
            result[i] = new Slot(i, entry.Label ?? string.Empty, entry.Text ?? string.Empty, entry.AppendEnter);
        }

        return result;
    }

    private void BackupCorruptFile()
    {
        try
        {
            string dir = Path.GetDirectoryName(_filePath) ?? ".";
            string stem = Path.GetFileNameWithoutExtension(_filePath);
            string ext = Path.GetExtension(_filePath);
            string backup = Path.Combine(dir, $"{stem}.corrupt-{DateTime.Now:yyyyMMdd-HHmmssfff}{ext}");

            File.Move(_filePath, backup, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 백업조차 실패하면 손상된 파일을 그대로 두고 기본값으로 진행한다.
            // 다음 저장에서 덮어써지지만, 사용자 데이터를 지우려 애쓰다 시작조차 못 하는 것보다 낫다.
        }
    }

    private static void ThrowIfOutOfRange(int index, string paramName)
    {
        if (index < 0 || index >= MacroProtocol.SlotCount)
            throw new ArgumentOutOfRangeException(
                paramName, index, $"슬롯 인덱스는 0 이상 {MacroProtocol.SlotCount} 미만이어야 합니다.");
    }

    // 예전 파일도 그대로 읽힌다. 빠진 값은 기본값이 된다.
    internal sealed record SlotFile(int Version, GridRotation Rotation, Hotkey CheatHotkey, SlotEntry[] Slots);

    internal sealed record SlotEntry(int Index, string? Label, string? Text, bool AppendEnter);
}
