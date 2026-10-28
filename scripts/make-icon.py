#!/usr/bin/env python3
"""
exe 파일 아이콘(.ico)을 만든다. 앱 안의 아이콘(src/MacroTyper/Ui/Theme.xaml 의 AppIcon)과 같은 도안이다.

    python3 scripts/make-icon.py

Pillow 가 필요하다: pip install Pillow

매크로패드 한쪽을 위에서 본 모양이다. 파란 판 위에 7열 4행으로 키를 놓되,
맨 왼쪽 열은 아랫줄에만 키가 있고 그 튀어나온 키가 초록색 치트시트 키다.

작은 크기에서 뭉개지지 않게 큰 판에 그린 뒤 줄인다.
"""

from pathlib import Path

from PIL import Image, ImageDraw

BOARD = 160  # 도안 좌표의 한 변. Theme.xaml 의 AppIcon 과 같다
SIZE = 1024  # 이 크기로 그린 뒤 각 아이콘 크기로 줄인다

TOP = (0x4A, 0x9B, 0xFF)  # 위에서 아래로 짙어지는 파랑
BOTTOM = (0x00, 0x55, 0xD4)
KEY = (255, 255, 255, 230)
CHEAT_KEY = (0x5B, 0xE3, 0x7D, 255)

CORNER = 36
KEY_SIZE, KEY_GAP, KEY_CORNER = 15, 4, 3.5
COLUMNS, ROWS = 7, 4

OUT = Path(__file__).resolve().parent.parent / "src" / "MacroTyper" / "Assets" / "app.ico"
ICON_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def scaled(value: float) -> int:
    """도안 좌표를 실제 픽셀로 옮긴다."""
    return round(value * SIZE / BOARD)


def draw_icon() -> Image.Image:
    # 판: 세로 그라데이션을 둥근 사각형 모양으로 오려 낸다.
    gradient = Image.new("RGBA", (SIZE, SIZE))
    for y in range(SIZE):
        t = y / (SIZE - 1)
        color = tuple(round(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)) + (255,)
        gradient.paste(color, (0, y, SIZE, y + 1))

    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, SIZE - 1, SIZE - 1], radius=scaled(CORNER), fill=255)

    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    image.paste(gradient, (0, 0), mask)

    # 키. 반투명 흰색이 판 위에 제대로 섞이도록 따로 그려 합친다.
    keys = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(keys)

    left = (BOARD - (COLUMNS * KEY_SIZE + (COLUMNS - 1) * KEY_GAP)) / 2
    top = (BOARD - (ROWS * KEY_SIZE + (ROWS - 1) * KEY_GAP)) / 2

    for row in range(ROWS):
        for column in range(COLUMNS):
            if column == 0 and row < ROWS - 1:
                continue  # 맨 왼쪽 열은 아랫줄에만 키가 있다

            x = left + column * (KEY_SIZE + KEY_GAP)
            y = top + row * (KEY_SIZE + KEY_GAP)
            draw.rounded_rectangle(
                [scaled(x), scaled(y), scaled(x + KEY_SIZE), scaled(y + KEY_SIZE)],
                radius=scaled(KEY_CORNER),
                fill=CHEAT_KEY if column == 0 else KEY,
            )

    return Image.alpha_composite(image, keys)


def main() -> None:
    icon = draw_icon()
    OUT.parent.mkdir(parents=True, exist_ok=True)

    # Pillow 가 각 크기로 줄여 하나의 .ico 에 담는다.
    icon.save(OUT, format="ICO", sizes=ICON_SIZES)

    print(f"{OUT} ({OUT.stat().st_size:,} bytes)")
    print("크기:", ", ".join(f"{w}x{h}" for w, h in ICON_SIZES))


if __name__ == "__main__":
    main()
