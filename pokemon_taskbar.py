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
import platform
import random
import sys
import tkinter as tk

from sprites import POKEMON, validate_all

TICK_MS = 40           # 화면 갱신 주기
STEP_SEC = 0.16        # 걷기 프레임 교체 주기
COLOR_KEY = "#ff00ff"  # 투명 처리에 쓰는 색(윈도우 전용)


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


def make_photo(grid, scale, flip=False):
    """색상 그리드를 tkinter 이미지로 만든다(투명 픽셀은 비워 둔다)."""
    height = len(grid)
    width = len(grid[0])
    photo = tk.PhotoImage(width=width * scale, height=height * scale)
    for y, row in enumerate(grid):
        cells = row[::-1] if flip else row
        x = 0
        while x < width:
            color = cells[x]
            if color is None:
                x += 1
                continue
            end = x
            while end < width and cells[end] == color:
                end += 1
            line = "{" + " ".join([color] * ((end - x) * scale)) + "}"
            photo.put(" ".join([line] * scale), to=(x * scale, y * scale))
            x = end
    return photo


class PokemonPet:
    """작업 표시줄 위를 돌아다니는 포켓몬 한 마리."""

    def __init__(self, app, pokemon):
        self.app = app
        self.pokemon = pokemon
        self.images = app.get_images(pokemon.key)

        sample = self.images["right"][0]
        self.width = sample.width()
        self.height = sample.height()
        self.hop = app.scale  # 걸을 때 위아래로 흔들리는 폭

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
        self.base_y = app.screen_height - (self.height + self.hop) - app.offset
        self.x = random.uniform(0, self.max_x)
        self.direction = random.choice((-1, 1))
        self.speed = app.speed * random.uniform(0.85, 1.15)
        self.state = "walk"
        self.state_left = 0.0
        self.anim_time = 0.0
        self.jump_time = -1.0
        self.ticks = 0

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
        self.window.after(TICK_MS, self.tick)

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
        self.state = "gone"
        self.window.destroy()

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

        # 다른 창을 클릭해도 계속 맨 앞에 남도록 주기적으로 다시 올린다.
        if self.ticks % 75 == 0:
            try:
                self.window.wm_attributes("-topmost", True)
            except tk.TclError:
                pass

        self.draw()
        self.place()
        self.window.after(TICK_MS, self.tick)

    def draw(self):
        facing = "right" if self.direction > 0 else "left"
        if self.state == "walk":
            frame = int(self.anim_time / STEP_SEC) % 2
        else:
            frame = 0
        self.canvas.itemconfigure(self.sprite, image=self.images[facing][frame])
        bounce = self.hop if (self.state == "walk" and frame == 1) else 0
        self.canvas.coords(self.sprite, 0, self.hop - bounce)

    def place(self):
        y = self.base_y
        if self.jump_time >= 0:
            y -= int(self.app.scale * 6 * math.sin(math.pi * self.jump_time / 0.45))
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
        self.image_cache = {}
        self.pets = []

        for key in args.species:
            self.add_pet(key)

        self.root.protocol("WM_DELETE_WINDOW", self.quit)
        self.root.bind_all("<Escape>", lambda _e: self.quit())

    def get_images(self, key):
        """방향별 걷기 이미지(캐시)."""
        if key not in self.image_cache:
            frames = POKEMON[key].frames()
            self.image_cache[key] = {
                "right": [make_photo(f, self.scale, flip=False) for f in frames],
                "left": [make_photo(f, self.scale, flip=True) for f in frames],
            }
        return self.image_cache[key]

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
        for pet in list(self.pets):
            pet.state = "gone"
        self.pets.clear()
        self.root.quit()
        self.root.destroy()

    def run(self):
        # Ctrl+C 로도 종료할 수 있게 주기적으로 인터프리터에 제어를 넘긴다.
        def heartbeat():
            self.root.after(200, heartbeat)

        heartbeat()
        self.root.mainloop()


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
    parser.add_argument("-s", "--scale", type=int, default=3, help="도트 확대 배율 (기본 3)")
    parser.add_argument(
        "--speed", type=float, default=55.0, help="이동 속도(초당 픽셀, 기본 55)"
    )
    parser.add_argument(
        "--offset", type=int, default=0, help="화면 맨 아래에서 띄울 높이(px, 기본 0)"
    )
    parser.add_argument(
        "--bg",
        default="#1e1e1e",
        help="투명 창을 못 쓰는 환경에서 사용할 배경색 (기본 #1e1e1e)",
    )
    parser.add_argument("--list", action="store_true", help="사용 가능한 포켓몬 목록 출력")
    args = parser.parse_args(argv)

    if args.scale < 1:
        parser.error("--scale 은 1 이상이어야 합니다")
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
        for pokemon in POKEMON.values():
            print("%-12s %s" % (pokemon.key, pokemon.name_ko))
        return 0
    try:
        App(args).run()
    except KeyboardInterrupt:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
