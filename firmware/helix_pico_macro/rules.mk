# Helix 트리에는 rules.mk 가 하나도 없다. 모든 설정이 keyboard.json 으로 되어 있어서
# 이 파일이 키맵 레벨에서 그 위에 덮어쓰는 유일한 수단이다.
# (builddefs/build_keyboard.mk 가 keyboard.json 기반 설정을 먼저 만들고 키맵 rules.mk 를 나중에 include 한다)

# 이 프로그램의 핵심. 없으면 키보드가 PC로 아무 신호도 보내지 못한다.
RAW_ENABLE = yes

# 꽂아 쓰는 PCB 가 어느 쪽 반쪽인가. 기본은 처음부터 써 온 오른쪽이다.
#   qmk compile -kb helix/pico -km eunsun -e SIDE=left
# 펌웨어 스스로는 알 길이 없으니 잘못 고르면 좌우가 거울처럼 뒤집힌다. config.h 참고.
SIDE ?= right
ifeq ($(strip $(SIDE)),left)
    OPT_DEFS += -DMACRO_SIDE_LEFT
else ifneq ($(strip $(SIDE)),right)
    $(error SIDE 는 left 또는 right 여야 합니다. 받은 값: $(SIDE))
endif

# VIA 와는 공존할 수 없다.
# quantum/via.c 의 raw_hid_receive() 는 __attribute__((weak)) 가 아니라서
# keymap.c 의 같은 이름 함수와 링크 단계에서 충돌한다(multiple definition).
VIA_ENABLE = no

# ATmega32U4 는 USB 엔드포인트와 플래시 용량이 모두 빠듯하다.
# Raw HID 를 넣을 자리를 만들기 위해 쓰지 않는 기능을 끈다.
# 이 키보드는 문장 삽입 전용 매크로패드라 아래 기능이 하나도 필요 없다.
MOUSEKEY_ENABLE = no
EXTRAKEY_ENABLE = no
CONSOLE_ENABLE  = no

# LED 는 쓰지 않는다. 펌웨어가 LED 로 데이터를 한 번도 보내지 않는다.
#
# LED 는 체인으로 이어져 앞 LED 가 넘겨준 데이터를 받는다. 체인 중간 납땜이 불량하면
# 그 뒤쪽이 깨진 데이터를 계속 받아 미친 듯이 깜빡인다. 아랫줄이 체인의 맨 끝이라 늘 거기서 터진다.
# 문장 삽입 전용 매크로패드에 애니메이션이 꼭 필요하지 않고, 32U4 플래시도 그만큼 아낀다.
#
# 되살리려면 yes 로 바꾼다. 애니메이션 기본값은 config.h 에 남겨 두었다.
RGB_MATRIX_ENABLE = no

# OLED 는 끈 채로 둔다. 용량 문제만이 아니다.
# keyboards/helix/helix.c 의 oled_task_kb() 가 RGB_MATRIX 가드 없이
# rgb_matrix_get_*() 와 RGB_MATRIX_*_STEP 을 직접 부르기 때문에,
# 둘의 조합에 따라 링크가 깨질 수 있다. 한쪽만 쓰는 이 구성에서는 OLED 가 필요 없다.
OLED_ENABLE = no

# LTO 는 keyboards/helix/info.json 의 build.lto 로 이미 켜져 있다.
