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


def color_runs(image, box, is_background):
    """같은 색이 몇 픽셀씩 이어지는지 모은다. 도트 한 칸의 크기를 재기 위한 것."""
    pixels = image.load()
    x0, y0, x1, y1 = box
    lengths = {}

    def add(run):
        if run >= 4:
            lengths[run] = lengths.get(run, 0) + 1

    def key(color):
        return (color[0] // 40, color[1] // 40, color[2] // 40)

    for y in range(y0, y1 + 1, 3):
        x = x0
        while x <= x1:
            current = key(pixels[x, y])
            start = x
            while x <= x1 and key(pixels[x, y]) == current:
                x += 1
            add(x - start)
    for x in range(x0, x1 + 1, 3):
        y = y0
        while y <= y1:
            current = key(pixels[x, y])
            start = y
            while y <= y1 and key(pixels[x, y]) == current:
                y += 1
            add(y - start)
    return lengths


def guess_cell_size(image, box, is_background):
    """도트 한 칸이 몇 픽셀인지 추정한다.

    픽셀아트는 같은 색이 '한 칸의 정수배'만큼 이어진다. 그래서 이어진 길이의
    분포에서 가장 잘 들어맞는 기본 단위를 찾으면 된다.
    """
    lengths = color_runs(image, box, is_background)
    if not lengths:
        raise SystemExit("도트 격자를 알아내지 못했습니다. --grid 로 지정하세요.")

    x0, y0, x1, y1 = box
    longest = max(x1 - x0 + 1, y1 - y0 + 1)

    best = None
    candidate = 4.0
    while candidate <= 64.0:
        score = 0.0
        for run, count in lengths.items():
            multiple = run / candidate
            nearest_multiple = round(multiple)
            if nearest_multiple < 1:
                continue
            error = abs(multiple - nearest_multiple)
            # 오차가 작을수록, 자주 나온 길이일수록 점수가 높다.
            score += count * max(0.0, 1.0 - error * 4.0)
        # 같은 점수라면 큰 칸(=원래 도트 크기)을 고른다.
        score *= candidate ** 0.5
        if best is None or score > best[0]:
            best = (score, candidate)
        candidate += 0.05

    cell = best[1]
    if longest / cell < 6:
        raise SystemExit("칸 크기 추정이 이상합니다(%.2f). --grid 로 지정하세요." % cell)
    return cell


def lattice_noise(image, x0, y0, cell, columns, rows):
    """(x0, y0) 에서 cell 간격으로 격자를 놓았을 때 칸 안 색의 흐트러짐."""
    pixels = image.load()
    width, height = image.size
    total = 0.0
    counted = 0
    for gy in range(rows):
        for gx in range(columns):
            left = x0 + gx * cell
            top = y0 + gy * cell
            low = [255, 255, 255]
            high = [0, 0, 0]
            seen = 0
            for y in range(int(top + cell * 0.2), int(top + cell * 0.8) + 1):
                for x in range(int(left + cell * 0.2), int(left + cell * 0.8) + 1):
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
    return total / counted if counted else 1e9


def align_lattice(image, box, cell):
    """격자의 시작 위치(위상)를 찾아 도트 경계에 딱 맞춘다.

    내용 상자의 왼쪽 위에서 그냥 시작하면 압축 잡음 때문에 조금씩 밀려서
    외곽선 색이 번진다. 한 칸 범위 안에서 가장 깔끔한 위치를 고른다.
    """
    x0, y0, x1, y1 = box
    columns = max(1, int(round((x1 - x0 + 1) / cell)))
    rows = max(1, int(round((y1 - y0 + 1) / cell)))
    step = max(1, int(cell / 8))

    best = None
    for dy in range(-int(cell // 2), int(cell // 2) + 1, step):
        for dx in range(-int(cell // 2), int(cell // 2) + 1, step):
            noise = lattice_noise(image, x0 + dx, y0 + dy, cell, columns, rows)
            if best is None or noise < best[0]:
                best = (noise, x0 + dx, y0 + dy)
    return best[1], best[2], columns, rows


def sample_grid(image, start, cell, columns, rows):
    """각 칸의 가운데만 평균 내어 색을 정한다(압축 잡음 회피)."""
    pixels = image.load()
    width, height = image.size
    x0, y0 = start

    grid = []
    for gy in range(rows):
        line = []
        for gx in range(columns):
            left = x0 + gx * cell
            top = y0 + gy * cell
            reds = greens = blues = count = 0
            for y in range(int(top + cell * 0.25), int(top + cell * 0.75) + 1):
                for x in range(int(left + cell * 0.25), int(left + cell * 0.75) + 1):
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


def parse_part(text):
    """'이름:x0,y0,x1,y1' 형식을 (이름, 사각형) 으로."""
    name, _, numbers = text.partition(":")
    values = [int(v) for v in numbers.split(",")]
    if len(values) != 4:
        raise SystemExit("--part 형식은 이름:x0,y0,x1,y1 입니다: %s" % text)
    return name, tuple(values)


def parse_motion(text):
    """'이름:dx,dy;dx,dy;...' 형식을 (이름, [(dx,dy), ...]) 으로."""
    name, _, steps = text.partition(":")
    offsets = []
    for step in steps.split(";"):
        dx, _, dy = step.partition(",")
        offsets.append((int(dx), int(dy)))
    return name, offsets


def part_frames(cells, parts, motions):
    """부위마다 프레임별로 조금씩 움직여 걷기 동작을 만든다.

    parts 는 [(이름, (x0, y0, x1, y1)), ...] 순서대로 검사하며, 어느 사각형에도
    들어가지 않는 픽셀은 'body' 로 묶인다. motions 는 부위별 프레임 이동량이다.
    """
    rows = len(cells)
    columns = len(cells[0])
    count = max(len(offsets) for offsets in motions.values())

    def part_of(x, y):
        for name, (x0, y0, x1, y1) in parts:
            if x0 <= x <= x1 and y0 <= y <= y1:
                return name
        return "body"

    def offset(name, index):
        steps = motions.get(name)
        if not steps:
            return 0, 0
        return steps[index % len(steps)]

    everything = [offset(name, i) for name in list(motions) + ["body"] for i in range(count)]
    pad_left = max(0, -min(dx for dx, _ in everything))
    pad_right = max(0, max(dx for dx, _ in everything))
    pad_top = max(0, -min(dy for _, dy in everything))
    pad_bottom = max(0, max(dy for _, dy in everything))

    frames = []
    for index in range(count):
        grid = [
            [None] * (columns + pad_left + pad_right)
            for _ in range(rows + pad_top + pad_bottom)
        ]
        for y in range(rows):
            for x in range(columns):
                color = cells[y][x]
                if color is None:
                    continue
                dx, dy = offset(part_of(x, y), index)
                grid[y + dy + pad_top][x + dx + pad_left] = color
        frames.append(grid)
    return trim_frames(frames)


def trim_frames(frames):
    """모든 프레임에서 공통으로 비어 있는 가장자리를 잘라낸다(정렬 유지)."""
    rows = len(frames[0])
    columns = len(frames[0][0])
    used_rows = [
        y for y in range(rows)
        if any(frame[y][x] is not None for frame in frames for x in range(columns))
    ]
    used_columns = [
        x for x in range(columns)
        if any(frame[y][x] is not None for frame in frames for y in range(rows))
    ]
    return [
        [
            [frame[y][x] for x in range(used_columns[0], used_columns[-1] + 1)]
            for y in range(used_rows[0], used_rows[-1] + 1)
        ]
        for frame in frames
    ]


# --- 5단계: sprites.py 에 써넣기 -----------------------------------------
def to_rows(cells, palette_map):
    rows = []
    for line in cells:
        text = "".join("." if color is None else palette_map[color] for color in line)
        rows.append(text.rstrip(".") or ".")
    return rows


def build_block(key, name, palette, frames, scale_factor, bounce=True, facing="right"):
    constant = key.upper()
    out = ["%s = Pokemon(" % constant]
    out.append('    key="%s",' % key)
    out.append('    name_ko="%s",' % name)
    out.append("    scale_factor=%s," % scale_factor)
    if not bounce:
        out.append("    bounce=False,")
    if facing != "right":
        out.append('    facing="%s",' % facing)
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
    parser.add_argument("--part", action="append", default=[], metavar="이름:x0,y0,x1,y1",
                        help="움직일 부위 사각형. 여러 번 쓸 수 있다")
    parser.add_argument("--motion", action="append", default=[], metavar="이름:dx,dy;dx,dy",
                        help="부위별 프레임 이동량. 이름 body 는 나머지 전부")
    parser.add_argument("--facing", choices=["left", "right"], default="right",
                        help="원본 그림이 보고 있는 방향 (기본 right)")
    parser.add_argument("--no-bounce", action="store_true",
                        help="프로그램이 주는 위아래 흔들림을 끄고 프레임에 담긴 움직임만 쓴다")
    parser.add_argument("--preview", default="", help="확인용 png 경로")
    parser.add_argument("--dry-run", action="store_true", help="파일을 고치지 않는다")
    args = parser.parse_args()

    image = Image.open(args.image).convert("RGB")

    def is_background(color):
        return color[0] > 235 and color[1] > 235 and color[2] > 235

    box = content_box(image, is_background)
    x0, y0, x1, y1 = box
    if args.grid:
        cell = (x1 - x0 + 1) / args.grid
    else:
        cell = guess_cell_size(image, box, is_background)
    start_x, start_y, columns, rows = align_lattice(image, box, cell)
    if args.grid:
        columns = args.grid
    if args.rows:
        rows = args.rows
    print("도트 격자: %d x %d 칸 (칸 크기 %.2f)" % (columns, rows, cell))

    grid = sample_grid(image, (start_x, start_y), cell, columns, rows)
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

    if args.part or args.motion:
        parts = [parse_part(text) for text in args.part]
        motions = dict(parse_motion(text) for text in args.motion)
        frames = part_frames(cells, parts, motions)
        print("부위 %d 곳을 움직여 프레임 %d 장" % (len(parts), len(frames)))
    else:
        frames = walk_frames(cells, args.foot_band, args.foot_rise)
        print("걷기 프레임: %d 장" % len(frames))

    if args.preview:
        save_preview(frames, args.preview)
        print("미리보기: %s" % args.preview)

    block = build_block(
        args.key, args.name, palette,
        [to_rows(frame, palette_map) for frame in frames],
        args.scale_factor,
        bounce=not args.no_bounce,
        facing=args.facing,
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
