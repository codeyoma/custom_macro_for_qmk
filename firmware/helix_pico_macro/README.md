# Helix Pico 매크로패드 펌웨어

Helix Pico **오른쪽 PCB 한쪽만** 단독으로 쓰는 키맵이다.
물리 키 25개 중 24개가 문장 슬롯, 맨 아랫줄 **왼쪽 끝** 1개가 치트시트 레이어 키다.
LED 는 색이 흐르는 애니메이션으로 돈다.

## 반드시 먼저 읽을 것

**TRRS 케이블을 뽑아 두세요.**

AVR soft serial의 대기 루프에는 타임아웃이 없고 인터럽트가 꺼진 구간에서 돈다
(`platforms/avr/drivers/serial.c`). 케이블이 꽂혀 있는데 반대편이 전원을 받지 못하면
마스터가 그 루프에 갇혀 **키보드 전체가 먹통**이 된다.

케이블이 없으면 해당 핀이 내부 풀업으로 high라 즉시 통과하므로 안전하다.

## 설치

이 폴더의 네 파일을 QMK 키맵 폴더로 복사한다.

```bash
qmk new-keymap -kb helix/pico -km eunsun
```

`helix/pico`의 기본 키맵은 `keymap.c`가 아니라 `keymap.json` 하나뿐이고,
`qmk new-keymap`은 그 폴더를 통째로 복사한다. **생성된 `keymap.json`을 반드시 지워야 한다.**
남아 있으면 빌드가 JSON 경로를 타서 `keymap.c`가 통째로 무시된다.

```bash
KM=~/qmk_firmware/keyboards/helix/pico/keymaps/eunsun
rm -f "$KM/keymap.json"
cp keymap.c config.h rules.mk macro_protocol.h "$KM/"
```

## 빌드와 플래시

```bash
qmk compile -kb helix/pico -km eunsun
```

```bash
qmk flash -kb helix/pico -km eunsun
```

빌드 타겟은 `helix/pico` 하나뿐이다. `helix/pico/sc`, `helix/pico/under`, `helix/pico/base`
같은 하위 리비전은 2025년 7월 QMK PR #25428에서 전부 제거되었고 alias도 남지 않았다.
인터넷의 옛 Helix Pico 문서 대부분이 이 경로를 쓰므로 그대로 따라 하면 실패한다.

부트로더 진입은 TRRS 잭 옆 물리 리셋 버튼을 쓴다. Pro Micro(Caterina)라 리셋 두 번으로도 들어간다.

## 키 배치

오른쪽 PCB를 정면에서 볼 때 왼쪽 위부터 순서대로다.

맨 아랫줄만 왼쪽으로 한 칸 튀어나와 있고, 그 자리가 레이어 키다.

```
         1   2   3   4   5   6
         7   8   9  10  11  12
        13  14  15  16  17  18
[레이어] 19  20  21  22  23  24
```

`[레이어]` 키를 누르고 있는 동안 PC 화면에 치트시트가 뜬다. 그 상태에서 다른 키를 누르면
해당 문장이 삽입되고, 키를 떼면 치트시트가 사라진다.

## 왜 이런 설정인가

| 설정 | 이유 |
|---|---|
| `MASTER_RIGHT` | 이게 없으면 QMK가 USB 꽂힌 쪽을 무조건 왼쪽으로 취급해, 오른쪽 자리에 넣은 키코드에 영원히 도달하지 못한다 |
| `SPLIT_MAX_CONNECTION_ERRORS 1` | 반대편이 없으므로 첫 실패에 바로 끊긴 것으로 판정해 헛스캔을 줄인다 |
| `RAW_ENABLE = yes` | 이 프로그램의 통신 수단. QMK 기본값은 off다 |
| `MOUSEKEY`/`EXTRAKEY`/`CONSOLE` = no | ATmega32U4는 USB 엔드포인트가 빠듯하다. Raw HID 자리를 만든다 |
| `OLED`/`RGB_MATRIX` = no | 한쪽만 쓰므로 불필요하고, 32U4의 빠듯한 플래시 용량을 아낀다 |

`SPLIT_KEYBOARD = no`로 split을 끄면 **안 된다.** `MATRIX_ROWS`는 `keyboard.json`에서 8로
산출된 채 남는데 split이 꺼지면 `MATRIX_ROWS_PER_HAND`가 4가 아니라 8이 되어,
`row_pins` 배열 뒤쪽 4칸이 0인 상태로 엉뚱한 핀을 스캔한다. 유령 입력이 생긴다.
QMK는 이미 한쪽만 쓰는 경우를 정식으로 지원한다.

## 하드웨어 참고값

| 항목 | 값 | 위치 |
|---|---|---|
| MCU | ATmega32U4 (Caterina) | `helix/info.json`의 `development_board: promicro`에서 유도 |
| 매트릭스 | 8행 × 7열 (반쪽당 4행) | `keyboard.json`에서 자동 산출, 파일에 리터럴 없음 |
| LAYOUT | 50키 (반쪽당 25키, 행별 6/6/6/7) | `keyboard.json`의 `layouts.LAYOUT` |
| VID | `0x3265` (Yushakobo) | `keyboards/helix/info.json` |
| PID | `0x0001` | `keyboards/helix/pico/keyboard.json` |
