// MacroTyper Raw HID 프로토콜.
//
// 이 파일은 펌웨어와 PC 프로그램이 공유하는 유일한 계약이다.
// 값을 바꾸면 src/MacroTyper.Core/MacroProtocol.cs 도 함께 바꿔야 한다.
//
// 패킷은 항상 RAW_EPSIZE(32) 바이트이고, 쓰지 않는 바이트는 0으로 채운다.
//
//   [0]      매직 0xAB. 다른 Raw HID 트래픽(VIA 등)과 구분한다.
//   [1]      명령 코드
//   [2]      인자 (슬롯 인덱스 또는 레이어 번호)
//   [3..31]  패딩

#pragma once

#define MACRO_MAGIC 0xAB

// 키보드 -> PC
#define MACRO_CMD_PASTE 0x01        // [2] = 슬롯 인덱스 0..23
#define MACRO_CMD_OVERLAY_SHOW 0x02 // [2] = 레이어 번호
#define MACRO_CMD_OVERLAY_HIDE 0x03
#define MACRO_CMD_PONG 0x11

// PC -> 키보드
#define MACRO_CMD_PING 0x10

#define MACRO_SLOT_COUNT 24
