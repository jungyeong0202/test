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
import time
import tkinter as tk
from tkinter import font as tkfont
from tkinter import messagebox, ttk

import settings as settings_file
from sprites import EVOLUTIONS, POKEMON, base_species, validate_all

UI_FONT_FAMILY = "Noto Sans KR"
UI_FONT_PATH = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "assets", "fonts", "NotoSansKR-VF.ttf")
_UI_FONT_REGISTERED = False


def register_ui_font():
    """Windows에 설치하지 않고 현재 프로세스에서만 내장 UI 글꼴을 등록한다."""
    global _UI_FONT_REGISTERED
    if _UI_FONT_REGISTERED:
        return True
    if sys.platform != "win32" or not os.path.isfile(UI_FONT_PATH):
        return False
    try:
        # FR_PRIVATE: 다른 프로그램과 시스템 글꼴 목록에는 노출하지 않는다.
        loaded = ctypes.windll.gdi32.AddFontResourceExW(UI_FONT_PATH, 0x10, 0)
        _UI_FONT_REGISTERED = bool(loaded)
    except (AttributeError, OSError):
        _UI_FONT_REGISTERED = False
    return _UI_FONT_REGISTERED


def configure_tk_ui_fonts(root):
    """명시하지 않은 Tk/ttk 글자도 Noto Sans KR을 상속하게 한다."""
    root.option_add("*Font", (UI_FONT_FAMILY, 9))
    for name in (
            "TkDefaultFont", "TkTextFont", "TkFixedFont", "TkMenuFont",
            "TkHeadingFont", "TkCaptionFont", "TkSmallCaptionFont",
            "TkIconFont", "TkTooltipFont"):
        try:
            tkfont.nametofont(name, root=root).configure(family=UI_FONT_FAMILY)
        except tk.TclError:
            pass

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
EVOLVE_PER_PET = 1.0        # 한 번 쓰다듬을 때마다
# (친밀도, 산책 거리(px), 성장의 물방울). 마지막 값은 이후 3단계 진화에도 쓴다.
EVOLUTION_REQUIREMENTS = ((10.0, 10000.0, 1), (25.0, 40000.0, 3))
EVOLUTION_INCOME_MULTIPLIERS = (1.0, 1.5, 2.25)
# 기존 테스트·외부 스크립트와의 호환용 1단계 기준값.
EVOLVE_PET_NEED, EVOLVE_WALK_NEED, _EVOLVE_DROPS_NEED = EVOLUTION_REQUIREMENTS[0]
DEFAULT_WALK_SPEED = 55.0   # 기본 산책 속도(px/초)
COINS_PER_WALK = 100        # 100px를 걸을 때마다 받는 돈(원)
COIN_WALK_DISTANCE = 100.0  # 이만큼 걸을 때마다 돈을 받는다
POKEMON_PRICE = int(        # 기본 속도로 두 시간 산책해 얻는 돈
    DEFAULT_WALK_SPEED * 2 * 60 * 60 / COIN_WALK_DISTANCE * COINS_PER_WALK
)
FOOD_COST = 8000            # 5분 2배 산책으로 얻는 추가 수입보다 조금 낮춘 가격(원)
FOOD_FRIENDSHIP = 2.0       # 포켓푸드 한 개가 채우는 친밀도
FOOD_SPEED_MULTIPLIER = 2.0
FOOD_BOOST_SECONDS = 5 * 60
GROWTH_DROP_COST = 15000    # 성장의 물방울 한 개 가격(원)
POKEMON_GRADES = {
    "pikachu": ("일반", 1.0, 88), "charmander": ("일반", 1.0, 88),
    "bulbasaur": ("일반", 1.0, 88), "squirtle": ("일반", 1.0, 88),
    "ditto": ("준전설", 1.6, 10), "mew": ("초전설", 2.5, 2),
}
GRADE_DRAW_CHANCES = (("일반", 0.88), ("준전설", 0.10), ("초전설", 0.02))
MARKET_UPDATE_SEC = 10.0    # 이 간격마다 모의 주가가 한 번 변한다
MARKET_OPEN_SECONDS = 60 * 60
MARKET_CLOSED_SECONDS = 5 * 60
STOCK_RELIST_SECONDS = 30 * 60
STOCK_DELIST_PRICE = 600
STOCK_CRISIS_PRICE = 600  # 이 아래에서는 기준가 복귀보다 상장폐지 위험을 우선한다
STOCK_LISTINGS = (
    ("피카츄전기", 1000, 12), ("꼬부기워터", 1800, 7),
    ("이상해씨농장", 2700, 10), ("파이리화력", 1300, 18),
    ("메타몽랩", 2200, 24), ("뮤테크", 3500, 30),
    ("이브이패션", 1600, 15), ("고라파덕물류", 1200, 20),
    ("럭키메디컬", 2400, 9), ("갸라도스해운", 3000, 22),
    ("잠만보식품", 1900, 11), ("팬텀게임즈", 2800, 28),
)
STOCK_COUNT = 6
STOCK_MAX_ORDER_QUANTITY = 2_147_483_647
# 주 성향은 가격이 움직이는 방식을, 보조 성향은 같은 주 성향 안의 미세한 차이를 만든다.
# 12 × 8 조합을 저장해 신규 상장 뒤에도 같은 성격이 유지된다.
STOCK_PRIMARY_TRAITS = (
    {"name": "안정형", "description": "낮은 변동성과 강한 기준가 회귀", "noise": .55,
     "drift": 0., "trend_change": .28, "trend": .50, "reversion": 1.50,
     "market": .55, "event": .70, "phase": "", "burst": 0.},
    {"name": "성장형", "description": "완만한 상승 기대와 보통 수준의 조정", "noise": .80,
     "drift": .22, "trend_change": .18, "trend": 1.00, "reversion": .85,
     "market": 1.00, "event": 1.00, "phase": "", "burst": 0.},
    {"name": "가치형", "description": "가격이 낮아질수록 반등력이 강해짐", "noise": .70,
     "drift": 0., "trend_change": .26, "trend": .65, "reversion": 2.00,
     "market": .80, "event": .90, "phase": "", "burst": 0.},
    {"name": "추세형", "description": "상승·하락 방향이 비교적 오래 지속", "noise": .85,
     "drift": 0., "trend_change": .08, "trend": 1.70, "reversion": .50,
     "market": 1.00, "event": .95, "phase": "", "burst": 0.},
    {"name": "반전형", "description": "한 방향으로 움직인 뒤 되돌림이 잦음", "noise": .75,
     "drift": 0., "trend_change": .32, "trend": -.90, "reversion": 1.50,
     "market": .75, "event": .90, "phase": "", "burst": 0.},
    {"name": "뉴스형", "description": "평소에는 조용하지만 이벤트에 크게 반응", "noise": .45,
     "drift": 0., "trend_change": .20, "trend": .60, "reversion": 1.00,
     "market": .55, "event": 1.70, "phase": "", "burst": 0.},
    {"name": "시장추종형", "description": "전체 시장의 상승·하락 국면을 강하게 추종", "noise": .70,
     "drift": 0., "trend_change": .18, "trend": .80, "reversion": .75,
     "market": 1.80, "event": .90, "phase": "", "burst": 0.},
    {"name": "역행형", "description": "전체 시장과 반대로 움직일 가능성이 큼", "noise": .70,
     "drift": 0., "trend_change": .20, "trend": .70, "reversion": 1.00,
     "market": -.80, "event": .90, "phase": "", "burst": 0.},
    {"name": "박스권형", "description": "기준가 주변의 일정 범위를 반복해서 오감", "noise": .50,
     "drift": 0., "trend_change": .30, "trend": .40, "reversion": 2.50,
     "market": .40, "event": .65, "phase": "", "burst": 0.},
    {"name": "개장형", "description": "개장 직후 10분 동안 움직임이 커짐", "noise": .75,
     "drift": 0., "trend_change": .18, "trend": 1.00, "reversion": .80,
     "market": 1.00, "event": 1.00, "phase": "open", "burst": .03},
    {"name": "마감형", "description": "마감 전 10분 동안 움직임이 커짐", "noise": .75,
     "drift": 0., "trend_change": .18, "trend": 1.00, "reversion": .80,
     "market": 1.00, "event": 1.00, "phase": "close", "burst": .03},
    {"name": "투기형", "description": "급등락이 잦고 상장폐지 위험이 큼", "noise": 1.05,
     "drift": 0., "trend_change": .12, "trend": 1.30, "reversion": .25,
     "market": 1.25, "event": 1.35, "phase": "", "burst": .08},
)
STOCK_SECONDARY_TRAITS = (
    {"name": "낙관적", "description": "상승 쪽으로 아주 약한 힘을 받음", "drift": .12,
     "noise": 1., "market": 1., "event": 1., "trend_change": 1., "recovery": 0., "overheat": 0., "negative": 1.},
    {"name": "비관적", "description": "하락 쪽으로 아주 약한 힘을 받음", "drift": -.12,
     "noise": 1., "market": 1., "event": 1., "trend_change": 1., "recovery": 0., "overheat": 0., "negative": 1.},
    {"name": "민첩함", "description": "추세와 시장 변화에 빠르게 반응", "drift": 0.,
     "noise": 1.10, "market": 1.20, "event": 1.10, "trend_change": 1.25, "recovery": 0., "overheat": 0., "negative": 1.},
    {"name": "둔감함", "description": "시장과 뉴스의 영향이 천천히 반영", "drift": 0.,
     "noise": .80, "market": .70, "event": .75, "trend_change": .75, "recovery": 0., "overheat": 0., "negative": 1.},
    {"name": "회복력", "description": "급락 뒤 기준가를 향한 반등력이 강함", "drift": 0.,
     "noise": 1., "market": 1., "event": .90, "trend_change": 1., "recovery": .10, "overheat": 0., "negative": .82},
    {"name": "취약함", "description": "악재와 공포장에 더 크게 흔들림", "drift": 0.,
     "noise": 1.08, "market": 1., "event": 1., "trend_change": 1., "recovery": 0., "overheat": 0., "negative": 1.25},
    {"name": "과열주의", "description": "연속 상승 뒤 조정 압력이 커짐", "drift": 0.,
     "noise": 1., "market": 1., "event": 1., "trend_change": 1., "recovery": 0., "overheat": .12, "negative": 1.},
    {"name": "이벤트저항", "description": "호재와 악재 모두 비교적 작게 반영", "drift": 0.,
     "noise": 1., "market": 1., "event": .55, "trend_change": 1., "recovery": 0., "overheat": 0., "negative": 1.},
)
STOCK_EVENT_CHANCE = 0.13   # 10초 갱신에서도 분당 이벤트 수를 이전과 비슷하게
STOCK_FEE_RATE = 0.02
STOCK_HALT_SECONDS = 20
# 시장 국면은 10초 갱신 6~18회(1~3분) 동안 이어진다.
MARKET_REGIME_NAMES = ("횡보장", "상승장", "하락장", "과열장", "공포장")
MARKET_REGIME_DRIFTS = (0.0, 2.0, -2.0, 4.0, -4.0)
MARKET_REGIME_WEIGHTS = (3, 2, 2, 1, 1)
MARKET_REGIME_UPDATES = (6, 18)
MARKET_TICK_SCALE = 0.70   # 갱신이 두 배 빨라진 만큼 일반 변동은 줄인다
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


def evolution_stage(key):
    """기본형은 0, 한 번 진화한 모습은 1로 센다. 이후 3단계 사슬도 자동 지원한다."""
    stage = 0
    current = key
    while True:
        previous = next((source for source, target in EVOLUTIONS.items() if target == current), None)
        if previous is None:
            return stage
        stage += 1
        current = previous


def base_species_key(key):
    """진화체도 원래 포켓몬의 등급을 따른다."""
    current = key
    while True:
        previous = next((source for source, target in EVOLUTIONS.items() if target == current), None)
        if previous is None:
            return current
        current = previous


def pokemon_grade(key):
    """등급 이름과 산책 보상 배율을 돌려준다."""
    return POKEMON_GRADES.get(base_species_key(key), ("일반", 1.0, 88))


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
        font=(UI_FONT_FAMILY, 10, "bold"),
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
        self.food_boost_left = 0.0
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
        self.canvas.bind("<Double-Button-1>", lambda _event: app.open_game_menu())

        self.place()
        self.after_id = self.window.after(TICK_MS, self.tick)

    # --- 조작 -----------------------------------------------------------
    def build_menu(self, app):
        """우클릭 메뉴. 명령줄 없이도 웬만한 건 여기서 다 된다."""
        menu = pokemon_menu(self.window)
        menu.add_command(label="●  포켓몬 센터  ●", state="disabled")
        menu.add_command(label="", state="disabled")
        self.menu_status_index = menu.index("end")
        menu.add_separator()
        menu.add_command(label="━━ 포켓몬 관리 ━━", state="disabled")

        choose = pokemon_menu(menu)
        choose.add_command(label="", command=app.buy_random_pet)
        self.random_purchase_index = choose.index("end")
        self.pet_purchase_menu = choose
        menu.add_cascade(label="◆ 새 포켓몬 영입", menu=choose)
        menu.add_command(label="이 포켓몬 보내주기", command=self.release)

        # 먹이와 진화 아이템은 모두가 공유한다. 메뉴를 열 때마다 수량을 갱신한다.
        menu.add_separator()
        menu.add_command(label="━━ 생활 · 경제 ━━", state="disabled")
        shop = pokemon_menu(menu)
        shop.add_command(label="", command=app.buy_food)
        self.food_buy_index = shop.index("end")
        shop.add_command(label="", command=app.buy_growth_drop)
        self.drop_buy_index = shop.index("end")
        menu.add_cascade(label="", menu=shop)
        self.shop_index = menu.index("end")
        menu.add_command(label="", command=lambda: app.feed_pet(self))
        self.feed_index = menu.index("end")

        menu.add_command(label="", command=app.open_stock_overlay)
        self.stock_index = menu.index("end")

        # 진화하는 포켓몬이면 여기에 진행 상황을 보여 준다.
        self.evolve_index = None
        if self.next_key:
            menu.add_command(label="", state="disabled", command=self.start_evolving)
            self.evolve_index = menu.index("end")
        menu.configure(postcommand=self.refresh_menu)
        menu.add_separator()
        menu.add_command(label="━━ 움직임 · 설정 ━━", state="disabled")

        sizes = pokemon_menu(menu)
        for label, value in SIZE_CHOICES:
            sizes.add_radiobutton(
                label=label, value=value, variable=app.scale_var,
                command=lambda v=value: app.set_scale(v),
            )
        menu.add_cascade(label="크기 조절", menu=sizes)

        speeds = pokemon_menu(menu)
        for label, value in SPEED_CHOICES:
            speeds.add_radiobutton(
                label=label, value=value, variable=app.speed_var,
                command=lambda v=value: app.set_speed(v),
            )
        menu.add_cascade(label="산책 속도", menu=speeds)

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
        self.friendship = min(self.evolution_requirement()[0], self.friendship + EVOLVE_PER_PET)

    def fed(self):
        """포켓푸드로 친밀도와 5분짜리 2배 산책 버프를 준다."""
        self.spawn_emote("heart")
        self.food_boost_left = FOOD_BOOST_SECONDS
        if not self.next_key or self.evolving:
            return
        self.friendship = min(self.evolution_requirement()[0], self.friendship + FOOD_FRIENDSHIP)

    def food_boost_label(self):
        """남은 산책 부스트 시간을 메뉴에 짧게 표시한다."""
        if self.food_boost_left <= 0:
            return "2배 산책 5분"
        seconds = int(math.ceil(self.food_boost_left))
        return "2배 산책 %d:%02d" % (seconds // 60, seconds % 60)

    def refresh_menu(self):
        """메뉴를 열 때마다 상점과 진화 항목을 지금 상태로 고쳐 쓴다."""
        self.menu.entryconfigure(
            self.menu_status_index,
            label="보유금 %s  ·  포켓푸드 %d개  ·  성장 물방울 %d개" % (
                format_won(self.app.coins), self.app.food, self.app.growth_drops
            ),
        )
        self.menu.entryconfigure(
            self.shop_index, label="◆ 상점 · 보유금 %s" % format_won(self.app.coins)
        )
        self.menu.nametowidget(self.menu.entrycget(self.shop_index, "menu")).entryconfigure(
            self.food_buy_index,
            label="포켓푸드  ·  %s  ·  2배 산책 5분  ·  현재 %d개" % (
                format_won(FOOD_COST), self.app.food
            ),
            state="normal" if self.app.coins >= FOOD_COST else "disabled",
        )
        self.menu.nametowidget(self.menu.entrycget(self.shop_index, "menu")).entryconfigure(
            self.drop_buy_index,
            label="성장의 물방울  ·  %s  ·  현재 %d개" % (
                format_won(GROWTH_DROP_COST), self.app.growth_drops
            ),
            state="normal" if self.app.coins >= GROWTH_DROP_COST else "disabled",
        )
        self.menu.entryconfigure(
            self.feed_index, label="▶ 먹이 주기  ·  %s  ·  %d개 보유" % (
                self.food_boost_label(), self.app.food
            ),
            state="normal" if self.app.food else "disabled",
        )
        self.pet_purchase_menu.entryconfigure(
            self.random_purchase_index,
            label="랜덤 영입 — %s  (일반 88% · 준전설 10% · 초전설 2%%)" % format_won(POKEMON_PRICE),
            state="normal" if self.app.coins >= POKEMON_PRICE else "disabled",
        )
        self.menu.entryconfigure(
            self.stock_index,
            label="▶ 주식시장 열기  ·  평가액 %s" % format_won(self.app.stock_portfolio_value()),
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
            drops_need = self.evolution_requirement()[2]
            if self.app.growth_drops < drops_need:
                needs.append("성장의 물방울 %d개" % drops_need)
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
        self.walked = min(self.evolution_requirement()[1], self.walked + actual)
        self.app.earn_walk_coins(actual * self.income_multiplier())
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
        multiplier = FOOD_SPEED_MULTIPLIER if self.food_boost_left > 0 else 1.0
        self.walk_speed = min(
            self.speed * multiplier, self.walk_speed + WALK_ACCEL * multiplier * dt
        )
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

        if self.food_boost_left > 0 and not self.app.paused:
            self.food_boost_left = max(0.0, self.food_boost_left - dt)

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
        friendship_need, walk_need, drops_need = self.evolution_requirement()
        return (
            bool(self.next_key)
            and self.friendship >= friendship_need
            and self.walked >= walk_need
            and self.app.growth_drops >= drops_need
            and not self.evolving
        )

    def evolution_requirement(self):
        """현재 모습에서 다음 진화로 갈 때 필요한 친밀도·산책·물방울."""
        stage = min(evolution_stage(self.pokemon.key), len(EVOLUTION_REQUIREMENTS) - 1)
        return EVOLUTION_REQUIREMENTS[stage]

    def income_multiplier(self):
        """등급과 진화 단계 보상을 함께 적용한 산책 수입 배율."""
        stage = min(evolution_stage(self.pokemon.key), len(EVOLUTION_INCOME_MULTIPLIERS) - 1)
        return pokemon_grade(self.pokemon.key)[1] * EVOLUTION_INCOME_MULTIPLIERS[stage]

    def pets_left(self):
        """진화까지 몇 번 더 쓰다듬어야 하는지."""
        return max(0, int(-(-(self.evolution_requirement()[0] - self.friendship) // EVOLVE_PER_PET)))

    def walk_left(self):
        """진화까지 몇 픽셀을 더 산책해야 하는지."""
        return max(0, int(math.ceil(self.evolution_requirement()[1] - self.walked)))

    def start_evolving(self):
        """진화 연출을 시작한다. 끝나면 앱이 새 포켓몬으로 갈아 끼운다."""
        if not self.can_evolve():
            return
        self.evolution_images()
        self.app.growth_drops -= self.evolution_requirement()[2]
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


class GameMenuOverlay:
    """홈·포켓몬·상점·주식·설정을 한곳에 모은 게임형 포켓몬 센터."""

    RED = "#ee5960"
    RED_DARK = "#b72e36"
    BLUE = "#5aa7f3"
    YELLOW = "#e9bd39"
    INK = "#eef4ff"
    MUTED = "#aab8cd"
    PAPER = "#182236"
    PANEL = "#202d43"
    SOFT = "#2c3950"
    LINE = "#45536a"
    GREEN = "#54c995"

    def __init__(self, app):
        self.app = app
        self.window = tk.Toplevel(app.root)
        self.window.title("포켓몬 센터")
        self.window.overrideredirect(True)
        self.window.wm_attributes("-topmost", True)
        width = min(920, max(736, app.screen_width - 40))
        height = min(660, max(620, app.screen_height - 80))
        self.window.geometry("%dx%d+%d+%d" % (
            width, height, max(0, (app.screen_width - width) // 2),
            max(0, (app.screen_height - height) // 3),
        ))
        self.window.minsize(736, 620)
        self.window.configure(bg=self.INK, padx=3, pady=3)
        self.topmost = True
        self.selected_index = 0
        self.after_id = None
        self.tabs = {}
        self.tab_buttons = {}
        self.drag_origin = None
        self._configure_progress_styles()
        self._build_header()
        self._build_shell()
        self._build_footer()
        self.window.bind("<Configure>", self._apply_responsive_layout, add="+")
        self._apply_responsive_layout()
        self.window.protocol("WM_DELETE_WINDOW", self.close)
        for number, key in enumerate(("home", "pets", "shop", "stock", "settings"), 1):
            self.window.bind("<Control-Key-%d>" % number,
                             lambda _event, key=key: self._keyboard_select_tab(key))
        self.select_tab("home")
        self.refresh()
        self.after_id = self.window.after(700, self.auto_refresh)

    def _configure_progress_styles(self):
        style = ttk.Style(self.window)
        style.configure("Friend.Horizontal.TProgressbar", troughcolor=self.SOFT,
                        background=self.BLUE, borderwidth=0, thickness=13)
        style.configure("Walk.Horizontal.TProgressbar", troughcolor=self.SOFT,
                        background=self.GREEN, borderwidth=0, thickness=13)

    def _build_header(self):
        header = tk.Frame(self.window, bg=self.RED, height=62,
                          highlightbackground=self.INK, highlightthickness=0,
                          cursor="fleur")
        header.pack(fill="x")
        header.pack_propagate(False)
        ball = tk.Canvas(header, width=38, height=38, bg=self.RED, highlightthickness=0)
        ball.pack(side="left", padx=(14, 12), pady=10)
        ball.create_oval(3, 3, 37, 37, fill="white", outline="white", width=2)
        ball.create_rectangle(4, 17, 36, 23, fill=self.INK, outline=self.INK)
        ball.create_oval(14, 14, 26, 26, fill="white", outline=self.INK, width=3)
        title = tk.Frame(header, bg=self.RED)
        title.pack(side="left", fill="both", expand=True)
        title_label = tk.Label(title, text="포켓몬 센터", bg=self.RED, fg="white",
                               font=(UI_FONT_FAMILY, 14, "bold"), anchor="w")
        title_label.pack(anchor="w", pady=(7, 0))
        subtitle = tk.Label(title, text="함께 걷고, 성장하고, 새로운 친구를 만나세요",
                            bg=self.RED, fg="#fce1e2", font=(UI_FONT_FAMILY, 9), anchor="w")
        subtitle.pack(anchor="w")
        controls = tk.Frame(header, bg=self.RED)
        controls.pack(side="right", padx=(0, 10), pady=10)
        for text, command in (("—", self.minimize), ("×", self.close)):
            button = tk.Button(controls, text=text, command=command, bg=self.RED_DARK, fg="white",
                               activebackground=self.RED_DARK, activeforeground="white",
                               width=3, height=1, bd=2, relief="solid",
                               highlightbackground="white", highlightthickness=1,
                               font=(UI_FONT_FAMILY, 11, "bold"), cursor="hand2")
            button.pack(side="left", padx=2, ipadx=1, ipady=2)
        self.wallet = tk.Label(header, bg="#c94a50", fg="white",
                               highlightbackground="white", highlightthickness=2,
                               font=(UI_FONT_FAMILY, 10, "bold"), padx=11, pady=7)
        self.wallet.pack(side="right", padx=(5, 8))
        tk.Frame(header, bg=self.INK, height=3).place(
            x=0, rely=1.0, relwidth=1.0, anchor="sw")
        for widget in (header, ball, title, title_label, subtitle):
            widget.bind("<ButtonPress-1>", self.begin_drag)
            widget.bind("<B1-Motion>", self.drag)

    def _build_shell(self):
        shell = tk.Frame(self.window, bg=self.PAPER)
        shell.pack(fill="both", expand=True)
        nav = tk.Frame(shell, bg=self.SOFT, width=176,
                       highlightbackground=self.INK, highlightthickness=0)
        self.navigation_panel = nav
        nav.pack(side="left", fill="y")
        nav.pack_propagate(False)
        items = (("home", "⌂  홈"), ("pets", "◉  포켓몬"),
                 ("shop", "◆  상점"), ("stock", "↗  주식"),
                 ("settings", "⚙  설정"))
        for key, text in items:
            button = tk.Button(nav, text=text, command=lambda key=key: self.select_tab(key),
                               bg=self.SOFT, fg=self.INK, activebackground="#39465c",
                               activeforeground=self.INK, bd=0, relief="flat", anchor="w",
                               highlightbackground=self.INK, highlightthickness=0,
                               font=(UI_FONT_FAMILY, 10, "bold"), padx=11, pady=10,
                               cursor="hand2")
            button.pack(fill="x", padx=(10, 16), pady=(14 if key == "home" else 0, 7))
            self.tab_buttons[key] = button
        self.content = tk.Frame(shell, bg=self.PAPER)
        self.content.pack(side="left", fill="both", expand=True)
        self._build_home_tab()
        self._build_pets_tab()
        self._build_shop_tab()
        self._build_stock_tab()
        self._build_settings_tab()

    def _build_footer(self):
        footer = tk.Frame(self.window, bg=self.PANEL,
                          highlightbackground=self.LINE, highlightthickness=1)
        footer.pack(fill="x")
        tk.Label(footer, text="● 메뉴는 자유롭게 이동하고 최소화할 수 있습니다",
                 bg=self.PANEL, fg=self.MUTED, font=(UI_FONT_FAMILY, 9),
                 padx=14, pady=7).pack(side="left")
        self.saved_label = tk.Label(footer, text="최근 저장됨 · 방금 전", bg=self.PANEL,
                                    fg=self.MUTED, font=(UI_FONT_FAMILY, 9), padx=14)
        self.saved_label.pack(side="right")

    def _new_tab(self, key):
        frame = tk.Frame(self.content, bg=self.PAPER, padx=16, pady=16)
        self.tabs[key] = frame
        return frame

    def _heading(self, parent, title, hint):
        row = tk.Frame(parent, bg=self.PAPER)
        row.pack(fill="x", pady=(0, 10))
        tk.Label(row, text=title, bg=self.PAPER, fg=self.INK,
                 font=(UI_FONT_FAMILY, 14, "bold")).pack(side="left")
        tk.Label(row, text=hint, bg=self.PAPER, fg=self.MUTED,
                 font=(UI_FONT_FAMILY, 9)).pack(side="right", pady=5)

    def _card(self, parent, **pack_options):
        frame = tk.Frame(parent, bg=self.PANEL, highlightbackground=self.LINE,
                         highlightthickness=2, bd=0, relief="flat", padx=14, pady=12)
        frame.pack(**pack_options)
        return frame

    def _build_home_tab(self):
        tab = self._new_tab("home")
        heading = tk.Frame(tab, bg=self.PAPER)
        heading.pack(fill="x", pady=(0, 12))
        tk.Label(heading, text="오늘의 파트너", bg=self.PAPER, fg=self.INK,
                 font=(UI_FONT_FAMILY, 14, "bold")).pack(side="left")
        self.home_heading_hint = tk.Label(heading, text="산책 중 · 수입 x1.0",
                                          bg=self.PAPER, fg=self.MUTED,
                                          font=(UI_FONT_FAMILY, 9))
        self.home_heading_hint.pack(side="right", pady=5)
        # 홈은 시안처럼 상태 확인에 집중하고, 파트너 전환은 '내 포켓몬'에서 한다.
        hero = tk.Frame(tab, bg=self.PAPER, height=335)
        hero.pack(fill="x")
        hero.pack_propagate(False)
        visual = tk.Canvas(hero, bg=self.PANEL, width=252, height=335,
                           highlightbackground=self.LINE, highlightthickness=2)
        visual.pack(side="left", fill="y", padx=(0, 12))
        for x in range(-335, 252, 24):
            visual.create_line(x, 335, x + 335, 0, fill="#404441")
        self.pet_shadow_item = visual.create_oval(42, 233, 210, 256, fill="#3c4353", outline="")
        self.stage_badge = tk.Label(visual, bg=self.BLUE, fg="white",
                                    font=(UI_FONT_FAMILY, 8, "bold"), padx=8, pady=3)
        self.stage_badge.place(x=10, y=10)
        self.pet_image = visual
        self.pet_image_item = None
        status = self._card(hero, side="left", fill="both", expand=True)
        name_row = tk.Frame(status, bg=self.PANEL)
        name_row.pack(fill="x")
        self.home_name = tk.Label(name_row, bg=self.PANEL, fg=self.INK,
                                  font=(UI_FONT_FAMILY, 14, "bold"))
        self.home_name.pack(side="left")
        self.grade_badge = tk.Label(name_row, bg=self.YELLOW, fg="#4b3900",
                                    font=(UI_FONT_FAMILY, 8, "bold"), padx=8, pady=3)
        self.grade_badge.pack(side="left", padx=(5, 0))
        self.income_label = tk.Label(name_row, bg=self.PANEL, fg=self.GREEN,
                                     font=(UI_FONT_FAMILY, 9, "bold"))
        self.income_label.pack(side="right")
        self.friend_label = self._progress_row(status, "친밀도")
        self.friend_progress = ttk.Progressbar(status, maximum=100,
                                               style="Friend.Horizontal.TProgressbar")
        self.friend_progress.pack(fill="x", pady=(0, 9))
        self.walk_label = self._progress_row(status, "진화 산책 거리")
        self.walk_progress = ttk.Progressbar(status, maximum=100,
                                             style="Walk.Horizontal.TProgressbar")
        self.walk_progress.pack(fill="x", pady=(0, 10))
        self.buff_label = tk.Label(status, bg="#283d5a", fg=self.INK, anchor="center",
                                   font=(UI_FONT_FAMILY, 9, "bold"), padx=10, pady=8)
        self.buff_label.pack(fill="x", pady=(0, 10))
        actions = tk.Frame(status, bg=self.PANEL)
        actions.pack(fill="x")
        self.home_feed = self.game_button(actions, "먹이 주기", self.RED, self.feed_selected)
        self.home_evolve = self.game_button(actions, "진화", self.BLUE, self.evolve_selected)
        self.home_recall = self.game_button(actions, "위치 찾기", self.BLUE, self.recall_selected)
        for button in (self.home_feed, self.home_evolve, self.home_recall):
            button.configure(height=2, pady=6)
            button.pack(side="left", fill="x", expand=True, padx=2, ipady=2)
        self.evolve_note = tk.Label(status, bg=self.PANEL, fg=self.MUTED, anchor="e",
                                    justify="right", font=(UI_FONT_FAMILY, 9), pady=0)
        self.evolve_note.pack(fill="x", pady=(6, 0))
        shortcuts = tk.Frame(tab, bg=self.PAPER)
        shortcuts.pack(fill="x", pady=(12, 0))
        self.shortcut_buttons = {}
        for text, detail, key in (("내 포켓몬", "목록과 상태 관리", "pets"),
                                  ("포켓몬 상점", "먹이와 진화 아이템", "shop"),
                                  ("주식시장", "내 평가액 확인", "stock")):
            button = tk.Button(shortcuts, text="%s\n%s   ›" % (text, detail),
                               command=lambda key=key: self.select_tab(key), bg=self.PANEL,
                               fg=self.INK, activebackground=self.SOFT, bd=0, relief="flat",
                               highlightbackground=self.LINE, highlightthickness=2,
                               justify="left", anchor="w", padx=12, pady=8,
                               font=(UI_FONT_FAMILY, 9, "bold"), cursor="hand2")
            button.pack(side="left", fill="x", expand=True, padx=4, ipady=4)
            self.shortcut_buttons[key] = button

    def _progress_row(self, parent, text):
        row = tk.Frame(parent, bg=self.PANEL)
        row.pack(fill="x", pady=(12, 4))
        tk.Label(row, text=text, bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 9)).pack(side="left")
        value = tk.Label(row, bg=self.PANEL, fg=self.INK,
                         font=(UI_FONT_FAMILY, 9, "bold"))
        value.pack(side="right")
        return value

    def _build_pets_tab(self):
        tab = self._new_tab("pets")
        self._heading(tab, "내 포켓몬", "선택한 포켓몬의 상태와 행동을 관리합니다")
        roster = tk.Frame(tab, bg=self.PAPER)
        roster.pack(fill="x")
        self.roster_canvas = tk.Canvas(roster, bg=self.PAPER, height=164,
                                       highlightthickness=0, bd=0)
        roster_scroll = tk.Scrollbar(roster, orient="vertical", command=self.roster_canvas.yview)
        self.roster_canvas.configure(yscrollcommand=roster_scroll.set)
        self.roster_canvas.pack(side="left", fill="both", expand=True)
        roster_scroll.pack(side="right", fill="y")
        self.roster_inner = tk.Frame(self.roster_canvas, bg=self.PAPER)
        self.roster_window = self.roster_canvas.create_window(
            (0, 0), window=self.roster_inner, anchor="nw")
        self.roster_inner.bind("<Configure>", lambda _event: self.roster_canvas.configure(
            scrollregion=self.roster_canvas.bbox("all")))
        self.roster_canvas.bind("<Configure>", lambda event: self.roster_canvas.itemconfigure(
            self.roster_window, width=event.width))
        self.roster_inner.columnconfigure(0, weight=1)
        self.roster_inner.columnconfigure(1, weight=1)
        self.roster_buttons = []
        self.pet_recruit = self.game_button(tab, "＋  새 포켓몬 영입", self.PANEL, self.buy_random)
        self.pet_recruit.configure(anchor="w", justify="left")
        self.pet_recruit.pack(fill="x", pady=(6, 0), ipady=5)
        card = self._card(tab, fill="x", pady=(10, 0))
        tk.Label(card, text="선택 포켓몬 관리", bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(anchor="w", pady=(0, 8))
        actions = tk.Frame(card, bg=self.PANEL)
        actions.pack(fill="x")
        self.pet_feed = self.game_button(actions, "먹이 주기", self.RED, self.feed_selected)
        self.pet_evolve = self.game_button(actions, "진화", self.BLUE, self.evolve_selected)
        self.pet_recall = self.game_button(actions, "화면 가운데로", self.GREEN, self.recall_selected)
        self.pet_release = self.game_button(actions, "보내주기…", "#6b7280", self.release_selected)
        for button in (self.pet_feed, self.pet_evolve, self.pet_recall, self.pet_release):
            button.configure(height=2, pady=6)
            button.pack(side="left", fill="x", expand=True, padx=2, ipady=2)

    def _build_shop_tab(self):
        tab = self._new_tab("shop")
        heading = tk.Frame(tab, bg=self.PAPER)
        heading.pack(fill="x", pady=(0, 12))
        tk.Label(heading, text="프렌들리 상점", bg=self.PAPER, fg=self.INK,
                 font=(UI_FONT_FAMILY, 14, "bold")).pack(side="left")
        self.shop_inventory = tk.Label(heading, bg=self.PAPER, fg=self.MUTED,
                                       font=(UI_FONT_FAMILY, 9))
        self.shop_inventory.pack(side="right", pady=5)
        self.shop_feedback = tk.Label(tab, text="상품을 구매하면 결과와 남은 잔액을 알려드립니다.",
                                      bg="#283d5a", fg=self.MUTED, anchor="w",
                                      font=(UI_FONT_FAMILY, 9), padx=10, pady=7)
        self.shop_feedback.pack(fill="x", pady=(0, 8))
        grid = tk.Frame(tab, bg=self.PAPER)
        grid.pack(fill="x")
        grid.columnconfigure(0, weight=1)
        grid.columnconfigure(1, weight=1)
        self.shop_food, self.shop_food_owned = self._shop_card(
            grid, 0, 0, "●", "포켓푸드",
            "5분 동안 산책 속도가 2배가 되고 친밀도가 2 올라갑니다.", FOOD_COST, self.buy_food)
        self.shop_drop, self.shop_drop_owned = self._shop_card(
            grid, 0, 1, "◆", "성장의 물방울",
            "진화 조건을 모두 채운 포켓몬이 진화할 때 사용합니다.", GROWTH_DROP_COST, self.buy_drop)
        self.shop_draw, self.shop_draw_owned = self._shop_card(
            grid, 1, 0, "◉", "랜덤 포켓볼",
            "새로운 포켓몬 한 마리를 무작위로 영입합니다.", POKEMON_PRICE, self.buy_random)

    def _shop_card(self, parent, row, column, icon, name, detail, price, command):
        card = tk.Frame(parent, bg=self.PANEL, highlightbackground=self.LINE,
                        highlightthickness=2, padx=14, pady=12, height=176)
        card.grid(row=row, column=column, columnspan=2 if row == 1 and column == 0 else 1,
                  sticky="nsew", padx=(0, 5) if column == 0 and row == 0 else
                  (5, 0) if column == 1 else 0,
                  pady=(0, 5) if row == 0 else (5, 0))
        card.grid_propagate(False)
        top = tk.Frame(card, bg=self.PANEL)
        top.pack(fill="x")
        tk.Label(top, text=icon, bg=self.SOFT, fg=self.RED,
                 font=(UI_FONT_FAMILY, 17, "bold"), width=3, pady=6).pack(side="left", padx=(0, 10))
        name_label = tk.Label(top, text=name, bg=self.PANEL, fg=self.INK, anchor="w",
                              font=(UI_FONT_FAMILY, 11, "bold"))
        owned = tk.Label(top, bg=self.PANEL, fg=self.MUTED, anchor="e",
                         font=(UI_FONT_FAMILY, 8, "bold"))
        owned.pack(side="right")
        name_label.pack(side="left", fill="x", expand=True)
        tk.Label(card, text=detail, bg=self.PANEL, fg=self.MUTED, anchor="nw", justify="left",
                 wraplength=270, font=(UI_FONT_FAMILY, 9)).pack(fill="both", expand=True, pady=6)
        bottom = tk.Frame(card, bg=self.PANEL)
        bottom.pack(fill="x")
        tk.Label(bottom, text=format_won(price), bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(side="left")
        button = self.game_button(bottom, "영입하기" if name == "랜덤 포켓볼" else "구매",
                                  self.RED, command)
        button.pack(side="right", ipady=3)
        return button, owned

    def _build_stock_tab(self):
        tab = self._new_tab("stock")
        heading = tk.Frame(tab, bg=self.PAPER)
        heading.pack(fill="x", pady=(0, 12))
        tk.Label(heading, text="포켓몬 주식시장", bg=self.PAPER, fg=self.INK,
                 font=(UI_FONT_FAMILY, 14, "bold")).pack(side="left")
        self.stock_heading_hint = tk.Label(heading, bg=self.PAPER, fg=self.MUTED,
                                            font=(UI_FONT_FAMILY, 9))
        self.stock_heading_hint.pack(side="right", pady=5)
        card = self._card(tab, fill="x")
        title_row = tk.Frame(card, bg=self.PANEL)
        title_row.pack(fill="x", pady=(0, 7))
        tk.Label(title_row, text="내 투자 현황", bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 13, "bold")).pack(side="left")
        tk.Label(title_row, text="게임 머니 전용", bg=self.YELLOW, fg="#4b3900",
                 font=(UI_FONT_FAMILY, 8, "bold"), padx=8, pady=3).pack(side="left", padx=8)
        portfolio_row = tk.Frame(card, bg=self.PANEL)
        portfolio_row.pack(fill="x", pady=2)
        tk.Label(portfolio_row, text="주식 평가액", bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 9)).pack(side="left")
        self.stock_portfolio = tk.Label(portfolio_row, bg=self.PANEL, fg=self.INK,
                                        font=(UI_FONT_FAMILY, 9, "bold"))
        self.stock_portfolio.pack(side="right")
        cash_row = tk.Frame(card, bg=self.PANEL)
        cash_row.pack(fill="x", pady=2)
        tk.Label(cash_row, text="현금", bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 9)).pack(side="left")
        self.stock_cash = tk.Label(cash_row, bg=self.PANEL, fg=self.INK,
                                   font=(UI_FONT_FAMILY, 9, "bold"))
        self.stock_cash.pack(side="right")
        market = tk.Frame(card, bg="#283d5a", padx=10, pady=8)
        market.pack(fill="x", pady=8)
        self.market_regime = tk.Label(market, bg="#283d5a", fg=self.INK,
                                      font=(UI_FONT_FAMILY, 9))
        self.market_regime.pack(side="left")
        self.market_update = tk.Label(market, bg="#283d5a", fg=self.INK,
                                      font=(UI_FONT_FAMILY, 9, "bold"))
        self.market_update.pack(side="right")
        self.stock_button = self.game_button(card, "전체 주식창 열기", self.RED,
                                              self.app.open_stock_overlay)
        self.stock_button.pack(anchor="w", ipady=5, pady=(4, 0))
        previews = tk.Frame(tab, bg=self.PAPER)
        previews.pack(fill="x", pady=(10, 0))
        self.stock_positions_preview = self._stock_preview_card(previews, "내 보유 종목")
        self.stock_market_preview = self._stock_preview_card(previews, "시장 한눈에")

    def _stock_preview_card(self, parent, title):
        card = self._card(parent, side="left", fill="both", expand=True, padx=5)
        tk.Label(card, text=title, bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(anchor="w")
        body = tk.Label(card, bg=self.PANEL, fg=self.MUTED, anchor="nw", justify="left",
                        wraplength=250, font=(UI_FONT_FAMILY, 9), height=5)
        body.pack(fill="both", expand=True, pady=(7, 0))
        return body

    def _build_settings_tab(self):
        tab = self._new_tab("settings")
        self._heading(tab, "게임 설정", "변경사항은 자동으로 저장됩니다")
        self._choice_card(tab, "포켓몬 크기", SIZE_CHOICES, self.app.set_scale, "scale")
        self._choice_card(tab, "산책 속도", SPEED_CHOICES, self.app.set_speed, "speed")
        window_row = self._settings_action_row(tab, "창 표시")
        self.top_button = self.game_button(window_row, "항상 위 켜짐", self.PANEL, self.toggle_topmost)
        self.back_button = self.game_button(window_row, "뒤로 보내기", self.PANEL, self.send_to_back)
        for button in (self.top_button, self.back_button):
            button.pack(side="left", padx=3, ipady=4)
        game_row = self._settings_action_row(tab, "게임 동작")
        self.pause_button = self.game_button(game_row, "전체 일시정지", self.PANEL, self.toggle_pause)
        if self.app.system == "Windows":
            self.autostart_button = self.game_button(game_row, "윈도우 시작 시 실행", self.PANEL,
                                                     self.toggle_autostart)
        else:
            self.autostart_button = None
        for button in (self.pause_button, self.autostart_button):
            if button is not None:
                button.pack(side="left", padx=3, ipady=4)
        danger_row = self._settings_action_row(tab, "위험 작업", danger=True)
        self.quit_button = self.game_button(danger_row, "게임 종료…", self.RED, self.confirm_quit)
        self.quit_button.pack(side="left", padx=3, ipady=4)

    def _settings_action_row(self, parent, title, danger=False):
        card = self._card(parent, fill="x", pady=3)
        tk.Label(card, text=title, bg=self.PANEL, fg=self.RED if danger else self.INK,
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(anchor="w", pady=(0, 7))
        row = tk.Frame(card, bg=self.PANEL)
        row.pack(fill="x")
        return row

    def _choice_card(self, parent, title, choices, command, kind):
        card = self._card(parent, fill="x", pady=6)
        tk.Label(card, text=title, bg=self.PANEL, fg=self.INK,
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(anchor="w", pady=(0, 7))
        row = tk.Frame(card, bg=self.PANEL)
        row.pack(fill="x")
        buttons = []
        for label, value in choices:
            button = self.game_button(row, label, self.PANEL,
                                      lambda value=value: self.run_action(lambda: command(value)))
            button.pack(side="left", fill="x", expand=True, padx=2, ipady=3)
            button.choice_value = value
            buttons.append(button)
        if kind == "scale":
            self.scale_buttons = buttons
        else:
            self.speed_buttons = buttons

    @staticmethod
    def game_button(parent, text, color, command):
        return tk.Button(parent, text=text, command=command, bg=color, fg="white", bd=0,
                         relief="flat", activebackground=color, activeforeground="white",
                         highlightbackground=GameMenuOverlay.INK,
                         highlightcolor=GameMenuOverlay.INK, highlightthickness=2,
                          disabledforeground=GameMenuOverlay.MUTED,
                          font=(UI_FONT_FAMILY, 9, "bold"), cursor="hand2", padx=10, pady=5)

    def _apply_responsive_layout(self, _event=None):
        """좁은 창에서는 탐색 영역과 포켓몬 초상화를 줄여 본문 잘림을 막는다."""
        if not hasattr(self, "navigation_panel") or not hasattr(self, "pet_image"):
            return
        compact = self.window.winfo_width() < 850
        nav_width = 138 if compact else 176
        portrait_width = 190 if compact else 252
        self.navigation_panel.configure(width=nav_width)
        self.pet_image.configure(width=portrait_width)
        center = portrait_width // 2
        self.pet_image.coords(self.pet_shadow_item, center - 84, 233, center + 84, 256)
        if self.pet_image_item is not None:
            self.pet_image.coords(self.pet_image_item, center, 154)

    def select_tab(self, key):
        for name, frame in self.tabs.items():
            if name == key:
                frame.pack(fill="both", expand=True)
            else:
                frame.pack_forget()
            self.tab_buttons[name].configure(
                bg=self.RED if name == key else self.SOFT,
                fg="white" if name == key else self.INK,
                relief="sunken" if name == key else "flat",
                bd=2 if name == key else 0,
                highlightthickness=1 if name == key else 0,
            )
        self.refresh()

    def refresh(self):
        self.selected_index = min(
            max(0, self.selected_index), max(0, len(self.app.pets) - 1))
        self.wallet.configure(text="◉  %s" % format_won(self.app.coins))
        self.shortcut_buttons["pets"].configure(
            text="내 포켓몬\n%d마리 관리하기  ›" % len(self.app.pets))
        self.shortcut_buttons["shop"].configure(
            text="포켓몬 상점\n먹이와 진화 아이템  ›")
        self.shop_inventory.configure(text="보유 아이템  ·  포켓푸드 %d개  ·  성장의 물방울 %d개" % (
            self.app.food, self.app.growth_drops))
        self.shop_food_owned.configure(text="보유 %d개" % self.app.food)
        self.shop_drop_owned.configure(text="보유 %d개" % self.app.growth_drops)
        self.shop_draw_owned.configure(text="보유 %d마리" % len(self.app.pets))
        for button, affordable, action in ((self.shop_food, self.app.coins >= FOOD_COST, "구매"),
                                           (self.shop_drop, self.app.coins >= GROWTH_DROP_COST, "구매"),
                                           (self.shop_draw, self.app.coins >= POKEMON_PRICE, "영입하기")):
            button.configure(state="normal" if affordable else "disabled",
                             bg=self.RED if affordable else self.SOFT,
                             activebackground=self.RED if affordable else self.SOFT,
                             text=action if affordable else "잔액 부족")
        pet = self.selected_pet()
        if len(self.roster_buttons) != len(self.app.pets):
            for button in self.roster_buttons:
                button.destroy()
            self.roster_buttons = []
            for index in range(len(self.app.pets)):
                button = tk.Button(self.roster_inner, text="", bg=self.PANEL, fg=self.INK,
                                   activebackground=self.SOFT, anchor="w", justify="left",
                                   highlightbackground=self.LINE, highlightthickness=2,
                                   bd=0, relief="flat", padx=14, pady=10,
                                   font=(UI_FONT_FAMILY, 9, "bold"), cursor="hand2",
                                   command=lambda index=index: self.set_selected(index))
                button.grid(row=index // 2, column=index % 2, sticky="nsew",
                            padx=(0, 5) if index % 2 == 0 else (5, 0), pady=5, ipady=8)
                self.roster_buttons.append(button)
        for index, button in enumerate(self.roster_buttons):
            roster_pet = self.app.pets[index]
            roster_grade, _income, _chance = pokemon_grade(roster_pet.pokemon.key)
            button.configure(
                text="●  %s  · %s\n     %d단계 · 수입 x%.2g" % (
                    roster_pet.pokemon.name_ko, roster_grade,
                    evolution_stage(roster_pet.pokemon.key) + 1,
                    roster_pet.income_multiplier()),
                state="normal", highlightbackground=self.RED if index == self.selected_index else self.LINE,
            )
        self.pet_recruit.configure(
            text="＋  새 포켓몬 영입\n     %s · 일반 88%% · 준전설 10%% · 초전설 2%%%s" % (
                format_won(POKEMON_PRICE), "" if self.app.coins >= POKEMON_PRICE else " · 잔액 부족"),
            state="normal" if self.app.coins >= POKEMON_PRICE else "disabled")
        if pet:
            grade, _grade_income, _chance = pokemon_grade(pet.pokemon.key)
            stage = evolution_stage(pet.pokemon.key) + 1
            friendship_need, walk_need, drops_need = pet.evolution_requirement()
            self.home_name.configure(text=pet.pokemon.name_ko)
            self.grade_badge.configure(text=grade)
            self.stage_badge.configure(text="%d단계" % stage)
            self.home_heading_hint.configure(text="%s · 수입 x%.2g" % (
                "산책 일시정지" if self.app.paused else "산책 중", pet.income_multiplier()))
            self.income_label.configure(text="+%s / 100px" % format_won(
                int(round(COINS_PER_WALK * pet.income_multiplier()))))
            self.friend_label.configure(text="%.0f / %.0f" % (pet.friendship, friendship_need))
            self.walk_label.configure(text="%s / %spx" % (
                "{:,}".format(int(pet.walked)), "{:,}".format(int(walk_need))))
            self.friend_progress["value"] = min(100, pet.friendship * 100.0 / max(1, friendship_need))
            self.walk_progress["value"] = min(100, pet.walked * 100.0 / max(1, walk_need))
            self.buff_label.configure(text="● 포켓푸드 효과  ·  %s" % pet.food_boost_label())
            image = self.app.get_images(pet.pokemon)["right"][0]
            zoom = max(1, min(3, 150 // max(1, image.width(), image.height())))
            menu_image = image.zoom(zoom, zoom) if zoom > 1 else image
            if self.pet_image_item is None:
                self.pet_image_item = self.pet_image.create_image(126, 154, image=menu_image)
            else:
                self.pet_image.itemconfigure(self.pet_image_item, image=menu_image)
            self.pet_image.tag_raise(self.pet_image_item)
            self.pet_image.image = menu_image
            self.evolve_note.configure(text=self.evolution_note(pet, drops_need))
        feed_state = "normal" if pet and self.app.food and not pet.evolving else "disabled"
        evolve_state = "normal" if pet and pet.can_evolve() else "disabled"
        release_state = "normal" if pet and len(self.app.pets) > 1 else "disabled"
        feed_reason = "포켓몬 없음" if not pet else "진화 중" if pet.evolving else "포켓푸드 없음"
        evolve_reason = ("포켓몬 없음" if not pet else "다음 진화 없음" if not pet.next_key
                         else "진화 중" if pet.evolving else "조건 미달")
        for button in (self.home_feed, self.pet_feed):
            button.configure(state=feed_state, bg=self.RED if feed_state == "normal" else self.SOFT,
                             activebackground=self.RED if feed_state == "normal" else self.SOFT,
                             text="먹이 주기" if feed_state == "normal" else "먹이 주기\n" + feed_reason)
        for button in (self.home_evolve, self.pet_evolve):
            button.configure(state=evolve_state, bg=self.BLUE if evolve_state == "normal" else self.SOFT,
                             activebackground=self.BLUE if evolve_state == "normal" else self.SOFT,
                             text="진화" if evolve_state == "normal" else "진화\n" + evolve_reason)
        self.pet_release.configure(state=release_state,
                                   text="보내주기…" if release_state == "normal" else "보내주기\n마지막 포켓몬")
        self.pause_button.configure(text="산책 재개" if self.app.paused else "전체 일시정지")
        self.top_button.configure(text="항상 위 %s" % ("켜짐" if self.topmost else "꺼짐"))
        if self.autostart_button is not None:
            self.autostart_button.configure(text="윈도우 시작 시 실행")
        for button in self.scale_buttons:
            self._refresh_choice_button(button, abs(self.app.scale - button.choice_value) < 0.01)
        for button in self.speed_buttons:
            self._refresh_choice_button(button, abs(self.app.speed - button.choice_value) < 0.01)
        self._refresh_choice_button(self.top_button, self.topmost)
        self._refresh_choice_button(self.pause_button, self.app.paused)
        if self.autostart_button is not None:
            self._refresh_choice_button(self.autostart_button, self.app.autostart_var.get())
        self._refresh_choice_button(self.back_button, False)
        value = self.app.stock_portfolio_value()
        percent = self.app.stock_portfolio_change_percent()
        self.shortcut_buttons["stock"].configure(
            text="주식시장\n%s · %+.1f%%  ›" % (format_won(value), percent))
        self.stock_heading_hint.configure(text=self.app.market_session_text())
        self.stock_portfolio.configure(text="%s (%+.1f%%)" % (format_won(value), percent))
        self.stock_cash.configure(text=format_won(self.app.coins))
        self.market_regime.configure(text="시장 국면 · %s" % self.app.market_regime_label())
        if self.app.market_is_open:
            left = max(1, int(math.ceil(MARKET_UPDATE_SEC - self.app.market_seconds)))
            self.market_update.configure(text="%d초 후 갱신" % left)
        else:
            self.market_update.configure(text="휴장 중")
        positions = []
        for index, shares in enumerate(self.app.stock_shares):
            if shares > 0 and len(positions) < 3:
                positions.append("%s  %d주  ·  %s" % (
                    self.app.stock_name(index), shares,
                    format_won(shares * self.app.stock_prices[index])))
        self.stock_positions_preview.configure(text="\n".join(positions) if positions else
                                               "보유 종목이 없습니다.\n전체 주식창에서 종목을 살펴보세요.")
        movers = sorted(range(STOCK_COUNT),
                        key=lambda index: abs(self.app.stock_change_percent(index)), reverse=True)[:2]
        market_lines = ["%s  %+.1f%%" % (self.app.stock_name(index),
                                         self.app.stock_change_percent(index)) for index in movers]
        market_lines.append("최근 소식 · %s" % (self.app.stock_event or "새 소식을 기다리는 중"))
        self.stock_market_preview.configure(text="\n".join(market_lines))
        save_age = max(0, int(time.time() - self.app.last_save_time))
        if save_age < 10:
            saved_text = "방금 전"
        elif save_age < 60:
            saved_text = "%d초 전" % save_age
        elif save_age < 3600:
            saved_text = "%d분 전" % (save_age // 60)
        else:
            saved_text = time.strftime("%H:%M", time.localtime(self.app.last_save_time))
        self.saved_label.configure(text="최근 저장됨 · " + saved_text)

    def _refresh_choice_button(self, button, selected):
        label = button.cget("text")
        if label.startswith("✓ "):
            label = label[2:]
        button.configure(text=("✓ " + label) if selected else label,
                         bg=self.BLUE if selected else self.PANEL,
                         fg="white" if selected else self.INK,
                         activebackground=self.BLUE if selected else self.SOFT,
                         activeforeground="white" if selected else self.INK)

    def _keyboard_select_tab(self, key):
        self.select_tab(key)
        self.tab_buttons[key].focus_set()
        return "break"

    def evolution_note(self, pet, drops_need=None):
        if not pet.next_key:
            return "현재 등록된 다음 진화가 없습니다."
        if pet.evolving:
            return "진화하는 중입니다…"
        if pet.can_evolve():
            return "%s로 진화할 준비가 완료되었습니다!" % POKEMON[pet.next_key].name_ko
        if drops_need is None:
            drops_need = pet.evolution_requirement()[2]
        needs = []
        if pet.pets_left(): needs.append("친밀도 %d" % pet.pets_left())
        if pet.walk_left(): needs.append("산책 %spx" % "{:,}".format(pet.walk_left()))
        if self.app.growth_drops < drops_need:
            needs.append("성장의 물방울 %d개" % (drops_need - self.app.growth_drops))
        return "진화까지 " + " · ".join(needs)

    def begin_drag(self, event):
        self.drag_origin = (event.x_root - self.window.winfo_x(),
                            event.y_root - self.window.winfo_y())

    def drag(self, event):
        if self.drag_origin is None:
            return
        self.window.geometry("+%d+%d" % (event.x_root - self.drag_origin[0],
                                         event.y_root - self.drag_origin[1]))

    def minimize(self):
        # override-redirect 창도 작업 표시줄로 최소화되도록 잠시 기본 창 장식을 되돌린다.
        self.window.overrideredirect(False)
        self.window.iconify()
        self.window.after(120, lambda: self.window.overrideredirect(True))

    def auto_refresh(self):
        try:
            self.refresh()
            self.after_id = self.window.after(700, self.auto_refresh)
        except tk.TclError:
            self.after_id = None

    def set_selected(self, index):
        self.selected_index = min(max(0, index), max(0, len(self.app.pets) - 1))
        self.refresh()

    def run_action(self, action):
        action()
        self.refresh()

    def selected_pet(self):
        return self.app.pets[self.selected_index] if 0 <= self.selected_index < len(self.app.pets) else None

    def feed_selected(self):
        pet = self.selected_pet()
        if pet: self.run_action(lambda: self.app.feed_pet(pet))

    def evolve_selected(self):
        pet = self.selected_pet()
        if pet: self.run_action(pet.start_evolving)

    def release_selected(self):
        pet = self.selected_pet()
        if pet and messagebox.askyesno("포켓몬 보내주기",
                                      "%s을(를) 정말 보내줄까요?" % pet.pokemon.name_ko,
                                      parent=self.window):
            self.run_action(lambda: self.app.remove_pet(pet))

    def recall_selected(self):
        pet = self.selected_pet()
        if not pet:
            return
        pet.dragging = False
        pet.lift = 0.0
        pet.vertical_speed = 0.0
        pet.x = max(0, pet.max_x / 2.0)
        pet.place()
        pet.window.lift()

    def buy_food(self):
        before = self.app.food
        self.app.buy_food()
        success = self.app.food > before
        self.shop_feedback.configure(
            text=("●  포켓푸드 1개 구매 완료 · 남은 잔액 %s" % format_won(self.app.coins))
            if success else "!  포켓푸드를 구매할 잔액이 부족합니다.",
            fg=self.GREEN if success else self.RED)
        self.refresh()

    def buy_drop(self):
        before = self.app.growth_drops
        self.app.buy_growth_drop()
        success = self.app.growth_drops > before
        self.shop_feedback.configure(
            text=("●  성장의 물방울 1개 구매 완료 · 남은 잔액 %s" % format_won(self.app.coins))
            if success else "!  성장의 물방울을 구매할 잔액이 부족합니다.",
            fg=self.GREEN if success else self.RED)
        self.refresh()

    def buy_random(self):
        if not messagebox.askyesno("랜덤 영입", "%s을 사용해 새 포켓몬을 영입할까요?" %
                                   format_won(POKEMON_PRICE), parent=self.window):
            return
        before = len(self.app.pets)
        self.app.buy_random_pet()
        if len(self.app.pets) > before:
            self.selected_index = len(self.app.pets) - 1
            pet = self.selected_pet()
            messagebox.showinfo("영입 성공", "%s이(가) 새로운 친구가 되었습니다!" %
                                pet.pokemon.name_ko, parent=self.window)
            self.shop_feedback.configure(
                text="●  새 포켓몬 영입 완료 · 남은 잔액 %s" % format_won(self.app.coins),
                fg=self.GREEN)
        self.refresh()

    def toggle_pause(self):
        self.app.pause_var.set(not self.app.paused)
        self.app.toggle_pause()
        self.refresh()

    def toggle_autostart(self):
        self.app.autostart_var.set(not self.app.autostart_var.get())
        self.app.toggle_autostart()
        self.refresh()

    def confirm_quit(self):
        if messagebox.askyesno("게임 종료", "포켓몬 센터와 모든 포켓몬을 종료할까요?",
                               parent=self.window):
            self.app.quit()

    def toggle_topmost(self):
        self.topmost = not self.topmost
        self.window.wm_attributes("-topmost", self.topmost)
        self.top_button.configure(text="항상 위: %s" % ("켜짐" if self.topmost else "꺼짐"))

    def send_to_back(self):
        self.topmost = False
        self.window.wm_attributes("-topmost", False)
        self.top_button.configure(text="항상 위: 꺼짐")
        self.window.lower()

    def close(self):
        if self.after_id is not None:
            try:
                self.window.after_cancel(self.after_id)
            except tk.TclError:
                pass
            self.after_id = None
        if self.app.game_menu is self:
            self.app.game_menu = None
        try:
            self.window.destroy()
        except tk.TclError:
            pass


class StockOverlay:
    """주가·보유량·최근 가격 그래프를 한 창에 보여 주는 오버레이."""

    RISE = "#ff7a85"

    @classmethod
    def percent_colour(cls, value):
        """Return the shared stock percentage colour for gain, loss, or flat."""
        if value > 0:
            return cls.RISE
        if value < 0:
            return GameMenuOverlay.BLUE
        return GameMenuOverlay.MUTED

    @staticmethod
    def split_signed_percent(text):
        """Separate an event's signed percentage so it can receive its own colour."""
        for token in text.split():
            if len(token) > 2 and token[0] in "+-" and token.endswith("%"):
                try:
                    value = float(token[:-1])
                except ValueError:
                    continue
                cleaned = text.replace(token, "", 1)
                while "  " in cleaned:
                    cleaned = cleaned.replace("  ", " ")
                return cleaned.strip(), token, value
        return text, "", 0.0

    def __init__(self, app):
        self.app = app
        self.window = tk.Toplevel(app.root)
        self.window.overrideredirect(True)
        self.window.wm_attributes("-topmost", True)
        self.window.configure(bg=GameMenuOverlay.INK, padx=3, pady=3)
        compact = app.screen_width < 840 or app.screen_height < 920
        width = min(820, max(420, app.screen_width - 20)) if compact else 820
        height = min(900, max(420, app.screen_height - 20)) if compact else 900
        self.window.geometry("%dx%d+%d+%d" % (
            width, height, max(0, (app.screen_width - width) // 2),
            max(0, (app.screen_height - height) // 3),
        ))
        self.selected_index = 0
        self.buying = True
        self.owned_only = False
        self._build_toss_layout(compact)

    @staticmethod
    def make_button(parent, label, color, command):
        return tk.Button(
            parent, text=label, command=command, bg=color, fg="white",
            activebackground=color, activeforeground="white", bd=0,
            highlightbackground=GameMenuOverlay.INK, highlightthickness=2,
            padx=10, pady=2, font=(UI_FONT_FAMILY, 10, "bold"), cursor="hand2",
        )

    @staticmethod
    def make_quick_button(parent, label, command):
        """카드를 밀어내지 않는 작은 수량 바로가기 버튼."""
        return tk.Button(
            parent, text=label, command=command, bg=GameMenuOverlay.SOFT, fg=GameMenuOverlay.INK,
            activebackground=GameMenuOverlay.LINE, activeforeground=GameMenuOverlay.INK, bd=0,
            highlightbackground=GameMenuOverlay.LINE, highlightthickness=1,
            padx=3, pady=0, font=(UI_FONT_FAMILY, 9, "bold"), cursor="hand2",
        )

    def _build_toss_layout(self, compact):
        """토스증권처럼 목록은 가볍게, 선택 종목은 깊게 보는 두 패널 구성."""
        if compact:
            viewport = tk.Canvas(self.window, bg=MENU_RED, highlightthickness=0)
            vertical_scrollbar = tk.Scrollbar(self.window, command=viewport.yview)
            horizontal_scrollbar = tk.Scrollbar(
                self.window, orient="horizontal", command=viewport.xview)
            viewport.configure(
                yscrollcommand=vertical_scrollbar.set,
                xscrollcommand=horizontal_scrollbar.set,
            )
            vertical_scrollbar.pack(side="right", fill="y")
            horizontal_scrollbar.pack(side="bottom", fill="x")
            viewport.pack(side="left", fill="both", expand=True)
            body = tk.Frame(viewport, bg=GameMenuOverlay.PAPER, width=814, height=894)
            body.pack_propagate(False)
            viewport.create_window((0, 0), window=body, anchor="nw")
            body.bind("<Configure>", lambda _event: viewport.configure(
                scrollregion=viewport.bbox("all")))
        else:
            body = tk.Frame(self.window, bg=GameMenuOverlay.PAPER)
            body.pack(fill="both", expand=True)

        header = tk.Frame(body, bg=GameMenuOverlay.RED, height=46)
        header.pack(fill="x")
        tk.Label(header, text="포켓몬 주식시장", bg=GameMenuOverlay.RED, fg="white",
                 font=(UI_FONT_FAMILY, 13, "bold")).pack(side="left", padx=16, pady=9)
        self.update_hint = tk.Label(header, bg=GameMenuOverlay.RED, fg="#fce1e2",
                                    font=(UI_FONT_FAMILY, 10, "bold"))
        self.session_badge = tk.Label(header, bg="#ffe8cc", fg="#9c2f31",
                                      font=(UI_FONT_FAMILY, 10, "bold"), padx=7, pady=2)
        close_button = tk.Button(
            header, text="×", command=self.close, bg=GameMenuOverlay.RED_DARK, fg="white",
            activebackground=GameMenuOverlay.RED_DARK, activeforeground="white", bd=0,
            font=(UI_FONT_FAMILY, 14, "bold"), cursor="hand2")
        close_button.pack(side="right", padx=8, pady=4)
        self.session_badge.pack(side="right", padx=(0, 7), pady=10)
        self.update_hint.pack(side="right", padx=(0, 12), pady=14)

        portfolio = tk.Frame(body, bg=GameMenuOverlay.PANEL,
                             highlightbackground=GameMenuOverlay.LINE, highlightthickness=2)
        portfolio.pack(fill="x", padx=12, pady=10)
        metrics = tk.Frame(portfolio, bg=GameMenuOverlay.PANEL)
        metrics.pack(fill="x", padx=14, pady=(7, 2))
        total_metric = tk.Frame(metrics, bg=GameMenuOverlay.PANEL)
        total_metric.pack(side="left", fill="x", expand=True)
        tk.Label(total_metric, text="내 투자 현황", bg=GameMenuOverlay.PANEL,
                 fg=GameMenuOverlay.MUTED, anchor="w",
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(fill="x")
        self.balance = tk.Label(total_metric, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.INK,
                                anchor="w", font=(UI_FONT_FAMILY, 17, "bold"))
        self.balance.pack(fill="x")
        cash_metric = tk.Frame(metrics, bg=GameMenuOverlay.PANEL, width=155)
        cash_metric.pack(side="left", padx=(12, 0))
        tk.Label(cash_metric, text="보유 현금", bg=GameMenuOverlay.PANEL,
                 fg=GameMenuOverlay.MUTED, anchor="w",
                 font=(UI_FONT_FAMILY, 10)).pack(fill="x")
        self.cash_value = tk.Label(cash_metric, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.INK,
                                   anchor="w", font=(UI_FONT_FAMILY, 12, "bold"))
        self.cash_value.pack(fill="x")
        stock_metric = tk.Frame(metrics, bg=GameMenuOverlay.PANEL, width=175)
        stock_metric.pack(side="left", padx=(10, 0))
        tk.Label(stock_metric, text="주식 평가액", bg=GameMenuOverlay.PANEL,
                 fg=GameMenuOverlay.MUTED, anchor="w",
                 font=(UI_FONT_FAMILY, 10)).pack(fill="x")
        portfolio_value_row = tk.Frame(stock_metric, bg=GameMenuOverlay.PANEL)
        portfolio_value_row.pack(fill="x")
        self.portfolio_value = tk.Label(
            portfolio_value_row, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.INK,
            anchor="w", font=(UI_FONT_FAMILY, 12, "bold"))
        self.portfolio_value.pack(side="left")
        self.portfolio_percent = tk.Label(
            portfolio_value_row, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.MUTED,
            anchor="w", font=(UI_FONT_FAMILY, 12, "bold"))
        self.portfolio_percent.pack(side="left", padx=(6, 0))
        self.market_summary = tk.Label(portfolio, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.MUTED, anchor="w",
                                       font=(UI_FONT_FAMILY, 10, "bold"), padx=14, pady=2)
        self.market_summary.pack(fill="x")

        content = tk.Frame(body, bg=GameMenuOverlay.PAPER)
        content.pack(fill="both", expand=True, padx=12, pady=(0, 12))
        watch = tk.Frame(content, bg=GameMenuOverlay.PANEL, width=250,
                         highlightbackground=GameMenuOverlay.LINE, highlightthickness=2)
        watch.pack(side="left", fill="y")
        watch.pack_propagate(False)
        watch_header = tk.Frame(watch, bg=GameMenuOverlay.PANEL)
        watch_header.pack(fill="x", padx=7, pady=7)
        self.all_stocks_tab = self.make_quick_button(
            watch_header, "전체", lambda: self.set_stock_filter(False))
        self.all_stocks_tab.pack(side="left", ipadx=7, ipady=3)
        self.owned_stocks_tab = self.make_quick_button(
            watch_header, "보유", lambda: self.set_stock_filter(True))
        self.owned_stocks_tab.pack(side="left", padx=4, ipadx=7, ipady=3)
        tk.Label(watch_header, text="현재가", bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.MUTED,
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(side="right")
        self.list_rows = []
        for index in range(STOCK_COUNT):
            row = tk.Frame(watch, bg=GameMenuOverlay.PANEL, height=72, cursor="hand2", takefocus=True,
                           highlightbackground=GameMenuOverlay.LINE, highlightcolor=GameMenuOverlay.RED,
                           highlightthickness=1)
            row.pack(fill="x", padx=7, pady=1)
            row.pack_propagate(False)
            accent = tk.Frame(row, bg=GameMenuOverlay.PANEL, width=4)
            accent.place(x=0, y=0, relheight=1)
            name = tk.Label(row, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.INK, anchor="w",
                            font=(UI_FONT_FAMILY, 11, "bold"))
            name.place(x=11, y=9, width=128)
            holding = tk.Label(row, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.MUTED, anchor="w",
                                font=(UI_FONT_FAMILY, 10))
            holding.place(x=11, y=37, width=132)
            price = tk.Label(row, bg=GameMenuOverlay.PANEL, anchor="e", font=(UI_FONT_FAMILY, 11, "bold"))
            price.place(x=141, y=9, width=94)
            change = tk.Label(row, bg=GameMenuOverlay.PANEL, anchor="e", font=(UI_FONT_FAMILY, 10, "bold"))
            change.place(x=141, y=37, width=94)
            for widget in (row, accent, name, holding, price, change):
                widget.bind("<Button-1>", lambda _event, index=index: self.select_stock(index, False))
            row.bind("<Return>", lambda _event, index=index: self.select_stock(index, True))
            row.bind("<space>", lambda _event, index=index: self.select_stock(index, True))
            row.bind("<Up>", lambda _event, index=index: self.select_stock((index - 1) % STOCK_COUNT, True))
            row.bind("<Left>", lambda _event, index=index: self.select_stock((index - 1) % STOCK_COUNT, True))
            row.bind("<Down>", lambda _event, index=index: self.select_stock((index + 1) % STOCK_COUNT, True))
            row.bind("<Right>", lambda _event, index=index: self.select_stock((index + 1) % STOCK_COUNT, True))
            self.list_rows.append((row, accent, name, holding, price, change))

        detail = tk.Frame(content, bg=GameMenuOverlay.PANEL,
                          highlightbackground=GameMenuOverlay.LINE, highlightthickness=2)
        detail.pack(side="left", fill="both", expand=True, padx=(10, 0))
        self.detail_name = tk.Label(detail, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.INK, anchor="w",
                                    font=(UI_FONT_FAMILY, 15, "bold"), padx=18, pady=10)
        self.detail_name.pack(fill="x")
        price_row = tk.Frame(detail, bg=GameMenuOverlay.PANEL)
        price_row.pack(fill="x", padx=18)
        self.detail_price = tk.Label(price_row, bg=GameMenuOverlay.PANEL, anchor="w",
                                     font=(UI_FONT_FAMILY, 22, "bold"))
        self.detail_price.pack(side="left", fill="x", expand=True)
        self.detail_change = tk.Label(price_row, bg=GameMenuOverlay.PANEL, anchor="e",
                                      font=(UI_FONT_FAMILY, 12, "bold"))
        self.detail_change.pack(side="right")
        self.detail_meta = tk.Label(detail, bg=GameMenuOverlay.PANEL, fg=GameMenuOverlay.MUTED,
                                    anchor="w", justify="left", wraplength=500,
                                    font=(UI_FONT_FAMILY, 10), padx=18, pady=3)
        self.detail_meta.pack(fill="x")
        self.detail_session = tk.Label(detail, bg=GameMenuOverlay.SOFT, fg=GameMenuOverlay.INK, anchor="w",
                                       font=(UI_FONT_FAMILY, 9, "bold"), padx=12, pady=4)
        self.detail_session.pack(fill="x", padx=16, pady=(0, 3))
        self.detail_graph = tk.Canvas(detail, width=500, height=155, bg=GameMenuOverlay.PANEL,
                                      highlightthickness=0)
        self.detail_graph.pack(fill="x", padx=16, pady=(2, 5))
        holding_card = tk.Frame(detail, bg=GameMenuOverlay.SOFT, height=68)
        holding_card.pack(fill="x", padx=16, pady=(0, 6))
        holding_card.pack_propagate(False)
        self.detail_holding = tk.Label(
            holding_card, bg=GameMenuOverlay.SOFT, fg=GameMenuOverlay.INK, anchor="w",
            justify="left", font=(UI_FONT_FAMILY, 11, "bold"), padx=12, pady=7)
        self.detail_holding.place(x=0, y=0, relwidth=1, relheight=1)
        self.detail_profit_percent = tk.Label(
            holding_card, bg=GameMenuOverlay.SOFT, fg=GameMenuOverlay.MUTED, anchor="e",
            font=(UI_FONT_FAMILY, 11, "bold"))
        self.detail_profit_percent.place(relx=1, x=-12, y=27, width=78, anchor="ne")
        self.event_card = tk.Frame(detail, bg="#283d5a",
                                   highlightbackground=GameMenuOverlay.LINE, highlightthickness=1)
        self.event_card.pack(fill="x", padx=16, pady=(0, 7))
        self.event_title = tk.Label(self.event_card, bg="#283d5a", fg=GameMenuOverlay.MUTED,
                                    anchor="w", font=(UI_FONT_FAMILY, 10, "bold"), padx=12, pady=2)
        self.event_title.pack(fill="x")
        self.detail_event = tk.Label(self.event_card, bg="#283d5a", fg=GameMenuOverlay.INK,
                                     anchor="w", justify="left", wraplength=490,
                                     font=(UI_FONT_FAMILY, 10), padx=12, pady=3)
        self.detail_event.pack(fill="x")
        self.event_percent = tk.Label(
            self.event_card, bg="#283d5a", fg=GameMenuOverlay.MUTED, anchor="e",
            font=(UI_FONT_FAMILY, 10, "bold"))
        self.event_percent.place(relx=1, x=-12, y=2, width=82, anchor="ne")
        order = tk.Frame(detail, bg=GameMenuOverlay.SOFT,
                         highlightbackground=GameMenuOverlay.LINE, highlightthickness=1,
                         padx=12, pady=10)
        order.pack(fill="x", padx=16, pady=(0, 12))
        trade_tabs = tk.Frame(order, bg=GameMenuOverlay.SOFT)
        trade_tabs.pack(fill="x", pady=(0, 8))
        self.buy_tab = self.make_button(trade_tabs, "매수", GameMenuOverlay.RED,
                                        lambda: self.set_trade_mode(True))
        self.buy_tab.pack(side="left", fill="x", expand=True, padx=(0, 4))
        self.sell_tab = self.make_button(trade_tabs, "매도", GameMenuOverlay.PANEL,
                                         lambda: self.set_trade_mode(False))
        self.sell_tab.pack(side="left", fill="x", expand=True, padx=(4, 0))
        quantity_row = tk.Frame(order, bg=GameMenuOverlay.SOFT)
        quantity_row.pack(fill="x")
        tk.Label(quantity_row, text="주문 수량", bg=GameMenuOverlay.SOFT, fg=GameMenuOverlay.INK,
                 font=(UI_FONT_FAMILY, 10, "bold")).pack(side="left")
        self.quantity = tk.Spinbox(
            quantity_row, from_=1, to=STOCK_MAX_ORDER_QUANTITY, width=9, justify="center",
                                   font=(UI_FONT_FAMILY, 11, "bold"), command=self.refresh)
        self.quantity.pack(side="left", padx=(8, 10), ipady=2)
        self.quantity.bind("<KeyRelease>", lambda _event: self.refresh())
        for label, amount in (("1", 1), ("5", 5), ("10", 10)):
            self.make_quick_button(quantity_row, label, lambda amount=amount: self.set_selected_quantity(amount)).pack(
                side="left", padx=2, ipadx=7, ipady=2)
        self.make_quick_button(quantity_row, "최대", self.set_maximum_quantity).pack(
            side="left", padx=(4, 0), ipadx=7, ipady=2)
        self.order_summary = tk.Label(order, bg=GameMenuOverlay.SOFT, fg=GameMenuOverlay.INK,
                                      anchor="w", justify="left", font=(UI_FONT_FAMILY, 10), pady=7)
        self.order_summary.pack(fill="x")
        self.action = self.make_button(order, "매수하기", GameMenuOverlay.RED,
                                       lambda: self.trade_selected(self.buying))
        self.action.pack(fill="x", ipady=6)
        self.trade_toast = tk.Frame(body, bg="#254843",
                                    highlightbackground=GameMenuOverlay.GREEN,
                                    highlightthickness=2)
        self.trade_toast_title = tk.Label(
            self.trade_toast, bg="#254843", fg=GameMenuOverlay.GREEN,
            anchor="w", font=(UI_FONT_FAMILY, 11, "bold"), padx=14, pady=4)
        self.trade_toast_title.pack(fill="x")
        self.trade_toast_detail = tk.Label(
            self.trade_toast, bg="#254843", fg=GameMenuOverlay.INK,
            anchor="w", font=(UI_FONT_FAMILY, 10), padx=14, pady=2)
        self.trade_toast_detail.pack(fill="x")
        self.trade_toast_after = None
        self.window.bind("<Escape>", self.close_on_escape)
        self.window.protocol("WM_DELETE_WINDOW", self.close)
        self.drag_origin = None
        for widget in (header, self.update_hint):
            widget.bind("<ButtonPress-1>", self.begin_drag)
            widget.bind("<B1-Motion>", self.drag)
        self.refresh_toss()

    def select_stock(self, index, focus=False):
        self.selected_index = index
        if focus:
            self.list_rows[index][0].focus_set()
        else:
            self.window.focus_set()
        self.refresh_toss()
        return "break"

    def set_selected_quantity(self, quantity):
        self.quantity.delete(0, "end")
        self.quantity.insert(0, str(min(STOCK_MAX_ORDER_QUANTITY, max(1, quantity))))
        self.refresh_toss()

    def set_trade_mode(self, buying):
        self.buying = buying
        self.refresh_toss()

    def keyboard_trade_mode(self, buying):
        self.set_trade_mode(buying)
        self.action.focus_set()
        return "break"

    def set_stock_filter(self, owned_only):
        if owned_only and not self.app.stock_shares[self.selected_index]:
            first_owned = next((index for index, shares in enumerate(self.app.stock_shares)
                                if shares), None)
            if first_owned is None:
                return
            self.selected_index = first_owned
        self.owned_only = owned_only
        self.refresh_toss()

    def refresh_event_card(self, index):
        background = "#283d5a"
        border = GameMenuOverlay.LINE
        title_colour = GameMenuOverlay.MUTED
        if not self.app.market_is_open:
            title = "●  시장 휴장"
            text = self.app.market_session_text() + " · 재개 후 주문할 수 있습니다."
        elif self.app.stock_halt_seconds[index]:
            title = "●  선택 종목 거래 정지"
            text = "변동성 완화장치 작동 중 · %d초 후 거래 재개" % self.app.stock_halt_seconds[index]
            background = "#433042"
            border = self.RISE
            title_colour = self.RISE
        elif not self.app.stock_event:
            title = "●  시장 알림"
            text = "새 이벤트를 기다리는 중입니다."
        elif self.app.stock_name(index) in self.app.stock_event:
            title = "●  선택 종목 이벤트"
            text = self.app.stock_event
            background = "#3a3749"
            border = GameMenuOverlay.YELLOW
            title_colour = GameMenuOverlay.YELLOW
        else:
            title = "●  전체 시장 이벤트"
            text = self.app.stock_event + " · 선택 종목과 직접 관련 없는 소식"
            title_colour = GameMenuOverlay.YELLOW
        text, percent_text, percent_value = self.split_signed_percent(text)
        self.event_card.configure(bg=background, highlightbackground=border)
        self.event_title.configure(text=title, bg=background, fg=title_colour)
        self.detail_event.configure(text=text, bg=background, fg=GameMenuOverlay.INK)
        self.event_percent.configure(
            text=percent_text, bg=background,
            fg=self.percent_colour(percent_value) if percent_text else GameMenuOverlay.MUTED,
        )

    def show_trade_feedback(self, success, title, detail):
        background = "#254843" if success else "#433042"
        accent = GameMenuOverlay.GREEN if success else self.RISE
        self.trade_toast.configure(bg=background, highlightbackground=accent)
        self.trade_toast_title.configure(
            text=("✓  " if success else "!  ") + title, bg=background, fg=accent)
        self.trade_toast_detail.configure(text=detail, bg=background)
        self.trade_toast.place(x=287, y=62, width=500, height=72)
        self.trade_toast.lift()
        if self.trade_toast_after is not None:
            self.window.after_cancel(self.trade_toast_after)
        self.trade_toast_after = self.window.after(2800, self.trade_toast.place_forget)

    def set_maximum_quantity(self):
        maximum = (self.app.stock_maximum_buy_quantity(self.selected_index) if self.buying
                   else self.app.stock_maximum_sell_quantity(self.selected_index))
        self.set_selected_quantity(max(1, maximum))

    def selected_quantity_toss(self):
        try:
            return min(STOCK_MAX_ORDER_QUANTITY, max(1, int(self.quantity.get())))
        except (ValueError, tk.TclError):
            return 1

    def trade_selected(self, buying):
        index = self.selected_index
        quantity = self.selected_quantity_toss()
        amount = (self.app.stock_buy_cost(index) if buying else self.app.stock_sell_proceeds(index)) * quantity
        if self.app.stock_delisted[index]:
            self.show_trade_feedback(False, "주문할 수 없습니다", "상장폐지된 종목입니다.")
            return
        if not self.app.market_is_open:
            self.show_trade_feedback(False, "지금은 휴장 중입니다", self.app.market_session_text())
            return
        if self.app.stock_halt_seconds[index]:
            self.show_trade_feedback(False, "거래가 일시 정지됐습니다",
                                     "%d초 후 다시 시도해 주세요." % self.app.stock_halt_seconds[index])
            return
        if buying and self.app.coins < amount:
            self.show_trade_feedback(False, "보유금이 부족합니다",
                                     "%s이 더 필요합니다." % format_won(amount - self.app.coins))
            return
        if not buying and self.app.stock_shares[index] < quantity:
            self.show_trade_feedback(False, "보유 수량이 부족합니다",
                                     "현재 %d주를 보유하고 있습니다." % self.app.stock_shares[index])
            return
        if quantity >= 10 or (buying and amount >= self.app.coins * 0.2):
            action = "매수" if buying else "매도"
            if not messagebox.askyesno("거래 확인", "%s %d주\n%s\n거래할까요?" % (
                    action, quantity, format_won(amount)), parent=self.window):
                return
        if buying:
            self.app.buy_stock(index, quantity)
            self.refresh_toss()
            self.show_trade_feedback(
                True, "매수 완료 · %s %d주" % (self.app.stock_name(index), quantity),
                "%s · 남은 현금 %s" % (format_won(amount), format_won(self.app.coins)))
        else:
            self.app.sell_stock(index, quantity)
            self.refresh_toss()
            self.show_trade_feedback(
                True, "매도 완료 · %s %d주" % (self.app.stock_name(index), quantity),
                "%s · 남은 보유 %d주" % (format_won(amount), self.app.stock_shares[index]))

    def refresh_toss(self):
        self.buy_tab.configure(
            bg=GameMenuOverlay.RED if self.buying else GameMenuOverlay.PANEL,
            fg="white" if self.buying else GameMenuOverlay.MUTED,
            activebackground=GameMenuOverlay.RED if self.buying else GameMenuOverlay.PANEL,
        )
        self.sell_tab.configure(
            bg=GameMenuOverlay.PANEL if self.buying else GameMenuOverlay.BLUE,
            fg=GameMenuOverlay.MUTED if self.buying else "white",
            activebackground=GameMenuOverlay.PANEL if self.buying else GameMenuOverlay.BLUE,
        )
        self.action.configure(
            bg=GameMenuOverlay.RED if self.buying else GameMenuOverlay.BLUE,
            activebackground=GameMenuOverlay.RED if self.buying else GameMenuOverlay.BLUE,
        )
        total = self.app.stock_portfolio_value()
        percent = self.app.stock_portfolio_change_percent()
        self.balance.configure(text=format_won(self.app.coins + total))
        self.cash_value.configure(text=format_won(self.app.coins))
        self.portfolio_value.configure(text=format_won(total))
        self.portfolio_percent.configure(
            text="%+.1f%%" % percent, fg=self.percent_colour(percent))
        self.market_summary.configure(text=self.app.market_mover_summary())
        owned_count = sum(1 for shares in self.app.stock_shares if shares)
        self.all_stocks_tab.configure(
            text="전체 %d" % STOCK_COUNT,
            bg=GameMenuOverlay.PANEL if self.owned_only else GameMenuOverlay.SOFT,
            fg=GameMenuOverlay.MUTED if self.owned_only else GameMenuOverlay.INK,
        )
        self.owned_stocks_tab.configure(
            text="보유 %d" % owned_count,
            bg=GameMenuOverlay.SOFT if self.owned_only else GameMenuOverlay.PANEL,
            fg=GameMenuOverlay.INK if self.owned_only else GameMenuOverlay.MUTED,
            state="normal" if owned_count else "disabled",
        )
        update_text = self.app.market_session_text()
        if self.app.market_is_open:
            update_text += " · %d초 후 갱신" % max(
                1, int(math.ceil(MARKET_UPDATE_SEC - self.app.market_seconds))
            )
        self.update_hint.configure(text=update_text)
        self.session_badge.configure(
            text="개장" if self.app.market_is_open else "휴장",
            bg="#ffe8cc" if self.app.market_is_open else "#dfe7f3",
            fg="#9c2f31" if self.app.market_is_open else "#4b5f7a",
        )
        for index, (row, accent, name, holding, price, change) in enumerate(self.list_rows):
            row.pack_forget()
            if not self.owned_only or self.app.stock_shares[index]:
                row.pack(fill="x", padx=7, pady=1)
            selected = index == self.selected_index
            background = GameMenuOverlay.SOFT if selected else GameMenuOverlay.PANEL
            foreground = GameMenuOverlay.INK
            current = self.app.stock_prices[index]
            delta = self.app.stock_change_percent(index)
            colour = self.percent_colour(delta)
            for widget in (row, name, holding, price, change):
                widget.configure(bg=background)
            accent.configure(bg=self.RISE if selected else background)
            name.configure(text=self.app.stock_name(index), fg=foreground)
            price.configure(text="상장폐지" if self.app.stock_delisted[index] else format_won(current), fg=colour)
            change.configure(text="신규 상장 대기" if self.app.stock_delisted[index] else "%+.1f%%" % delta, fg=colour)
            holding.configure(text="보유 %d주" % self.app.stock_shares[index]
                              if self.app.stock_shares[index]
                              else self.app.stock_primary_trait(index)["name"])
        index = self.selected_index
        price = self.app.stock_prices[index]
        delta = self.app.stock_change_percent(index)
        colour = self.percent_colour(delta)
        price_colour = colour if delta else GameMenuOverlay.INK
        self.detail_name.configure(text=self.app.stock_name(index))
        opening_price = self.app.stock_session_open_prices[index]
        self.detail_session.configure(text="장 기준가 %s  ·  %s" % (
            format_won(opening_price),
            "거래 가능" if self.app.market_is_open else "휴장 중 · 주문 불가",
        ), fg=GameMenuOverlay.GREEN if self.app.market_is_open else GameMenuOverlay.MUTED)
        if self.app.stock_delisted[index]:
            self.detail_price.configure(text="상장폐지", fg=GameMenuOverlay.RED)
            self.detail_change.configure(text="신규 상장 대기", fg=GameMenuOverlay.MUTED)
            self.detail_meta.configure(text="신규 상장까지 %d분" % max(1, int(math.ceil(self.app.stock_relist_seconds[index] / 60.0))))
            self.detail_holding.configure(text="보유 주식은 소멸했습니다. 새 종목 상장을 기다려 주세요.")
            self.detail_profit_percent.configure(text="", fg=GameMenuOverlay.MUTED)
            self.order_summary.configure(text="상장폐지 종목은 주문할 수 없습니다.")
            self.action.configure(text="주문할 수 없습니다", state="disabled")
            self.quantity.configure(state="disabled")
        else:
            self.detail_price.configure(text=format_won(price), fg=price_colour)
            self.detail_change.configure(text="장 시작 대비  %+.1f%%" % delta, fg=colour)
            self.detail_meta.configure(text="%s · 위험도 %s · 기본 변동폭 ±%d%%\n%s" % (
                self.app.stock_profile(index), self.app.stock_risk_label(index),
                self.app.stock_listing(index)[2], self.app.stock_profile_description(index),
            ))
            profit_percent = self.app.stock_profit_percent(index)
            self.detail_holding.configure(text=self.app.stock_position_text(index, include_percent=False))
            self.detail_profit_percent.configure(
                text="%+.1f%%" % profit_percent if self.app.stock_shares[index] else "",
                fg=self.percent_colour(profit_percent),
            )
            if not self.app.market_is_open:
                self.order_summary.configure(text="휴장 중에는 주문할 수 없습니다.")
                self.action.configure(text="휴장 중 · 주문 불가", state="disabled")
                self.quantity.configure(state="disabled")
            elif self.app.stock_halt_seconds[index]:
                self.order_summary.configure(text="변동성 완화장치가 해제되면 주문할 수 있습니다.")
                self.action.configure(text="거래 정지 · 주문 불가", state="disabled")
                self.quantity.configure(state="disabled")
            else:
                quantity = self.selected_quantity_toss()
                self.quantity.configure(state="normal")
                gross = price * quantity
                amount = (self.app.stock_buy_cost(index) if self.buying
                          else self.app.stock_sell_proceeds(index)) * quantity
                fee = abs(amount - gross)
                if self.buying:
                    maximum = self.app.stock_maximum_buy_quantity(index)
                    self.order_summary.configure(text=(
                        "주문금액 %s  ·  수수료 %s\n주문 후 현금 %s  ·  이번 주문 최대 %d주" % (
                            format_won(amount), format_won(fee),
                            format_won(max(0, self.app.coins - amount)), maximum)))
                    affordable = self.app.coins >= amount
                    self.action.configure(
                        text=("%d주 매수하기\n%s" % (quantity, format_won(amount))) if affordable
                        else "보유금이 부족합니다\n%s 필요" % format_won(amount),
                        state="normal" if affordable else "disabled")
                else:
                    shares = self.app.stock_shares[index]
                    self.order_summary.configure(text=(
                        "예상 수령액 %s  ·  수수료 %s\n현재 보유 %d주  ·  매도 후 %d주  ·  최대 %d주" % (
                            format_won(amount), format_won(fee), shares,
                            max(0, shares - quantity), self.app.stock_maximum_sell_quantity(index))))
                    enough = shares >= quantity
                    self.action.configure(
                        text=("%d주 매도하기\n%s" % (quantity, format_won(amount)))
                        if enough else "보유 수량이 부족합니다",
                        state="normal" if enough else "disabled")
        self.refresh_event_card(index)
        self.draw_graph(self.detail_graph, self.app.stock_history[index], opening_price)

    def close_on_escape(self, _event):
        """창만 닫고 전역 Esc 종료 단축키로 전파하지 않는다."""
        self.close()
        return "break"

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
    def draw_graph(canvas, values, opening_price=None):
        """최근 가격, 장 기준가, 최고·최저를 한눈에 보여 주는 선 그래프."""
        canvas.delete("all")
        width = max(int(canvas.cget("width")), canvas.winfo_width())
        height = int(canvas.cget("height"))
        reference = opening_price if opening_price and opening_price > 0 else values[0]
        actual_low, actual_high = min(min(values), reference), max(max(values), reference)
        value_range = max(1, actual_high - actual_low)
        margin = max(50, value_range // 4)
        low, high = actual_low - margin, actual_high + margin
        spread = max(1, high - low)
        plot_top, plot_bottom = 29, height - 24
        plot_height = plot_bottom - plot_top
        for share in (0.25, 0.5, 0.75):
            y = int(plot_top + plot_height * share)
            canvas.create_line(0, y, width, y, fill=GameMenuOverlay.LINE)
        reference_y = plot_bottom - plot_height * (reference - low) / spread
        canvas.create_line(0, reference_y, width, reference_y,
                           fill=GameMenuOverlay.MUTED, dash=(4, 4))
        points = []
        for index, value in enumerate(values):
            x = 4 if len(values) == 1 else 4 + (width - 8) * index / (len(values) - 1)
            y = plot_bottom - plot_height * (value - low) / spread
            points.extend((x, y))
        colour = StockOverlay.RISE if values[-1] >= values[0] else GameMenuOverlay.BLUE
        if len(points) >= 4:
            canvas.create_line(*points, fill=colour, width=3, smooth=True)
        canvas.create_oval(points[-2] - 3, points[-1] - 3,
                           points[-2] + 3, points[-1] + 3,
                           fill=colour, outline=colour)
        label_font = (UI_FONT_FAMILY, 10, "bold")
        canvas.create_text(0, 12, text="최근 20회", fill=GameMenuOverlay.MUTED,
                           font=label_font, anchor="w")
        canvas.create_text(width, 12, text="최고 " + format_won(actual_high),
                           fill=GameMenuOverlay.MUTED, font=label_font, anchor="e")
        canvas.create_text(0, height - 10, text="장 시작 " + format_won(reference),
                           fill=GameMenuOverlay.MUTED, font=label_font, anchor="w")
        canvas.create_text(width, height - 10, text="최저 " + format_won(actual_low),
                           fill=GameMenuOverlay.MUTED, font=label_font, anchor="e")
        if len(values) == 1:
            canvas.create_text(width / 2, reference_y - 17,
                               text="가격 데이터를 모으는 중입니다",
                               fill=GameMenuOverlay.MUTED, font=label_font)

    def refresh(self):
        self.refresh_toss()

    def close(self):
        if self.app.stock_overlay is self:
            self.app.stock_overlay = None
        if getattr(self, "trade_toast_after", None) is not None:
            try:
                self.window.after_cancel(self.trade_toast_after)
            except tk.TclError:
                pass
        try:
            self.window.destroy()
        except tk.TclError:
            pass


class App:
    """펫 여러 마리를 관리하는 본체."""

    def __init__(self, args):
        self.ui_font_loaded = register_ui_font()
        self.root = tk.Tk()
        configure_tk_ui_fonts(self.root)
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
        self.stock_primary_trait_ids = [
            value % len(STOCK_PRIMARY_TRAITS) for value in args.stock_primary_trait_ids]
        self.stock_secondary_trait_ids = [
            value % len(STOCK_SECONDARY_TRAITS) for value in args.stock_secondary_trait_ids]
        self.stock_delisted = [bool(value) for value in args.stock_delisted]
        self.stock_relist_seconds = list(args.stock_relist_seconds)
        self.stock_average_prices = list(args.stock_average_prices)
        self.stock_halt_seconds = list(args.stock_halt_seconds)
        self.stock_history = [[price] for price in self.stock_prices]
        self.stock_session_open_prices = list(self.stock_prices)
        self.stock_event = ""
        self.stock_event_history = []
        self.market_regime = 0
        self.market_regime_updates = MARKET_REGIME_UPDATES[0]
        self.stock_trends = [0] * STOCK_COUNT
        self.stock_overlay = None
        self.game_menu = None
        self.market_seconds = 0.0
        self.market_is_open = True
        self.market_session_seconds = float(MARKET_OPEN_SECONDS)
        self.halt_seconds = 0.0
        self.coin_walk_progress = 0.0
        self.last_save_time = time.time()
        # 메뉴의 체크/선택 표시를 여러 창이 함께 쓰도록 앱이 들고 있는다.
        self.scale_var = tk.DoubleVar(master=self.root, value=self.scale)
        self.speed_var = tk.DoubleVar(master=self.root, value=self.speed)
        self.pause_var = tk.BooleanVar(master=self.root, value=False)
        self.autostart_var = tk.BooleanVar(master=self.root, value=autostart_enabled())

        for index, key in enumerate(args.species):
            boost = args.food_boost_seconds[index] if index < len(args.food_boost_seconds) else 0
            self.add_pet(key, boost)
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
            "currency_version": settings_file.CURRENCY_VERSION,
            "stock_prices": list(self.stock_prices),
            "stock_shares": list(self.stock_shares),
            "stock_listing_ids": list(self.stock_listing_ids),
            "stock_primary_trait_ids": list(self.stock_primary_trait_ids),
            "stock_secondary_trait_ids": list(self.stock_secondary_trait_ids),
            "stock_delisted": [int(value) for value in self.stock_delisted],
            "stock_relist_seconds": list(self.stock_relist_seconds),
            "stock_average_prices": list(self.stock_average_prices),
            "stock_halt_seconds": list(self.stock_halt_seconds),
            "food_boost_seconds": (
                [int(math.ceil(pet.food_boost_left)) for pet in self.pets[:12]]
                + [0] * max(0, 12 - len(self.pets))
            ),
        }

    def save_settings(self):
        """지금 상태를 파일에 남긴다. 실패해도 그냥 넘어간다."""
        settings_file.save(self.current_settings(), self.settings_path)
        self.last_save_time = time.time()

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

    def buy_stock(self, index, quantity=1):
        """현재 가격으로 선택한 수량의 가상 주식을 산다."""
        if (not self.market_is_open or self.stock_delisted[index]
                or self.stock_halt_seconds[index]):
            return
        quantity = max(1, int(quantity))
        shares = self.stock_shares[index]
        cost = self.stock_buy_cost(index) * quantity
        if self.coins < cost:
            return
        self.coins -= cost
        self.stock_average_prices[index] = int(round(
            (self.stock_average_prices[index] * shares + cost) / float(shares + quantity)
        ))
        self.stock_shares[index] = shares + quantity
        self.save_settings()
        self.refresh_stock_overlay()

    def sell_stock(self, index, quantity=1):
        """현재 가격으로 선택한 수량의 가상 주식을 판다."""
        quantity = max(1, int(quantity))
        if (not self.market_is_open or self.stock_delisted[index] or self.stock_halt_seconds[index]
                or self.stock_shares[index] < quantity):
            return
        self.stock_shares[index] -= quantity
        self.coins += self.stock_sell_proceeds(index) * quantity
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
        return "%s · %s" % (
            self.stock_primary_trait(index)["name"], self.stock_secondary_trait(index)["name"])

    def stock_primary_trait(self, index):
        return STOCK_PRIMARY_TRAITS[
            self.stock_primary_trait_ids[index] % len(STOCK_PRIMARY_TRAITS)]

    def stock_secondary_trait(self, index):
        return STOCK_SECONDARY_TRAITS[
            self.stock_secondary_trait_ids[index] % len(STOCK_SECONDARY_TRAITS)]

    def stock_profile_description(self, index):
        return "%s · %s" % (
            self.stock_primary_trait(index)["description"],
            self.stock_secondary_trait(index)["description"],
        )

    def stock_risk_label(self, index):
        volatility = self.stock_listing(index)[2]
        primary = self.stock_primary_trait(index)
        secondary = self.stock_secondary_trait(index)
        effective = volatility * primary["noise"] * secondary["noise"]
        if primary["name"] == "투기형" or secondary["name"] == "취약함" or effective > 19:
            return "매우 높음"
        if effective > 13 or primary["burst"]:
            return "높음"
        if effective > 8:
            return "보통"
        return "낮음"

    @staticmethod
    def stock_fee(amount):
        return int(math.ceil(amount * STOCK_FEE_RATE))

    def stock_buy_cost(self, index):
        return self.stock_prices[index] + self.stock_fee(self.stock_prices[index])

    def stock_maximum_buy_quantity(self, index):
        affordable = self.coins // max(1, self.stock_buy_cost(index))
        remaining_capacity = max(0, STOCK_MAX_ORDER_QUANTITY - self.stock_shares[index])
        return max(0, min(affordable, remaining_capacity))

    def stock_maximum_sell_quantity(self, index):
        return max(0, self.stock_shares[index])

    def stock_sell_proceeds(self, index):
        return max(0, self.stock_prices[index] - self.stock_fee(self.stock_prices[index]))

    def stock_profit_percent(self, index):
        average = self.stock_average_prices[index]
        if not self.stock_shares[index] or average <= 0:
            return 0.0
        return (self.stock_sell_proceeds(index) - average) * 100.0 / average

    def stock_holding_value(self, index):
        return self.stock_sell_proceeds(index) * self.stock_shares[index]

    def stock_holding_profit(self, index):
        return self.stock_holding_value(index) - (
            self.stock_average_prices[index] * self.stock_shares[index]
        )

    def stock_position_text(self, index, include_percent=True):
        shares = self.stock_shares[index]
        trend = ("하락 추세", "횡보", "상승 추세")[self.stock_trends[index] + 1]
        if not shares:
            return "보유 주식 없음\n매수 후 평균 매입가·평가액·손익이 표시됩니다."
        percent_text = " (%+.1f%%)" % self.stock_profit_percent(index) if include_percent else ""
        return "보유 %d주  ·  평균 매입가 %s\n평가액 %s  ·  손익 %s원%s\n%s" % (
            shares, format_won(self.stock_average_prices[index]),
            format_won(self.stock_holding_value(index)),
            "{:+,}".format(self.stock_holding_profit(index)),
            percent_text, trend,
        )

    def stock_portfolio_value(self):
        return sum(
            self.stock_sell_proceeds(index) * shares
            for index, shares in enumerate(self.stock_shares)
            if not self.stock_delisted[index]
        )

    def stock_portfolio_cost_basis(self):
        return sum(
            average * shares for average, shares, delisted in zip(
                self.stock_average_prices, self.stock_shares, self.stock_delisted
            ) if not delisted
        )

    def stock_portfolio_change_percent(self):
        cost_basis = self.stock_portfolio_cost_basis()
        if not cost_basis:
            return 0.0
        return (self.stock_portfolio_value() - cost_basis) * 100.0 / cost_basis

    def relist_stock(self, index):
        """상장폐지된 슬롯에 임의 성격의 새 포켓몬 종목을 상장한다."""
        candidates = [listing_id for listing_id in range(len(STOCK_LISTINGS))
                      if listing_id != self.stock_listing_ids[index]]
        self.stock_listing_ids[index] = random.choice(candidates)
        used_primary = {
            trait_id % len(STOCK_PRIMARY_TRAITS)
            for other, trait_id in enumerate(self.stock_primary_trait_ids)
            if other != index and not self.stock_delisted[other]
        }
        primary_candidates = [
            trait_id for trait_id in range(len(STOCK_PRIMARY_TRAITS))
            if trait_id not in used_primary
        ] or list(range(len(STOCK_PRIMARY_TRAITS)))
        self.stock_primary_trait_ids[index] = random.choice(primary_candidates)
        self.stock_secondary_trait_ids[index] = random.choice(
            list(range(len(STOCK_SECONDARY_TRAITS))))
        _name, starting_price, _volatility = self.stock_listing(index)
        self.stock_prices[index] = starting_price
        self.stock_shares[index] = 0
        self.stock_average_prices[index] = 0
        self.stock_delisted[index] = False
        self.stock_relist_seconds[index] = 0
        self.stock_halt_seconds[index] = 0
        self.stock_history[index] = [starting_price]
        self.stock_session_open_prices[index] = starting_price
        self.announce_stock_event("%s 신규 상장! %s" % (
            self.stock_name(index), self.stock_profile(index)))

    def announce_stock_event(self, text):
        """시장 속보와 최근 다섯 개 기록을 함께 갱신한다."""
        self.stock_event = text
        self.stock_event_history.insert(0, "%s  %s" % (time.strftime("%H:%M"), text))
        self.stock_event_history = self.stock_event_history[:5]

    def market_regime_label(self):
        """현재 전체 시장의 짧은 흐름을 사람이 읽을 수 있게 표시한다."""
        return MARKET_REGIME_NAMES[self.market_regime]

    def market_mover_summary(self):
        """주문 전에 시장 전체 방향과 다음 갱신을 한 줄로 보여 준다."""
        rising = sum(1 for index in range(STOCK_COUNT)
                     if self.stock_change_percent(index) > 0 and not self.stock_delisted[index])
        falling = sum(1 for index in range(STOCK_COUNT)
                      if self.stock_change_percent(index) < 0 and not self.stock_delisted[index])
        halted = sum(1 for seconds in self.stock_halt_seconds if seconds)
        if not self.market_is_open:
            return "휴장 중 · 재개까지 %s" % self.market_session_time_left()
        left = max(1, int(math.ceil(MARKET_UPDATE_SEC - self.market_seconds)))
        return "다음 갱신 %d초  ·  상승 %d  하락 %d  정지 %d" % (
            left, rising, falling, halted,
        )

    def market_session_time_left(self):
        """현재 장 상태가 바뀌기까지 남은 시간을 MM:SS로 보여 준다."""
        seconds = max(0, int(math.ceil(self.market_session_seconds)))
        return "%02d:%02d" % divmod(seconds, 60)

    def market_session_text(self):
        """개장 또는 휴장 상태와 다음 전환 시점을 짧게 표시한다."""
        if self.market_is_open:
            return "개장 · 마감까지 %s" % self.market_session_time_left()
        return "휴장 · 재개까지 %s" % self.market_session_time_left()

    def tick_market_session(self, seconds):
        """1시간 개장과 5분 휴장을 반복하고 전환 시 시장 속보를 남긴다."""
        self.market_session_seconds -= seconds
        if self.market_session_seconds > 0:
            return False
        self.market_is_open = not self.market_is_open
        self.market_session_seconds = (MARKET_OPEN_SECONDS if self.market_is_open
                                       else MARKET_CLOSED_SECONDS)
        self.market_seconds = 0.0
        if self.market_is_open:
            # 새 장이 열리는 가격이 그 장의 기준가다. 따라서 등락률은 0%부터 시작한다.
            self.stock_history = [[price] for price in self.stock_prices]
            self.stock_session_open_prices = list(self.stock_prices)
        self.announce_stock_event(
            "시장 개장! 1시간 동안 거래 가능합니다." if self.market_is_open
            else "장 마감! 5분 동안 휴장합니다."
        )
        self.save_settings()
        self.refresh_stock_overlay()
        return True

    def update_market_regime(self):
        """국면은 잠시 유지해 그래프에 읽을 수 있는 흐름을 만든다."""
        self.market_regime_updates -= 1
        if self.market_regime_updates > 0:
            return
        roll = random.randint(0, sum(MARKET_REGIME_WEIGHTS) - 1)
        running = 0
        for index, weight in enumerate(MARKET_REGIME_WEIGHTS):
            running += weight
            if roll < running:
                self.market_regime = index
                break
        self.market_regime_updates = random.randint(*MARKET_REGIME_UPDATES)
        self.announce_stock_event("시장 국면 전환: %s" % self.market_regime_label())

    def stock_market_change(self, index, volatility):
        """국면·추세·기준가 회귀·잡음을 합친 이번 갱신의 변동률."""
        primary = self.stock_primary_trait(index)
        secondary = self.stock_secondary_trait(index)
        trend_change = primary["trend_change"] * secondary["trend_change"]
        if random.random() < min(.75, trend_change):
            self.stock_trends[index] = random.randint(-1, 1)
        _name, starting_price, _volatility = self.stock_listing(index)
        price_gap = (starting_price - self.stock_prices[index]) * 100.0 / starting_price
        pull_rate = 0.20 if volatility <= 10 else 0.12 if volatility <= 18 else 0.06
        mean_reversion = 0.0 if self.stock_prices[index] < STOCK_CRISIS_PRICE else max(
            -5.0, min(5.0, price_gap * pull_rate * primary["reversion"])
        )
        trend = (self.stock_trends[index] * max(1.0, volatility * 0.16)
                 * primary["trend"])
        noise = (random.randint(-volatility, volatility)
                 * primary["noise"] * secondary["noise"])
        market = (MARKET_REGIME_DRIFTS[self.market_regime]
                  * primary["market"] * secondary["market"])
        if market < 0:
            market *= secondary["negative"]
        change = (noise + market + trend + mean_reversion
                  + primary["drift"] + secondary["drift"])

        # 가치형과 회복력은 하락했을 때만 부드럽게 반등한다. 손실을 막는 하한선은 아니다.
        if price_gap > 0 and primary["name"] == "가치형":
            change += min(4.0, price_gap * .08)
        if price_gap > 0 and secondary["recovery"]:
            change += min(3.0, price_gap * secondary["recovery"])

        session_change = self.stock_change_percent(index)
        if primary["name"] == "반전형" and abs(session_change) > 8:
            change -= math.copysign(min(4.0, abs(session_change) * .08), session_change)
        if session_change > 12 and secondary["overheat"]:
            change -= min(4.0, (session_change - 12) * secondary["overheat"])

        elapsed = MARKET_OPEN_SECONDS - self.market_session_seconds
        if primary["phase"] == "open":
            change *= 1.65 if elapsed <= 10 * 60 else .75
        elif primary["phase"] == "close":
            change *= 1.65 if self.market_session_seconds <= 10 * 60 else .75
        if primary["burst"] and random.random() < primary["burst"]:
            change += (-1 if random.random() < .5 else 1) * volatility * .90
        return change * MARKET_TICK_SCALE

    def stock_event_change(self, index, event_percent):
        """같은 뉴스라도 주·보조 성향에 따라 실제 반영되는 크기를 다르게 한다."""
        primary = self.stock_primary_trait(index)
        secondary = self.stock_secondary_trait(index)
        multiplier = primary["event"] * secondary["event"]
        if event_percent < 0:
            multiplier *= secondary["negative"]
        return event_percent * multiplier

    @staticmethod
    def stock_price_after_change(price, change):
        """+x/-x를 역수로 적용해 같은 세기의 왕복이 가격을 깎지 않게 한다."""
        factor = 1.0 + change / 100.0 if change >= 0 else 1.0 / (1.0 - change / 100.0)
        return max(1, int(round(price * factor)))

    def update_market(self):
        """종목 성격별 등락과 이벤트, 상장폐지·신규 상장을 처리한다."""
        if not self.market_is_open:
            return
        self.stock_event = ""
        self.update_market_regime()
        event_index = None
        event_percent = 0
        active = [index for index in range(STOCK_COUNT)
                  if not self.stock_delisted[index] and not self.stock_halt_seconds[index]]
        if active and random.random() < STOCK_EVENT_CHANCE:
            event_index = random.choice(active)
            event_name, event_percent = random.choice(
                STOCK_EVENTS[self.stock_listing_ids[event_index] % len(STOCK_EVENTS)]
            )
            event_percent = self.stock_event_change(event_index, event_percent)
            event_text = "%s %s  %+.0f%%" % (
                self.stock_name(event_index), event_name, event_percent
            )
        else:
            event_text = ""

        for index in range(STOCK_COUNT):
            if self.stock_delisted[index]:
                self.stock_relist_seconds[index] -= int(MARKET_UPDATE_SEC)
                if self.stock_relist_seconds[index] <= 0:
                    self.relist_stock(index)
                continue
            if self.stock_halt_seconds[index]:
                continue
            _name, _starting_price, volatility = self.stock_listing(index)
            change = self.stock_market_change(index, volatility)
            if index == event_index:
                change += event_percent
            price = self.stock_price_after_change(self.stock_prices[index], change)
            if price <= STOCK_DELIST_PRICE:
                self.stock_prices[index] = 0
                self.stock_shares[index] = 0
                self.stock_average_prices[index] = 0
                self.stock_delisted[index] = True
                self.stock_relist_seconds[index] = STOCK_RELIST_SECONDS
                self.announce_stock_event("%s 상장폐지! 보유 주식은 소멸했습니다." % self.stock_name(index))
            else:
                self.stock_prices[index] = price
            self.stock_history[index].append(self.stock_prices[index])
            self.stock_history[index] = self.stock_history[index][-20:]
        if event_index is not None and not self.stock_delisted[event_index]:
            self.stock_halt_seconds[event_index] = STOCK_HALT_SECONDS
            self.announce_stock_event(event_text + " · 변동성 완화장치 발동(20초 거래 정지)")
        self.save_settings()
        self.refresh_stock_overlay()

    def stock_change_percent(self, index):
        """이번 개장 때 정한 기준가와 비교한 등락률."""
        opening_price = self.stock_session_open_prices[index]
        if opening_price <= 0:
            return 0.0
        return (self.stock_prices[index] - opening_price) * 100.0 / opening_price

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

    def open_game_menu(self):
        if self.game_menu is not None:
            try:
                self.game_menu.window.lift()
                self.game_menu.window.focus_force()
                self.game_menu.refresh()
                return
            except tk.TclError:
                self.game_menu = None
        self.game_menu = GameMenuOverlay(self)

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
        boosts = [pet.food_boost_left for pet in self.pets]
        for pet in list(self.pets):
            pet.destroy()
        self.pets = []
        for key, place, boost in zip(keys, places, boosts):
            self.add_pet(key, boost)
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

    def add_pet(self, key, food_boost_left=0.0):
        self.pets.append(PokemonPet(self, POKEMON[key]))
        self.pets[-1].food_boost_left = max(0.0, food_boost_left)
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
        boost = pet.food_boost_left
        index = self.pets.index(pet) if pet in self.pets else len(self.pets)
        if pet in self.pets:
            self.pets.remove(pet)
        pet.destroy()
        grown = PokemonPet(self, POKEMON[key])
        grown.food_boost_left = boost
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
        """등급 확률을 적용한 랜덤 영입. 개별 포켓몬은 직접 살 수 없다."""
        if self.coins < POKEMON_PRICE:
            return
        roll = random.random()
        total = 0.0
        grade = "일반"
        for name, chance in GRADE_DRAW_CHANCES:
            total += chance
            if roll < total:
                grade = name
                break
        choices = [key for key in base_species() if pokemon_grade(key)[0] == grade]
        self.buy_pet(random.choice(choices or base_species()))

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
        if self.game_menu is not None:
            self.game_menu.close()
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
            self.tick_market_session(0.2)
            if self.market_is_open:
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
            else:
                self.refresh_stock_overlay()
            self.heartbeat_id = self.root.after(200, heartbeat)

        self.root.after(120, self.open_game_menu)
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
    args.stock_primary_trait_ids = saved["stock_primary_trait_ids"]
    args.stock_secondary_trait_ids = saved["stock_secondary_trait_ids"]
    args.stock_delisted = saved["stock_delisted"]
    args.stock_relist_seconds = saved["stock_relist_seconds"]
    args.stock_average_prices = saved["stock_average_prices"]
    args.stock_halt_seconds = saved["stock_halt_seconds"]
    args.food_boost_seconds = saved["food_boost_seconds"]

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
