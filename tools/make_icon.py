#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""도트 스프라이트로 윈도우 실행 파일용 아이콘(.ico)을 만든다.

외부 라이브러리 없이 ICO 포맷을 직접 기록한다.

    python tools/make_icon.py [포켓몬이름]
"""

import os
import struct
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

from sprites import POKEMON  # noqa: E402


def render_at(grid, size):
    """스프라이트를 size x size 캔버스에 꽉 차게(비율 유지) 그린다.

    확대/축소 모두 최근접 이웃 방식이라 도트 느낌이 그대로 남는다.
    (x, y) -> (r, g, b) 딕셔너리를 돌려주며, 없는 좌표는 투명이다.
    """
    rows = len(grid)
    columns = len(grid[0])
    scale = min(float(size) / columns, float(size) / rows)
    width = max(1, int(columns * scale))
    height = max(1, int(rows * scale))
    left = (size - width) // 2
    top = (size - height) // 2

    pixels = {}
    for y in range(height):
        source_y = min(rows - 1, int(y / scale))
        for x in range(width):
            source_x = min(columns - 1, int(x / scale))
            color = grid[source_y][source_x]
            if color is None:
                continue
            pixels[(left + x, top + y)] = (
                int(color[1:3], 16),
                int(color[3:5], 16),
                int(color[5:7], 16),
            )
    return pixels


def image_bytes(pixels, size):
    """BITMAPINFOHEADER + 32bpp BGRA + AND 마스크를 만든다."""
    xor = bytearray()
    for y in range(size - 1, -1, -1):  # ICO 의 비트맵은 아래에서 위로
        for x in range(size):
            color = pixels.get((x, y))
            if color is None:
                xor += b"\x00\x00\x00\x00"
            else:
                xor += bytes((color[2], color[1], color[0], 255))

    mask_row = (size + 31) // 32 * 4  # 1bpp, 4바이트 정렬
    and_mask = bytearray()
    for y in range(size - 1, -1, -1):
        row = bytearray(mask_row)
        for x in range(size):
            if (x, y) not in pixels:
                row[x // 8] |= 0x80 >> (x % 8)
        and_mask += row

    header = struct.pack(
        "<IiiHHIIiiII",
        40,            # biSize
        size,          # biWidth
        size * 2,      # biHeight (XOR + AND)
        1,             # biPlanes
        32,            # biBitCount
        0,             # biCompression = BI_RGB
        len(xor) + len(and_mask),
        0, 0, 0, 0,
    )
    return bytes(header) + bytes(xor) + bytes(and_mask)


def build_ico(grid, sizes):
    images = []
    for size in sizes:
        images.append(image_bytes(render_at(grid, size), size))

    out = bytearray(struct.pack("<HHH", 0, 1, len(images)))
    offset = 6 + 16 * len(images)
    for size, data in zip(sizes, images):
        out += struct.pack(
            "<BBBBHHII",
            size if size < 256 else 0,
            size if size < 256 else 0,
            0, 0, 1, 32,
            len(data),
            offset,
        )
        offset += len(data)
    for data in images:
        out += data
    return bytes(out)


def main():
    key = sys.argv[1] if len(sys.argv) > 1 else "pikachu"
    if key not in POKEMON:
        raise SystemExit("모르는 포켓몬입니다: %s" % key)

    grid = POKEMON[key].frames()[0]
    ico = build_ico(grid, [16, 24, 32, 48, 64, 128])

    target = os.path.normpath(
        os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "csharp", "pokemon.ico")
    )
    with open(target, "wb") as handle:
        handle.write(ico)
    print("생성 완료: %s (%d bytes)" % (target, len(ico)))


if __name__ == "__main__":
    main()
