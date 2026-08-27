# -*- coding: utf-8 -*-
"""스프라이트 데이터와 앱 동작에 대한 기본 테스트.

GUI(디스플레이)가 없는 환경에서는 화면이 필요한 테스트만 건너뛴다.

    python -m unittest test_pokemon_taskbar -v
"""

import contextlib
import io
import importlib.util
import os
import shutil
import tempfile
import time
import unittest
from unittest import mock

import settings as settings_file

# 테스트가 진짜 사용자 설정 파일을 건드리지 않도록 임시 폴더로 돌려 둔다.
_SETTINGS_DIR = tempfile.mkdtemp(prefix="pokemon-taskbar-test-")
os.environ[settings_file.ENV_OVERRIDE] = os.path.join(_SETTINGS_DIR, "settings.txt")

import sprites

try:
    import tkinter as tk

    _root = tk.Tk()
    _root.destroy()
    HAS_DISPLAY = True
except Exception:  # tkinter 미설치 또는 디스플레이 없음
    tk = None
    HAS_DISPLAY = False

if HAS_DISPLAY:
    import pokemon_taskbar as pt
else:  # 화면이 필요 없는 부분만 쓰기 위해 지연 임포트
    pt = None

needs_display = unittest.skipUnless(HAS_DISPLAY, "tkinter 디스플레이가 필요합니다")


def tearDownModule():
    shutil.rmtree(_SETTINGS_DIR, ignore_errors=True)


class SettingsTest(unittest.TestCase):
    """설정 저장/불러오기. 화면이 없어도 확인할 수 있다."""

    def setUp(self):
        self.path = os.path.join(_SETTINGS_DIR, "case.txt")

    def tearDown(self):
        if os.path.exists(self.path):
            os.remove(self.path)

    def test_round_trip(self):
        values = dict(settings_file.DEFAULTS)
        values["species"] = ["ditto", "pikachu"]
        values["scale"] = 6.0
        values["speed"] = 95.0
        values["offset"] = 12
        values["on_taskbar"] = True
        values["coins"] = 41
        values["food"] = 3
        values["growth_drops"] = 2
        values["stock_prices"] = [11, 19, 28, 31, 37, 41]
        values["stock_shares"] = [2, 0, 4, 1, 3, 0]
        self.assertTrue(settings_file.save(values, self.path))
        self.assertEqual(settings_file.load(self.path), values)

    def test_missing_file_gives_defaults(self):
        self.assertEqual(settings_file.load(self.path)["species"], ["pikachu"])

    def test_broken_values_fall_back(self):
        values = settings_file.parse_text(
            "scale = 헬로\nspeed = -5\noffset = ?\nspecies = 없는놈, ditto\n쓰레기줄",
            known_species=set(sprites.POKEMON),
        )
        self.assertEqual(values["scale"], settings_file.DEFAULTS["scale"])
        self.assertEqual(values["speed"], settings_file.DEFAULTS["speed"])
        self.assertEqual(values["offset"], settings_file.DEFAULTS["offset"])
        self.assertEqual(values["species"], ["ditto"])

    def test_unknown_species_are_dropped(self):
        values = settings_file.parse_text(
            "species = 없는놈", known_species=set(sprites.POKEMON)
        )
        self.assertEqual(values["species"], settings_file.DEFAULTS["species"])

    def test_old_coin_settings_are_moved_to_won_once(self):
        values = settings_file.parse_text(
            "coins = 30\nstock_prices = 10, 18, 27"
        )
        self.assertEqual(values["coins"], 3000)
        self.assertEqual(values["stock_prices"][:3], [1000, 1800, 2700])
        self.assertEqual(values["stock_prices"][3:], [1300, 2200, 3500])
        self.assertEqual(settings_file.parse_text(settings_file.format_text(values)), values)

    def test_saving_into_a_new_folder(self):
        deep = os.path.join(_SETTINGS_DIR, "새폴더", "settings.txt")
        self.assertTrue(settings_file.save(dict(settings_file.DEFAULTS), deep))
        self.assertTrue(os.path.exists(deep))

    def test_env_override_is_used(self):
        self.assertEqual(
            settings_file.settings_path(), os.environ[settings_file.ENV_OVERRIDE]
        )


class SpriteTest(unittest.TestCase):
    def test_all_sprites_are_valid(self):
        self.assertTrue(sprites.validate_all())

    def test_frames_are_rectangular_and_match(self):
        for pokemon in sprites.POKEMON.values():
            frames = pokemon.frames()
            self.assertGreaterEqual(len(frames), 2, pokemon.key)
            if pokemon.move == "walk":
                self.assertEqual(
                    len(frames) % 2, 0, "%s: 걷기 프레임은 짝수여야 자연스럽다" % pokemon.key
                )
            width = len(frames[0][0])
            for frame in frames:
                self.assertEqual(len(frame), len(pokemon.frame_rows[0]), pokemon.key)
                for row in frame:
                    self.assertEqual(len(row), width, pokemon.key)

    def test_floating_sprites_have_a_cycle(self):
        """떠다니는 포켓몬은 돌려 볼 프레임이 여러 장 있어야 한다."""
        floating = [p for p in sprites.POKEMON.values() if p.move == "float"]
        self.assertTrue(floating, "떠다니는 포켓몬이 하나는 있어야 합니다")
        for pokemon in floating:
            self.assertGreaterEqual(len(pokemon.frames()), 2, pokemon.key)
            self.assertFalse(pokemon.bounce, "%s: 프레임이 흔들림을 담당한다" % pokemon.key)

    def test_move_mode_is_known(self):
        for pokemon in sprites.POKEMON.values():
            self.assertIn(pokemon.move, ("walk", "hop", "float"), pokemon.key)

    def test_walk_frames_differ(self):
        for pokemon in sprites.POKEMON.values():
            frames = pokemon.frames()
            self.assertNotEqual(
                frames[0], frames[1], "%s: 걷기 프레임이 동일합니다" % pokemon.key
            )

    def test_walk_frames_change_the_whole_body(self):
        original = sprites.POKEMON["pikachu"].frames()
        walking = pt.whole_walk_frames(original)
        self.assertEqual(len(walking), len(original))
        self.assertTrue(all(len(frame) == len(walking[0]) for frame in walking))
        self.assertTrue(all(len(row) == len(walking[0][0]) for frame in walking for row in frame))
        self.assertNotEqual(walking[0], pt.pad_on_ground(
            original[0], len(walking[0][0]), len(walking[0])
        ))

    def test_walk_poses_use_the_same_canvas_as_the_body_frames(self):
        pokemon = sprites.POKEMON["pikachu"]
        walking = pt.whole_walk_frames(pokemon.frames())
        width = len(walking[0][0])
        height = len(walking[0])
        for pose in pokemon.poses().values():
            padded = pt.pad_on_ground(pose, width, height)
            self.assertEqual(len(padded), height)
            self.assertTrue(all(len(row) == width for row in padded))

    def test_facing_is_declared(self):
        for pokemon in sprites.POKEMON.values():
            self.assertIn(pokemon.facing, ("left", "right"), pokemon.key)

    def test_scale_factor_is_positive(self):
        for pokemon in sprites.POKEMON.values():
            self.assertGreater(pokemon.scale_factor, 0, pokemon.key)

    def test_imported_sprites_have_a_walk_cycle(self):
        for key in ("pikachu", "charmander", "squirtle", "bulbasaur"):
            frames = sprites.POKEMON[key].frames()
            self.assertEqual(len(frames), 4, key)
            # 0/2 는 가만히 선 자세, 1/3 은 각각 다른 발을 든 자세
            self.assertEqual(frames[0], frames[2], key)
            self.assertNotEqual(frames[1], frames[3], key)

    def test_sprites_have_visible_pixels(self):
        for pokemon in sprites.POKEMON.values():
            filled = sum(1 for row in pokemon.frames()[0] for cell in row if cell)
            self.assertGreater(filled, 50, pokemon.key)


class CellSizeTest(unittest.TestCase):
    """그림에서 도트 격자를 알아내는 부분.

    도트 한 칸을 실제보다 작게 잡으면 몇 칸마다 한 칸씩 늘어나, 외곽선이
    두꺼워지고 그림이 뭉개진다. 뮤를 들여올 때 7.82 픽셀짜리를 6.95 로 잡아
    실제로 그랬다. 부드럽게 확대되어 저장된 그림에서도 맞히는지 확인한다.
    """

    def setUp(self):
        try:
            from PIL import Image                       # noqa: F401
        except ImportError:
            self.skipTest("Pillow 가 필요합니다")
        spec = importlib.util.spec_from_file_location(
            "import_sprite", os.path.join(os.path.dirname(__file__),
                                          "tools", "import_sprite.py")
        )
        self.tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.tool)

    def _art(self, columns, rows, seed=7):
        """작은 도트 그림 하나를 만든다."""
        from PIL import Image

        import random as rng
        maker = rng.Random(seed)
        colors = [(20, 20, 30), (240, 170, 205), (200, 110, 150), (255, 255, 255)]
        image = Image.new("RGB", (columns, rows), (255, 255, 255))
        pixels = image.load()
        for y in range(rows):
            for x in range(columns):
                # 가장자리는 흰 여백으로 둬서 내용 상자가 잡히게 한다.
                edge = x < 2 or y < 2 or x >= columns - 2 or y >= rows - 2
                pixels[x, y] = (255, 255, 255) if edge else maker.choice(colors[:3])
        return image

    def _blown_up(self, columns, rows, cell):
        """정수배가 아닌 크기로 부드럽게 늘린다(사람이 올리는 그림처럼)."""
        from PIL import Image

        art = self._art(columns, rows)
        return art.resize((int(columns * cell), int(rows * cell)), Image.BICUBIC)

    def _measure(self, image):
        def is_background(color):
            return color[0] > 235 and color[1] > 235 and color[2] > 235

        box = self.tool.content_box(image, is_background)
        guess = self.tool.guess_cell_size(image, box, is_background)
        return self.tool.refine_cell(image, box, guess)

    def test_finds_a_non_integer_cell_size(self):
        for cell in (7.82, 9.4):
            got = self._measure(self._blown_up(28, 24, cell))
            self.assertAlmostEqual(
                got, cell, delta=cell * 0.03,
                msg="칸 %.2f 를 %.2f 로 쟀습니다" % (cell, got)
            )

    def test_does_not_land_on_a_multiple(self):
        """칸 크기의 두 배·세 배도 점수가 높다. 거기에 걸리면 안 된다."""
        cell = 8.0
        got = self._measure(self._blown_up(28, 24, cell))
        self.assertLess(got, cell * 1.5, "칸을 배수로 잡았습니다: %.2f" % got)


@needs_display
class ArgumentTest(unittest.TestCase):
    def test_default_scale_draws_at_one_and_a_half(self):
        app = pt.App(pt.parse_args([]))
        try:
            for pokemon in sprites.POKEMON.values():
                self.assertAlmostEqual(app.sprite_scale(pokemon), 1.5, places=6, msg=pokemon.key)
        finally:
            app.quit()

    def test_default_is_one_pikachu(self):
        args = pt.parse_args([])
        self.assertEqual(args.species, ["pikachu"])

    def test_count_adds_more_pets(self):
        args = pt.parse_args(["--count", "4"])
        self.assertEqual(len(args.species), 4)

    def test_count_never_hands_out_evolved_pokemon(self):
        """진화체는 진화로만 만나야 한다. 무작위로 나눠 주면 안 된다."""
        for _ in range(60):
            args = pt.parse_args(["--count", "5"])
            for key in args.species:
                self.assertNotIn(key, sprites.EVOLVED_ONLY, "무작위로 나왔습니다: %s" % key)

    def test_named_evolved_pokemon_is_still_allowed(self):
        """직접 이름을 대면(명령줄) 쓸 수 있어야 한다."""
        args = pt.parse_args(["-p", "wartortle"])
        self.assertEqual(args.species, ["wartortle"])

    def test_on_taskbar_is_off_by_default(self):
        self.assertFalse(pt.parse_args([]).on_taskbar)
        self.assertTrue(pt.parse_args(["--on-taskbar"]).on_taskbar)

    def test_named_pokemon_is_kept(self):
        args = pt.parse_args(["-p", "squirtle", "-p", "bulbasaur"])
        self.assertEqual(args.species[:2], ["squirtle", "bulbasaur"])

    def test_unknown_pokemon_is_rejected(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            pt.parse_args(["-p", "mudkip"])

    def test_scale_must_be_positive(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
            pt.parse_args(["--scale", "0"])


@needs_display
class FacingTest(unittest.TestCase):
    """이동 방향과 그림이 보는 방향 맞추기."""

    def test_left_facing_art_flips_when_walking_right(self):
        pikachu = sprites.POKEMON["pikachu"]
        self.assertEqual(pikachu.facing, "left")
        self.assertTrue(pt.flip_for(pikachu, moving_right=True))
        self.assertFalse(pt.flip_for(pikachu, moving_right=False))

    def test_right_facing_art_flips_when_walking_left(self):
        plain = sprites.Pokemon(
            "test", "테스트", {"K": "#000000"}, rows=["KK", "KK"], step_rows={0: "K."}
        )
        self.assertEqual(plain.facing, "right")
        self.assertFalse(pt.flip_for(plain, moving_right=True))
        self.assertTrue(pt.flip_for(plain, moving_right=False))

    def test_every_sprite_faces_its_way(self):
        for pokemon in sprites.POKEMON.values():
            for moving_right in (True, False):
                flipped = pt.flip_for(pokemon, moving_right)
                looks_right = (pokemon.facing == "right") != flipped
                self.assertEqual(looks_right, moving_right, pokemon.key)


@needs_display
class SpriteListTest(unittest.TestCase):
    """--list 는 어느 빌드를 쓰는지 확인하는 용도이므로 실제 크기를 보여야 한다."""

    def test_list_shows_size_and_frames(self):
        lines = pt.sprite_list()
        self.assertEqual(len(lines), len(sprites.POKEMON))
        for line, pokemon in zip(lines, sprites.POKEMON.values()):
            frames = pokemon.frames()
            self.assertIn(pokemon.key, line)
            self.assertIn("%dx%d" % (len(frames[0][0]), len(frames[0])), line)
            self.assertIn("%d프레임" % len(frames), line)


@needs_display
class ImageTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = tk.Tk()
        cls.root.withdraw()

    @classmethod
    def tearDownClass(cls):
        cls.root.destroy()

    def test_fractional_scale_keeps_the_shape(self):
        """1.5 배처럼 소수로 키워도 가로세로 비율이 1픽셀 넘게 틀어지지 않아야 한다."""
        for pokemon in sprites.POKEMON.values():
            grid = pokemon.frames()[0]
            width = len(grid[0])
            height = len(grid)
            photo = pt.make_photo(grid, 1.5)
            self.assertEqual(photo.width(), int(width * 1.5 + 0.5), pokemon.key)
            self.assertEqual(photo.height(), int(height * 1.5 + 0.5), pokemon.key)
            # 비율이 틀어진 정도를 픽셀로 환산하면 1픽셀 미만이어야 한다
            expected_height = photo.width() * height / width
            self.assertLess(abs(photo.height() - expected_height), 1.0, pokemon.key)

    def test_photo_size_matches_scale(self):
        grid = sprites.POKEMON["pikachu"].frames()[0]
        photo = pt.make_photo(grid, 3)
        self.assertEqual(photo.width(), len(grid[0]) * 3)
        self.assertEqual(photo.height(), len(grid) * 3)

    def test_flip_mirrors_the_sprite(self):
        grid = sprites.POKEMON["charmander"].frames()[0]
        normal = pt.make_photo(grid, 2)
        flipped = pt.make_photo(grid, 2, flip=True)
        width = normal.width()
        row = 20
        self.assertEqual(normal.get(0, row), flipped.get(width - 1, row))


@needs_display
class PetMovementTest(unittest.TestCase):
    def setUp(self):
        self.app = pt.App(pt.parse_args(["-p", "pikachu", "--speed", "200"]))

    def tearDown(self):
        try:
            self.app.quit()
        except Exception:
            pass

    def _pump(self, seconds):
        """이벤트 루프를 주어진 시간만큼 돌린다.

        무작위 행동(갑자기 멈추거나 방향을 바꾸는 것)은 잠시 꺼서 결과를 예측 가능하게 만든다.
        """
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0):
            end = time.time() + seconds
            while time.time() < end:
                self.app.root.update()
                time.sleep(0.01)

    def test_pet_stands_on_the_ground_line(self):
        # 창에는 효과가 튀어나갈 여백이 있으므로 창 높이 기준으로 바닥을 잡는다.
        pet = self.app.pets[0]
        expected = min(self.app.ground_y - pet.window_height,
                       self.app.screen_height - pet.window_height)
        self.assertEqual(pet.base_y, max(0, expected))

    def test_pet_walks_and_stays_on_screen(self):
        pet = self.app.pets[0]
        pet.state = "walk"
        start = pet.x
        self._pump(1.2)
        self.assertNotAlmostEqual(pet.x, start, places=1)
        self.assertGreaterEqual(pet.x, 0)
        self.assertLessEqual(pet.x, pet.max_x)

    def test_pet_turns_around_at_the_edge(self):
        pet = self.app.pets[0]
        pet.x = pet.max_x
        pet.direction = 1
        pet.state = "walk"
        self._pump(0.3)
        self.assertEqual(pet.direction, -1)
        self.assertLessEqual(pet.x, pet.max_x)

    def test_walking_starts_with_acceleration(self):
        pet = self.app.pets[0]
        pet.x = pet.max_x / 2.0
        pet.walk_speed = 0.0
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0):
            pet.tick()
        self.assertGreater(pet.walk_speed, 0.0)
        self.assertLess(pet.walk_speed, pet.speed)

    def test_walk_frame_follows_distance(self):
        pet = self.app.pets[0]
        pet.gait_distance = pt.WALK_STRIDE / pet.frame_count
        self.assertEqual(pet.walk_frame(), 1)

    def test_walk_bob_rises_between_footsteps(self):
        pet = self.app.pets[0]
        pet.gait_distance = 0.0
        self.assertEqual(pet.walk_bob(), 0)
        pet.gait_distance = pt.WALK_STRIDE * 2 / pt.WALK_SUBSTEPS
        self.assertEqual(pet.walk_bob(), pet.bounce_px)

    def test_idle_action_uses_the_pokemon_personality(self):
        pet = self.app.pets[0]
        with mock.patch("pokemon_taskbar.random.random", return_value=0.0):
            pet.start_idle_action()
        self.assertEqual(pet.idle_action, "spark")
        pet.update_idle_action(pt.IDLE_EFFECT_EVERY)
        self.assertTrue(pet.effects)

    def test_nearby_walkers_stop_to_greet(self):
        first = self.app.pets[0]
        second = self.app.add_pet("charmander")
        first.x = 100
        second.x = 100 + pt.GREETING_DISTANCE / 2
        first.state = second.state = "walk"
        self.assertTrue(self.app.start_greeting_near(first))
        self.assertGreater(first.greeting_left, 0)
        self.assertGreater(second.greeting_left, 0)
        self.assertEqual(first.direction, 1)
        self.assertEqual(second.direction, -1)
        first.greeting_step(pt.TICK_MS / 1000.0)
        self.assertIn("talk", [effect["kind"] for effect in first.effects])

    def test_turn_slows_before_changing_direction(self):
        pet = self.app.pets[0]
        pet.x = pet.max_x
        pet.direction = 1
        pet.walk_speed = pet.speed
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0):
            pet.tick()
            self.assertEqual(pet.direction, 1)
            self.assertEqual(pet.state, "slow_stop")
            # 빠른 속도에서는 감속 구간도 길어진다.
            for _ in range(20):
                pet.tick()
        self.assertEqual(pet.direction, -1)
        self.assertEqual(pet.state, "walk")

    def test_idle_pet_starts_walking_again(self):
        pet = self.app.pets[0]
        pet.set_state("idle")
        pet.state_left = 0.05
        self._pump(0.3)
        self.assertEqual(pet.state, "walk")

    def test_playful_hops_return_to_walking(self):
        pet = self.app.pets[0]
        pet.start_playing()
        pet.state_left = 0.0
        pet.tick()
        self.assertEqual(pet.state, "play_air")
        self.assertGreater(pet.lift, 0)

        # 두 번의 짧은 점프가 끝나면 다시 걷는다.
        for _ in range(pt.PLAY_HOPS):
            pet.lift = 0.0
            pet.vertical_speed = 0.0
            pet.tick()
            if pet.state == "play_wait":
                pet.state_left = 0.0
                pet.tick()
        self.assertEqual(pet.state, "walk")

    def test_release_removes_the_pet(self):
        self.app.add_pet("squirtle")
        pet = self.app.pets[0]
        self.app.remove_pet(pet)
        self.assertNotIn(pet, self.app.pets)
        self.assertEqual(len(self.app.pets), 1)


@needs_display
@needs_display
class GroundLineTest(unittest.TestCase):
    """포켓몬이 서 있을 바닥 높이 계산."""

    def test_work_area_falls_back_to_the_screen_bottom(self):
        # 윈도우가 아니면 작업 영역을 알 수 없으므로 화면 맨 아래를 쓴다.
        self.assertEqual(pt.work_area_bottom(1080), 1080)

    def test_work_area_never_exceeds_the_screen(self):
        self.assertLessEqual(pt.work_area_bottom(1080), 1080)

    def test_on_taskbar_uses_the_screen_bottom(self):
        app = pt.App(pt.parse_args(["--on-taskbar"]))
        try:
            self.assertEqual(app.ground_y, app.screen_height)
        finally:
            app.quit()

    def test_sprite_scale_follows_the_scale_factor(self):
        app = pt.App(pt.parse_args(["--scale", "3"]))
        try:
            # 이미지에서 들여온 촘촘한 도트는 1배로 그린다
            for key in sprites.POKEMON:
                self.assertAlmostEqual(app.sprite_scale(sprites.POKEMON[key]), 1, places=6)
            # 배율을 따로 주지 않은 스프라이트는 --scale 을 그대로 쓴다
            plain = sprites.Pokemon(
                "test", "테스트", {"K": "#000000"}, rows=["KK", "KK"], step_rows={0: "K."}
            )
            self.assertEqual(app.sprite_scale(plain), 3)
        finally:
            app.quit()

    def test_sprite_scale_never_collapses(self):
        """아주 작은 배율을 줘도 도트가 사라지지는 않아야 한다."""
        app = pt.App(pt.parse_args(["--scale", "0.1"]))
        try:
            for pokemon in sprites.POKEMON.values():
                scale = app.sprite_scale(pokemon)
                self.assertGreaterEqual(scale, pt.MIN_SPRITE_SCALE, pokemon.key)
                photo = pt.make_photo(pokemon.frames()[0], scale, master=app.root)
                self.assertGreaterEqual(photo.width(), 1, pokemon.key)
                self.assertGreaterEqual(photo.height(), 1, pokemon.key)
        finally:
            app.quit()

    def test_default_ground_is_at_or_above_the_screen_bottom(self):
        app = pt.App(pt.parse_args([]))
        try:
            self.assertLessEqual(app.ground_y, app.screen_height)
        finally:
            app.quit()

    def test_offset_lifts_the_pet(self):
        plain = pt.App(pt.parse_args([]))
        try:
            base = plain.pets[0].base_y
        finally:
            plain.quit()

        lifted = pt.App(pt.parse_args(["--offset", "40"]))
        try:
            self.assertEqual(base - 40, lifted.pets[0].base_y)
        finally:
            lifted.quit()


class FakeMouse:
    """마우스 이벤트 흉내. 화면 좌표만 있으면 된다."""

    def __init__(self, x, y):
        self.x_root = x
        self.y_root = y


@needs_display
class HopTest(unittest.TestCase):
    """메타몽처럼 뛰어다니는 이동."""

    def setUp(self):
        self.app = pt.App(pt.parse_args(["-p", "ditto"]))
        self.pet = self.app.pets[0]

    def tearDown(self):
        self.app.quit()

    def _run(self, seconds):
        for _ in range(int(seconds / (pt.TICK_MS / 1000.0))):
            self.pet.tick()

    def test_ditto_is_a_hopper(self):
        self.assertEqual(sprites.POKEMON["ditto"].move, "hop")
        self.assertEqual(self.pet.move, "hop")
        self.assertEqual(len(self.pet.images["right"]), 3)

    def test_it_leaves_the_ground(self):
        heights = []
        self._run(0)
        for _ in range(200):
            self.pet.tick()
            heights.append(self.pet.lift)
        self.assertGreater(max(heights), 5, "뛰어오르지 않는다")
        self.assertEqual(min(heights), 0.0, "바닥에 닿지 않는다")

    def test_it_only_moves_while_in_the_air(self):
        # 바닥에 있는 동안에는 앞으로 나아가지 않아야 한다.
        self.pet.hop_state = "rest"
        self.pet.hop_timer = 10.0
        self.pet.lift = 0.0
        before = self.pet.x
        self._run(1.0)
        self.assertEqual(self.pet.x, before)

    def test_it_travels_over_time(self):
        start = self.pet.x
        self._run(6.0)
        self.assertNotAlmostEqual(self.pet.x, start, places=1)
        self.assertGreaterEqual(self.pet.x, 0)
        self.assertLessEqual(self.pet.x, self.pet.max_x)

    def test_frames_follow_the_hop(self):
        self.pet.hop_state = "rest"
        self.assertEqual(self.pet.hop_frame(), 0)
        self.pet.hop_state = "crouch"
        self.assertEqual(self.pet.hop_frame(), 1)
        self.pet.hop_state = "land"
        self.assertEqual(self.pet.hop_frame(), 1)
        self.pet.hop_state = "air"
        self.assertEqual(self.pet.hop_frame(), 2)

    def test_hopper_does_not_use_the_walk_bounce(self):
        self.assertEqual(self.pet.bounce_px, 0)
        self.assertFalse(sprites.POKEMON["ditto"].bounce)

    def test_hopper_can_be_dragged_and_lands(self):
        self.pet.on_press(FakeMouse(100, self.pet.base_y))
        self.pet.on_drag(FakeMouse(400, self.pet.base_y - 150))
        self.assertGreater(self.pet.lift, 100)
        self.pet.on_release(FakeMouse(400, self.pet.base_y - 150))
        # 놓으면 떨어져 착지한다. (착지한 뒤에는 곧바로 다시 뛰므로
        #  '지금 바닥에 있는지'가 아니라 '바닥에 닿았는지'를 본다.)
        landed = False
        for _ in range(50):
            self.pet.tick()
            if self.pet.lift == 0.0:
                landed = True
                break
        self.assertTrue(landed, "놓았는데 착지하지 않는다")


@needs_display
class FloatTest(unittest.TestCase):
    """뮤처럼 공중에 떠다니는 이동."""

    def setUp(self):
        self.app = pt.App(pt.parse_args(["-p", "mew"]))
        self.pet = self.app.pets[0]

    def tearDown(self):
        self.app.quit()

    def _run(self, seconds):
        for _ in range(int(seconds / (pt.TICK_MS / 1000.0))):
            self.pet.tick()

    def test_mew_is_a_floater(self):
        self.assertEqual(sprites.POKEMON["mew"].move, "float")
        self.assertEqual(self.pet.move, "float")

    def test_it_starts_in_the_air(self):
        self.assertGreater(self.pet.lift, 0.0, "처음부터 떠 있어야 한다")

    def test_it_never_touches_the_ground(self):
        lowest = self.pet.lift
        for _ in range(600):                      # 24초
            self.pet.tick()
            lowest = min(lowest, self.pet.lift)
        self.assertGreater(lowest, 0.0, "바닥에 내려앉았다")

    def test_it_stays_on_screen(self):
        for _ in range(600):
            self.pet.tick()
            self.assertGreaterEqual(self.pet.lift, 0.0)
            self.assertLessEqual(self.pet.lift, self.pet.base_y)
            self.assertGreaterEqual(self.pet.x, 0)
            self.assertLessEqual(self.pet.x, self.pet.max_x)

    def test_it_bobs_up_and_down(self):
        heights = []
        for _ in range(int(pt.FLOAT_BOB_SEC / (pt.TICK_MS / 1000.0)) + 4):
            self.pet.tick()
            heights.append(self.pet.lift)
        self.assertGreater(max(heights) - min(heights), 1.0, "살랑거리지 않는다")

    def test_it_drifts_sideways(self):
        start = self.pet.x
        self._run(6.0)
        self.assertNotAlmostEqual(self.pet.x, start, places=1)

    def test_it_does_not_fall_when_dropped(self):
        self.pet.on_press(FakeMouse(100, self.pet.base_y))
        self.pet.on_drag(FakeMouse(300, self.pet.base_y - 200))
        self.assertGreater(self.pet.lift, 150)
        self.pet.on_release(FakeMouse(300, self.pet.base_y - 200))
        for _ in range(50):                       # 2초
            self.pet.tick()
        self.assertGreater(self.pet.lift, 0.0, "놓았더니 떨어졌다")

    def test_it_returns_to_its_hover_band(self):
        # 아주 높이 올려놓아도 스스로 제자리 높이로 내려온다.
        self.pet.on_press(FakeMouse(100, self.pet.base_y))
        self.pet.on_drag(FakeMouse(100, 0))
        self.pet.on_release(FakeMouse(100, 0))
        self.pet.drag_moved = True
        high = self.pet.lift
        self._run(12.0)
        self.assertLess(self.pet.lift, high, "제 높이로 돌아오지 않는다")

    def test_it_is_not_permanently_stretched(self):
        # 늘 공중에 있다고 해서 계속 '늘어남' 자세가 되면 안 된다.
        self.pet.blinking = 0.0
        self.pet.napping = False
        self.assertIsNone(self.pet.choose_pose())

    def test_floater_does_not_use_the_walk_bounce(self):
        self.assertEqual(self.pet.bounce_px, 0)

    def test_frames_cycle_while_floating(self):
        seen = set()
        for _ in range(int(4 * pt.FLOAT_STEP_SEC / (pt.TICK_MS / 1000.0)) + 8):
            self.pet.tick()
            seen.add(int(self.pet.anim_time / pt.FLOAT_STEP_SEC) % self.pet.frame_count)
        self.assertGreater(len(seen), 1, "프레임이 넘어가지 않는다")


@needs_display
class EvolutionTest(unittest.TestCase):
    """함께 산책하고 아껴 준 뒤, 직접 선택해서 진화한다."""

    def setUp(self):
        self.app = pt.App(pt.parse_args(["-p", "squirtle"]))
        self.pet = self.app.pets[0]

    def tearDown(self):
        self.app.quit()

    def _run(self, seconds):
        for _ in range(int(seconds / (pt.TICK_MS / 1000.0))):
            self.app.pets[0].tick()

    def _meet_requirements(self):
        for _ in range(int(pt.EVOLVE_PET_NEED)):
            self.pet.petted()
        self.pet.walked = pt.EVOLVE_WALK_NEED
        self.app.growth_drops = 1

    def _start_evolving(self):
        self._meet_requirements()
        self.assertTrue(self.pet.can_evolve())
        self.pet.start_evolving()
        self.assertTrue(self.pet.evolving)

    def test_squirtle_evolves_into_wartortle(self):
        self.assertEqual(sprites.POKEMON["squirtle"].evolves_to, "wartortle")
        self.assertEqual(self.pet.next_key, "wartortle")

    def test_most_pokemon_do_not_evolve(self):
        self.assertIsNone(sprites.POKEMON["pikachu"].evolves_to)

    def test_it_is_not_ready_at_the_start(self):
        self.assertFalse(self.pet.can_evolve())
        self.assertEqual(self.pet.pets_left(), int(pt.EVOLVE_PET_NEED))
        self.assertEqual(self.pet.walk_left(), int(pt.EVOLVE_WALK_NEED))

    def test_petting_needs_a_walk_and_manual_choice(self):
        for _ in range(int(pt.EVOLVE_PET_NEED)):
            self.pet.petted()
        self.assertFalse(self.pet.can_evolve())
        self.assertFalse(self.pet.evolving)
        self.assertEqual(self.pet.pets_left(), 0)
        self.assertEqual(self.pet.walk_left(), int(pt.EVOLVE_WALK_NEED))

    def test_walking_needs_petting(self):
        self.pet.walked = pt.EVOLVE_WALK_NEED
        self.assertFalse(self.pet.evolving)
        self.assertFalse(self.pet.can_evolve())
        self.assertEqual(self.pet.pets_left(), int(pt.EVOLVE_PET_NEED))
        self.assertEqual(self.pet.walk_left(), 0)

    def test_ready_pet_waits_for_manual_choice(self):
        self._meet_requirements()
        self.assertTrue(self.pet.can_evolve())
        self.assertFalse(self.pet.evolving)
        self.pet.start_evolving()
        self.assertTrue(self.pet.evolving)

    def test_growth_drop_is_required_for_evolution(self):
        self._meet_requirements()
        self.app.growth_drops = 0
        self.assertFalse(self.pet.can_evolve())

    def test_evolution_uses_one_growth_drop(self):
        self._meet_requirements()
        self.pet.start_evolving()
        self.assertEqual(self.app.growth_drops, 0)

    def test_only_walking_earns_money(self):
        before = self.app.coins
        self.pet.petted()
        self.assertEqual(self.app.coins, before)
        self.pet.x = self.pet.max_x / 2.0
        self.pet.direction = 1
        self.pet.advance_walk(pt.COIN_WALK_DISTANCE)
        self.assertEqual(self.app.coins, before + pt.COINS_PER_WALK)

    def test_pokemon_costs_two_hours_of_walk_money(self):
        expected = int(
            pt.DEFAULT_WALK_SPEED * 2 * 60 * 60
            / pt.COIN_WALK_DISTANCE * pt.COINS_PER_WALK
        )
        self.assertEqual(pt.POKEMON_PRICE, expected)
        self.app.coins = pt.POKEMON_PRICE
        self.app.buy_pet("pikachu")
        self.assertEqual(self.app.coins, 0)
        self.assertEqual(len(self.app.pets), 2)

    def test_pokemon_is_not_added_without_enough_money(self):
        self.app.coins = pt.POKEMON_PRICE - 1
        self.app.buy_pet("pikachu")
        self.assertEqual(len(self.app.pets), 1)

    def test_food_can_be_bought_and_fed(self):
        self.app.coins = pt.FOOD_COST
        self.app.buy_food()
        self.assertEqual(self.app.coins, 0)
        self.assertEqual(self.app.food, 1)
        self.app.feed_pet(self.pet)
        self.assertEqual(self.app.food, 0)
        self.assertEqual(self.pet.friendship, pt.FOOD_FRIENDSHIP)

    def test_growth_drop_can_be_bought(self):
        self.app.coins = pt.GROWTH_DROP_COST
        self.app.buy_growth_drop()
        self.assertEqual(self.app.coins, 0)
        self.assertEqual(self.app.growth_drops, 1)

    def test_stock_can_be_bought_and_sold(self):
        self.app.coins = 1000
        self.app.stock_prices = [1000, 1800, 2700, 1300, 2200, 3500]
        self.app.buy_stock(0)
        self.assertEqual(self.app.coins, 0)
        self.assertEqual(self.app.stock_shares, [1, 0, 0, 0, 0, 0])
        self.app.sell_stock(0)
        self.assertEqual(self.app.coins, 1000)
        self.assertEqual(self.app.stock_shares, [0, 0, 0, 0, 0, 0])

    def test_stock_can_be_delisted_below_100_won(self):
        self.app.stock_prices = [101, 1800, 2700, 1300, 2200, 3500]
        self.app.stock_shares[0] = 3
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0), \
                mock.patch("pokemon_taskbar.random.randint", return_value=-12):
            self.app.update_market()
        self.assertTrue(self.app.stock_delisted[0])
        self.assertEqual(self.app.stock_prices[0], 0)
        self.assertEqual(self.app.stock_shares[0], 0)
        self.assertEqual(self.app.stock_relist_seconds[0], pt.STOCK_RELIST_SECONDS)

    def test_delisted_stock_relists_with_a_random_personality(self):
        self.app.stock_delisted[0] = True
        self.app.stock_relist_seconds[0] = int(pt.MARKET_UPDATE_SEC)
        self.app.stock_listing_ids[0] = 0
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0), \
                mock.patch("pokemon_taskbar.random.choice", return_value=7):
            self.app.update_market()
        self.assertFalse(self.app.stock_delisted[0])
        self.assertEqual(self.app.stock_listing_ids[0], 7)
        self.assertEqual(self.app.stock_prices[0], pt.STOCK_LISTINGS[7][1])

    def test_stock_history_keeps_the_latest_twenty_prices(self):
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0), \
                mock.patch("pokemon_taskbar.random.randint", return_value=7):
            for _ in range(24):
                self.app.update_market()
        self.assertEqual(len(self.app.stock_history[0]), 20)
        self.assertGreater(self.app.stock_history[0][-1], self.app.stock_history[0][0])

    def test_stock_change_percent_uses_the_visible_graph_period(self):
        self.app.stock_prices = [1000, 1800, 2700, 1300, 2200, 3500]
        self.app.stock_history = [[1000], [1800], [2700], [1300], [2200], [3500]]
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0), \
                mock.patch("pokemon_taskbar.random.randint", return_value=10):
            self.app.update_market()
        self.assertAlmostEqual(self.app.stock_change_percent(0), 10.0)

    def test_stock_market_opens_in_its_own_overlay(self):
        self.app.open_stock_overlay()
        overlay = self.app.stock_overlay
        self.assertIsNotNone(overlay)
        self.assertTrue(overlay.window.winfo_exists())
        self.assertTrue(overlay.rows[0][2].find_all(), "그래프가 그려지지 않습니다")
        old_x = overlay.window.winfo_x()
        old_y = overlay.window.winfo_y()
        overlay.begin_drag(FakeMouse(old_x + 10, old_y + 10))
        overlay.drag(FakeMouse(old_x + 50, old_y + 40))
        self.app.root.update_idletasks()
        self.assertGreater(overlay.window.winfo_x(), old_x)
        overlay.close()
        self.assertIsNone(self.app.stock_overlay)

    def test_start_evolving_rejects_unmet_conditions(self):
        self.pet.start_evolving()
        self.assertFalse(self.pet.evolving)

    def test_walking_counts_toward_evolution(self):
        self.pet.x = self.pet.max_x / 2.0
        self.pet.direction = 1
        self.pet.speed = 200.0
        before = self.pet.walked
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0):
            self.pet.tick()
        self.assertGreater(self.pet.walked, before)

    def test_time_alone_never_evolves_it(self):
        """아끼던 모습이 예고 없이 바뀌면 안 된다. 시간만으로는 진화하지 않는다."""
        self._run(30.0)
        self.assertEqual(self.pet.friendship, 0.0)
        self.assertGreater(self.pet.walked, 0.0)
        self.assertFalse(self.pet.evolving)

    def test_petting_more_does_not_start_or_overfill_it(self):
        for _ in range(int(pt.EVOLVE_PET_NEED) * 3):
            self.pet.petted()
        self.assertEqual(self.pet.friendship, pt.EVOLVE_PET_NEED)
        self.assertFalse(self.pet.evolving)
        self.assertEqual(self.pet.evolve_step, 0)

    def test_the_flash_ends_with_the_evolved_form(self):
        self._start_evolving()
        # 번쩍임이 다 끝날 만큼 돌린다.
        for _ in range(400):
            if self.app.pets and self.app.pets[0].pokemon.key == "wartortle":
                break
            self.app.pets[0].tick()
        self.assertEqual(len(self.app.pets), 1)
        self.assertEqual(self.app.pets[0].pokemon.key, "wartortle")
        self.assertIsNone(self.app.pets[0].next_key)

    def test_it_stays_where_it_was(self):
        self.pet.x = 200.0
        self.pet.direction = -1
        self._start_evolving()
        for _ in range(400):
            if self.app.pets[0].pokemon.key == "wartortle":
                break
            self.app.pets[0].tick()
        grown = self.app.pets[0]
        self.assertEqual(grown.pokemon.key, "wartortle")
        self.assertAlmostEqual(grown.x, 200.0, delta=1.0)
        self.assertEqual(grown.direction, -1)

    def test_it_does_not_move_while_evolving(self):
        self._start_evolving()
        start = self.pet.x
        for _ in range(5):
            self.pet.tick()
        self.assertEqual(self.pet.x, start)

    def test_it_cannot_be_dragged_while_evolving(self):
        self._start_evolving()
        self.pet.on_press(FakeMouse(100, self.pet.base_y))
        self.assertFalse(self.pet.dragging)

    def test_the_window_fits_both_forms(self):
        """번쩍이는 동안 진화한 모습이 잘리면 안 된다."""
        after = self.app.get_images(sprites.POKEMON["wartortle"])["right"][0]
        self.assertGreaterEqual(self.pet.width, after.width())
        self.assertGreaterEqual(self.pet.height, after.height())

    def test_it_still_stands_on_the_ground(self):
        """창이 커져도 발은 바닥에 그대로 붙어 있어야 한다."""
        top = self.pet.base_y + self.pet.margin_top + self.pet.hop + self.pet.own_dy
        self.assertEqual(top + self.pet.own_height, self.app.ground_y - self.app.offset)

    def test_the_flash_speeds_up(self):
        gaps = [self.pet.evolve_flash_seconds(i) for i in range(pt.EVOLVE_FLASHES)]
        self.assertEqual(gaps, sorted(gaps, reverse=True))
        self.assertAlmostEqual(gaps[0], pt.EVOLVE_FIRST_SEC)
        self.assertAlmostEqual(gaps[-1], pt.EVOLVE_LAST_SEC)

    def test_silhouettes_are_pure_white(self):
        white = self.app.get_white(sprites.POKEMON["squirtle"])["right"]
        seen = set()
        for y in range(0, white.height(), 3):
            for x in range(0, white.width(), 3):
                if not white.transparency_get(x, y):
                    seen.add(white.get(x, y))
        self.assertTrue(seen)
        for colour in seen:
            self.assertEqual(tuple(colour), (255, 255, 255))

    def test_the_evolved_form_is_saved(self):
        self._start_evolving()
        for _ in range(400):
            if self.app.pets[0].pokemon.key == "wartortle":
                break
            self.app.pets[0].tick()
        self.assertEqual(self.app.current_settings()["species"], ["wartortle"])


@needs_display
class DragTest(unittest.TestCase):
    """클릭한 채로 끌어서 옮기기."""

    def setUp(self):
        self.app = pt.App(pt.parse_args([]))
        self.pet = self.app.pets[0]
        self.pet.x = 100
        self.pet.place()

    def tearDown(self):
        self.app.quit()

    def _grab(self, x=None, y=None):
        x = 100 if x is None else x
        y = self.pet.base_y if y is None else y
        self.pet.on_press(FakeMouse(x, y))

    def test_drag_moves_the_pet(self):
        self._grab()
        self.pet.on_drag(FakeMouse(300, self.pet.base_y - 120))
        self.assertEqual(int(self.pet.x), 300)
        self.assertAlmostEqual(self.pet.lift, 120, delta=1)

    def test_pet_does_not_walk_while_held(self):
        self._grab()
        self.pet.on_drag(FakeMouse(300, self.pet.base_y - 50))
        before = self.pet.x
        for _ in range(20):
            self.pet.tick()
        self.assertEqual(self.pet.x, before)

    def test_dropped_pet_falls_back_to_the_ground(self):
        self._grab()
        self.pet.on_drag(FakeMouse(300, self.pet.base_y - 150))
        self.pet.on_release(FakeMouse(300, self.pet.base_y - 150))
        self.assertGreater(self.pet.lift, 0)
        for _ in range(100):          # 4초면 충분히 떨어진다
            self.pet.tick()
        self.assertEqual(self.pet.lift, 0.0)

    def test_short_click_still_jumps(self):
        self._grab()
        self.pet.on_release(FakeMouse(100, self.pet.base_y))
        self.assertGreater(self.pet.vertical_speed, 0)
        self.pet.tick()
        self.assertGreater(self.pet.lift, 0)

    def test_drag_stays_on_screen(self):
        self._grab()
        self.pet.on_drag(FakeMouse(-500, self.pet.base_y + 500))
        self.assertGreaterEqual(self.pet.x, 0)
        self.assertGreaterEqual(self.pet.lift, 0)
        self.pet.on_drag(FakeMouse(99999, -99999))
        self.assertLessEqual(self.pet.x, self.pet.max_x)
        self.assertLessEqual(self.pet.lift, self.pet.base_y)

    def test_pet_never_starts_off_screen(self):
        """--offset 을 아무리 크게 줘도 창은 화면 안에 있어야 한다."""
        for offset in (0, 500, 5000, -500):
            app = pt.App(pt.parse_args(["--offset", str(offset)]))
            try:
                for pet in app.pets:
                    self.assertGreaterEqual(pet.base_y, 0, "offset=%d" % offset)
                    self.assertLessEqual(
                        pet.base_y + pet.window_height, app.screen_height, "offset=%d" % offset
                    )
            finally:
                app.quit()

    def test_app_always_has_at_least_one_pet(self):
        """설정이 비어 있어도 빈 화면으로 남지 않는다."""
        args = pt.parse_args([])
        args.species = []
        app = pt.App(args)
        try:
            self.assertEqual([pet.pokemon.key for pet in app.pets], ["pikachu"])
        finally:
            app.quit()

    def test_drag_stays_above_the_ground_even_off_screen(self):
        """--offset 을 크게 줘 바닥이 화면 위로 올라가도 lift 는 음수가 되지 않는다."""
        app = pt.App(pt.parse_args(["--offset", "5000"]))
        try:
            pet = app.pets[0]
            self.assertGreaterEqual(pet.base_y, 0)   # 화면 안으로 붙잡혀 있다
            pet.on_press(FakeMouse(100, 100))
            pet.on_drag(FakeMouse(400, 50))
            self.assertGreaterEqual(pet.lift, 0.0)
            pet.on_release(FakeMouse(400, 50))
            with mock.patch("pokemon_taskbar.random.random", return_value=1.0):
                for _ in range(60):
                    pet.tick()
            self.assertEqual(pet.lift, 0.0)
        finally:
            app.quit()

    def test_events_after_removal_are_ignored(self):
        """보내 준 뒤에 뒤늦게 도착한 마우스 이벤트가 예외를 내지 않아야 한다."""
        pet = self.pet
        pet.on_press(FakeMouse(100, pet.base_y))
        self.app.remove_pet(pet)
        pet.on_drag(FakeMouse(300, 200))     # 늦게 도착한 이벤트
        pet.on_release(FakeMouse(300, 200))
        pet.on_press(FakeMouse(300, 200))
        self.assertEqual(pet.state, "gone")

    def test_pet_keeps_walking_after_being_dropped(self):
        self._grab()
        self.pet.on_drag(FakeMouse(300, self.pet.base_y - 30))
        self.pet.on_release(FakeMouse(300, self.pet.base_y - 30))
        self.pet.state = "walk"
        start = self.pet.x
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0):
            for _ in range(40):
                self.pet.tick()
        self.assertEqual(self.pet.lift, 0.0)
        self.assertNotAlmostEqual(self.pet.x, start, places=1)


@needs_display
class EffectTest(unittest.TestCase):
    """착지 먼지 / 클릭 하트 / 낮잠 Zzz."""

    def setUp(self):
        self.app = pt.App(pt.parse_args(["-p", "pikachu"]))
        self.pet = self.app.pets[0]

    def tearDown(self):
        self.app.quit()

    def _kinds(self):
        return [effect["kind"] for effect in self.pet.effects]

    def test_click_pops_a_heart(self):
        self.pet.on_press(FakeMouse(100, self.pet.base_y))
        self.pet.on_release(FakeMouse(100, self.pet.base_y))
        self.assertIn("heart", self._kinds())

    def test_dragging_does_not_pop_a_heart(self):
        self.pet.on_press(FakeMouse(100, self.pet.base_y))
        self.pet.on_drag(FakeMouse(300, self.pet.base_y - 100))
        self.pet.on_release(FakeMouse(300, self.pet.base_y - 100))
        self.assertNotIn("heart", self._kinds())

    def test_landing_kicks_up_dust(self):
        self.pet.on_press(FakeMouse(100, self.pet.base_y))
        self.pet.on_release(FakeMouse(100, self.pet.base_y))     # 폴짝
        seen = False
        for _ in range(30):
            self.pet.tick()
            if "dust" in self._kinds():
                seen = True
                break
        self.assertTrue(seen, "착지했는데 먼지가 일지 않는다")

    def test_gentle_landing_makes_no_dust(self):
        # 아주 살짝 떠 있다가 내려오는 정도로는 먼지가 일지 않아야 한다.
        self.pet.lift = 1.0
        self.pet.vertical_speed = 0.0
        for _ in range(10):
            self.pet.tick()
        self.assertNotIn("dust", self._kinds())

    def test_nap_sends_up_zzz(self):
        self.pet.set_state("idle")
        self.pet.napping = True
        self.pet.state_left = 6.0
        self.pet.zzz_timer = 0.05
        for _ in range(10):
            self.pet.tick()
        self.assertIn("zzz", self._kinds())

    def test_effects_fade_away(self):
        self.pet.spawn_dust()
        self.pet.spawn_emote("heart")
        self.assertGreater(len(self.pet.effects), 0)
        for _ in range(60):          # 2.4초면 전부 사라진다
            self.pet.tick()
        self.assertEqual(self.pet.effects, [])

    def test_window_has_room_for_effects(self):
        pet = self.pet
        self.assertGreater(pet.window_width, pet.width)
        self.assertGreater(pet.window_height, pet.height + pet.hop)
        # 발끝은 여전히 바닥에 닿아 있어야 한다
        self.assertEqual(pet.base_y + pet.window_height, self.app.ground_y)


@needs_display
class PoseTest(unittest.TestCase):
    """같은 도트에서 만들어 낸 자세들(눌림·늘어남·눈 감기)."""

    def setUp(self):
        self.app = pt.App(pt.parse_args(["-p", "pikachu"]))
        self.pet = self.app.pets[0]

    def tearDown(self):
        self.app.quit()

    def test_every_pokemon_can_blink(self):
        for pokemon in sprites.POKEMON.values():
            self.assertIn("blink", pokemon.poses(), pokemon.key)

    def test_walkers_have_squash_and_stretch(self):
        for pokemon in sprites.POKEMON.values():
            if pokemon.move == "walk":
                self.assertIn("squash", pokemon.poses(), pokemon.key)
                self.assertIn("stretch", pokemon.poses(), pokemon.key)

    def test_poses_match_the_frame_size(self):
        for pokemon in sprites.POKEMON.values():
            frame = pokemon.frames()[0]
            for name, pose in pokemon.poses().items():
                self.assertEqual(len(pose), len(frame), "%s/%s" % (pokemon.key, name))
                self.assertEqual(len(pose[0]), len(frame[0]), "%s/%s" % (pokemon.key, name))

    def test_in_the_air_it_stretches(self):
        self.pet.lift = 20.0
        self.assertEqual(self.pet.choose_pose(), "stretch")

    def test_landing_squashes(self):
        self.pet.lift = 0.0
        self.pet.land_squash = pt.LAND_SQUASH_TIME
        self.assertEqual(self.pet.choose_pose(), "squash")

    def test_it_blinks_now_and_then(self):
        self.pet.blink_timer = 0.01
        seen = False
        for _ in range(20):
            self.pet.tick()
            if self.pet.choose_pose() == "blink":
                seen = True
                break
        self.assertTrue(seen, "눈을 깜빡이지 않는다")

    def test_it_does_not_blink_in_mid_air(self):
        self.pet.blink_timer = 0.01
        for _ in range(10):
            self.pet.lift = 30.0          # 계속 공중에 떠 있게 붙잡아 둔다
            self.pet.vertical_speed = 0.0
            self.pet.tick()
        self.assertEqual(self.pet.blinking, 0.0)

    def test_napping_breathes(self):
        self.pet.napping = True
        self.pet.breath = pt.BREATH_SEC * 1.5
        self.assertEqual(self.pet.choose_pose(), "squash")

    def test_held_pet_uses_the_plain_frame(self):
        self.pet.dragging = True
        self.pet.lift = 50.0
        self.assertIsNone(self.pet.choose_pose())

    def test_pose_images_are_built_for_both_directions(self):
        for side in ("pose_right", "pose_left"):
            self.assertIn("blink", self.pet.images[side])
            self.assertIn("stretch", self.pet.images[side])


@needs_display
class MenuTest(unittest.TestCase):
    """우클릭 메뉴로 하는 일들이 실제로 반영되고 저장되는지."""

    def setUp(self):
        self.path = os.path.join(_SETTINGS_DIR, "menu.txt")
        if os.path.exists(self.path):
            os.remove(self.path)
        self.app = pt.App(pt.parse_args(["--settings", self.path]))

    def tearDown(self):
        self.app.quit()

    def _saved(self):
        return settings_file.load(self.path, known_species=set(sprites.POKEMON))

    def test_adding_a_pokemon_is_remembered(self):
        self.app.add_pet_and_save("ditto")
        self.assertEqual(self._saved()["species"], ["pikachu", "ditto"])

    def test_removing_a_pokemon_is_remembered(self):
        self.app.add_pet_and_save("ditto")
        self.app.remove_pet(self.app.pets[0])
        self.assertEqual(self._saved()["species"], ["ditto"])

    def test_changing_size_rebuilds_and_saves(self):
        self.app.add_pet_and_save("ditto")
        before = [pet.width for pet in self.app.pets]
        self.app.set_scale(9.0)
        after = [pet.width for pet in self.app.pets]
        self.assertEqual(len(after), len(before))
        for old, new in zip(before, after):
            self.assertGreater(new, old)
        self.assertEqual(self._saved()["scale"], 9.0)
        self.assertEqual(self._saved()["species"], ["pikachu", "ditto"])

    def test_changing_speed_applies_to_every_pet(self):
        self.app.add_pet_and_save("charmander")
        self.app.set_speed(95.0)
        self.assertEqual(self._saved()["speed"], 95.0)
        for pet in self.app.pets:
            self.assertGreater(pet.speed, 55.0)

    def test_pause_stops_the_pets(self):
        pet = self.app.pets[0]
        pet.state = "walk"
        self.app.pause_var.set(True)
        self.app.toggle_pause()
        self.assertTrue(self.app.paused)
        start = pet.x
        for _ in range(40):
            pet.tick()
        self.assertEqual(pet.x, start)
        self.app.pause_var.set(False)
        self.app.toggle_pause()
        with mock.patch("pokemon_taskbar.random.random", return_value=1.0):
            for _ in range(40):
                pet.tick()
        self.assertNotAlmostEqual(pet.x, start, places=1)

    def _add_menu_labels(self, pet):
        labels = []
        submenu = pet.menu.nametowidget(pet.menu.entrycget(0, "menu"))
        for index in range(submenu.index("end") + 1):
            if submenu.type(index) == "command":
                labels.append(submenu.entrycget(index, "label"))
        return labels

    def test_menu_has_every_starting_pokemon(self):
        labels = self._add_menu_labels(self.app.pets[0])
        for key in sprites.base_species():
            self.assertIn(sprites.POKEMON[key].name_ko, labels)

    def test_menu_hides_evolved_pokemon(self):
        """진화체는 진화로만 만날 수 있어야 한다."""
        labels = self._add_menu_labels(self.app.pets[0])
        for key in sprites.EVOLVED_ONLY:
            self.assertNotIn(sprites.POKEMON[key].name_ko, labels)

    def test_autostart_is_safe_off_windows(self):
        # 윈도우가 아니면 등록되지 않고, 예외도 나지 않아야 한다.
        if pt.platform.system() != "Windows":
            self.assertFalse(pt.autostart_enabled())
            self.assertFalse(pt.set_autostart(True))
            self.app.autostart_var.set(True)
            self.app.toggle_autostart()
            self.assertFalse(self.app.autostart_var.get())


@needs_display
class LifecycleTest(unittest.TestCase):
    """종료 경로에서 예외나 오류 출력이 없어야 한다."""

    def setUp(self):
        self.app = pt.App(pt.parse_args(["--count", "2"]))

    def tearDown(self):
        self.app.quit()

    def test_quit_can_be_called_twice(self):
        self.app.quit()
        self.app.quit()  # 메뉴에서 두 번 눌러도 예외가 없어야 한다

    def test_destroy_cancels_the_pending_timer(self):
        pet = self.app.pets[0]
        self.assertIsNotNone(pet.after_id)
        self.app.remove_pet(pet)
        self.assertIsNone(pet.after_id)
        self.assertEqual(pet.state, "gone")

    def test_quit_destroys_every_pet(self):
        pets = list(self.app.pets)
        self.app.quit()
        self.assertEqual(self.app.pets, [])
        for pet in pets:
            self.assertIsNone(pet.after_id)

    def test_tick_after_destroy_does_nothing(self):
        pet = self.app.pets[0]
        self.app.remove_pet(pet)
        pet.tick()  # 이미 예약돼 있던 콜백이 뒤늦게 불려도 조용히 끝나야 한다
        self.assertIsNone(pet.after_id)


class GeneratedCSharpTest(unittest.TestCase):
    """C# 판 도트 데이터가 sprites.py 와 어긋나지 않았는지 확인한다."""

    def test_sprites_cs_is_up_to_date(self):
        root = os.path.dirname(os.path.abspath(__file__))
        spec = importlib.util.spec_from_file_location(
            "gen_sprites_cs", os.path.join(root, "tools", "gen_sprites_cs.py")
        )
        generator = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(generator)

        with open(os.path.join(root, "csharp", "Sprites.cs"), encoding="utf-8-sig") as handle:
            current = handle.read()

        self.assertEqual(
            current.replace("\r\n", "\n"),
            generator.build(),
            "csharp/Sprites.cs 가 오래됐습니다. python tools/gen_sprites_cs.py 를 실행하세요.",
        )


if __name__ == "__main__":
    unittest.main()
