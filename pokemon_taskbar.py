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
import os
import platform
import random
import sys
import tkinter as tk

import settings as settings_file
from sprites import POKEMON, validate_all

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
SPEED_CHOICES = (("느리게", 30.0), ("보통", 55.0), ("빠르게", 95.0))
TICK_MS = 40           # 화면 갱신 주기
STEP_SEC = 0.16        # 걷기 프레임 교체 주기
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


class PokemonPet:
    """작업 표시줄 위를 돌아다니는 포켓몬 한 마리."""

    def __init__(self, app, pokemon):
        self.app = app
        self.pokemon = pokemon
        self.images = app.get_images(pokemon)
        self.frame_count = len(self.images["right"])
        self.scale = app.sprite_scale(pokemon)

        sample = self.images["right"][0]
        self.width = sample.width()
        self.height = sample.height()
        self.hop = max(1, int(round(self.scale)))  # 걸을 때 위아래로 흔들리는 폭
        # 먼지나 하트가 몸 밖으로 튀어나갈 자리를 창에 미리 마련해 둔다.
        self.dot = max(1, int(round(self.scale)))
        self.margin_x = self.dot * 7
        self.margin_top = self.dot * 9
        self.window_width = self.width + self.margin_x * 2
        self.window_height = self.height + self.hop + self.margin_top
        self.effects = []

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
            self.margin_x, self.margin_top + self.hop, anchor="nw",
            image=self.images["right"][0],
        )

        self.max_x = max(0, app.screen_width - self.window_width)
        self.base_y = app.ground_y - self.window_height - app.offset
        self.x = random.uniform(0, self.max_x)
        self.direction = random.choice((-1, 1))
        self.speed = app.speed * random.uniform(0.85, 1.15)
        self.move = pokemon.move
        self.state = "walk"
        self.state_left = 0.0
        self.hop_state = "rest"
        self.hop_timer = random.uniform(*HOP_REST)
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
        self.lift = 0.0        # 바닥에서 떠 있는 높이(px)
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
        menu = tk.Menu(self.window, tearoff=0)

        choose = tk.Menu(menu, tearoff=0)
        for pokemon in POKEMON.values():
            choose.add_command(
                label=pokemon.name_ko,
                command=lambda key=pokemon.key: app.add_pet_and_save(key),
            )
        choose.add_separator()
        choose.add_command(label="무작위", command=app.add_random_pet)
        menu.add_cascade(label="포켓몬 추가", menu=choose)
        menu.add_command(label="이 포켓몬 보내주기", command=self.release)
        menu.add_separator()

        sizes = tk.Menu(menu, tearoff=0)
        for label, value in SIZE_CHOICES:
            sizes.add_radiobutton(
                label=label, value=value, variable=app.scale_var,
                command=lambda v=value: app.set_scale(v),
            )
        menu.add_cascade(label="크기", menu=sizes)

        speeds = tk.Menu(menu, tearoff=0)
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
        ceiling = max(0.0, float(self.base_y))
        self.x = min(max(0, event.x_root - offset_x), self.max_x)
        self.lift = min(max(0.0, self.base_y - (event.y_root - offset_y)), ceiling)
        self.place()

    def on_release(self, _event):
        """놓으면 떨어진다. 거의 움직이지 않았으면 그냥 클릭으로 보고 폴짝 뛴다."""
        if not self.dragging or self.state == "gone":
            return
        self.dragging = False
        if self.drag_moved:
            self.vertical_speed = 0.0
        else:
            self.vertical_speed = JUMP_SPEED
            self.spawn_emote("heart")

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
        self.state = state
        self.napping = False
        if state == "idle":
            if random.random() < NAP_CHANCE:
                # 가끔은 길게 낮잠을 잔다. 이때 머리 위로 Zzz 가 올라간다.
                self.state_left = random.uniform(*NAP_SECONDS)
                self.napping = True
                self.zzz_timer = 0.35
            else:
                self.state_left = random.uniform(0.8, 3.0)

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

        if self.app.paused:
            pass                     # 잠시 멈춤: 제자리에서 가만히
        elif self.move == "hop":
            self.hop_step(dt)
        elif self.state == "walk":
            self.anim_time += dt
            self.x += self.direction * self.speed * dt
            if self.x <= 0:
                self.x = 0
                self.direction = 1
            elif self.x >= self.max_x:
                self.x = self.max_x
                self.direction = -1
            elif random.random() < 0.004:
                self.direction = -self.direction
            if random.random() < 0.005:
                self.set_state("idle")
        else:
            self.state_left -= dt
            if self.state_left <= 0:
                self.set_state("walk")

        # 떠 있으면 중력으로 끌어내린다.
        if self.lift > 0 or self.vertical_speed != 0:
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
        """머리 위로 하트나 Zzz 를 띄운다."""
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
        if self.lift > self.dot:
            return "stretch"
        if self.land_squash > 0:
            return "squash"
        if self.napping and int(self.breath / BREATH_SEC) % 2 == 1:
            return "squash"
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

            dots = HEART_DOTS if effect["kind"] == "heart" else ZZZ_DOTS
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
        facing = "right" if self.direction > 0 else "left"
        if self.dragging:
            frame = 0
        elif self.move == "hop":
            frame = self.hop_frame()
        elif self.state == "walk":
            frame = int(self.anim_time / STEP_SEC) % self.frame_count
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
        walking = self.move == "walk" and self.state == "walk"
        bounce = self.bounce_px if (walking and pose is None and frame % 2 == 1) else 0
        # 들려 있으면 버둥거린다.
        sway = self.dot if (self.dragging and int(self.wiggle / WIGGLE_SEC) % 2) else 0
        self.canvas.coords(
            self.sprite, self.margin_x + sway, self.margin_top + self.hop - bounce
        )
        self.draw_effects()

    def place(self):
        y = self.base_y - int(self.lift)
        self.window.geometry("+%d+%d" % (int(self.x), y))


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
        self.pets = []
        self.quitting = False
        self.paused = False
        self.heartbeat_id = None
        self.settings_path = args.settings
        # 메뉴의 체크/선택 표시를 여러 창이 함께 쓰도록 앱이 들고 있는다.
        self.scale_var = tk.DoubleVar(master=self.root, value=self.scale)
        self.speed_var = tk.DoubleVar(master=self.root, value=self.speed)
        self.pause_var = tk.BooleanVar(master=self.root, value=False)
        self.autostart_var = tk.BooleanVar(master=self.root, value=autostart_enabled())

        for key in args.species:
            self.add_pet(key)

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
        }

    def save_settings(self):
        """지금 상태를 파일에 남긴다. 실패해도 그냥 넘어간다."""
        settings_file.save(self.current_settings(), self.settings_path)

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

    def get_images(self, pokemon):
        """방향별 걷기 이미지(캐시)."""
        if pokemon.key not in self.image_cache:
            frames = pokemon.frames()
            scale = self.sprite_scale(pokemon)
            poses = pokemon.poses()
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

    def add_pet_and_save(self, key):
        self.add_pet(key)
        self.save_settings()

    def add_random_pet(self):
        self.add_pet_and_save(random.choice(list(POKEMON)))

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
            args.species.append(random.choice(list(POKEMON)))
    else:
        args.species = list(saved["species"])
        while len(args.species) < args.count:
            args.species.append(random.choice(list(POKEMON)))
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
