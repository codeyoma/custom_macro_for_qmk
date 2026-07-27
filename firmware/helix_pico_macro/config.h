#pragma once

// 물리적 오른쪽 PCB를 단독으로 USB에 연결해 쓴다.
//
// 이게 없으면 QMK는 USB가 꽂힌 쪽을 무조건 왼쪽으로 취급한다
// (quantum/split_common/split_util.c 의 is_keyboard_left_impl() 최종 #else 분기).
// 그러면 keymap.c 에서 오른쪽 자리에 넣은 키코드에 영원히 도달하지 못한다.
#define MASTER_RIGHT

// 반대쪽 반쪽은 아예 연결하지 않는다.
// 기본값(10회 실패 후 연결 끊김 판정)은 부팅 직후 잠깐 헛스캔을 돌게 하므로
// 첫 실패에 바로 끊긴 것으로 보고, 재확인 간격도 늘려 스캔 낭비를 줄인다.
#define SPLIT_MAX_CONNECTION_ERRORS 1
#define SPLIT_CONNECTION_CHECK_TIMEOUT 3000

// 주의: rules.mk 에 SPLIT_KEYBOARD = no 를 넣지 말 것.
// MATRIX_ROWS 는 keyboard.json 에서 8로 산출된 채 남는데
// split 이 꺼지면 MATRIX_ROWS_PER_HAND 가 4가 아니라 8이 되어
// row_pins 배열 뒤쪽 4칸이 0인 채로 엉뚱한 핀을 스캔한다. 유령 입력이 생긴다.
