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


def edge_strength(image, box):
    """줄 사이가 얼마나 달라지는지. 도트 경계에서 값이 크게 튄다."""
    pixels = image.load()
    x0, y0, x1, y1 = box
    columns = [0.0] * (x1 - x0 + 1)
    rows = [0.0] * (y1 - y0 + 1)
    for y in range(y0, y1 + 1):
        previous = pixels[x0, y]
        for x in range(x0 + 1, x1 + 1):
            here = pixels[x, y]
            columns[x - x0] += (abs(here[0] - previous[0]) + abs(here[1] - previous[1])
                                + abs(here[2] - previous[2]))
            previous = here
    for x in range(x0, x1 + 1):
        previous = pixels[x, y0]
        for y in range(y0 + 1, y1 + 1):
            here = pixels[x, y]
            rows[y - y0] += (abs(here[0] - previous[0]) + abs(here[1] - previous[1])
                             + abs(here[2] - previous[2]))
            previous = here
    return columns, rows


def period_score(signal, cell, phase):
    """격자선 위의 변화량이 칸 안쪽보다 얼마나 두드러지는지."""
    on_total = on_count = 0.0
    off_total = off_count = 0.0
    for index, value in enumerate(signal):
        place = (index - phase) % cell
        if place < 0.5 or place > cell - 0.5:
            on_total += value
            on_count += 1
        else:
            off_total += value
            off_count += 1
    if not on_count or not off_count:
        return 0.0
    off_mean = off_total / off_count
    if off_mean <= 0.0:
        return 0.0
    return (on_total / on_count) / off_mean


def guess_cell_size(image, box, is_background):
    """도트 한 칸이 몇 픽셀인지 추정한다.

    픽셀아트를 크게 늘려 저장하면 도트 경계마다 색이 바뀐다. 그 경계가 일정한
    간격으로 되풀이되므로, 여러 간격을 대 보고 '경계 위'가 '칸 안쪽'보다 가장
    크게 튀는 간격을 고르면 된다.

    예전에는 같은 색이 이어지는 길이로 쟀는데, 원본이 부드럽게 확대되어
    저장된 그림(픽셀마다 색이 조금씩 다른 그림)에서는 이어지는 길이가
    믿을 수 없어 칸을 실제보다 작게 잡았다. 실제로 7.82 픽셀짜리 그림을
    6.95 로 잡아, 여덟 칸마다 한 칸씩 늘어난 뭉갠 도트가 나온 적이 있다.
    """
    x0, y0, x1, y1 = box
    longest = max(x1 - x0 + 1, y1 - y0 + 1)
    columns, rows = edge_strength(image, box)

    # 도트가 최소 열두 칸은 된다고 보고, 그보다 큰 칸은 아예 보지 않는다.
    high = min(64.0, longest / 12.0)
    scored = []
    candidate = 3.0
    while candidate <= high:
        score = 0.0
        for signal in (columns, rows):
            best_phase = 0.0
            for step in range(8):
                phase = candidate * step / 8.0
                value = period_score(signal, candidate, phase)
                if value > best_phase:
                    best_phase = value
            score += best_phase
        scored.append((candidate, score))
        candidate += 0.02

    if not scored:
        raise SystemExit("도트 격자를 알아내지 못했습니다. --grid 로 지정하세요.")

    # 칸 크기의 두 배, 세 배도 (한 칸 걸러 격자선을 놓는 셈이라) 점수가 높다.
    # 그래서 가장 높은 점수만 보면 배수에 걸린다. 최고점에 가까운 것들 중
    # 가장 작은 칸을 골라야 진짜 한 칸이다.
    top = max(score for _cell, score in scored)
    cell = min(size for size, score in scored if score >= top * 0.9)
    if longest / cell < 6:
        raise SystemExit("칸 크기 추정이 이상합니다(%.2f). --grid 로 지정하세요." % cell)
    return cell


def lattice_noise(image, x0, y0, cell, columns, rows, stride=2, samples=3):
    """(x0, y0) 에서 cell 간격으로 격자를 놓았을 때 칸 안 색의 흐트러짐.

    칸마다 몇 점만 찍어 보는 방식이라 빠르다. 격자가 도트 경계와 맞으면
    한 칸은 거의 단색이라 값이 0 에 가깝다.
    """
    pixels = image.load()
    width, height = image.size
    if samples > 1:
        spots = [0.25 + 0.5 * i / (samples - 1) for i in range(samples)]
    else:
        spots = [0.5]

    total = 0.0
    counted = 0
    for gy in range(0, rows, stride):
        for gx in range(0, columns, stride):
            low = [255, 255, 255]
            high = [0, 0, 0]
            seen = 0
            for fy in spots:
                y = int(y0 + (gy + fy) * cell)
                for fx in spots:
                    x = int(x0 + (gx + fx) * cell)
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


def refine_cell(image, box, cell):
    """추정한 칸 크기를 주변에서 조금씩 흔들어 보며 가장 깔끔한 값을 고른다.

    칸 크기가 조금만 어긋나도 칸이 쌓이면서 밀려(드리프트), 오른쪽 끝에서는
    도트 경계를 반 칸씩 넘어가 색이 뒤섞인다.

    예전에는 '칸 크기가 내용 폭을 정확히 나눠야 한다'고 보고 폭/칸수 만
    후보로 삼았다. 하지만 흐릿한 가장자리가 잘려 내용 상자가 도트 경계에
    맞지 않으면 그 가정이 틀리고, 오히려 맞게 추정한 칸 크기를 망가뜨린다.
    (7.82 를 7.77 로 되돌려 놓는 식이다.) 그래서 추정값 둘레만 훑는다.
    """
    best = None
    step = cell * 0.002
    for index in range(-20, 21):
        candidate = cell + index * step
        if candidate < 3.0:
            continue
        start_x, start_y, columns, rows = align_lattice(image, box, candidate)
        noise = lattice_noise(image, start_x, start_y, candidate, columns, rows)
        if best is None or noise < best[0]:
            best = (noise, candidate)
    return best[1]


SPRITE_SHARE = 0.35   # 칸에서 이만큼은 그림이어야 그림 칸으로 본다


def sample_grid(image, start, cell, columns, rows, is_background=None):
    """각 칸을 면적 평균해 대표색을 정한다.

    이때 배경(흰 여백)에 해당하는 픽셀은 평균에서 빼야 한다. 그렇지 않으면
    가장자리 칸이 '외곽선 + 흰 배경'의 중간색이 되어, 그림 둘레에 옅은 점이
    남는다. 배경이 대부분인 칸은 아예 배경으로 돌린다.
    """
    pixels = image.load()
    width, height = image.size
    x0, y0 = start

    grid = []
    for gy in range(rows):
        top = y0 + gy * cell
        line = []
        for gx in range(columns):
            left = x0 + gx * cell
            reds = greens = blues = inside = total = 0
            for y in range(int(round(top)), int(round(top + cell))):
                if not 0 <= y < height:
                    continue
                for x in range(int(round(left)), int(round(left + cell))):
                    if not 0 <= x < width:
                        continue
                    color = pixels[x, y][:3]
                    total += 1
                    if is_background is not None and is_background(color):
                        continue          # 배경은 평균에 넣지 않는다
                    reds += color[0]
                    greens += color[1]
                    blues += color[2]
                    inside += 1

            if total == 0 or inside == 0 or inside < total * SPRITE_SHARE:
                line.append((255, 255, 255))      # 배경 칸
            else:
                line.append((reds // inside, greens // inside, blues // inside))
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


def resample(cells, new_width, new_height):
    """도트 그리드를 최근접 이웃으로 늘리거나 줄인다."""
    height = len(cells)
    width = len(cells[0])
    return [
        [
            cells[min(height - 1, y * height // new_height)][
                min(width - 1, x * width // new_width)
            ]
            for x in range(new_width)
        ]
        for y in range(new_height)
    ]


def squash_frames(cells, amount):
    """다리가 없는 포켓몬(메타몽 등)을 위한 웅크림/늘어남 3프레임.

    0 평소, 1 웅크림(납작), 2 늘어남(길쭉). 세 프레임 모두 같은 크기의
    캔버스에 바닥을 맞춰 올려서, 뛰어오를 때 발밑이 흔들리지 않는다.
    """
    height = len(cells)
    width = len(cells[0])
    shapes = [
        (width, height),
        (width + amount, height - amount),
        (max(1, width - amount), height + amount),
    ]
    canvas_width = max(w for w, _ in shapes)
    canvas_height = max(h for _, h in shapes)

    frames = []
    for shape_width, shape_height in shapes:
        scaled = resample(cells, shape_width, shape_height)
        grid = [[None] * canvas_width for _ in range(canvas_height)]
        left = (canvas_width - shape_width) // 2
        top = canvas_height - shape_height          # 바닥 맞춤
        for y, row in enumerate(scaled):
            for x, color in enumerate(row):
                grid[top + y][left + x] = color
        frames.append(grid)
    return frames


def place_on_canvas(grid, canvas_width, canvas_height):
    """그리드를 더 큰 캔버스의 아래쪽 가운데에 올린다."""
    height = len(grid)
    width = len(grid[0])
    out = [[None] * canvas_width for _ in range(canvas_height)]
    left = (canvas_width - width) // 2
    top = canvas_height - height
    for y, row in enumerate(grid):
        for x, color in enumerate(row):
            out[top + y][left + x] = color
    return out


def blink_pose(cells, eyes):
    """눈 자리를 주변 살색으로 덮고 가로선을 그어 감은 눈을 만든다."""
    out = [list(row) for row in cells]
    for x0, y0, x1, y1 in eyes:
        # 눈 테두리 바깥의 색을 모아 가장 흔한 색을 살색으로 본다
        around = {}
        darkest = None
        for y in range(y0 - 1, y2_end(y1, cells) + 1):
            for x in range(x0 - 1, x2_end(x1, cells[0]) + 1):
                if not (0 <= y < len(cells) and 0 <= x < len(cells[0])):
                    continue
                color = cells[y][x]
                if color is None:
                    continue
                inside = x0 <= x <= x1 and y0 <= y <= y1
                if inside:
                    if darkest is None or brightness(color) < brightness(darkest):
                        darkest = color
                else:
                    around[color] = around.get(color, 0) + 1
        if not around:
            continue
        skin = max(around.items(), key=lambda item: item[1])[0]
        line = darkest or skin

        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if 0 <= y < len(out) and 0 <= x < len(out[0]) and out[y][x] is not None:
                    out[y][x] = skin
        middle = (y0 + y1) // 2
        for x in range(x0, x1 + 1):
            if 0 <= middle < len(out) and 0 <= x < len(out[0]) and out[middle][x] is not None:
                out[middle][x] = line
    return out


def y2_end(value, cells):
    return min(value + 1, len(cells) - 1)


def x2_end(value, row):
    return min(value + 1, len(row) - 1)


def brightness(color):
    return color[0] * 299 + color[1] * 587 + color[2] * 114


# --- 5단계: sprites.py 에 써넣기 -----------------------------------------
def to_rows(cells, palette_map):
    rows = []
    for line in cells:
        text = "".join("." if color is None else palette_map[color] for color in line)
        rows.append(text.rstrip(".") or ".")
    return rows


def build_block(key, name, palette, frames, scale_factor, bounce=True, facing="right",
                move="walk", poses=None):
    constant = key.upper()
    out = ["%s = Pokemon(" % constant]
    out.append('    key="%s",' % key)
    out.append('    name_ko="%s",' % name)
    out.append("    scale_factor=%s," % scale_factor)
    if not bounce:
        out.append("    bounce=False,")
    if facing != "right":
        out.append('    facing="%s",' % facing)
    if move != "walk":
        out.append('    move="%s",' % move)
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
    if poses:
        out.append("    pose_rows={")
        for pose_name in sorted(poses):
            out.append('        "%s": [' % pose_name)
            for row in poses[pose_name]:
                out.append('            "%s",' % row)
            out.append("        ],")
        out.append("    },")
    out.append(")")
    return "\n".join(out)


def splice(source, key, block):
    """마커 사이의 정의를 갈아 끼운다. 없으면 새로 추가한다."""
    begin = "# --- 자동 생성 시작: %s ---" % key
    end = "# --- 자동 생성 끝: %s ---" % key
    body = "%s\n%s\n%s" % (begin, block, end)
    constant = key.upper()

    if begin in source and end in source:
        head = source[: source.index(begin)]
        tail = source[source.index(end) + len(end):]
        return register(head + body + tail, constant)

    # 기존 손그림 정의가 있으면 그 자리를 대신한다.
    pattern = re.compile(
        r"^%s = Pokemon\(\n(?:.*\n)*?\)\n" % re.escape(constant), re.MULTILINE
    )
    if pattern.search(source):
        return register(pattern.sub(body + "\n", source, count=1), constant)

    marker = "\nPOKEMON = "
    if marker not in source:
        raise SystemExit("sprites.py 에서 POKEMON 정의를 찾지 못했습니다.")
    index = source.index(marker)
    return register(source[:index] + "\n" + body + "\n" + source[index:], constant)


def register(source, constant):
    """POKEMON 목록에 아직 없으면 끝에 추가한다."""
    match = re.search(r"^POKEMON = \{p\.key: p for p in \(([^)]*)\)\}", source, re.MULTILINE)
    if not match:
        raise SystemExit("sprites.py 의 POKEMON 목록 형식을 알아보지 못했습니다.")

    names = [name.strip() for name in match.group(1).split(",") if name.strip()]
    if constant in names:
        return source
    names.append(constant)
    replacement = "POKEMON = {p.key: p for p in (%s)}" % ", ".join(names)
    return source[: match.start()] + replacement + source[match.end():]


def load_frame(path, index):
    """그림 한 장을 흰 배경 위에 올려 RGB 로 읽는다.

    투명한 부분을 그냥 convert("RGB") 하면 팔레트의 투명 자리에 적힌 색이
    그대로 나온다. 검정일 때도, 자홍(#ff00ff)일 때도 있다. 이 도구는 '거의
    흰색'만 배경으로 보므로, 그대로 두면 배경이 그림의 일부가 되어 버린다.
    흰 종이 위에 올린 셈 치면 뒤쪽 계산을 하나도 건드리지 않아도 된다.

    GIF 처럼 여러 장이 든 파일은 index 번째 장을 쓴다.
    """
    return flatten(read_rgba(path, index))


def read_rgba(path, index, quiet=False):
    """그림 한 장을 알파째로 읽는다."""
    with Image.open(path) as source:
        total = getattr(source, "n_frames", 1)
        if index:
            if index >= total:
                raise SystemExit("프레임 %d 번은 없다. 이 파일에는 %d 장이 들어 있다."
                                 % (index, total))
            source.seek(index)
        if total > 1 and not quiet:
            print("여러 장이 든 파일이다: %d 장 중 %d 번째를 쓴다." % (total, index))
        return source.convert("RGBA")


def flatten(rgba):
    """흰 종이 위에 올려 RGB 로 만든다."""
    paper = Image.new("RGBA", rgba.size, (255, 255, 255, 255))
    return Image.alpha_composite(paper, rgba).convert("RGB")


def sample_alpha(rgba, start, cell, columns, rows):
    """칸마다 얼마나 불투명한지 평균한다.

    투명한 자리를 흰색으로 바꿔 읽으면, 그림 안쪽에 갇힌 구멍(피카츄의 꼬리와
    몸 사이 같은 곳)이 흰 도트로 굳는다. clear_background 는 가장자리에서
    이어진 배경만 지우기 때문이다 — 눈동자 흰자를 지키려고 그렇게 되어 있다.
    원본이 '여기는 비었다' 고 말해 주는 알파값을 따로 봐야 둘을 구별할 수 있다.
    """
    pixels = rgba.load()
    width, height = rgba.size
    x0, y0 = start
    grid = []
    for gy in range(rows):
        top = y0 + gy * cell
        line = []
        for gx in range(columns):
            left = x0 + gx * cell
            total = count = 0
            for y in range(int(round(top)), int(round(top + cell))):
                if not 0 <= y < height:
                    continue
                for x in range(int(round(left)), int(round(left + cell))):
                    if not 0 <= x < width:
                        continue
                    total += pixels[x, y][3]
                    count += 1
            line.append(0 if count == 0 else total // count)
        grid.append(line)
    return grid


def main():
    parser = argparse.ArgumentParser(description="도트 이미지를 스프라이트로 변환")
    parser.add_argument("image", help="변환할 png/jpg/gif 파일")
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
    parser.add_argument("--pose-squash", type=int, default=0, metavar="칸",
                        help="눌린/늘어난 자세를 만들어 둔다 (공중·착지·숨쉬기에 쓰임)")
    parser.add_argument("--eyes", action="append", default=[], metavar="x0,y0,x1,y1",
                        help="눈 위치. 주면 눈 감은 자세를 만든다. 여러 번 쓸 수 있다")
    parser.add_argument("--hop", type=int, default=0, metavar="칸",
                        help="다리 없이 폴짝 뛰는 포켓몬. 웅크림/늘어남 폭(도트 수)")
    parser.add_argument("--float", dest="floats", action="store_true",
                        help="바닥을 딛지 않고 공중에 떠다닌다")
    parser.add_argument("--facing", choices=["left", "right"], default="right",
                        help="원본 그림이 보고 있는 방향 (기본 right)")
    parser.add_argument("--no-bounce", action="store_true",
                        help="프로그램이 주는 위아래 흔들림을 끄고 프레임에 담긴 움직임만 쓴다")
    parser.add_argument("--frame", type=int, default=0, metavar="번호",
                        help="여러 장이 든 파일(GIF)에서 쓸 장 번호 (기본 0)")
    parser.add_argument("--idle-frames", default="", metavar="0,12,24",
                        help="가만히 있을 때 돌려 보여 줄 장 번호들 (GIF)")
    parser.add_argument("--preview", default="", help="확인용 png 경로")
    parser.add_argument("--dry-run", action="store_true", help="파일을 고치지 않는다")
    args = parser.parse_args()

    source = read_rgba(args.image, args.frame)
    image = flatten(source)

    def is_background(color):
        return color[0] > 235 and color[1] > 235 and color[2] > 235

    box = content_box(image, is_background)
    x0, y0, x1, y1 = box
    if args.grid:
        cell = (x1 - x0 + 1) / args.grid
    else:
        cell = refine_cell(image, box, guess_cell_size(image, box, is_background))
    start_x, start_y, columns, rows = align_lattice(image, box, cell)
    if args.grid:
        columns = args.grid
    if args.rows:
        rows = args.rows
    print("도트 격자: %d x %d 칸 (칸 크기 %.2f)" % (columns, rows, cell))

    def read_cells(rgba, label):
        """한 장을 같은 격자로 읽어 (대표색, 비었는지) 를 돌려준다."""
        flat = flatten(rgba)
        one = sample_grid(flat, (start_x, start_y), cell, columns, rows, is_background)
        empty = clear_background(one, is_background)
        # 원본이 투명하다고 한 자리는 그림 안쪽이라도 비운다. 안 그러면 꼬리와
        # 몸 사이처럼 갇힌 구멍이 흰 도트로 남는다.
        transparent = sample_alpha(rgba, (start_x, start_y), cell, columns, rows)
        holes = 0
        for gy in range(rows):
            for gx in range(columns):
                if transparent[gy][gx] < 128 and not empty[gy][gx]:
                    empty[gy][gx] = True
                    holes += 1
        if holes:
            print("%s: 그림 안쪽의 투명한 칸 %d 개를 비웠다." % (label, holes))
        return one, empty

    grid, outside = read_cells(source, "기본")

    # 가만히 있을 때 돌릴 장들도 같은 격자로 읽는다. 격자와 자르는 상자를 함께
    # 써야 장끼리 어긋나지 않는다.
    idle_numbers = [int(n) for n in args.idle_frames.split(",") if n.strip() != ""] \
        if args.idle_frames else []
    idle_reads = [read_cells(read_rgba(args.image, n, quiet=True), "%d번" % n)
                  for n in idle_numbers]

    inside_colors = []
    for one, empty in [(grid, outside)] + idle_reads:
        inside_colors.extend(one[y][x] for y in range(rows) for x in range(columns)
                             if not empty[y][x])
    palette_colors = quantize(inside_colors, args.colors)

    def to_cells(one, empty):
        return [[None if empty[y][x]
                 else palette_colors[nearest(one[y][x], palette_colors)]
                 for x in range(columns)] for y in range(rows)]

    cells = to_cells(grid, outside)
    idle_cells = [to_cells(one, empty) for one, empty in idle_reads]

    # 남은 여백 잘라내기. 모든 장을 합쳐서 상자를 하나만 잡아야 장끼리 어긋나지
    # 않는다.
    every = [cells] + idle_cells
    used_rows = [y for y in range(rows)
                 if any(block[y][x] is not None for block in every for x in range(columns))]
    used_columns = [x for x in range(columns)
                    if any(block[y][x] is not None for block in every for y in range(rows))]

    def crop(block):
        return [[block[y][x] for x in range(used_columns[0], used_columns[-1] + 1)]
                for y in range(used_rows[0], used_rows[-1] + 1)]

    cells = crop(cells)
    idle_cells = [crop(block) for block in idle_cells]
    print("잘라낸 크기: %d x %d" % (len(cells[0]), len(cells)))
    if idle_cells:
        print("가만히 있을 때 돌릴 장: %d 개 (%s)"
              % (len(idle_cells), ", ".join(str(n) for n in idle_numbers)))

    # 자주 쓰인 색부터 팔레트 문자 배정
    counts = {}
    for line in cells:
        for color in line:
            if color is not None:
                counts[color] = counts.get(color, 0) + 1
    ordered = sorted(counts.items(), key=lambda item: -item[1])
    palette = [(PALETTE_CHARS[i], color) for i, (color, _) in enumerate(ordered)]
    palette_map = {color: char for char, color in palette}

    if args.hop:
        frames = squash_frames(cells, args.hop)
        print("웅크림/늘어남 %d칸으로 프레임 %d장" % (args.hop, len(frames)))
    elif args.part or args.motion:
        parts = [parse_part(text) for text in args.part]
        motions = dict(parse_motion(text) for text in args.motion)
        frames = part_frames(cells, parts, motions)
        print("부위 %d 곳을 움직여 프레임 %d 장" % (len(parts), len(frames)))
    else:
        frames = walk_frames(cells, args.foot_band, args.foot_rise)
        print("걷기 프레임: %d 장" % len(frames))

    poses = {}
    # 가만히 있을 때 돌릴 장들은 idle0, idle1 … 이라는 이름의 자세로 넣는다.
    # 자세는 이미 '프레임과 같은 크기의 대체 그림' 이고, 크기가 어긋나면
    # sprites.py 의 검증이 잡아 준다. 새 칸을 만들 이유가 없다.
    for number, block in enumerate(idle_cells):
        poses["idle%d" % number] = block

    if args.pose_squash or args.eyes or idle_cells:
        amount = args.pose_squash
        base_height = len(cells)
        base_width = len(cells[0])

        if amount:
            poses["squash"] = resample(cells, base_width + amount, base_height - amount)
            poses["stretch"] = resample(cells, max(1, base_width - amount), base_height + amount)
        if args.eyes:
            eyes = []
            for text in args.eyes:
                values = [int(v) for v in text.split(",")]
                if len(values) != 4:
                    raise SystemExit("--eyes 형식은 x0,y0,x1,y1 입니다: %s" % text)
                eyes.append(tuple(values))
            poses["blink"] = blink_pose(cells, eyes)

        # 프레임과 자세를 같은 크기 캔버스에 바닥 맞춰 올린다.
        canvas_width = max([len(frame[0]) for frame in frames]
                           + [len(grid[0]) for grid in poses.values()])
        canvas_height = max([len(frame) for frame in frames]
                            + [len(grid) for grid in poses.values()])
        frames = [place_on_canvas(frame, canvas_width, canvas_height) for frame in frames]
        poses = {
            name: place_on_canvas(grid, canvas_width, canvas_height)
            for name, grid in poses.items()
        }
        print("자세 %s 를 함께 만듦 (%dx%d)" % (", ".join(sorted(poses)), canvas_width, canvas_height))

    if args.preview:
        save_preview(frames, args.preview)
        print("미리보기: %s" % args.preview)

    block = build_block(
        args.key, args.name, palette,
        [to_rows(frame, palette_map) for frame in frames],
        args.scale_factor,
        bounce=not args.no_bounce and not args.hop and not args.floats,
        facing=args.facing,
        move="hop" if args.hop else ("float" if args.floats else "walk"),
        poses={name: to_rows(grid, palette_map) for name, grid in poses.items()},
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
