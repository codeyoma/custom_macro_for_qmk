#pragma once

// 어느 쪽 PCB 를 단독으로 USB 에 꽂아 쓰는지. 빌드할 때 rules.mk 의 SIDE 로 고른다.
//
// QMK 는 USB 가 꽂힌 쪽을 기본으로 왼쪽으로 본다. MASTER_RIGHT 가 그걸 오른쪽으로 뒤집는다.
// Helix 에는 좌우를 알려 주는 핀이 없어서 펌웨어가 스스로 알 길이 없다.
//
// 반대쪽 PCB 에 구우면 키는 다 눌리지만 좌우가 거울처럼 뒤집힌다.
// 두 반쪽이 같은 기판을 뒤집어 쓴 것이라, 바깥쪽 끝 열이 늘 같은 매트릭스 열이기 때문이다.
#ifndef MACRO_SIDE_LEFT
#    define MASTER_RIGHT
#endif

// 반대쪽 반쪽은 아예 연결하지 않는다.
// 기본값(10회 실패 후 연결 끊김 판정)은 부팅 직후 잠깐 헛스캔을 돌게 하므로
// 첫 실패에 바로 끊긴 것으로 보고, 재확인 간격도 늘려 스캔 낭비를 줄인다.
#define SPLIT_MAX_CONNECTION_ERRORS 1
#define SPLIT_CONNECTION_CHECK_TIMEOUT 3000

// --- LED ---
//
// 지금은 rules.mk 에서 RGB_MATRIX_ENABLE = no 로 꺼 두어 아래 값들이 쓰이지 않는다.
// 다시 켜면(yes) 색이 계속 흐르는 모드로 시작한다.
// 애니메이션 목록은 keyboards/helix/info.json 에 이미 정의되어 있다
// (breathing, cycle_left_right, cycle_pinwheel, multisplash, solid_splash).
//
// 이 값들은 첫 부팅 때만 쓰인다. 그 뒤로는 EEPROM 에 저장된 설정을 따르므로
// 키보드에서 모드를 바꾸면 그게 유지된다.
#define RGB_MATRIX_DEFAULT_MODE RGB_MATRIX_CYCLE_LEFT_RIGHT
#define RGB_MATRIX_DEFAULT_SPD 60

// 한쪽만 써도 LED 25개가 동시에 켜진다. USB 500mA 안에서 돌도록 밝기를 낮춰 잡는다.
// (keyboard.json 의 max_brightness 150 이 상한이고 이건 시작값이다)
#define RGB_MATRIX_DEFAULT_VAL 100

// 주의: rules.mk 에 SPLIT_KEYBOARD = no 를 넣지 말 것.
// MATRIX_ROWS 는 keyboard.json 에서 8로 산출된 채 남는데
// split 이 꺼지면 MATRIX_ROWS_PER_HAND 가 4가 아니라 8이 되어
// row_pins 배열 뒤쪽 4칸이 0인 채로 엉뚱한 핀을 스캔한다. 유령 입력이 생긴다.
