# Helix 트리에는 rules.mk 가 하나도 없다. 모든 설정이 keyboard.json 으로 되어 있어서
# 이 파일이 키맵 레벨에서 그 위에 덮어쓰는 유일한 수단이다.
# (builddefs/build_keyboard.mk 가 keyboard.json 기반 설정을 먼저 만들고 키맵 rules.mk 를 나중에 include 한다)

# 이 프로그램의 핵심. 없으면 키보드가 PC로 아무 신호도 보내지 못한다.
RAW_ENABLE = yes

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

# 한쪽만 쓰므로 OLED 와 RGB 를 켤 이유가 없다. 용량도 아낀다.
OLED_ENABLE       = no
RGB_MATRIX_ENABLE = no

# LTO 는 keyboards/helix/info.json 의 build.lto 로 이미 켜져 있다.
