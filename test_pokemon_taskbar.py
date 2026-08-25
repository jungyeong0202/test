# -*- coding: utf-8 -*-
"""스프라이트 데이터와 앱 동작에 대한 기본 테스트.

GUI(디스플레이)가 없는 환경에서는 화면이 필요한 테스트만 건너뛴다.

    python -m unittest test_pokemon_taskbar -v
"""

import contextlib
import io
import importlib.util
import os
import time
import unittest
from unittest import mock

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


class SpriteTest(unittest.TestCase):
    def test_all_sprites_are_valid(self):
        self.assertTrue(sprites.validate_all())

    def test_frames_are_rectangular_and_match(self):
        for pokemon in sprites.POKEMON.values():
            frames = pokemon.frames()
            self.assertGreaterEqual(len(frames), 2, pokemon.key)
            self.assertEqual(len(frames) % 2, 0, "%s: 프레임은 짝수여야 걷기가 자연스럽다" % pokemon.key)
            width = len(frames[0][0])
            for frame in frames:
                self.assertEqual(len(frame), len(pokemon.frame_rows[0]), pokemon.key)
                for row in frame:
                    self.assertEqual(len(row), width, pokemon.key)

    def test_walk_frames_differ(self):
        for pokemon in sprites.POKEMON.values():
            frames = pokemon.frames()
            self.assertNotEqual(
                frames[0], frames[1], "%s: 걷기 프레임이 동일합니다" % pokemon.key
            )

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
        pet = self.app.pets[0]
        expected = self.app.ground_y - (pet.height + pet.hop)
        self.assertEqual(pet.base_y, expected)

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

    def test_idle_pet_starts_walking_again(self):
        pet = self.app.pets[0]
        pet.set_state("idle")
        pet.state_left = 0.05
        self._pump(0.3)
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
            for key in ("pikachu", "charmander", "squirtle", "bulbasaur"):
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
