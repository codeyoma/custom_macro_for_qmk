# Helix Pico 매크로패드 펌웨어

Helix Pico **한쪽 PCB 단독용** 키맵이다. 키 24개가 문장 슬롯이고, 맨 아랫줄에서 튀어나온 키가 치트시트 레이어 키다.
왼쪽·오른쪽 PCB 모두 쓸 수 있다. LED 는 끈다.

> **TRRS 케이블을 뽑아 두세요.** 꽂힌 채로 반대쪽에 전원이 없으면 키보드 전체가 멈춘다.

## 릴리즈 hex 로 굽기

[최신 릴리즈](https://github.com/codeyoma/custom_macro_for_qmk/releases/latest)에서 PCB 쪽에 맞는 파일을 받아 QMK Toolbox 등으로 굽는다.

| PCB | 파일 |
|---|---|
| 오른쪽 | `helix_pico_eunsun_right.hex` |
| 왼쪽 | `helix_pico_eunsun_left.hex` |

부트로더는 TRRS 잭 옆 리셋 버튼을 빠르게 두 번 눌러 들어간다(Pro Micro, Caterina).
PC 프로그램 관리창 위쪽에서도 같은 쪽(왼쪽/오른쪽)을 고른다.

## 직접 빌드

```bash
qmk new-keymap -kb helix/pico -km eunsun
```

생성된 `keymap.json` 은 꼭 지우고 이 폴더의 파일을 넣는다. 남아 있으면 `keymap.c` 가 무시된다.

```bash
KM=~/qmk_firmware/keyboards/helix/pico/keymaps/eunsun && rm -f "$KM/keymap.json" && cp keymap.c config.h rules.mk macro_protocol.h "$KM/"
```

```bash
qmk flash -kb helix/pico -km eunsun -e SIDE=left
```

`SIDE` 는 `left` 또는 `right`, 생략하면 `right` 다. 빌드 타겟은 `helix/pico` 하나뿐이다(옛 문서의 `helix/pico/sc` 등은 QMK 에서 없어졌다).

## 키 배치

정면에서 볼 때 왼쪽 위부터 번호를 매긴다. 튀어나온 `[레이어]` 키를 누르는 동안 치트시트가 뜬다.

```
오른쪽 PCB                             왼쪽 PCB
         1   2   3   4   5   6          1   2   3   4   5   6
         7   8   9  10  11  12          7   8   9  10  11  12
        13  14  15  16  17  18         13  14  15  16  17  18
[레이어] 19  20  21  22  23  24         19  20  21  22  23  24 [레이어]
```

쪽을 잘못 구우면 키는 모두 눌리지만 좌우가 거울처럼 뒤집힌다. 맞는 쪽으로 다시 구우면 된다.

## 설정 요약

| 설정 | 이유 |
|---|---|
| `SIDE` → `MASTER_RIGHT` | 꽂아 쓰는 쪽을 정한다. Helix 에는 좌우를 알려 주는 핀이 없다 |
| `RAW_ENABLE = yes` | PC 프로그램과 주고받는 통로(Raw HID) |
| `SPLIT_MAX_CONNECTION_ERRORS 1` | 반대쪽이 없으니 바로 끊긴 것으로 본다 |
| `MOUSEKEY`·`EXTRAKEY`·`CONSOLE` = no | 32U4 의 USB 엔드포인트를 Raw HID 에 내준다 |
| `OLED`·`RGB_MATRIX` = no | 쓰지 않는다. LED 는 아랫줄이 납땜 불량에 약해 깜빡이기도 한다 |

`SPLIT_KEYBOARD = no` 로 split 을 끄지 않는다. 행 수가 어긋나 엉뚱한 핀을 스캔하고 유령 입력이 생긴다.

하드웨어: ATmega32U4(Pro Micro, Caterina) · VID `0x3265` / PID `0x0001` · 반쪽당 4행 × 7열.
