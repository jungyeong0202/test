#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""화면 하단(작업 표시줄) 위를 포켓몬이 돌아다니는 데스크톱 펫.

표준 라이브러리(tkinter)만 사용하며 외부 이미지/네트워크가 필요 없다.

사용 예:
    python pokemon_taskbar.py                     # 피카츄 한 마리
    python pokemon_taskbar.py -p pikachu -p squirtle
    python pokemon_taskbar.py --count 3 --scale 4 --speed 70

마우스 조작:
    왼쪽 클릭  - 포켓몬이 폴짝 뛴다
    오른쪽 클릭 - 메뉴(추가 / 보내주기 / 종료)
"""

from __future__ import annotations

import argparse
import math
import ctypes
import platform
import random
import sys
import tkinter as tk

from sprites import POKEMON, validate_all

MIN_SPRITE_SCALE = 0.5  # 도트 하나가 이보다 작아지지는 않는다
TICK_MS = 40           # 화면 갱신 주기
STEP_SEC = 0.16        # 걷기 프레임 교체 주기
TOPMOST_TICKS = 5      # 몇 틱마다 "맨 앞"을 다시 주장할지 (5틱 = 0.2초)
COLOR_KEY = "#ff00ff"  # 투명 처리에 쓰는 색(윈도우 전용)

SPI_GETWORKAREA = 0x0030
HWND_TOPMOST = -1
SWP_NOSIZE = 0x0001
SWP_NOMOVE = 0x0002
SWP_NOACTIVATE = 0x0010


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

        self.window = tk.Toplevel(app.root)
        self.window.overrideredirect(True)
        self.window.wm_attributes("-topmost", True)
        background = setup_transparency(self.window, app.system) or app.background
        self.window.configure(bg=background)

        self.canvas = tk.Canvas(
            self.window,
            width=self.width,
            height=self.height + self.hop,
            bg=background,
            highlightthickness=0,
            bd=0,
        )
        self.canvas.pack()
        self.sprite = self.canvas.create_image(
            0, self.hop, anchor="nw", image=self.images["right"][0]
        )

        self.max_x = max(0, app.screen_width - self.width)
        self.base_y = app.ground_y - (self.height + self.hop) - app.offset
        self.x = random.uniform(0, self.max_x)
        self.direction = random.choice((-1, 1))
        self.speed = app.speed * random.uniform(0.85, 1.15)
        self.state = "walk"
        self.state_left = 0.0
        self.anim_time = 0.0
        self.jump_time = -1.0
        self.ticks = 0
        self.after_id = None

        self.menu = tk.Menu(self.window, tearoff=0)
        self.menu.add_command(label="포켓몬 추가", command=app.add_random_pet)
        self.menu.add_command(label="이 포켓몬 보내주기", command=self.release)
        self.menu.add_separator()
        self.menu.add_command(label="전부 종료", command=app.quit)

        self.window.bind("<Escape>", lambda _e: app.quit())
        self.canvas.bind("<Button-1>", self.on_click)
        self.canvas.bind("<Button-3>", self.on_menu)
        self.canvas.bind("<Button-2>", self.on_menu)  # macOS 오른쪽 클릭

        self.place()
        self.after_id = self.window.after(TICK_MS, self.tick)

    # --- 조작 -----------------------------------------------------------
    def on_click(self, _event):
        # 테두리 없는 창은 기본적으로 포커스를 받지 않아, 클릭했을 때만 키 입력을 받게 한다.
        try:
            self.window.focus_force()
        except tk.TclError:
            pass
        if self.jump_time < 0:
            self.jump_time = 0.0

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
        if state == "idle":
            self.state_left = random.uniform(0.8, 3.0)

    def tick(self):
        if self.state == "gone":
            return
        dt = TICK_MS / 1000.0
        self.ticks += 1

        if self.state == "walk":
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

        if self.jump_time >= 0:
            self.jump_time += dt
            if self.jump_time > 0.45:
                self.jump_time = -1.0

        # 다른 창을 클릭해도 항상 맨 앞에 남도록 자주 다시 주장한다.
        if self.ticks % TOPMOST_TICKS == 0:
            self.raise_above_all()

        self.draw()
        self.place()
        self.after_id = self.window.after(TICK_MS, self.tick)

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
        if self.state == "walk":
            frame = int(self.anim_time / STEP_SEC) % self.frame_count
        else:
            frame = 0
        self.canvas.itemconfigure(self.sprite, image=self.images[facing][frame])
        # 홀수 프레임에서 살짝 튀어올라 걷는 느낌을 준다.
        bounce = self.hop if (self.state == "walk" and frame % 2 == 1) else 0
        self.canvas.coords(self.sprite, 0, self.hop - bounce)

    def place(self):
        y = self.base_y
        if self.jump_time >= 0:
            y -= int(self.scale * 6 * math.sin(math.pi * self.jump_time / 0.45))
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
        self.ground_y = (
            self.screen_height if args.on_taskbar else work_area_bottom(self.screen_height)
        )
        self.image_cache = {}
        self.pets = []
        self.quitting = False
        self.heartbeat_id = None

        for key in args.species:
            self.add_pet(key)

        self.root.protocol("WM_DELETE_WINDOW", self.quit)
        self.root.bind_all("<Escape>", lambda _e: self.quit())

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
            self.image_cache[pokemon.key] = {
                "right": [
                    make_photo(f, scale, flip=flip_for(pokemon, True), master=self.root)
                    for f in frames
                ],
                "left": [
                    make_photo(f, scale, flip=flip_for(pokemon, False), master=self.root)
                    for f in frames
                ],
            }
        return self.image_cache[pokemon.key]

    def add_pet(self, key):
        self.pets.append(PokemonPet(self, POKEMON[key]))

    def add_random_pet(self):
        self.add_pet(random.choice(list(POKEMON)))

    def remove_pet(self, pet):
        if pet in self.pets:
            self.pets.remove(pet)
        pet.destroy()
        if not self.pets:
            self.quit()

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
    parser.add_argument("-c", "--count", type=int, default=1, help="마리 수 (기본 1)")
    parser.add_argument(
        "-s", "--scale", type=float, default=4.5,
        help="크기 배율 (기본 4.5. 3 을 주면 예전 크기, 6 이면 두 배)",
    )
    parser.add_argument(
        "--speed", type=float, default=55.0, help="이동 속도(초당 픽셀, 기본 55)"
    )
    parser.add_argument(
        "--offset", type=int, default=0, help="바닥에서 더 띄울 높이(px, 기본 0)"
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
        args.species = ["pikachu"] + [
            random.choice(list(POKEMON)) for _ in range(args.count - 1)
        ]
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
