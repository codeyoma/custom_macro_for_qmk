#pragma once

// 물리적 오른쪽 PCB를 단독으로 USB에 연결해 쓴다는 뜻이다.
//
// 다만 keymap.c 가 왼쪽 자리와 오른쪽 자리에 같은 키코드를 깔아 두므로
// 이 설정이 없어도, 반대로 왼쪽 PCB를 꽂아도 똑같이 동작한다.
// 예전에는 이 한 줄에 24키 전체가 걸려 있었다. 이제는 걸려 있지 않다.
#define MASTER_RIGHT

// 반대쪽 반쪽은 아예 연결하지 않는다.
// 기본값(10회 실패 후 연결 끊김 판정)은 부팅 직후 잠깐 헛스캔을 돌게 하므로
// 첫 실패에 바로 끊긴 것으로 보고, 재확인 간격도 늘려 스캔 낭비를 줄인다.
#define SPLIT_MAX_CONNECTION_ERRORS 1
#define SPLIT_CONNECTION_CHECK_TIMEOUT 3000

// --- LED ---
//
// 켜 두면 색이 계속 흐르는 모드로 시작한다.
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
