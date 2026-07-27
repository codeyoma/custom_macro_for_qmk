# 문장 매크로 (MacroTyper)

QMK 매크로패드의 키를 누르면, Windows에서 지금 커서가 있는 자리에 미리 등록해 둔 문장이 타이핑되어 들어간다.

- 하드웨어: Helix Pico 오른쪽 PCB 한쪽 (물리 키 25개)
- 문장 24개 + 치트시트 레이어 키 1개
- 클립보드를 전혀 건드리지 않는다
- 평소에는 트레이에만 상주한다

## 동작 방식

```
[매크로패드]  키를 누름
     │
     │  Raw HID 32바이트: 매직(0xAB) + 명령 + 슬롯 번호
     ▼
[MacroTyper]  번호로 문장을 찾아 SendInput 으로 한 글자씩 타이핑
     ▼
[활성 창]     커서 자리에 문장이 들어감
```

키보드는 **슬롯 번호만** 보내고 문장 내용은 전혀 모른다. 문장을 바꿔도 펌웨어를 다시 굽지 않는다.
펌웨어를 건드리는 것은 최초 1회뿐이다.

레이어 키를 누르고 있는 동안에는 24칸 치트시트가 화면에 뜬다. 이 오버레이는 포커스를 받지 않으므로
글을 쓰던 자리의 커서가 그대로 유지된다.

## 시작하기

### 1. 펌웨어

[firmware/helix_pico_macro/README.md](firmware/helix_pico_macro/README.md) 참고.

**한쪽만 쓸 때는 TRRS 케이블을 반드시 뽑아 두세요.** 케이블이 꽂힌 채 반대편이 전원을 받지 못하면
AVR soft serial의 타임아웃 없는 대기 루프에 갇혀 키보드 전체가 먹통이 된다.

```bash
qmk compile -kb helix/pico -km eunsun
```

### 2. 프로그램

Windows에서 빌드한다.

```bash
dotnet build src/MacroTyper/MacroTyper.csproj -c Release
```

단일 exe로 만들려면:

```bash
dotnet publish src/MacroTyper/MacroTyper.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

실행하면 트레이에만 뜬다. 트레이 아이콘을 더블클릭하면 문장 관리창이 열린다.

### 3. 테스트

순수 로직은 Windows 없이도 돌아간다. macOS에서도 실행된다.

```bash
dotnet test tests/MacroTyper.Tests/MacroTyper.Tests.csproj
```

## 구성

| 프로젝트 | 대상 | 내용 |
|---|---|---|
| `MacroTyper.Core` | `net8.0` | 프로토콜 해석, 슬롯 저장, 입력 변환, HID 수신. Windows 없이 테스트된다 |
| `MacroTyper` | `net8.0-windows` | WPF 화면과 실제 `SendInput` 호출 |
| `MacroTyper.Tests` | `net8.0` | Core 단위 테스트 |

Core를 크로스 플랫폼으로 분리한 덕분에 까다로운 로직(개행 처리, 서로게이트 페어, 손상 파일 복구)을
하드웨어도 Windows도 없이 검증할 수 있다.

## 설정 파일

`%APPDATA%\MacroTyper\slots.json`

임시 파일에 쓴 뒤 교체하므로 저장 중에 꺼져도 기존 파일이 남는다.
파일이 깨져서 읽을 수 없으면 `slots.corrupt-<시각>.json`으로 옮겨 두고 빈 슬롯으로 시작한다.
사용자가 쓴 문장을 조용히 지우지 않는다.

## 알려진 제약

**관리자 권한으로 뜬 창에는 들어가지 않는다.** Windows의 UIPI 때문에 일반 권한 프로세스는
관리자 권한 앱에 입력을 넣을 수 없다. 게다가 `SendInput`은 이 실패를 반환값으로도 알려주지 않는다.
그래서 이 프로그램은 삽입 전에 대상 창의 권한을 미리 확인해서, 막힐 상황이면 이유를 알려 준다.
필요하면 트레이 메뉴의 "관리자 권한으로 재시작"을 쓴다.

평소에는 일반 권한으로 두는 편이 낫다. 관리자 권한으로 실행하면 로그온 자동 실행이
매번 UAC 프롬프트를 띄우게 된다.

**한글 IME가 조합 중이면 글자가 깨질 수 있다.** 삽입 직전에 IME를 잠깐 닫아 조합을 확정시키지만,
Windows 11의 새 IME(TSF 기반)에서는 이 방법이 통하지 않을 수 있다.
확실하게 하려면 삽입 전에 한/영을 영문 상태로 두는 것이 안전하다.

**게임에는 들어가지 않는다.** DirectInput이나 Raw Input을 쓰는 게임은 주입된 입력을 걸러낸다.
이건 소프트웨어로 우회할 수 없다.

**긴 문장은 나눠서 보낸다.** 한 번에 보내면 대상 앱이 입력을 놓친다.
50자씩 잘라 보내되, 이모지가 잘리지 않도록 경계를 조정한다.

## 문서

- [설계 문서](docs/superpowers/specs/2026-07-27-qmk-macro-typer-design.md)
- [펌웨어 설치](firmware/helix_pico_macro/README.md)
