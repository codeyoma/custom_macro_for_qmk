#!/usr/bin/env python3
"""
exe 파일 아이콘(.ico)을 만든다. 트레이 아이콘과 같은 키보드 도안이다.

    python3 scripts/make-icon.py

Pillow 가 필요하다: pip install Pillow

작은 크기에서 뭉개지지 않게 큰 판에 그린 뒤 줄인다.
탐색기에서 16x16 으로 보일 때도 키보드로 알아볼 수 있어야 한다.
"""

from pathlib import Path

from PIL import Image, ImageDraw

SIZE = 1024  # 이 크기로 그린 뒤 각 아이콘 크기로 줄인다
BODY = (29, 158, 117, 255)  # 트레이 아이콘과 같은 초록
KEY = (255, 255, 255, 255)

OUT = Path(__file__).resolve().parent.parent / "src" / "MacroTyper" / "Assets" / "app.ico"
ICON_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def scaled(value: float) -> int:
    """16 단위 도안 좌표를 실제 픽셀로 옮긴다."""
    return round(value * SIZE / 16)


def draw_icon() -> Image.Image:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    # 본체
    draw.rounded_rectangle(
        [scaled(0.6), scaled(1.6), scaled(15.4), scaled(14.4)],
        radius=scaled(2.2),
        fill=BODY,
    )

    # 키 3열 x 2행
    key_w, key_h, radius = 2.9, 2.4, 0.5
    for row, top in enumerate((4.2, 7.7)):
        for column, left in enumerate((2.4, 6.55, 10.7)):
            draw.rounded_rectangle(
                [scaled(left), scaled(top), scaled(left + key_w), scaled(top + key_h)],
                radius=scaled(radius),
                fill=KEY,
            )

    # 스페이스바
    draw.rounded_rectangle(
        [scaled(2.4), scaled(11.2), scaled(13.6), scaled(13.0)],
        radius=scaled(radius),
        fill=KEY,
    )

    return image


def main() -> None:
    icon = draw_icon()
    OUT.parent.mkdir(parents=True, exist_ok=True)

    # Pillow 가 각 크기로 줄여 하나의 .ico 에 담는다.
    icon.save(OUT, format="ICO", sizes=ICON_SIZES)

    print(f"{OUT} ({OUT.stat().st_size:,} bytes)")
    print("크기:", ", ".join(f"{w}x{h}" for w, h in ICON_SIZES))


if __name__ == "__main__":
    main()
