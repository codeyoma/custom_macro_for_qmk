// Helix Pico 한쪽(오른쪽 PCB) 단독 매크로패드 키맵.
//
// 물리 키 25개 중 24개를 문장 슬롯에, 맨 아랫줄 마지막 1개를 치트시트 레이어 키에 쓴다.
// 키보드는 슬롯 인덱스만 보내고 문장 내용은 전혀 모른다.
// 문장을 바꿔도 펌웨어를 다시 구울 필요가 없다.
//
// 설치: 이 폴더의 파일들을 아래 위치로 복사한다.
//   qmk_firmware/keyboards/helix/pico/keymaps/eunsun/
// 그리고 qmk new-keymap 이 만들어 둔 keymap.json 은 반드시 지운다.
// keymap.json 이 남아 있으면 빌드가 JSON 경로를 타서 이 파일이 무시된다.
//
// 빌드:  qmk compile -kb helix/pico -km eunsun
// 플래시: qmk flash   -kb helix/pico -km eunsun

#include QMK_KEYBOARD_H
#include "raw_hid.h"
#include "macro_protocol.h"

enum layers {
    LAYER_BASE = 0,
    LAYER_CHEAT = 1,
};

enum custom_keycodes {
    MACRO_01 = SAFE_RANGE,
    MACRO_02, MACRO_03, MACRO_04, MACRO_05, MACRO_06,
    MACRO_07, MACRO_08, MACRO_09, MACRO_10, MACRO_11, MACRO_12,
    MACRO_13, MACRO_14, MACRO_15, MACRO_16, MACRO_17, MACRO_18,
    MACRO_19, MACRO_20, MACRO_21, MACRO_22, MACRO_23, MACRO_24,
};

// LAYOUT 인자는 50개다. 행별로 왼쪽 6 + 오른쪽 6, 마지막 행만 왼쪽 7 + 오른쪽 7.
// MASTER_RIGHT 를 정의했으므로 USB에 꽂힌 이 PCB가 오른쪽(matrix row 4~7)으로 잡힌다.
// 따라서 실제 키코드는 전부 각 행의 뒤쪽 자리에 놓고, 왼쪽 자리는 KC_NO로 둔다.
const uint16_t PROGMEM keymaps[][MATRIX_ROWS][MATRIX_COLS] = {

    // 평소 상태. 24개 키가 그대로 문장 1~24번이다.
    [LAYER_BASE] = LAYOUT(
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,     MACRO_01, MACRO_02, MACRO_03, MACRO_04, MACRO_05, MACRO_06,
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,     MACRO_07, MACRO_08, MACRO_09, MACRO_10, MACRO_11, MACRO_12,
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,     MACRO_13, MACRO_14, MACRO_15, MACRO_16, MACRO_17, MACRO_18,
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,
                                                      MACRO_19, MACRO_20, MACRO_21, MACRO_22, MACRO_23, MACRO_24, MO(LAYER_CHEAT)
    ),

    // 레이어 키를 누르고 있는 동안. 치트시트가 화면에 떠 있다.
    // 키 배치는 같다. 보면서 그대로 누르면 된다.
    [LAYER_CHEAT] = LAYOUT(
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,     MACRO_01, MACRO_02, MACRO_03, MACRO_04, MACRO_05, MACRO_06,
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,     MACRO_07, MACRO_08, MACRO_09, MACRO_10, MACRO_11, MACRO_12,
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,     MACRO_13, MACRO_14, MACRO_15, MACRO_16, MACRO_17, MACRO_18,
        KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO, KC_NO,
                                                      MACRO_19, MACRO_20, MACRO_21, MACRO_22, MACRO_23, MACRO_24, KC_TRNS
    ),
};

#ifdef RAW_ENABLE

// 주의: raw_hid_send() 는 엔드포인트가 빌 때까지 기다린다.
// 호스트 프로그램이 떠 있지 않으면 호출당 최대 10.2ms 블로킹된다
// (tmk_core/protocol/lufa/lufa.c 의 send_report() 가 timeout 255 를 카운트다운한다).
// 키를 누를 때 그만큼 지연되지만 키보드가 멈추지는 않는다.
static void send_macro_packet(uint8_t cmd, uint8_t arg) {
    uint8_t msg[MACRO_PACKET_SIZE] = {0};
    msg[0] = MACRO_MAGIC;
    msg[1] = cmd;
    msg[2] = arg;
    raw_hid_send(msg, MACRO_PACKET_SIZE);
}

// PC가 보낸 핑에 응답한다. 프로그램이 키보드 연결을 확인하는 경로다.
void raw_hid_receive(uint8_t *data, uint8_t length) {
    if (length < 3 || data[0] != MACRO_MAGIC) {
        return;
    }
    if (data[1] == MACRO_CMD_PING) {
        send_macro_packet(MACRO_CMD_PONG, 0);
    }
}

#else

// RAW_ENABLE 없이 빌드되면 키보드는 아무 신호도 보내지 않는다.
// 조용히 동작하지 않는 것보다 빌드 단계에서 알아채는 편이 낫다.
#    error "RAW_ENABLE = yes 가 필요합니다. keymaps/eunsun/rules.mk 를 확인하세요."

#endif

bool process_record_user(uint16_t keycode, keyrecord_t *record) {
    if (keycode >= MACRO_01 && keycode <= MACRO_24) {
        if (record->event.pressed) {
            send_macro_packet(MACRO_CMD_PASTE, (uint8_t)(keycode - MACRO_01));
        }
        // 눌렀을 때만 보낸다. 뗄 때는 아무것도 하지 않는다.
        return false;
    }
    return true;
}

layer_state_t layer_state_set_user(layer_state_t state) {
    // 레이어가 실제로 바뀔 때만 보낸다.
    // layer_state_set_user 는 관계없는 레이어 변화에도 불리므로
    // 그대로 두면 같은 신호가 계속 나간다.
    static bool overlay_visible = false;

    bool should_show = layer_state_cmp(state, LAYER_CHEAT);

    if (should_show != overlay_visible) {
        overlay_visible = should_show;
        send_macro_packet(
            should_show ? MACRO_CMD_OVERLAY_SHOW : MACRO_CMD_OVERLAY_HIDE,
            LAYER_CHEAT);
    }

    return state;
}
