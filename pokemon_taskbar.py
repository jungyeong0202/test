#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""화면 하단(작업 표시줄) 위를 포켓몬이 돌아다니는 데스크톱 펫.

표준 라이브러리(tkinter)만 사용하며 외부 이미지/네트워크가 필요 없다.

사용 예:
    python pokemon_taskbar.py                     # 피카츄 한 마리
    python pokemon_taskbar.py -p pikachu -p squirtle
    python pokemon_taskbar.py --count 3 --scale 4 --speed 70

마우스 조작:
    왼쪽 클릭   - 포켓몬이 폴짝 뛴다
    누른 채 끌기 - 원하는 자리로 옮긴다 (놓으면 바닥으로 떨어진다)
    오른쪽 클릭  - 메뉴(추가 / 보내주기 / 종료)
"""

from __future__ import annotations

import argparse
import ctypes
import math
import os
import platform
import random
import sys
import tkinter as tk

import settings as settings_file
from sprites import EVOLUTIONS, POKEMON, base_species, validate_all

MIN_SPRITE_SCALE = 0.5  # 도트 하나가 이보다 작아지지는 않는다
GRAVITY = 900.0        # 떨어지는 가속도(초당 픽셀^2)
JUMP_SPEED = 200.0     # 클릭했을 때 튀어오르는 속도(초당 픽셀)
DRAG_SLACK = 4         # 이보다 많이 움직이면 클릭이 아니라 끌기로 본다
HOP_SPEED = 205.0      # 뛰어다니는 포켓몬이 튀어오르는 속도
HOP_CROUCH_SEC = 0.10  # 뛰기 직전 웅크리는 시간
HOP_LAND_SEC = 0.10    # 착지하고 납작해져 있는 시간
HOP_REST = (0.10, 0.45)  # 다음 점프까지 쉬는 시간
HOP_BOOST = 2.0        # 공중에서만 나아가므로 걷기보다 빠르게
HOP_TURN_CHANCE = 0.12  # 착지할 때마다 이 확률로 방향을 바꾼다
# 걷는 포켓몬은 가끔 제자리에서 두 번 폴짝 뛰며 장난을 친다.
PLAY_CHANCE = 0.28
PLAY_HOPS = 2
PLAY_HOP_SPEED = 145.0
PLAY_WAIT_SEC = 0.12
PLAY_TURN_CHANCE = 0.45

# 공중에 떠다니는 포켓몬(뮤). 바닥을 딛지 않는다.
FLOAT_HEIGHT = (26.0, 120.0)   # 바닥에서 떠 있는 높이 범위(px)
FLOAT_RETARGET = (1.6, 4.5)    # 이 간격으로 떠 있을 높이를 새로 고른다
FLOAT_EASE = 1.6               # 새 높이로 옮겨 가는 빠르기
FLOAT_BOB_SEC = 2.2            # 위아래로 살랑거리는 한 주기(초)
FLOAT_BOB_DOTS = 1.5           # 살랑거리는 폭(도트 단위)
FLOAT_SPEED = 0.7              # 걷는 포켓몬보다 느긋하게 흘러 다닌다
FLOAT_STEP_SEC = 0.30          # 프레임 넘기는 간격
FLOAT_TURN_CHANCE = 0.003      # 틱마다 이 확률로 방향을 바꾼다
FLOAT_STOP_CHANCE = 0.004      # 틱마다 이 확률로 잠깐 멈춘다
FLOAT_NUDGE = 30.0             # 쓰다듬으면(클릭) 이만큼 위로 올라간다

# 진화. 함께 걸은 거리와 쓰다듬은 횟수를 채운 뒤, 메뉴에서 직접 진화한다.
#
# 시간이 흘렀다고 저절로 진화하지는 않는다. 아끼던 모습이 예고 없이 바뀌면
# 곤란하므로, 진화할지 말지는 쓰다듬는 사람이 정한다.
EVOLVE_PET_NEED = 8.0       # 이만큼 쓰다듬으면 친밀도 조건을 채운다
EVOLVE_PER_PET = 1.0        # 한 번 쓰다듬을 때마다
EVOLVE_WALK_NEED = 600.0    # 이만큼 걸으면 산책 조건을 채운다(px)
DEFAULT_WALK_SPEED = 55.0   # 기본 산책 속도(px/초)
COINS_PER_WALK = 100        # 100px를 걸을 때마다 받는 돈(원)
COIN_WALK_DISTANCE = 100.0  # 이만큼 걸을 때마다 돈을 받는다
POKEMON_PRICE = int(        # 기본 속도로 두 시간 산책해 얻는 돈
    DEFAULT_WALK_SPEED * 2 * 60 * 60 / COIN_WALK_DISTANCE * COINS_PER_WALK
)
FOOD_COST = 400             # 포켓푸드 한 개 가격(원)
FOOD_FRIENDSHIP = 2.0       # 포켓푸드 한 개가 채우는 친밀도
GROWTH_DROP_COST = 2500     # 성장의 물방울 한 개 가격(원)
MARKET_UPDATE_SEC = 20.0    # 이 간격마다 모의 주가가 한 번 변한다
STOCK_RELIST_SECONDS = 30 * 60
STOCK_LISTINGS = (
    ("피카츄전기", 1000, 12), ("꼬부기워터", 1800, 7),
    ("이상해씨농장", 2700, 10), ("파이리화력", 1300, 18),
    ("메타몽랩", 2200, 24), ("뮤테크", 3500, 30),
    ("이브이패션", 1600, 15), ("고라파덕물류", 1200, 20),
    ("럭키메디컬", 2400, 9), ("갸라도스해운", 3000, 22),
    ("잠만보식품", 1900, 11), ("팬텀게임즈", 2800, 28),
)
STOCK_COUNT = 6
STOCK_EVENT_CHANCE = 0.25
STOCK_FEE_RATE = 0.02
STOCK_HALT_SECONDS = 40
STOCK_EVENTS = (
    (("번개 발전소 증설", 18), ("송전탑 고장", -16)),
    (("정수장 장기 계약", 11), ("가뭄 경보", -12)),
    (("친환경 농장 수확", 15), ("병충해 주의보", -14)),
    (("화력 발전 수요 급증", 24), ("화산재 공급 차질", -22)),
    (("변신 연구 특허", 30), ("실험 결과 논란", -28)),
    (("신기술 발표", 38), ("연구소 보안 사고", -35)),
    (("신작 컬렉션 완판", 20), ("유행 변화", -18)),
    (("물류 허브 확장", 26), ("배송 지연", -23)),
    (("건강식 수요 증가", 14), ("진료비 규제", -13)),
    (("해운 노선 확대", 29), ("폭풍 운항 중단", -27)),
    (("간식 판매 호조", 17), ("원재료 가격 급등", -16)),
    (("대형 게임 출시", 34), ("서버 장애", -31)),
)
EVOLVE_FLASHES = 7          # 두 모습을 번갈아 번쩍이는 횟수
EVOLVE_FIRST_SEC = 0.30     # 처음 번쩍임 간격
EVOLVE_LAST_SEC = 0.07      # 마지막 번쩍임 간격 (점점 빨라진다)
EVOLVE_HOLD_SEC = 0.55      # 다 끝나고 새하얗게 머무는 시간


def format_won(amount):
    """게임 안의 돈을 천 단위 쉼표가 있는 원 단위로 표시한다."""
    return "{:,}원".format(amount)


MENU_CREAM = "#fff7e6"
MENU_RED = "#d9343b"
MENU_DARK = "#3a2d26"
MENU_DISABLED = "#a8917d"


def pokemon_menu(parent):
    """포켓볼의 빨강과 크림색을 쓰는 우클릭 메뉴를 만든다."""
    return tk.Menu(
        parent, tearoff=0, bg=MENU_CREAM, fg=MENU_DARK,
        activebackground=MENU_RED, activeforeground="#ffffff",
        disabledforeground=MENU_DISABLED, selectcolor=MENU_RED,
        relief=tk.RAISED, borderwidth=2, activeborderwidth=0,
        font=("Malgun Gothic", 10, "bold"),
    )


SIZE_CHOICES = (("작게", 3.0), ("보통", 4.5), ("크게", 6.0), ("아주 크게", 9.0))
EFFECT_GRAVITY = 260.0   # 먼지가 떨어지는 가속도
DUST_LIFE = 0.40         # 먼지가 사라지기까지
EMOTE_LIFE = 0.90        # 하트/Zzz 가 떠올랐다 사라지기까지
LAND_DUST_SPEED = 60.0   # 이 속도보다 세게 떨어져야 먼지가 인다
NAP_CHANCE = 0.18        # 멈춰 설 때 이 확률로 길게 낮잠을 잔다
NAP_SECONDS = (4.0, 9.0)
ZZZ_EVERY = 1.1          # 낮잠 중 Zzz 를 올려 보내는 간격
BLINK_EVERY = (3.0, 7.0) # 이 간격마다 한 번씩 눈을 깜빡인다
BLINK_TIME = 0.14        # 눈을 감고 있는 시간
LAND_SQUASH_TIME = 0.12  # 착지하고 눌려 있는 시간
BREATH_SEC = 0.9         # 낮잠 중 숨쉬기 한 박자
WIGGLE_SEC = 0.10        # 들려 있을 때 버둥거리는 간격
IDLE_ACTION_CHANCE = 0.55       # 낮잠이 아닌 대기 때 개성 모션을 할 확률
IDLE_ACTION_SECONDS = (0.9, 1.6)
IDLE_EFFECT_EVERY = 0.55        # 대기 효과를 다시 띄우는 간격
GREETING_DISTANCE = 150.0       # 이 거리 안에서 마주치면 인사한다(px)
GREETING_SECONDS = 1.15         # 서로 바라보며 인사하는 시간
GREETING_COOLDOWN = 5.0         # 같은 둘이 연달아 인사하지 않게 쉬는 시간
GREETING_TALK_EVERY = 0.34      # 서로 말풍선을 주고받는 박자

# 효과에 쓰는 아주 작은 도트 그림
HEART_DOTS = (
    (1, 0), (2, 0), (4, 0), (5, 0),
    (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (5, 1), (6, 1),
    (0, 2), (1, 2), (2, 2), (3, 2), (4, 2), (5, 2), (6, 2),
    (1, 3), (2, 3), (3, 3), (4, 3), (5, 3),
    (2, 4), (3, 4), (4, 4),
    (3, 5),
)
ZZZ_DOTS = (
    (0, 0), (1, 0), (2, 0), (3, 0),
    (2, 1),
    (1, 2),
    (0, 3), (1, 3), (2, 3), (3, 3),
)
TALK_DOTS = (
    (1, 0), (2, 0), (3, 0),
    (0, 1), (4, 1),
    (0, 2), (4, 2),
    (1, 3), (2, 3), (3, 3),
    (1, 4),
)
IDLE_DOTS = {
    "spark": ((1, 0), (1, 1), (0, 1), (2, 1), (1, 2)),
    "flame": ((1, 0), (0, 1), (1, 1), (2, 1), (0, 2), (1, 2)),
    "leaf": ((1, 0), (2, 0), (0, 1), (1, 1), (2, 1), (1, 2)),
    "bubble": ((0, 0), (1, 0), (0, 1), (1, 1)),
    "twinkle": ((1, 0), (0, 1), (1, 1), (2, 1), (1, 2)),
}
IDLE_ACTIONS = {
    "pikachu": ("spark", "#ffe14d"),
    "charmander": ("flame", "#ff783d"),
    "bulbasaur": ("leaf", "#79c95d"),
    "squirtle": ("bubble", "#8bd9ff"),
    "wartortle": ("bubble", "#8bd9ff"),
    "ditto": ("wiggle", "#dc7ae8"),
    "mew": ("twinkle", "#f6a5e5"),
}
SPEED_CHOICES = (("느리게", 30.0), ("보통", 55.0), ("빠르게", 95.0))
TICK_MS = 40           # 화면 갱신 주기
# 걸음 프레임은 시간 대신 실제 이동 거리에 맞춘다. 속도가 바뀌어도 발이 미끄러지지 않는다.
WALK_STRIDE = 35.0     # 4프레임 한 바퀴에 나아가는 거리(px)
WALK_ACCEL = 220.0     # 걷기 시작할 때 속도를 올리는 가속도
WALK_DECEL = 420.0     # 멈추거나 돌아설 때 속도를 줄이는 감속도
TURN_PAUSE_SEC = 0.12  # 멈춰 몸을 낮춘 채 방향을 바꾸는 시간
WALK_SUBSTEPS = 8      # 4장 도트를 더 부드럽게 보이게 나눈 보행 박자
WALK_BOB = (0.0, 0.45, 1.0, 0.45, 0.0, 0.45, 1.0, 0.45)
WALK_BODY_SIZE = 1     # 체중을 실을 때 몸 전체를 누르거나 늘릴 도트 수
TOPMOST_TICKS = 5      # 몇 틱마다 "맨 앞"을 다시 주장할지 (5틱 = 0.2초)
COLOR_KEY = "#ff00ff"  # 투명 처리에 쓰는 색(윈도우 전용)

SPI_GETWORKAREA = 0x0030
HWND_TOPMOST = -1
SWP_NOSIZE = 0x0001
SWP_NOMOVE = 0x0002
SWP_NOACTIVATE = 0x0010


RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
RUN_VALUE = "PokemonTaskbar"


def autostart_command():
    """윈도우 시작 시 실행할 명령. 콘솔 창이 뜨지 않도록 pythonw 를 쓴다."""
    script = os.path.abspath(__file__)
    launcher = sys.executable
    windowed = os.path.join(os.path.dirname(launcher), "pythonw.exe")
    if os.path.exists(windowed):
        launcher = windowed
    return '"%s" "%s"' % (launcher, script)


def autostart_enabled():
    """윈도우 시작 프로그램에 등록돼 있는지."""
    if platform.system() != "Windows":
        return False
    try:
        import winreg

        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_KEY) as key:
            winreg.QueryValueEx(key, RUN_VALUE)
        return True
    except Exception:
        return False


def set_autostart(enabled):
    """윈도우 시작 프로그램 등록/해제. 현재 사용자(HKCU)에만 쓴다."""
    if platform.system() != "Windows":
        return False
    try:
        import winreg

        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, RUN_KEY) as key:
            if enabled:
                winreg.SetValueEx(key, RUN_VALUE, 0, winreg.REG_SZ, autostart_command())
            else:
                try:
                    winreg.DeleteValue(key, RUN_VALUE)
                except FileNotFoundError:
                    pass
        return True
    except Exception:
        return False


def work_area_bottom(fallback):
    """작업 표시줄을 제외한 바탕화면 영역의 아래쪽 y 좌표.

    윈도우에서는 작업 표시줄 바로 위 선을 돌려주므로, 포켓몬이 표시줄을
    가리지 않고 그 위에 올라선다. 알아낼 수 없으면 fallback(화면 맨 아래).
    """
    if platform.system() != "Windows":
        return fallback
    try:
        class Rect(ctypes.Structure):
            _fields_ = [
                ("left", ctypes.c_long),
                ("top", ctypes.c_long),
                ("right", ctypes.c_long),
                ("bottom", ctypes.c_long),
            ]

        rect = Rect()
        ok = ctypes.windll.user32.SystemParametersInfoW(
            SPI_GETWORKAREA, 0, ctypes.byref(rect), 0
        )
        if ok and 0 < rect.bottom <= fallback:
            return int(rect.bottom)
    except Exception:
        pass
    return fallback


def setup_transparency(window, system):
    """창 배경을 투명하게 만든다. 실패하면 None을 돌려준다."""
    if system == "Windows":
        try:
            window.wm_attributes("-transparentcolor", COLOR_KEY)
            return COLOR_KEY
        except tk.TclError:
            return None
    if system == "Darwin":
        try:
            window.wm_attributes("-transparent", True)
            return "systemTransparent"
        except tk.TclError:
            return None
    return None


def flip_for(pokemon, moving_right):
    """이동 방향에 맞춰 그림을 좌우로 뒤집어야 하는지.

    원본이 보고 있는 방향과 가려는 방향이 다를 때만 뒤집는다.
    """
    faces_right = pokemon.facing == "right"
    return faces_right != moving_right


def make_photo(grid, scale, flip=False, master=None):
    """색상 그리드를 tkinter 이미지로 만든다(투명 픽셀은 비워 둔다).

    scale 은 도트 하나가 화면에서 차지할 픽셀 수이며 소수여도 된다.
    1.5 면 2픽셀과 1픽셀이 번갈아 나오는 식으로, 화면 픽셀마다 가장 가까운
    도트를 찍는다(최근접 이웃).

    master 를 넘기면 그 인터프리터에 이미지를 만든다. 여러 개의 Tk 를 동시에
    쓰더라도 이미지가 엉키지 않는다.
    """
    height = len(grid)
    width = len(grid[0])
    # 가로세로에 같은 반올림 규칙을 써야 비율이 그대로 유지된다.
    # (파이썬 round 는 .5 를 짝수로 보내므로 축마다 결과가 달라질 수 있다.)
    out_width = max(1, int(width * scale + 0.5))
    out_height = max(1, int(height * scale + 0.5))
    photo = tk.PhotoImage(master=master, width=out_width, height=out_height)

    def source_x(out_x):
        return min(width - 1, out_x * width // out_width)

    def source_y(out_y):
        return min(height - 1, out_y * height // out_height)

    out_y = 0
    while out_y < out_height:
        y = source_y(out_y)
        # 같은 도트 줄을 가리키는 화면 줄들을 한 번에 그린다.
        end_y = out_y
        while end_y < out_height and source_y(end_y) == y:
            end_y += 1
        band = end_y - out_y

        row = grid[y]
        cells = row[::-1] if flip else row
        out_x = 0
        while out_x < out_width:
            color = cells[source_x(out_x)]
            end_x = out_x
            while end_x < out_width and cells[source_x(end_x)] == color:
                end_x += 1
            if color is not None:
                line = "{" + " ".join([color] * (end_x - out_x)) + "}"
                photo.put(" ".join([line] * band), to=(out_x, out_y))
            out_x = end_x
        out_y = end_y
    return photo


def resample_grid(grid, width, height):
    """도트 격자 전체를 최근접 이웃으로 늘리거나 줄인다."""
    old_height = len(grid)
    old_width = len(grid[0])
    return [
        [
            grid[min(old_height - 1, y * old_height // height)][
                min(old_width - 1, x * old_width // width)
            ]
            for x in range(width)
        ]
        for y in range(height)
    ]


def pad_on_ground(grid, width, height):
    """크기가 달라진 그림을 가운데·아래에 맞춘 같은 캔버스에 놓는다."""
    padded = [[None] * width for _ in range(height)]
    top = height - len(grid)
    left = (width - len(grid[0])) // 2
    for y, row in enumerate(grid):
        padded[top + y][left:left + len(row)] = row
    return padded


def whole_walk_frames(frames):
    """발걸음마다 몸 전체가 눌리고 늘어나는 걷기 프레임을 만든다.

    디딤(0/2)에서는 몸통·귀·꼬리까지 한 칸 낮고 넓게 눌리고, 발을 든
    프레임(1/3)에서는 전체 실루엣이 한 칸 길고 가늘게 늘어난다. 모든
    도트를 한 번에 변형하므로 부위 일부를 섞어 윤곽이 깨지지 않는다.
    """
    shaped = []
    for index, frame in enumerate(frames):
        size = WALK_BODY_SIZE if index % 2 == 0 else -WALK_BODY_SIZE
        shaped.append(resample_grid(
            frame,
            max(1, len(frame[0]) + size),
            max(1, len(frame) - size),
        ))
    width = max(len(frame[0]) for frame in shaped)
    height = max(len(frame) for frame in shaped)
    return [pad_on_ground(frame, width, height) for frame in shaped]


class PokemonPet:
    """작업 표시줄 위를 돌아다니는 포켓몬 한 마리."""

    def __init__(self, app, pokemon):
        self.app = app
        self.pokemon = pokemon
        self.images = app.get_images(pokemon)
        self.frame_count = len(self.images["right"])
        self.scale = app.sprite_scale(pokemon)

        sample = self.images["right"][0]
        self.own_width = sample.width()
        self.own_height = sample.height()
        # 진화하면 몸집이 달라진다. 번쩍이는 동안 잘리지 않도록 두 모습이
        # 모두 들어갈 크기로 창을 잡아 둔다. 그림은 아래쪽에 맞춰 그리므로
        # 창이 커져도 발은 바닥에 그대로 붙어 있다.
        self.next_key = pokemon.evolves_to
        self.width = self.own_width
        self.height = self.own_height
        if self.next_key:
            after = app.get_images(POKEMON[self.next_key])["right"][0]
            self.width = max(self.width, after.width())
            self.height = max(self.height, after.height())
        self.own_dx = (self.width - self.own_width) // 2
        self.own_dy = self.height - self.own_height
        self.hop = max(1, int(round(self.scale)))  # 걸을 때 위아래로 흔들리는 폭
        # 먼지나 하트가 몸 밖으로 튀어나갈 자리를 창에 미리 마련해 둔다.
        self.dot = max(1, int(round(self.scale)))
        self.margin_x = self.dot * 7
        self.margin_top = self.dot * 9
        self.window_width = self.width + self.margin_x * 2
        self.window_height = self.height + self.hop + self.margin_top
        self.effects = []
        self.idle_action = None
        self.idle_action_left = 0.0
        self.idle_effect_left = 0.0
        self.idle_phase = 0.0
        self.greeting_left = 0.0
        self.greeting_phase = 0.0
        self.greeting_cooldown = 0.0
        self.greeting_leads = False
        self.greeting_talk_turn = -1

        self.window = tk.Toplevel(app.root)
        self.window.overrideredirect(True)
        self.window.wm_attributes("-topmost", True)
        background = setup_transparency(self.window, app.system) or app.background
        self.window.configure(bg=background)

        self.canvas = tk.Canvas(
            self.window,
            width=self.window_width,
            height=self.window_height,
            bg=background,
            highlightthickness=0,
            bd=0,
        )
        self.canvas.pack()
        self.sprite = self.canvas.create_image(
            self.margin_x + self.own_dx, self.margin_top + self.hop + self.own_dy,
            anchor="nw", image=self.images["right"][0],
        )

        self.max_x = max(0, app.screen_width - self.window_width)
        # 어떤 이유로든 화면 밖으로 나가지 않도록 붙잡아 둔다.
        wanted = app.ground_y - self.window_height - app.offset
        lowest = app.screen_height - self.window_height
        self.base_y = max(0, min(wanted, lowest))
        self.x = random.uniform(0, self.max_x)
        self.direction = random.choice((-1, 1))
        self.speed = app.speed * random.uniform(0.85, 1.15)
        self.walk_speed = 0.0
        self.gait_distance = 0.0
        self.move = pokemon.move
        self.state = "walk"
        self.state_left = 0.0
        self.stop_kind = None
        self.turn_direction = self.direction
        self.play_hops = 0
        self.hop_state = "rest"
        self.hop_timer = random.uniform(*HOP_REST)
        self.float_base = self.pick_float_height()
        self.float_target = self.float_base
        self.float_timer = random.uniform(*FLOAT_RETARGET)
        self.float_phase = random.uniform(0.0, FLOAT_BOB_SEC)
        self.friendship = 0.0      # 아껴 준 만큼 찬다.
        self.walked = 0.0          # 스스로 걸은 거리(px). 끌어다 놓은 거리는 세지 않는다.
        self.evolving = False
        self.evolve_step = 0
        self.evolve_timer = 0.0
        self.white = None          # 진화할 때 쓰는 하얀 실루엣
        self.napping = False
        self.zzz_timer = 0.0
        self.blink_timer = random.uniform(*BLINK_EVERY)
        self.blinking = 0.0
        self.land_squash = 0.0
        self.breath = 0.0
        self.wiggle = 0.0
        # 프레임에 몸통 움직임이 그려져 있으면 프로그램 쪽 흔들림은 끈다.
        self.bounce_px = self.hop if pokemon.bounce else 0
        self.anim_time = 0.0
        # 바닥에서 떠 있는 높이(px). 떠다니는 포켓몬은 처음부터 공중에 있다.
        self.lift = self.float_base if self.move == "float" else 0.0
        self.vertical_speed = 0.0
        self.dragging = False
        self.drag_offset = (0, 0)
        self.drag_start = (0, 0)
        self.drag_moved = False
        self.ticks = 0
        self.after_id = None

        self.menu = self.build_menu(app)

        self.window.bind("<Escape>", lambda _e: app.quit())
        self.canvas.bind("<Button-1>", self.on_press)
        self.canvas.bind("<B1-Motion>", self.on_drag)
        self.canvas.bind("<ButtonRelease-1>", self.on_release)
        self.canvas.bind("<Button-3>", self.on_menu)
        self.canvas.bind("<Button-2>", self.on_menu)  # macOS 오른쪽 클릭

        self.place()
        self.after_id = self.window.after(TICK_MS, self.tick)

    # --- 조작 -----------------------------------------------------------
    def build_menu(self, app):
        """우클릭 메뉴. 명령줄 없이도 웬만한 건 여기서 다 된다."""
        menu = pokemon_menu(self.window)
        menu.add_command(label="●  포켓몬 센터  ●", state="disabled")
        menu.add_separator()

        choose = pokemon_menu(menu)
        self.pet_purchase_indices = []
        # 진화해야 만날 수 있는 포켓몬은 목록에 넣지 않는다.
        for key in base_species():
            choose.add_command(
                label="",
                command=lambda key=key: app.buy_pet(key),
            )
            self.pet_purchase_indices.append((key, choose.index("end")))
        choose.add_separator()
        choose.add_command(label="", command=app.buy_random_pet)
        self.random_purchase_index = choose.index("end")
        self.pet_purchase_menu = choose
        menu.add_cascade(label="포켓몬 구매", menu=choose)
        menu.add_command(label="이 포켓몬 보내주기", command=self.release)

        # 먹이와 진화 아이템은 모두가 공유한다. 메뉴를 열 때마다 수량을 갱신한다.
        shop = pokemon_menu(menu)
        shop.add_command(label="", command=app.buy_food)
        self.food_buy_index = shop.index("end")
        shop.add_command(label="", command=app.buy_growth_drop)
        self.drop_buy_index = shop.index("end")
        menu.add_cascade(label="", menu=shop)
        self.shop_index = menu.index("end")
        menu.add_command(label="", command=lambda: app.feed_pet(self))
        self.feed_index = menu.index("end")

        menu.add_command(label="주식시장 열기", command=app.open_stock_overlay)

        # 진화하는 포켓몬이면 여기에 진행 상황을 보여 준다.
        self.evolve_index = None
        if self.next_key:
            menu.add_command(label="", state="disabled", command=self.start_evolving)
            self.evolve_index = menu.index("end")
        menu.configure(postcommand=self.refresh_menu)
        menu.add_separator()

        sizes = pokemon_menu(menu)
        for label, value in SIZE_CHOICES:
            sizes.add_radiobutton(
                label=label, value=value, variable=app.scale_var,
                command=lambda v=value: app.set_scale(v),
            )
        menu.add_cascade(label="크기", menu=sizes)

        speeds = pokemon_menu(menu)
        for label, value in SPEED_CHOICES:
            speeds.add_radiobutton(
                label=label, value=value, variable=app.speed_var,
                command=lambda v=value: app.set_speed(v),
            )
        menu.add_cascade(label="속도", menu=speeds)

        menu.add_checkbutton(
            label="잠시 멈춤", variable=app.pause_var, command=app.toggle_pause
        )

        if app.system == "Windows":
            menu.add_separator()
            menu.add_checkbutton(
                label="윈도우 시작할 때 실행",
                variable=app.autostart_var,
                command=app.toggle_autostart,
            )

        menu.add_separator()
        menu.add_command(label="전부 종료", command=app.quit)
        return menu

    def on_press(self, event):
        """누른 순간. 이 자리를 기억해 두고 끌기를 시작한다."""
        if self.state == "gone":
            return
        # 테두리 없는 창은 기본적으로 포커스를 받지 않아, 클릭했을 때만 키 입력을 받게 한다.
        try:
            self.window.focus_force()
        except tk.TclError:
            pass
        if self.evolving:
            return               # 진화하는 동안에는 건드릴 수 없다
        # 낮잠이나 장난 중에도 손에 들면 바로 평소 상태로 돌아온다.
        if self.move == "walk":
            self.set_state("walk")
        self.dragging = True
        self.drag_moved = False
        self.drag_start = (event.x_root, event.y_root)
        self.drag_offset = (
            event.x_root - int(self.x),
            event.y_root - (self.base_y - int(self.lift)),
        )
        self.vertical_speed = 0.0

    def on_drag(self, event):
        """누른 채로 움직이면 포켓몬이 손을 따라온다."""
        if not self.dragging or self.state == "gone":
            return
        if (abs(event.x_root - self.drag_start[0]) > DRAG_SLACK
                or abs(event.y_root - self.drag_start[1]) > DRAG_SLACK):
            self.drag_moved = True

        offset_x, offset_y = self.drag_offset
        # 바닥(0)과 화면 위쪽 사이로 제한한다. --offset 을 크게 줘서 바닥이
        # 화면 위로 올라가 버린 경우에도 음수가 되지 않도록 천장을 0 이상으로 둔다.
        ceiling = self.ceiling()
        self.x = min(max(0, event.x_root - offset_x), self.max_x)
        self.lift = min(max(0.0, self.base_y - (event.y_root - offset_y)), ceiling)
        self.place()

    def on_release(self, _event):
        """놓으면 떨어진다. 거의 움직이지 않았으면 그냥 클릭으로 보고 폴짝 뛴다."""
        if not self.dragging or self.state == "gone":
            return
        self.dragging = False
        if self.move == "float":
            # 떠다니는 포켓몬은 떨어지지 않는다. 놓은 자리에서 이어서 떠 있다가
            # 스스로 제 높이로 돌아간다.
            self.float_base = self.lift
            self.float_phase = 0.0
            if self.drag_moved:
                self.float_target = self.pick_float_height()
                self.float_timer = random.uniform(*FLOAT_RETARGET)
            else:
                # 쓰다듬으면 기분 좋게 조금 더 떠오른다.
                self.float_target = min(self.lift + FLOAT_NUDGE, self.ceiling())
                self.float_timer = max(self.float_timer, 1.2)
                self.petted()
            return
        if self.drag_moved:
            self.vertical_speed = 0.0
        else:
            self.vertical_speed = JUMP_SPEED
            self.petted()

    def petted(self):
        """쓰다듬었을 때. 하트가 뜨고 친밀도가 오른다."""
        self.spawn_emote("heart")
        if not self.next_key or self.evolving:
            return
        self.friendship = min(EVOLVE_PET_NEED, self.friendship + EVOLVE_PER_PET)

    def fed(self):
        """포켓푸드를 먹었을 때. 하트가 뜨고 친밀도가 크게 오른다."""
        self.spawn_emote("heart")
        if not self.next_key or self.evolving:
            return
        self.friendship = min(EVOLVE_PET_NEED, self.friendship + FOOD_FRIENDSHIP)

    def refresh_menu(self):
        """메뉴를 열 때마다 상점과 진화 항목을 지금 상태로 고쳐 쓴다."""
        self.menu.entryconfigure(
            self.shop_index, label="상점 (보유 %s)" % format_won(self.app.coins)
        )
        self.menu.nametowidget(self.menu.entrycget(self.shop_index, "menu")).entryconfigure(
            self.food_buy_index,
            label="포켓푸드 구매 — %s" % format_won(FOOD_COST),
            state="normal" if self.app.coins >= FOOD_COST else "disabled",
        )
        self.menu.nametowidget(self.menu.entrycget(self.shop_index, "menu")).entryconfigure(
            self.drop_buy_index,
            label="성장의 물방울 구매 — %s" % format_won(GROWTH_DROP_COST),
            state="normal" if self.app.coins >= GROWTH_DROP_COST else "disabled",
        )
        self.menu.entryconfigure(
            self.feed_index, label="포켓푸드 주기 (%d개)" % self.app.food,
            state="normal" if self.app.food else "disabled",
        )
        for key, index in self.pet_purchase_indices:
            self.pet_purchase_menu.entryconfigure(
                index, label="%s — %s" % (POKEMON[key].name_ko, format_won(POKEMON_PRICE)),
                state="normal" if self.app.coins >= POKEMON_PRICE else "disabled",
            )
        self.pet_purchase_menu.entryconfigure(
            self.random_purchase_index, label="무작위 — %s" % format_won(POKEMON_PRICE),
            state="normal" if self.app.coins >= POKEMON_PRICE else "disabled",
        )
        if self.evolve_index is None:
            return
        name = POKEMON[self.next_key].name_ko
        if self.evolving:
            label = "진화하는 중..."
            state = "disabled"
        elif self.can_evolve():
            label = "%s로 진화하기" % name
            state = "normal"
        else:
            needs = []
            if self.pets_left():
                needs.append("%d번 더 쓰다듬기" % self.pets_left())
            if self.walk_left():
                needs.append("%dpx 더 산책" % self.walk_left())
            if not self.app.growth_drops:
                needs.append("성장의 물방울 1개")
            label = "%s까지 %s" % (name, " · ".join(needs))
            state = "disabled"
        self.menu.entryconfigure(self.evolve_index, label=label, state=state)

    def on_menu(self, event):
        try:
            self.menu.tk_popup(event.x_root, event.y_root)
        finally:
            self.menu.grab_release()

    def release(self):
        self.app.remove_pet(self)

    def destroy(self):
        """예약된 콜백까지 정리하고 창을 닫는다."""
        self.state = "gone"
        if self.after_id is not None:
            try:
                self.window.after_cancel(self.after_id)
            except tk.TclError:
                pass
            self.after_id = None
        try:
            self.window.destroy()
        except tk.TclError:
            pass

    # --- 움직임 ---------------------------------------------------------
    def set_state(self, state):
        was_walking = self.state == "walk"
        self.state = state
        self.napping = False
        self.idle_action = None
        if state == "walk" and not was_walking:
            self.walk_speed = 0.0
        if state == "idle":
            if random.random() < NAP_CHANCE:
                # 가끔은 길게 낮잠을 잔다. 이때 머리 위로 Zzz 가 올라간다.
                self.state_left = random.uniform(*NAP_SECONDS)
                self.napping = True
                self.zzz_timer = 0.35
            else:
                self.state_left = random.uniform(0.8, 3.0)
                self.start_idle_action()

    def start_idle_action(self):
        """포켓몬마다 다른 짧은 대기 모션을 시작한다."""
        if random.random() >= IDLE_ACTION_CHANCE:
            return
        action = IDLE_ACTIONS.get(self.pokemon.key)
        if action is None:
            return
        self.idle_action = action[0]
        self.idle_action_left = random.uniform(*IDLE_ACTION_SECONDS)
        self.idle_effect_left = 0.0
        self.idle_phase = 0.0

    def update_idle_action(self, dt):
        """반짝임·불꽃·잎·거품 같은 포켓몬별 대기 효과를 갱신한다."""
        if self.idle_action is None:
            return
        self.idle_phase += dt
        self.idle_action_left -= dt
        self.idle_effect_left -= dt
        if self.idle_effect_left <= 0:
            self.idle_effect_left = IDLE_EFFECT_EVERY
            self.spawn_idle_effect()
        if self.idle_action_left <= 0:
            self.idle_action = None

    def spawn_idle_effect(self):
        action = IDLE_ACTIONS.get(self.pokemon.key)
        if action is None or action[0] == "wiggle":
            return
        self.effects.append({
            "kind": action[0],
            "x": self.margin_x + self.width * (0.48 + random.random() * 0.24),
            "y": self.margin_top + self.height * 0.16,
            "vx": -8 + random.random() * 16,
            "vy": -18.0,
            "life": EMOTE_LIFE,
            "color": action[1],
        })

    def can_greet(self):
        """다른 포켓몬을 만났을 때 인사할 수 있는 상태인지."""
        return (self.state == "walk" and not self.dragging and not self.evolving
                and (self.move == "float" or self.lift <= 0) and self.greeting_left <= 0
                and self.greeting_cooldown <= 0)

    def start_greeting(self, partner):
        """가까이 온 포켓몬을 바라보고 잠깐 인사한다."""
        self.state = "greet"
        self.walk_speed = 0.0
        self.napping = False
        self.idle_action = None
        self.greeting_left = GREETING_SECONDS
        self.greeting_phase = 0.0
        self.greeting_cooldown = GREETING_COOLDOWN
        self.greeting_leads = self.x < partner.x
        self.greeting_talk_turn = -1
        self.direction = 1 if partner.x > self.x else -1

    def greeting_speaking(self):
        """대화 박자에서 지금 말풍선을 띄울 쪽인지."""
        turn = int(self.greeting_phase / GREETING_TALK_EVERY) % 2
        return self.greeting_left > 0 and (turn == 0) == self.greeting_leads

    def greeting_step(self, dt):
        self.greeting_left -= dt
        self.greeting_phase += dt
        turn = int(self.greeting_phase / GREETING_TALK_EVERY)
        if turn != self.greeting_talk_turn:
            self.greeting_talk_turn = turn
            if self.greeting_speaking():
                self.spawn_emote("talk")
        if self.greeting_left <= 0:
            self.set_state("walk")

    def advance_walk(self, distance):
        """걸은 만큼 옮기고, 실제 이동 거리로 보행 프레임과 산책을 진행한다."""
        before_x = self.x
        self.x += self.direction * distance
        self.x = min(max(0.0, self.x), self.max_x)
        actual = abs(self.x - before_x)
        self.gait_distance += actual
        self.walked = min(EVOLVE_WALK_NEED, self.walked + actual)
        self.app.earn_walk_coins(actual)
        return actual

    def begin_stop(self, kind, turn_direction=None):
        """감속한 뒤 쉬거나 장난치거나 방향을 바꾼다."""
        self.state = "slow_stop"
        self.stop_kind = kind
        self.turn_direction = self.direction if turn_direction is None else turn_direction

    def finish_stop(self):
        """감속이 끝났을 때 다음 동작으로 넘긴다."""
        kind = self.stop_kind
        self.stop_kind = None
        if kind == "turn":
            self.state = "turn"
            self.state_left = TURN_PAUSE_SEC
            self.land_squash = max(self.land_squash, TURN_PAUSE_SEC)
        elif kind == "play":
            self.start_playing()
        else:
            self.set_state("idle")

    def slow_stop_step(self, dt):
        """지금 속도에서 부드럽게 멈춘다."""
        before_speed = self.walk_speed
        self.walk_speed = max(0.0, self.walk_speed - WALK_DECEL * dt)
        self.advance_walk((before_speed + self.walk_speed) * 0.5 * dt)
        if self.walk_speed <= 0:
            self.finish_stop()

    def turn_step(self, dt):
        """한 박자 멈춘 뒤 새 방향으로 걷기 시작한다."""
        self.state_left -= dt
        if self.state_left <= 0:
            self.direction = self.turn_direction
            self.set_state("walk")

    def walk_step(self, dt):
        """가속하며 걷고, 실제 이동 거리에 맞춰 발 프레임을 진행한다."""
        self.walk_speed = min(self.speed, self.walk_speed + WALK_ACCEL * dt)
        intended = self.walk_speed * dt
        actual = self.advance_walk(intended)
        if actual + 0.01 < intended:
            self.begin_stop("turn", -self.direction)
        elif random.random() < 0.004:
            self.begin_stop("turn", -self.direction)
        elif random.random() < 0.005:
            self.begin_stop("play" if random.random() < PLAY_CHANCE else "idle")

    def start_playing(self):
        """걷는 포켓몬이 가끔 하는 짧은 제자리 점프 놀이를 시작한다."""
        self.state = "play_wait"
        self.state_left = PLAY_WAIT_SEC
        self.play_hops = 0
        self.napping = False

    def play_step(self, dt):
        """잠깐 뜸을 들인 뒤 두 번 폴짝 뛰고 다시 걷는다."""
        if self.state == "play_air":
            if self.lift > 0:
                return
            if self.play_hops >= PLAY_HOPS:
                self.set_state("walk")
                return
            self.state = "play_wait"
            self.state_left = PLAY_WAIT_SEC
            if random.random() < PLAY_TURN_CHANCE:
                self.direction = -self.direction
            return

        self.state_left -= dt
        if self.state_left <= 0:
            self.play_hops += 1
            self.vertical_speed = PLAY_HOP_SPEED
            self.state = "play_air"

    def tick(self):
        if self.state == "gone":
            return
        dt = TICK_MS / 1000.0
        self.ticks += 1

        if self.dragging:
            # 손에 들려 있는 동안에는 스스로 움직이지 않는다.
            # 다만 다른 창에 가리지 않도록 맨 앞 주장은 계속한다.
            if self.ticks % TOPMOST_TICKS == 0:
                self.raise_above_all()
            self.draw()
            self.after_id = self.window.after(TICK_MS, self.tick)
            return

        if self.evolving:
            # 진화하는 동안에는 제자리에서 번쩍이기만 한다.
            if self.evolve_tick(dt):
                self.app.finish_evolving(self)
                return
        elif self.app.paused:
            pass                     # 잠시 멈춤: 제자리에서 가만히
        elif self.greeting_left > 0:
            self.greeting_step(dt)
        elif self.app.start_greeting_near(self):
            self.greeting_step(dt)
        elif self.move == "hop":
            self.hop_step(dt)
        elif self.move == "float":
            self.float_step(dt)
        elif self.state == "slow_stop":
            self.slow_stop_step(dt)
        elif self.state == "turn":
            self.turn_step(dt)
        elif self.state.startswith("play_"):
            self.play_step(dt)
        elif self.state == "walk":
            self.walk_step(dt)
        else:
            self.state_left -= dt
            if self.state_left <= 0:
                self.set_state("walk")

        # 떠 있으면 중력으로 끌어내린다. 떠다니는 포켓몬은 예외다.
        if (not self.evolving and self.move != "float"
                and (self.lift > 0 or self.vertical_speed != 0)):
            self.vertical_speed -= GRAVITY * dt
            self.lift += self.vertical_speed * dt
            if self.lift <= 0:
                # 세게 떨어졌으면 발밑에 먼지가 인다.
                if -self.vertical_speed >= LAND_DUST_SPEED:
                    self.spawn_dust()
                    self.land_squash = LAND_SQUASH_TIME
                self.lift = 0.0
                self.vertical_speed = 0.0

        self.update_timers(dt)
        self.update_idle_action(dt)
        self.update_effects(dt)
        if self.napping:
            self.zzz_timer -= dt
            if self.zzz_timer <= 0:
                self.zzz_timer = ZZZ_EVERY
                self.spawn_emote("zzz")

        # 다른 창을 클릭해도 항상 맨 앞에 남도록 자주 다시 주장한다.
        if self.ticks % TOPMOST_TICKS == 0:
            self.raise_above_all()

        self.draw()
        self.place()
        self.after_id = self.window.after(TICK_MS, self.tick)

    # --- 효과 ------------------------------------------------------------
    def spawn_dust(self):
        """착지할 때 발밑에서 먼지가 인다."""
        feet_x = self.margin_x + self.width / 2.0
        feet_y = self.margin_top + self.hop + self.height
        for index in range(6):
            side = -1 if index % 2 == 0 else 1
            spread = 0.4 + random.random() * 0.9
            self.effects.append({
                "kind": "dust",
                "x": feet_x + side * self.width * 0.18 * spread,
                "y": feet_y - self.dot,
                "vx": side * (30 + random.random() * 55),
                "vy": -(20 + random.random() * 45),
                "life": DUST_LIFE * (0.7 + random.random() * 0.6),
                "color": "#f2f2f2" if index % 2 else "#c0c0c0",
            })

    def spawn_emote(self, kind):
        """머리 위로 하트·Zzz·말풍선을 띄운다."""
        self.effects.append({
            "kind": kind,
            "x": self.margin_x + self.width * (0.55 + random.random() * 0.2),
            "y": float(self.margin_top),
            "vx": 8 + random.random() * 10,
            "vy": -28.0,
            "life": EMOTE_LIFE,
            "color": "#ff5f83" if kind == "heart" else "#ffffff",
        })

    def update_timers(self, dt):
        """눈 깜빡임, 착지 눌림, 숨쉬기, 버둥거림 박자를 센다."""
        if self.land_squash > 0:
            self.land_squash -= dt
        if self.greeting_cooldown > 0:
            self.greeting_cooldown -= dt

        if self.dragging:
            self.wiggle += dt
        else:
            self.wiggle = 0.0

        if self.napping:
            self.breath += dt
        else:
            self.breath = 0.0

        if self.blinking > 0:
            self.blinking -= dt
        elif self.lift <= 0 and not self.dragging:
            self.blink_timer -= dt
            if self.blink_timer <= 0:
                self.blinking = BLINK_TIME
                self.blink_timer = random.uniform(*BLINK_EVERY)

    def choose_pose(self):
        """지금 상황에 맞는 자세 이름. 없으면 None(평소 프레임)."""
        if self.dragging:
            return None
        # 떠다니는 포켓몬은 늘 공중에 있으므로 그것만으로 늘어나지는 않는다.
        if self.move != "float" and self.lift > self.dot:
            return "stretch"
        if self.land_squash > 0:
            return "squash"
        if self.napping and int(self.breath / BREATH_SEC) % 2 == 1:
            return "squash"
        if self.greeting_left > 0:
            return "stretch" if self.greeting_speaking() else "squash"
        if self.idle_action is not None and int(self.idle_phase / 0.22) % 2:
            return "stretch" if self.idle_action in ("spark", "flame", "twinkle") else "squash"
        if self.blinking > 0:
            return "blink"
        return None

    def update_effects(self, dt):
        alive = []
        for effect in self.effects:
            effect["life"] -= dt
            if effect["life"] <= 0:
                continue
            effect["x"] += effect["vx"] * dt
            effect["y"] += effect["vy"] * dt
            if effect["kind"] == "dust":
                effect["vy"] += EFFECT_GRAVITY * dt
            alive.append(effect)
        self.effects = alive

    def draw_effects(self):
        """효과를 캔버스에 사각형으로 찍는다. 매 프레임 지우고 다시 그린다."""
        self.canvas.delete("effect")
        for effect in self.effects:
            dot = self.dot
            if effect["kind"] == "dust":
                # 사라질수록 작아진다
                size = max(1, int(dot * (0.6 + 0.8 * effect["life"] / DUST_LIFE)))
                self.canvas.create_rectangle(
                    effect["x"], effect["y"], effect["x"] + size, effect["y"] + size,
                    fill=effect["color"], outline="", tags="effect",
                )
                continue

            dots = IDLE_DOTS.get(effect["kind"])
            if dots is None:
                dots = HEART_DOTS if effect["kind"] == "heart" else (
                    TALK_DOTS if effect["kind"] == "talk" else ZZZ_DOTS
                )
            # 절반쯤 남으면 깜빡이며 사라진다
            if effect["life"] < EMOTE_LIFE * 0.35 and int(effect["life"] * 20) % 2 == 0:
                continue
            for offset_x, offset_y in dots:
                left = effect["x"] + offset_x * dot
                top = effect["y"] + offset_y * dot
                self.canvas.create_rectangle(
                    left, top, left + dot, top + dot,
                    fill=effect["color"], outline="", tags="effect",
                )

    def evolution_images(self):
        """진화할 때 번갈아 보여 줄 하얀 실루엣 둘.

        지금 모습과 진화한 모습의 윤곽만 새하얗게 칠한 것이다. 한 창 안에서
        번갈아 보여 주므로, 그림마다 가운데·아래에 맞춰 놓을 위치도 함께 준다.
        """
        if self.white is None:
            after = self.app.get_white(POKEMON[self.next_key])
            sample = after["right"]
            self.white = [
                (self.app.get_white(self.pokemon), self.own_dx, self.own_dy),
                (after,
                 (self.width - sample.width()) // 2,
                 self.height - sample.height()),
            ]
        return self.white

    def can_evolve(self):
        """진화할 준비가 됐는지."""
        return (
            bool(self.next_key)
            and self.friendship >= EVOLVE_PET_NEED
            and self.walked >= EVOLVE_WALK_NEED
            and self.app.growth_drops > 0
            and not self.evolving
        )

    def pets_left(self):
        """진화까지 몇 번 더 쓰다듬어야 하는지."""
        return max(0, int(-(-(EVOLVE_PET_NEED - self.friendship) // EVOLVE_PER_PET)))

    def walk_left(self):
        """진화까지 몇 픽셀을 더 산책해야 하는지."""
        return max(0, int(math.ceil(EVOLVE_WALK_NEED - self.walked)))

    def start_evolving(self):
        """진화 연출을 시작한다. 끝나면 앱이 새 포켓몬으로 갈아 끼운다."""
        if not self.can_evolve():
            return
        self.evolution_images()
        self.app.growth_drops -= 1
        self.app.save_settings()
        self.evolving = True
        self.evolve_step = 0
        self.evolve_timer = EVOLVE_FIRST_SEC
        self.dragging = False

    def evolve_flash_seconds(self, step):
        """번쩍임 간격. 갈수록 짧아져 점점 빨라진다.

        EVOLVE_FLASHES 는 2 이상이어야 한다.
        """
        share = min(1.0, step / float(EVOLVE_FLASHES - 1))
        return EVOLVE_FIRST_SEC + (EVOLVE_LAST_SEC - EVOLVE_FIRST_SEC) * share

    def evolve_tick(self, dt):
        """번쩍임을 한 칸 진행한다. 다 끝났으면 True."""
        self.evolve_timer -= dt
        if self.evolve_timer > 0:
            return False
        self.evolve_step += 1
        if self.evolve_step > EVOLVE_FLASHES:
            return True
        if self.evolve_step == EVOLVE_FLASHES:
            self.evolve_timer = EVOLVE_HOLD_SEC       # 마지막엔 새하얗게 머문다
        else:
            self.evolve_timer = self.evolve_flash_seconds(self.evolve_step)
        return False

    def ceiling(self):
        """올라갈 수 있는 가장 높은 곳. 창이 화면 위로 나가지 않게 한다."""
        return max(0.0, float(self.base_y))

    def pick_float_height(self):
        """떠 있을 높이를 하나 고른다. 화면이 낮으면 그만큼 낮게 잡는다."""
        low, high = FLOAT_HEIGHT
        high = min(high, self.ceiling())
        low = min(low, high)
        return random.uniform(low, high)

    def float_step(self, dt):
        """뮤처럼 바닥을 딛지 않고 공중을 떠다닌다.

        가로로는 느긋하게 흘러 다니고, 세로로는 목표 높이를 이따금 새로 골라
        스르르 옮겨 가면서 그 위에서 살랑살랑 위아래로 흔들린다. 중력은 받지
        않으므로 걷는 포켓몬처럼 떨어지지 않는다.
        """
        self.anim_time += dt

        if self.state == "walk":
            self.x += self.direction * self.speed * FLOAT_SPEED * dt
            if self.x <= 0:
                self.x = 0
                self.direction = 1
            elif self.x >= self.max_x:
                self.x = self.max_x
                self.direction = -1
            elif random.random() < FLOAT_TURN_CHANCE:
                self.direction = -self.direction
            if random.random() < FLOAT_STOP_CHANCE:
                self.set_state("idle")
        else:
            self.state_left -= dt
            if self.state_left <= 0:
                self.set_state("walk")

        self.float_timer -= dt
        if self.float_timer <= 0:
            self.float_target = self.pick_float_height()
            self.float_timer = random.uniform(*FLOAT_RETARGET)

        # 목표 높이로 스르르 (한 틱에 다 가지 않도록 1.0 을 넘기지 않는다)
        self.float_base += (self.float_target - self.float_base) * min(1.0, FLOAT_EASE * dt)

        self.float_phase += dt
        bob = math.sin(self.float_phase / FLOAT_BOB_SEC * 2 * math.pi)
        wanted = self.float_base + bob * self.dot * FLOAT_BOB_DOTS
        self.lift = min(max(0.0, wanted), self.ceiling())

    def hop_step(self, dt):
        """메타몽처럼 폴짝폴짝 뛰어서 이동한다.

        웅크렸다가(crouch) 튀어올라(air) 앞으로 나아가고, 착지해서 납작해졌다가
        (land) 잠시 쉰 뒤(rest) 다시 뛴다. 공중에 있는 동안에만 앞으로 간다.
        """
        if self.lift > 0:
            self.hop_state = "air"
            self.x += self.direction * self.speed * HOP_BOOST * dt
            if self.x <= 0:
                self.x = 0
                self.direction = 1
            elif self.x >= self.max_x:
                self.x = self.max_x
                self.direction = -1
            return

        if self.hop_state == "air":          # 방금 착지했다
            self.hop_state = "land"
            self.hop_timer = HOP_LAND_SEC
            return

        self.hop_timer -= dt
        if self.hop_timer > 0:
            return

        if self.hop_state == "land":
            self.hop_state = "rest"
            self.hop_timer = random.uniform(*HOP_REST)
            self.start_idle_action()
            if random.random() < HOP_TURN_CHANCE:
                self.direction = -self.direction
        elif self.hop_state == "rest":
            self.hop_state = "crouch"
            self.hop_timer = HOP_CROUCH_SEC
        else:                                 # crouch
            self.vertical_speed = HOP_SPEED
            self.hop_state = "air"

    def hop_frame(self):
        """[평소, 웅크림, 늘어남] 중 지금 상태에 맞는 프레임."""
        if self.hop_state == "air":
            index = 2
        elif self.hop_state in ("crouch", "land"):
            index = 1
        else:
            index = 0
        return min(index, self.frame_count - 1)

    def walk_frame(self):
        """실제 걸은 거리에 맞는 보행 프레임."""
        phase = self.walk_phase()
        return int(phase * self.frame_count / WALK_SUBSTEPS)

    def walk_phase(self):
        """한 걸음 안에서 몸이 어디까지 올라왔는지(8단계)."""
        return int(self.gait_distance / WALK_STRIDE * WALK_SUBSTEPS) % WALK_SUBSTEPS

    def walk_bob(self):
        """발을 드는 중에는 부드럽게 올라갔다가, 디딜 때 다시 내려온다."""
        return int(self.bounce_px * WALK_BOB[self.walk_phase()] + 0.5)

    def raise_above_all(self):
        """포커스를 빼앗지 않으면서 창을 최상위로 올린다."""
        if self.app.system == "Windows":
            try:
                hwnd = int(self.window.wm_frame(), 16)
                user32 = ctypes.windll.user32
                user32.SetWindowPos.argtypes = [
                    ctypes.c_void_p, ctypes.c_void_p,
                    ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
                    ctypes.c_uint,
                ]
                user32.SetWindowPos(
                    ctypes.c_void_p(hwnd),
                    ctypes.c_void_p(HWND_TOPMOST),
                    0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE,
                )
                return
            except Exception:
                pass
        try:
            self.window.wm_attributes("-topmost", True)
        except tk.TclError:
            pass

    def draw(self):
        if self.evolving:
            self.draw_evolving()
            return
        facing = "right" if self.direction > 0 else "left"
        if self.dragging:
            frame = 0
        elif self.move == "hop":
            frame = self.hop_frame()
        elif self.move == "float":
            frame = int(self.anim_time / FLOAT_STEP_SEC) % self.frame_count
        elif self.state in ("walk", "slow_stop"):
            frame = self.walk_frame()
        else:
            frame = 0

        # 상황에 맞는 자세가 있으면 그것을, 없으면 평소 프레임을 쓴다.
        pose = self.choose_pose()
        image = None
        if pose:
            image = self.images["pose_" + facing].get(pose)
        if image is None:
            image = self.images[facing][frame]
        self.canvas.itemconfigure(self.sprite, image=image)

        # 홀수 프레임에서 살짝 튀어올라 걷는 느낌을 준다.
        # 뛰어다니는 포켓몬은 점프 자체가 움직임이라 흔들지 않는다.
        walking = self.move == "walk" and self.state in ("walk", "slow_stop")
        bounce = self.walk_bob() if walking and pose is None else 0
        # 들려 있으면 버둥거린다.
        sway = self.dot if (self.dragging and int(self.wiggle / WIGGLE_SEC) % 2) else 0
        if self.idle_action == "wiggle" and int(self.idle_phase / WIGGLE_SEC) % 2:
            sway += self.dot
        greet_bob = int(self.dot * 0.45) if self.greeting_speaking() else 0
        self.canvas.coords(
            self.sprite,
            self.margin_x + self.own_dx + sway,
            self.margin_top + self.hop + self.own_dy - bounce - greet_bob,
        )
        self.draw_effects()

    def draw_evolving(self):
        """진화 연출. 지금 모습과 진화한 모습을 번갈아 하얗게 보여 준다."""
        before, after = self.evolution_images()
        # 마지막 한 박자는 진화한 모습으로 새하얗게 머문다.
        images, offset_x, offset_y = after if self.evolve_step % 2 else before
        if self.evolve_step >= EVOLVE_FLASHES:
            images, offset_x, offset_y = after
        facing = "right" if self.direction > 0 else "left"
        self.canvas.itemconfigure(self.sprite, image=images[facing])
        self.canvas.coords(
            self.sprite,
            self.margin_x + offset_x, self.margin_top + self.hop + offset_y,
        )
        self.draw_effects()

    def place(self):
        y = self.base_y - int(self.lift)
        self.window.geometry("+%d+%d" % (int(self.x), y))


class StockOverlay:
    """주가·보유량·최근 가격 그래프를 한 창에 보여 주는 오버레이."""

    def __init__(self, app):
        self.app = app
        self.window = tk.Toplevel(app.root)
        self.window.overrideredirect(True)
        self.window.wm_attributes("-topmost", True)
        self.window.configure(bg=MENU_RED, padx=3, pady=3)
        self.window.geometry("710x525+%d+%d" % (
            (app.screen_width - 710) // 2, max(20, (app.screen_height - 525) // 3)
        ))

        body = tk.Frame(self.window, bg=MENU_CREAM)
        body.pack(fill="both", expand=True)
        title = tk.Frame(body, bg=MENU_RED, height=48)
        title.pack(fill="x")
        title_label = tk.Label(
            title, text="●  포켓몬 주식시장  ●", bg=MENU_RED, fg="white",
            font=("Malgun Gothic", 13, "bold"), pady=9,
        )
        title_label.pack(side="left", padx=14)
        tk.Button(
            title, text="×", command=self.close, bg=MENU_RED, fg="white",
            activebackground="#aa2028", activeforeground="white", bd=0,
            font=("Malgun Gothic", 14, "bold"), cursor="hand2",
        ).pack(side="right", padx=10)

        self.balance = tk.Label(
            body, bg=MENU_CREAM, fg=MENU_DARK, anchor="w",
            font=("Malgun Gothic", 11, "bold"), padx=16, pady=8,
        )
        self.balance.pack(fill="x")
        cards = tk.Frame(body, bg=MENU_CREAM)
        cards.pack(fill="both", expand=True, padx=10)
        self.rows = []
        for index in range(STOCK_COUNT):
            name = app.stock_name(index)
            card = tk.Frame(cards, bg="#fffdf7", highlightbackground="#d9ad74",
                            highlightthickness=1, width=335, height=124)
            card.grid(row=index // 2, column=index % 2, padx=3, pady=3, sticky="nsew")
            card.grid_propagate(False)
            info = tk.Frame(card, bg="#fffdf7")
            info.pack(fill="x", padx=8, pady=(5, 0))
            name_label = tk.Label(
                info, text=name, bg="#fffdf7", fg=MENU_DARK,
                font=("Malgun Gothic", 10, "bold"),
            )
            name_label.pack(side="left")
            price = tk.Label(info, bg="#fffdf7", fg=MENU_RED,
                             font=("Malgun Gothic", 10, "bold"))
            price.pack(side="right")
            position = tk.Label(card, bg="#fffdf7", fg=MENU_DISABLED, anchor="w",
                                font=("Malgun Gothic", 8), padx=8)
            position.pack(fill="x")
            graph = tk.Canvas(card, width=205, height=50, bg="#fffdf7",
                              highlightthickness=0)
            graph.pack(side="left", padx=(8, 4), pady=(1, 7))
            buttons = tk.Frame(card, bg="#fffdf7")
            buttons.pack(side="right", padx=(0, 8), pady=(1, 7))
            buy = self.make_button(buttons, "매수", MENU_RED,
                                   lambda index=index: self.trade(index, True))
            buy.pack(fill="x", pady=(0, 3))
            sell = self.make_button(buttons, "매도", "#3a81c7",
                                    lambda index=index: self.trade(index, False))
            sell.pack(fill="x")
            self.rows.append((name_label, price, position, graph, buy, sell))

        self.notice = tk.Label(
            body, text="가격은 20초마다 변동합니다", bg=MENU_CREAM,
            fg=MENU_DISABLED, font=("Malgun Gothic", 9), pady=4,
        )
        self.notice.pack()
        self.window.bind("<Escape>", lambda _event: self.close())
        self.window.protocol("WM_DELETE_WINDOW", self.close)
        self.drag_origin = None
        for widget in (title, title_label):
            widget.bind("<ButtonPress-1>", self.begin_drag)
            widget.bind("<B1-Motion>", self.drag)
        self.refresh()

    @staticmethod
    def make_button(parent, label, color, command):
        return tk.Button(
            parent, text=label, command=command, bg=color, fg="white",
            activebackground=color, activeforeground="white", bd=0,
            padx=10, pady=2, font=("Malgun Gothic", 9, "bold"), cursor="hand2",
        )

    def trade(self, index, buying):
        if buying:
            self.app.buy_stock(index)
        else:
            self.app.sell_stock(index)

    def begin_drag(self, event):
        """빨간 제목 영역을 잡은 위치를 기억한다."""
        self.drag_origin = (event.x_root - self.window.winfo_x(),
                            event.y_root - self.window.winfo_y())

    def drag(self, event):
        """제목 영역을 끌면 오버레이가 함께 움직인다."""
        if self.drag_origin is None:
            return
        offset_x, offset_y = self.drag_origin
        self.window.geometry("+%d+%d" % (event.x_root - offset_x, event.y_root - offset_y))

    @staticmethod
    def draw_graph(canvas, values):
        """최근 가격을 카드 안에 작은 선 그래프로 그린다."""
        canvas.delete("all")
        width = int(canvas.cget("width"))
        height = int(canvas.cget("height"))
        low, high = min(values), max(values)
        spread = max(100, high - low)
        for share in (0.25, 0.5, 0.75):
            y = int(height * share)
            canvas.create_line(0, y, width, y, fill="#f0dfc4")
        points = []
        for index, value in enumerate(values):
            x = 4 if len(values) == 1 else 4 + (width - 8) * index / (len(values) - 1)
            y = height - 5 - (height - 10) * (value - low) / spread
            points.extend((x, y))
        colour = "#2f9b67" if values[-1] >= values[0] else MENU_RED
        if len(points) >= 4:
            canvas.create_line(*points, fill=colour, width=3, smooth=True)
        canvas.create_oval(points[-2] - 3, points[-1] - 3,
                           points[-2] + 3, points[-1] + 3,
                           fill=colour, outline=colour)

    def refresh(self):
        self.balance.configure(text="보유금  %s" % format_won(self.app.coins))
        total_value = self.app.stock_portfolio_value()
        self.balance.configure(
            text="보유금  %s   ·   주식 평가액  %s" % (
                format_won(self.app.coins), format_won(total_value)
            )
        )
        for index, (name_label, price_label, position, graph, buy, sell) in enumerate(self.rows):
            name_label.configure(text=self.app.stock_name(index))
            price = self.app.stock_prices[index]
            shares = self.app.stock_shares[index]
            percent = self.app.stock_change_percent(index)
            colour = "#2f9b67" if percent > 0 else MENU_RED if percent < 0 else MENU_DARK
            if self.app.stock_delisted[index]:
                minutes = max(1, int(math.ceil(self.app.stock_relist_seconds[index] / 60.0)))
                price_label.configure(text="상장폐지 · 신규 상장까지 %d분" % minutes, fg=MENU_RED)
                position.configure(text="보유 주식 소멸 · 새 종목을 준비하고 있습니다")
                buy.configure(state="disabled")
                sell.configure(state="disabled")
            elif self.app.stock_halt_seconds[index]:
                price_label.configure(text="거래 일시정지 · %d초" % self.app.stock_halt_seconds[index], fg=MENU_RED)
                position.configure(text=self.app.stock_position_text(index))
                buy.configure(state="disabled")
                sell.configure(state="disabled")
            else:
                price_label.configure(
                    text="%s  ·  %+.1f%%  ·  보유 %d주" % (format_won(price), percent, shares),
                    fg=colour,
                )
                position.configure(text=self.app.stock_position_text(index))
                buy.configure(state="normal" if self.app.coins >= self.app.stock_buy_cost(index) else "disabled")
                sell.configure(state="normal" if shares else "disabled")
            self.draw_graph(graph, self.app.stock_history[index])
        self.notice.configure(text=(self.app.stock_event or "가격은 20초마다 변동합니다")
                              + "  ·  거래 수수료 2%")

    def close(self):
        if self.app.stock_overlay is self:
            self.app.stock_overlay = None
        try:
            self.window.destroy()
        except tk.TclError:
            pass


class App:
    """펫 여러 마리를 관리하는 본체."""

    def __init__(self, args):
        self.root = tk.Tk()
        self.root.withdraw()
        self.system = platform.system()
        self.scale = args.scale
        self.speed = args.speed
        self.offset = args.offset
        self.background = args.bg
        self.screen_width = self.root.winfo_screenwidth()
        self.screen_height = self.root.winfo_screenheight()
        # 기본값은 작업 표시줄 "위"에 올라서기. --on-taskbar 면 표시줄 위를 걷는다.
        self.on_taskbar = args.on_taskbar
        self.ground_y = (
            self.screen_height if args.on_taskbar else work_area_bottom(self.screen_height)
        )
        self.image_cache = {}
        self.white_cache = {}
        self.pets = []
        self.quitting = False
        self.paused = False
        self.heartbeat_id = None
        self.settings_path = args.settings
        self.coins = args.coins
        self.food = args.food
        self.growth_drops = args.growth_drops
        self.stock_prices = list(args.stock_prices)
        self.stock_shares = list(args.stock_shares)
        self.stock_listing_ids = list(args.stock_listing_ids)
        self.stock_delisted = [bool(value) for value in args.stock_delisted]
        self.stock_relist_seconds = list(args.stock_relist_seconds)
        self.stock_average_prices = list(args.stock_average_prices)
        self.stock_halt_seconds = list(args.stock_halt_seconds)
        self.stock_history = [[price] for price in self.stock_prices]
        self.stock_event = ""
        self.stock_overlay = None
        self.market_seconds = 0.0
        self.halt_seconds = 0.0
        self.coin_walk_progress = 0.0
        # 메뉴의 체크/선택 표시를 여러 창이 함께 쓰도록 앱이 들고 있는다.
        self.scale_var = tk.DoubleVar(master=self.root, value=self.scale)
        self.speed_var = tk.DoubleVar(master=self.root, value=self.speed)
        self.pause_var = tk.BooleanVar(master=self.root, value=False)
        self.autostart_var = tk.BooleanVar(master=self.root, value=autostart_enabled())

        for key in args.species:
            self.add_pet(key)
        if not self.pets:
            # 설정이 이상해도 빈 화면으로 남지 않도록 한 마리는 꼭 띄운다.
            self.add_pet("pikachu")

        self.root.protocol("WM_DELETE_WINDOW", self.quit)
        self.root.bind_all("<Escape>", lambda _e: self.quit())

    def current_settings(self):
        """지금 상태를 설정 딕셔너리로."""
        return {
            "species": [pet.pokemon.key for pet in self.pets] or ["pikachu"],
            "scale": self.scale,
            "speed": self.speed,
            "offset": self.offset,
            "on_taskbar": self.on_taskbar,
            "coins": self.coins,
            "food": self.food,
            "growth_drops": self.growth_drops,
            "stock_prices": list(self.stock_prices),
            "stock_shares": list(self.stock_shares),
            "stock_listing_ids": list(self.stock_listing_ids),
            "stock_delisted": [int(value) for value in self.stock_delisted],
            "stock_relist_seconds": list(self.stock_relist_seconds),
            "stock_average_prices": list(self.stock_average_prices),
            "stock_halt_seconds": list(self.stock_halt_seconds),
        }

    def save_settings(self):
        """지금 상태를 파일에 남긴다. 실패해도 그냥 넘어간다."""
        settings_file.save(self.current_settings(), self.settings_path)

    def earn_coins(self, amount):
        """돈을 얻고 설정 파일에도 남긴다."""
        if amount <= 0:
            return
        self.coins += amount
        self.save_settings()

    def earn_walk_coins(self, distance):
        """스스로 걸은 100px마다 100원을 얻는다."""
        self.coin_walk_progress += distance
        amount = int(self.coin_walk_progress // COIN_WALK_DISTANCE)
        if amount:
            self.coin_walk_progress -= amount * COIN_WALK_DISTANCE
            self.earn_coins(amount * COINS_PER_WALK)

    def buy_food(self):
        """포켓푸드를 한 개 산다."""
        if self.coins < FOOD_COST:
            return
        self.coins -= FOOD_COST
        self.food += 1
        self.save_settings()

    def buy_growth_drop(self):
        """진화에 필요한 성장의 물방울을 한 개 산다."""
        if self.coins < GROWTH_DROP_COST:
            return
        self.coins -= GROWTH_DROP_COST
        self.growth_drops += 1
        self.save_settings()

    def feed_pet(self, pet):
        """포켓푸드 하나를 골라 둔 포켓몬에게 준다."""
        if not self.food or pet.evolving:
            return
        self.food -= 1
        pet.fed()
        self.save_settings()

    def buy_stock(self, index):
        """현재 가격으로 가상 주식 한 주를 산다."""
        if self.stock_delisted[index] or self.stock_halt_seconds[index]:
            return
        shares = self.stock_shares[index]
        cost = self.stock_buy_cost(index)
        if self.coins < cost:
            return
        self.coins -= cost
        self.stock_average_prices[index] = int(round(
            (self.stock_average_prices[index] * shares + cost) / float(shares + 1)
        ))
        self.stock_shares[index] = shares + 1
        self.save_settings()
        self.refresh_stock_overlay()

    def sell_stock(self, index):
        """현재 가격으로 가상 주식 한 주를 판다."""
        if self.stock_delisted[index] or self.stock_halt_seconds[index] or not self.stock_shares[index]:
            return
        self.stock_shares[index] -= 1
        self.coins += self.stock_sell_proceeds(index)
        if not self.stock_shares[index]:
            self.stock_average_prices[index] = 0
        self.save_settings()
        self.refresh_stock_overlay()

    def stock_listing(self, index):
        """현재 슬롯에 상장된 종목의 이름·시작가·변동폭을 돌려준다."""
        return STOCK_LISTINGS[self.stock_listing_ids[index] % len(STOCK_LISTINGS)]

    def stock_name(self, index):
        return self.stock_listing(index)[0]

    def stock_profile(self, index):
        volatility = self.stock_listing(index)[2]
        return "안정형" if volatility <= 10 else "성장형" if volatility <= 18 else "고위험형"

    @staticmethod
    def stock_fee(amount):
        return int(math.ceil(amount * STOCK_FEE_RATE))

    def stock_buy_cost(self, index):
        return self.stock_prices[index] + self.stock_fee(self.stock_prices[index])

    def stock_sell_proceeds(self, index):
        return max(0, self.stock_prices[index] - self.stock_fee(self.stock_prices[index]))

    def stock_profit_percent(self, index):
        average = self.stock_average_prices[index]
        if not self.stock_shares[index] or average <= 0:
            return 0.0
        return (self.stock_sell_proceeds(index) - average) * 100.0 / average

    def stock_position_text(self, index):
        _name, _starting_price, volatility = self.stock_listing(index)
        shares = self.stock_shares[index]
        if not shares:
            return "%s · 변동폭 ±%d%% · 보유 없음" % (self.stock_profile(index), volatility)
        return "%s · 변동폭 ±%d%% · 평균 %s · 수익 %+.1f%%" % (
            self.stock_profile(index), volatility,
            format_won(self.stock_average_prices[index]), self.stock_profit_percent(index),
        )

    def stock_portfolio_value(self):
        return sum(
            self.stock_sell_proceeds(index) * shares
            for index, shares in enumerate(self.stock_shares)
            if not self.stock_delisted[index]
        )

    def relist_stock(self, index):
        """상장폐지된 슬롯에 임의 성격의 새 포켓몬 종목을 상장한다."""
        candidates = [listing_id for listing_id in range(len(STOCK_LISTINGS))
                      if listing_id != self.stock_listing_ids[index]]
        self.stock_listing_ids[index] = random.choice(candidates)
        _name, starting_price, _volatility = self.stock_listing(index)
        self.stock_prices[index] = starting_price
        self.stock_shares[index] = 0
        self.stock_average_prices[index] = 0
        self.stock_delisted[index] = False
        self.stock_relist_seconds[index] = 0
        self.stock_halt_seconds[index] = 0
        self.stock_history[index] = [starting_price]
        self.stock_event = "%s 신규 상장!" % self.stock_name(index)

    def update_market(self):
        """종목 성격별 등락과 이벤트, 상장폐지·신규 상장을 처리한다."""
        self.stock_event = ""
        event_index = None
        event_percent = 0
        active = [index for index in range(STOCK_COUNT)
                  if not self.stock_delisted[index] and not self.stock_halt_seconds[index]]
        if active and random.random() < STOCK_EVENT_CHANCE:
            event_index = random.choice(active)
            event_name, event_percent = random.choice(
                STOCK_EVENTS[self.stock_listing_ids[event_index] % len(STOCK_EVENTS)]
            )
            self.stock_event = "%s %s  %+.0f%%" % (
                self.stock_name(event_index), event_name, event_percent
            )

        for index in range(STOCK_COUNT):
            if self.stock_delisted[index]:
                self.stock_relist_seconds[index] -= int(MARKET_UPDATE_SEC)
                if self.stock_relist_seconds[index] <= 0:
                    self.relist_stock(index)
                continue
            if self.stock_halt_seconds[index]:
                continue
            _name, _starting_price, volatility = self.stock_listing(index)
            change = random.randint(-volatility, volatility)
            if index == event_index:
                change += event_percent
            price = max(1, int(round(self.stock_prices[index] * (100 + change) / 100.0)))
            if price < 100:
                self.stock_prices[index] = 0
                self.stock_shares[index] = 0
                self.stock_average_prices[index] = 0
                self.stock_delisted[index] = True
                self.stock_relist_seconds[index] = STOCK_RELIST_SECONDS
                self.stock_event = "%s 상장폐지! 보유 주식은 소멸했습니다." % self.stock_name(index)
            else:
                self.stock_prices[index] = price
            self.stock_history[index].append(self.stock_prices[index])
            self.stock_history[index] = self.stock_history[index][-20:]
        if event_index is not None and not self.stock_delisted[event_index]:
            self.stock_halt_seconds[event_index] = STOCK_HALT_SECONDS
            self.stock_event += " · 변동성 완화장치 발동(40초 거래 정지)"
        self.save_settings()
        self.refresh_stock_overlay()

    def stock_change_percent(self, index):
        """그래프에 보이는 기간의 시작 가격과 비교한 등락률."""
        history = self.stock_history[index]
        if not history or history[0] <= 0:
            return 0.0
        return (self.stock_prices[index] - history[0]) * 100.0 / history[0]

    def open_stock_overlay(self):
        """주식시장 오버레이를 하나만 열고, 이미 열려 있으면 앞으로 가져온다."""
        if self.stock_overlay is not None:
            try:
                self.stock_overlay.window.deiconify()
                self.stock_overlay.window.lift()
                self.stock_overlay.window.focus_force()
                self.stock_overlay.refresh()
                return
            except tk.TclError:
                self.stock_overlay = None
        self.stock_overlay = StockOverlay(self)

    def refresh_stock_overlay(self):
        """열려 있는 주식시장에 최신 가격과 보유량을 반영한다."""
        if self.stock_overlay is None:
            return
        try:
            self.stock_overlay.refresh()
        except tk.TclError:
            self.stock_overlay = None

    def set_scale(self, scale):
        """크기를 바꾸고 지금 있는 포켓몬을 그대로 다시 만든다."""
        if scale == self.scale:
            return
        self.scale = scale
        self.scale_var.set(scale)
        self.image_cache.clear()
        self.rebuild()

    def set_speed(self, speed):
        self.speed = speed
        self.speed_var.set(speed)
        for pet in self.pets:
            pet.speed = speed * random.uniform(0.85, 1.15)
        self.save_settings()

    def toggle_pause(self):
        self.paused = bool(self.pause_var.get())

    def rebuild(self):
        """포켓몬을 모두 지웠다가 같은 구성으로 다시 만든다."""
        keys = [pet.pokemon.key for pet in self.pets]
        places = [pet.x for pet in self.pets]
        for pet in list(self.pets):
            pet.destroy()
        self.pets = []
        for key, place in zip(keys, places):
            self.add_pet(key)
            self.pets[-1].x = min(place, self.pets[-1].max_x)
        self.save_settings()

    def sprite_scale(self, pokemon):
        """스프라이트별 확대 배율(도트 하나가 차지할 화면 픽셀 수).

        도트가 촘촘한 그림은 scale_factor 를 작게 잡아 더 작게 그린다.
        정수가 아니어도 되며, 1.5 처럼 딱 떨어지는 값일수록 도트가 고르게 보인다.
        """
        return max(MIN_SPRITE_SCALE, self.scale * pokemon.scale_factor)

    def get_white(self, pokemon):
        """진화할 때 쓰는 하얀 실루엣(캐시). 윤곽만 남기고 전부 하얗게 칠한다."""
        if pokemon.key not in self.white_cache:
            frame = pokemon.frames()[0]
            shape = [
                ["#ffffff" if cell else None for cell in row]
                for row in frame
            ]
            scale = self.sprite_scale(pokemon)
            self.white_cache[pokemon.key] = {
                "right": make_photo(shape, scale, flip=flip_for(pokemon, True),
                                    master=self.root),
                "left": make_photo(shape, scale, flip=flip_for(pokemon, False),
                                   master=self.root),
            }
        return self.white_cache[pokemon.key]

    def get_images(self, pokemon):
        """방향별 걷기 이미지(캐시)."""
        if pokemon.key not in self.image_cache:
            frames = pokemon.frames()
            poses = pokemon.poses()
            if pokemon.move == "walk":
                frames = whole_walk_frames(frames)
                width = len(frames[0][0])
                height = len(frames[0])
                # 눈 깜빡임·착지 같은 자세도 같은 캔버스에 아래로 맞춘다.
                # 걷다가 자세가 바뀌어도 그림이 옆이나 위로 튀지 않는다.
                poses = {
                    name: pad_on_ground(grid, width, height)
                    for name, grid in poses.items()
                }
            scale = self.sprite_scale(pokemon)
            self.image_cache[pokemon.key] = {
                "right": [
                    make_photo(f, scale, flip=flip_for(pokemon, True), master=self.root)
                    for f in frames
                ],
                "left": [
                    make_photo(f, scale, flip=flip_for(pokemon, False), master=self.root)
                    for f in frames
                ],
                "pose_right": {
                    name: make_photo(g, scale, flip=flip_for(pokemon, True), master=self.root)
                    for name, g in poses.items()
                },
                "pose_left": {
                    name: make_photo(g, scale, flip=flip_for(pokemon, False), master=self.root)
                    for name, g in poses.items()
                },
            }
        return self.image_cache[pokemon.key]

    def add_pet(self, key):
        self.pets.append(PokemonPet(self, POKEMON[key]))
        return self.pets[-1]

    def start_greeting_near(self, pet):
        """걷다가 충분히 가까워진 두 포켓몬을 함께 인사시킨다."""
        if not pet.can_greet():
            return False
        center = pet.x + pet.width / 2.0
        for partner in self.pets:
            if partner is pet or not partner.can_greet():
                continue
            partner_center = partner.x + partner.width / 2.0
            if abs(center - partner_center) <= GREETING_DISTANCE:
                pet.start_greeting(partner)
                partner.start_greeting(pet)
                return True
        return False

    def finish_evolving(self, pet):
        """번쩍임이 끝났다. 같은 자리에 진화한 포켓몬을 놓는다."""
        key = pet.next_key
        if key is None:
            return
        where = pet.x
        facing = pet.direction
        index = self.pets.index(pet) if pet in self.pets else len(self.pets)
        if pet in self.pets:
            self.pets.remove(pet)
        pet.destroy()
        grown = PokemonPet(self, POKEMON[key])
        grown.x = min(max(0, where), grown.max_x)
        grown.direction = facing
        grown.place()
        self.pets.insert(index, grown)
        self.save_settings()

    def add_pet_and_save(self, key):
        self.add_pet(key)
        self.save_settings()

    def buy_pet(self, key):
        """두 시간 산책값으로 포켓몬 한 마리를 산다."""
        if self.coins < POKEMON_PRICE:
            return
        self.coins -= POKEMON_PRICE
        self.add_pet(key)
        self.save_settings()

    def buy_random_pet(self):
        """두 시간 산책값으로 무작위 포켓몬 한 마리를 산다."""
        if self.coins < POKEMON_PRICE:
            return
        self.buy_pet(random.choice(base_species()))

    def remove_pet(self, pet):
        if pet in self.pets:
            self.pets.remove(pet)
        pet.destroy()
        if not self.pets:
            self.quit()
        else:
            self.save_settings()

    def toggle_autostart(self):
        """윈도우 시작 프로그램 등록을 켜고 끈다. 실패하면 표시를 되돌린다."""
        wanted = bool(self.autostart_var.get())
        if not set_autostart(wanted):
            self.autostart_var.set(autostart_enabled())

    def quit(self):
        """모든 펫을 정리하고 프로그램을 끝낸다. 여러 번 불러도 안전하다."""
        if self.quitting:
            return
        self.quitting = True
        if self.stock_overlay is not None:
            self.stock_overlay.close()
        for pet in list(self.pets):
            pet.destroy()
        self.pets.clear()
        if self.heartbeat_id is not None:
            try:
                self.root.after_cancel(self.heartbeat_id)
            except tk.TclError:
                pass
            self.heartbeat_id = None
        try:
            self.root.quit()
            self.root.destroy()
        except tk.TclError:
            pass

    def run(self):
        # Ctrl+C 로도 종료할 수 있게 주기적으로 인터프리터에 제어를 넘긴다.
        def heartbeat():
            self.market_seconds += 0.2
            self.halt_seconds += 0.2
            if self.halt_seconds >= 1.0:
                self.halt_seconds -= 1.0
                halted = [index for index, seconds in enumerate(self.stock_halt_seconds) if seconds]
                for index in halted:
                    self.stock_halt_seconds[index] -= 1
                if halted:
                    self.save_settings()
                    self.refresh_stock_overlay()
            if self.market_seconds >= MARKET_UPDATE_SEC:
                self.market_seconds -= MARKET_UPDATE_SEC
                self.update_market()
            self.heartbeat_id = self.root.after(200, heartbeat)

        heartbeat()
        self.root.mainloop()


def sprite_list():
    """어떤 도트가 들어 있는지 한 줄씩. 어느 빌드를 쓰는지 확인할 때 쓴다."""
    lines = []
    for pokemon in POKEMON.values():
        frames = pokemon.frames()
        lines.append(
            "%-12s %-6s %2dx%-2d  %d프레임  %s 보는 그림"
            % (pokemon.key, pokemon.name_ko, len(frames[0][0]), len(frames[0]),
               len(frames), "오른쪽" if pokemon.facing == "right" else "왼쪽")
        )
    return lines


def parse_args(argv=None):
    parser = argparse.ArgumentParser(
        description="화면 하단바 위를 포켓몬이 돌아다니는 프로그램",
    )
    parser.add_argument(
        "-p",
        "--pokemon",
        action="append",
        dest="species",
        default=None,
        metavar="이름",
        help="등장시킬 포켓몬 (%s). 여러 번 쓸 수 있다." % ", ".join(POKEMON),
    )
    parser.add_argument("-c", "--count", type=int, default=None, help="마리 수 (기본 1)")
    parser.add_argument(
        "-s", "--scale", type=float, default=None,
        help="크기 배율 (기본 4.5. 3 을 주면 예전 크기, 6 이면 두 배)",
    )
    parser.add_argument(
        "--speed", type=float, default=None, help="이동 속도(초당 픽셀, 기본 55)"
    )
    parser.add_argument(
        "--offset", type=int, default=None, help="바닥에서 더 띄울 높이(px, 기본 0)"
    )
    parser.add_argument(
        "--settings", default=None, metavar="파일",
        help="설정 파일 경로 (기본: %s)" % settings_file.settings_path(),
    )
    parser.add_argument(
        "--on-taskbar",
        action="store_true",
        help="작업 표시줄 위에 올라서지 않고, 표시줄 위를 그대로 걸어 다닌다",
    )
    parser.add_argument(
        "--bg",
        default="#1e1e1e",
        help="투명 창을 못 쓰는 환경에서 사용할 배경색 (기본 #1e1e1e)",
    )
    parser.add_argument("--list", action="store_true", help="사용 가능한 포켓몬 목록 출력")
    args = parser.parse_args(argv)

    # 명령줄 > 저장된 설정 > 기본값 순으로 채운다.
    saved = settings_file.load(args.settings, known_species=set(POKEMON))
    if args.scale is None:
        args.scale = saved["scale"]
    if args.speed is None:
        args.speed = saved["speed"]
    if args.offset is None:
        args.offset = saved["offset"]
    if not args.on_taskbar:
        args.on_taskbar = saved["on_taskbar"]
    if args.count is None:
        args.count = len(saved["species"]) if not args.species else 1
    args.coins = saved["coins"]
    args.food = saved["food"]
    args.growth_drops = saved["growth_drops"]
    args.stock_prices = saved["stock_prices"]
    args.stock_shares = saved["stock_shares"]
    args.stock_listing_ids = saved["stock_listing_ids"]
    args.stock_delisted = saved["stock_delisted"]
    args.stock_relist_seconds = saved["stock_relist_seconds"]
    args.stock_average_prices = saved["stock_average_prices"]
    args.stock_halt_seconds = saved["stock_halt_seconds"]

    if args.scale <= 0:
        parser.error("--scale 은 0보다 커야 합니다")
    if args.count < 1:
        parser.error("--count 는 1 이상이어야 합니다")

    if args.species:
        unknown = [name for name in args.species if name not in POKEMON]
        if unknown:
            parser.error(
                "모르는 포켓몬입니다: %s (가능: %s)" % (", ".join(unknown), ", ".join(POKEMON))
            )
        while len(args.species) < args.count:
            args.species.append(random.choice(base_species()))
    else:
        args.species = list(saved["species"])
        while len(args.species) < args.count:
            args.species.append(random.choice(base_species()))
        args.species = args.species[: max(1, args.count)]
    return args


def main(argv=None):
    validate_all()
    args = parse_args(argv)
    if args.list:
        for line in sprite_list():
            print(line)
        return 0
    try:
        App(args).run()
    except KeyboardInterrupt:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
