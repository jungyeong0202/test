#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""도트 그림 이미지를 이 프로그램의 스프라이트 데이터로 변환한다.

png/jpg 로 된 픽셀아트를 넣으면
  1. 몇 배로 확대된 그림인지 알아내 원래 도트 크기로 되돌리고
  2. 색을 몇 가지로 정리해 팔레트를 만들고
  3. 바깥쪽 배경을 투명으로 처리한 뒤
  4. 다리를 번갈아 드는 걷기 4프레임을 만들어
sprites.py 안의 해당 포켓몬 정의를 통째로 갈아 끼운다.

    python tools/import_sprite.py 그림.png --key pikachu --name 피카츄

Pillow 가 필요하다:  pip install Pillow
"""

import argparse
import os
import random
import re
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPRITES_PY = os.path.join(ROOT, "sprites.py")
# 팔레트 문자: 자주 쓰는 색부터 순서대로 붙는다.
PALETTE_CHARS = "KYOWRBGTDECSLMNPQ"


# --- 1단계: 이미지에서 도트 격자 뽑아내기 --------------------------------
def content_box(image, is_background):
    width, height = image.size
    pixels = image.load()
    columns = [
        x for x in range(width)
        if any(not is_background(pixels[x, y]) for y in range(height))
    ]
    rows = [
        y for y in range(height)
        if any(not is_background(pixels[x, y]) for x in range(width))
    ]
    if not columns or not rows:
        raise SystemExit("이미지가 비어 있습니다.")
    return columns[0], rows[0], columns[-1], rows[-1]


def cell_noise(image, box, columns, rows):
    """격자를 columns x rows 로 나눴을 때 각 칸 안의 색이 얼마나 고른지.

    진짜 도트 경계와 맞아떨어지면 한 칸은 거의 단색이라 값이 0 에 가깝다.
    """
    pixels = image.load()
    width, height = image.size
    x0, y0, x1, y1 = box
    step_x = (x1 - x0 + 1) / columns
    step_y = (y1 - y0 + 1) / rows
    if step_x < 2 or step_y < 2:
        return None

    total = 0.0
    counted = 0
    for gy in range(rows):
        for gx in range(columns):
            left = x0 + gx * step_x
            top = y0 + gy * step_y
            low = [255, 255, 255]
            high = [0, 0, 0]
            seen = 0
            for y in range(int(top + step_y * 0.25), int(top + step_y * 0.75) + 1):
                for x in range(int(left + step_x * 0.25), int(left + step_x * 0.75) + 1):
                    if not (0 <= x < width and 0 <= y < height):
                        continue
                    color = pixels[x, y]
                    seen += 1
                    for channel in range(3):
                        low[channel] = min(low[channel], color[channel])
                        high[channel] = max(high[channel], color[channel])
            if seen > 1:
                total += max(high[c] - low[c] for c in range(3))
                counted += 1
    return total / counted if counted else None


def guess_cells(image, box, horizontal, limit=80):
    """도트 개수를 추정한다.

    칸 안이 충분히 고르게 되는 가장 작은 칸 수를 고른다. 배수(2배, 3배)도
    똑같이 고르지만, 원래 도트 크기를 원하므로 가장 작은 값을 쓴다.
    """
    scores = {}
    for cells in range(8, limit + 1):
        noise = (
            cell_noise(image, box, cells, cells)
            if horizontal is None
            else (
                cell_noise(image, box, cells, horizontal)
                if horizontal
                else None
            )
        )
        if noise is not None:
            scores[cells] = noise
    if not scores:
        raise SystemExit("도트 격자를 알아내지 못했습니다. --grid 로 직접 지정하세요.")
    best = min(scores.values())
    threshold = max(best * 1.6, best + 4.0)
    return min(cells for cells, noise in scores.items() if noise <= threshold)


def sample_grid(image, box, columns, rows):
    """각 칸의 가운데만 평균 내어 색을 정한다(압축 잡음 회피)."""
    pixels = image.load()
    width, height = image.size
    x0, y0, x1, y1 = box
    step_x = (x1 - x0 + 1) / columns
    step_y = (y1 - y0 + 1) / rows

    grid = []
    for gy in range(rows):
        line = []
        for gx in range(columns):
            left = x0 + gx * step_x
            top = y0 + gy * step_y
            reds = greens = blues = count = 0
            for y in range(int(top + step_y * 0.3), int(top + step_y * 0.7) + 1):
                for x in range(int(left + step_x * 0.3), int(left + step_x * 0.7) + 1):
                    if 0 <= x < width and 0 <= y < height:
                        red, green, blue = pixels[x, y][:3]
                        reds += red
                        greens += green
                        blues += blue
                        count += 1
            if not count:
                line.append((255, 255, 255))
            else:
                line.append((reds // count, greens // count, blues // count))
        grid.append(line)
    return grid


# --- 2단계: 배경 지우기 ---------------------------------------------------
def clear_background(grid, is_background):
    """가장자리에서 이어진 배경색만 투명 처리한다.

    눈동자 속 흰색처럼 그림 안쪽에 갇힌 밝은 색은 그대로 둔다.
    """
    rows = len(grid)
    columns = len(grid[0])
    outside = [[False] * columns for _ in range(rows)]
    stack = []
    for x in range(columns):
        stack.append((x, 0))
        stack.append((x, rows - 1))
    for y in range(rows):
        stack.append((0, y))
        stack.append((columns - 1, y))

    while stack:
        x, y = stack.pop()
        if not (0 <= x < columns and 0 <= y < rows) or outside[y][x]:
            continue
        if not is_background(grid[y][x]):
            continue
        outside[y][x] = True
        stack.extend([(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)])
    return outside


# --- 3단계: 색 정리 -------------------------------------------------------
def quantize(colors, count, rounds=8, seed=7):
    """k-평균으로 색을 count 가지로 줄인다."""
    random.seed(seed)
    unique = list({color for color in colors})
    if len(unique) <= count:
        return unique

    # 자주 쓰인 색을 씨앗으로 삼되 서로 충분히 떨어진 것만 고른다.
    frequency = {}
    for color in colors:
        frequency[color] = frequency.get(color, 0) + 1
    seeds = []
    for color, _ in sorted(frequency.items(), key=lambda item: -item[1]):
        if all(distance(color, chosen) > 900 for chosen in seeds):
            seeds.append(color)
        if len(seeds) == count:
            break
    while len(seeds) < count:
        seeds.append(random.choice(unique))

    for _ in range(rounds):
        buckets = [[] for _ in seeds]
        for color in colors:
            buckets[nearest(color, seeds)].append(color)
        for index, bucket in enumerate(buckets):
            if bucket:
                seeds[index] = (
                    sum(c[0] for c in bucket) // len(bucket),
                    sum(c[1] for c in bucket) // len(bucket),
                    sum(c[2] for c in bucket) // len(bucket),
                )
    return seeds


def distance(a, b):
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


def nearest(color, palette):
    best = 0
    best_distance = None
    for index, candidate in enumerate(palette):
        value = distance(color, candidate)
        if best_distance is None or value < best_distance:
            best = index
            best_distance = value
    return best


# --- 4단계: 걷기 프레임 만들기 --------------------------------------------
def feet_runs(cells, band):
    """맨 아랫부분에서 좌우 발을 찾아 (시작열, 끝열) 로 돌려준다."""
    rows = len(cells)
    columns = len(cells[0])
    used = [
        any(cells[y][x] is not None for y in range(rows - band, rows))
        for x in range(columns)
    ]
    runs = []
    x = 0
    while x < columns:
        if used[x]:
            start = x
            while x < columns and used[x]:
                x += 1
            runs.append((start, x - 1))
        else:
            x += 1
    return runs


def lift_foot(cells, run, band, rise=1):
    """발 하나를 rise 칸 들어 올린 새 그리드를 만든다."""
    rows = len(cells)
    columns = len(cells[0])
    out = [list(row) for row in cells]
    start, end = run
    for x in range(start, min(end + 1, columns)):
        for y in range(rows - band, rows):
            out[y][x] = None
    for x in range(start, min(end + 1, columns)):
        for y in range(rows - band, rows):
            if cells[y][x] is not None and y - rise >= 0:
                out[y - rise][x] = cells[y][x]
    return out


def walk_frames(cells, band, rise):
    """가만히 → 왼발 들기 → 가만히 → 오른발 들기 의 4프레임."""
    runs = feet_runs(cells, band)
    if len(runs) < 2:
        return [cells, cells]
    left = runs[0]
    right = runs[-1]
    return [
        cells,
        lift_foot(cells, left, band, rise),
        cells,
        lift_foot(cells, right, band, rise),
    ]


# --- 5단계: sprites.py 에 써넣기 -----------------------------------------
def to_rows(cells, palette_map):
    rows = []
    for line in cells:
        text = "".join("." if color is None else palette_map[color] for color in line)
        rows.append(text.rstrip(".") or ".")
    return rows


def build_block(key, name, palette, frames, scale_factor):
    constant = key.upper()
    out = ["%s = Pokemon(" % constant]
    out.append('    key="%s",' % key)
    out.append('    name_ko="%s",' % name)
    out.append("    scale_factor=%s," % scale_factor)
    out.append("    palette={")
    for char, color in palette:
        out.append('        "%s": "#%02x%02x%02x",' % (char, color[0], color[1], color[2]))
    out.append("    },")
    out.append("    frame_rows=[")
    for frame in frames:
        out.append("        [")
        for row in frame:
            out.append('            "%s",' % row)
        out.append("        ],")
    out.append("    ],")
    out.append(")")
    return "\n".join(out)


def splice(source, key, block):
    """마커 사이의 정의를 갈아 끼운다. 없으면 새로 추가한다."""
    begin = "# --- 자동 생성 시작: %s ---" % key
    end = "# --- 자동 생성 끝: %s ---" % key
    body = "%s\n%s\n%s" % (begin, block, end)

    if begin in source and end in source:
        head = source[: source.index(begin)]
        tail = source[source.index(end) + len(end):]
        return head + body + tail

    # 기존 손그림 정의가 있으면 그 자리를 대신한다.
    constant = key.upper()
    pattern = re.compile(
        r"^%s = Pokemon\(\n(?:.*\n)*?\)\n" % re.escape(constant), re.MULTILINE
    )
    if pattern.search(source):
        return pattern.sub(body + "\n", source, count=1)

    marker = "\nPOKEMON = "
    if marker not in source:
        raise SystemExit("sprites.py 에서 POKEMON 정의를 찾지 못했습니다.")
    index = source.index(marker)
    return source[:index] + "\n" + body + "\n" + source[index:]


def main():
    parser = argparse.ArgumentParser(description="도트 이미지를 스프라이트로 변환")
    parser.add_argument("image", help="변환할 png/jpg 파일")
    parser.add_argument("--key", required=True, help="포켓몬 키 (예: pikachu)")
    parser.add_argument("--name", required=True, help="한글 이름 (예: 피카츄)")
    parser.add_argument("--colors", type=int, default=8, help="쓸 색 개수 (기본 8)")
    parser.add_argument("--grid", type=int, default=0, help="가로 도트 수 (0=자동)")
    parser.add_argument("--rows", type=int, default=0, help="세로 도트 수 (0=자동)")
    parser.add_argument("--scale-factor", default="1 / 3",
                        help="--scale 에 곱할 배율 (기본 '1 / 3')")
    parser.add_argument("--foot-band", type=int, default=2, help="발로 볼 아래쪽 줄 수")
    parser.add_argument("--foot-rise", type=int, default=1, help="발을 들어 올릴 칸 수")
    parser.add_argument("--preview", default="", help="확인용 png 경로")
    parser.add_argument("--dry-run", action="store_true", help="파일을 고치지 않는다")
    args = parser.parse_args()

    image = Image.open(args.image).convert("RGB")

    def is_background(color):
        return color[0] > 235 and color[1] > 235 and color[2] > 235

    box = content_box(image, is_background)
    x0, y0, x1, y1 = box
    if args.grid and args.rows:
        columns, rows = args.grid, args.rows
    else:
        # 도트는 정사각형이므로 가로세로를 같은 칸 수로 놓고 한 번에 찾는다.
        square = guess_cells(image, box, None)
        span_x = x1 - x0 + 1
        span_y = y1 - y0 + 1
        cell = max(span_x, span_y) / square
        columns = args.grid or max(1, int(round(span_x / cell)))
        rows = args.rows or max(1, int(round(span_y / cell)))
    print("도트 격자: %d x %d 칸" % (columns, rows))

    grid = sample_grid(image, box, columns, rows)
    outside = clear_background(grid, is_background)

    inside_colors = [
        grid[y][x] for y in range(rows) for x in range(columns) if not outside[y][x]
    ]
    palette_colors = quantize(inside_colors, args.colors)

    cells = []
    for y in range(rows):
        line = []
        for x in range(columns):
            if outside[y][x]:
                line.append(None)
            else:
                line.append(palette_colors[nearest(grid[y][x], palette_colors)])
        cells.append(line)

    # 남은 여백 잘라내기
    used_rows = [y for y in range(rows) if any(cell is not None for cell in cells[y])]
    used_columns = [
        x for x in range(columns) if any(cells[y][x] is not None for y in range(rows))
    ]
    cells = [
        [cells[y][x] for x in range(used_columns[0], used_columns[-1] + 1)]
        for y in range(used_rows[0], used_rows[-1] + 1)
    ]
    print("잘라낸 크기: %d x %d" % (len(cells[0]), len(cells)))

    # 자주 쓰인 색부터 팔레트 문자 배정
    counts = {}
    for line in cells:
        for color in line:
            if color is not None:
                counts[color] = counts.get(color, 0) + 1
    ordered = sorted(counts.items(), key=lambda item: -item[1])
    palette = [(PALETTE_CHARS[i], color) for i, (color, _) in enumerate(ordered)]
    palette_map = {color: char for char, color in palette}

    frames = walk_frames(cells, args.foot_band, args.foot_rise)
    print("걷기 프레임: %d 장" % len(frames))

    if args.preview:
        save_preview(frames, args.preview)
        print("미리보기: %s" % args.preview)

    block = build_block(
        args.key, args.name, palette,
        [to_rows(frame, palette_map) for frame in frames],
        args.scale_factor,
    )
    if args.dry_run:
        print(block)
        return

    with open(SPRITES_PY, encoding="utf-8") as handle:
        source = handle.read()
    with open(SPRITES_PY, "w", encoding="utf-8") as handle:
        handle.write(splice(source, args.key, block))
    print("sprites.py 를 갱신했습니다: %s" % args.key)


def save_preview(frames, path, scale=6, pad=6):
    height = len(frames[0])
    width = len(frames[0][0])
    canvas = Image.new(
        "RGB",
        ((width * len(frames) + pad * (len(frames) + 1)) * scale, (height + 2 * pad) * scale),
        (43, 110, 168),
    )
    pixels = canvas.load()
    for index, frame in enumerate(frames):
        offset = (pad + index * (width + pad)) * scale
        for y, line in enumerate(frame):
            for x, color in enumerate(line):
                if color is None:
                    continue
                for dy in range(scale):
                    for dx in range(scale):
                        pixels[offset + x * scale + dx, (pad + y) * scale + dy] = color
    canvas.save(path)


if __name__ == "__main__":
    main()
