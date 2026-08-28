# -*- coding: utf-8 -*-
"""도트 데이터와 도구를 검사한다.

프로그램 자체(C# 판)는 `csharp/Tests.cs` 가 검사한다. 여기 남은 것은 파이썬으로
만드는 것들이다.

    python3 -m unittest test_tools -v
"""

import importlib.util
import os
import subprocess
import sys
import unittest

import sprites


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


class SpriteQualityTest(unittest.TestCase):
    """손으로 두 번 겪은 두 가지 실수를 못박는다.

    * 부위 상자가 다리 중간을 가로지르면, 윗다리만 올라가고 발끝은 바닥에
      남는다. 구멍도 조각도 생기지 않아 다른 검사로는 안 잡힌다.
    * 배경(흰색)이 그림 안으로 새어 들어오면 테두리에 흰 칸이 붙는다.
      눈동자 흰자처럼 원래 흰 부분은 그림 안쪽에 있다.
    """

    @staticmethod
    def _floor(grid, x):
        for y in range(len(grid) - 1, -1, -1):
            if grid[y][x] is not None:
                return y
        return -1

    @staticmethod
    def _pieces(grid):
        from collections import deque

        height = len(grid)
        width = len(grid[0])
        seen = [[False] * width for _ in range(height)]
        count = 0
        for sy in range(height):
            for sx in range(width):
                if grid[sy][sx] is None or seen[sy][sx]:
                    continue
                count += 1
                queue = deque([(sx, sy)])
                seen[sy][sx] = True
                while queue:
                    x, y = queue.popleft()
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1),
                                   (1, 1), (1, -1), (-1, 1), (-1, -1)):
                        nx, ny = x + dx, y + dy
                        if (0 <= nx < width and 0 <= ny < height
                                and not seen[ny][nx] and grid[ny][nx] is not None):
                            seen[ny][nx] = True
                            queue.append((nx, ny))
        return count

    def test_lifting_a_foot_brings_the_toes_along(self):
        for pokemon in sprites.POKEMON.values():
            if pokemon.move != "walk":
                continue          # 뛰기·떠다니기는 몸 전체가 변형된다
            frames = pokemon.frames()
            base = frames[0]
            width = len(base[0])
            for index in range(1, len(frames)):
                grid = frames[index]
                moved = stuck = 0
                for x in range(width):
                    if all(base[y][x] == grid[y][x] for y in range(len(base))):
                        continue
                    moved += 1
                    if (self._floor(base, x) >= 0
                            and self._floor(grid, x) == self._floor(base, x)):
                        stuck += 1
                if moved:
                    self.assertLessEqual(
                        stuck, moved // 2,
                        "%s 걷기%d: 바뀐 세로줄 %d개 중 %d개에서 발끝이 바닥에 "
                        "남았습니다 (상자가 다리를 가로지릅니다)"
                        % (pokemon.key, index, moved, stuck))

    @staticmethod
    def _feet(grid, reach=3):
        """바닥에 닿아 있는 세로줄을 이어 붙여 발을 가려낸다.

        아래 몇 줄을 통째로 보면 두 발이 하나로 뭉친다(다리가 위에서 이어지기
        때문이다). 세로줄마다 '맨 아래 칠해진 줄' 을 재서, 그것이 그림의 바닥
        가까이 있는 줄만 발로 본다. 발 사이의 빈틈은 바닥이 훨씬 위에 있거나
        아예 없으므로 저절로 갈린다. 재는 폭을 넉넉히 잡으면 안 된다 —
        피카츄는 두 발 사이 배가 바닥에서 서너 줄밖에 안 떨어져 있어서
        두 발이 하나로 뭉친다.
        """
        height = len(grid)
        width = len(grid[0])
        floors = []
        for x in range(width):
            floor = -1
            for y in range(height - 1, -1, -1):
                if grid[y][x] is not None:
                    floor = y
                    break
            floors.append(floor)
        bottom = max(floors)
        if bottom < 0:
            return []
        feet = []
        start = None
        for x in range(width + 1):
            grounded = x < width and floors[x] >= bottom - reach
            if grounded and start is None:
                start = x
            elif not grounded and start is not None:
                feet.append((start, x - 1))
                start = None
        return feet

    def test_every_foot_takes_a_step(self):
        """발이 둘인데 하나만 움직이거나, 발 하나를 세로로 갈라 반쪽만
        들어 올리면 걸음이 어색해진다. 라이츄에서 실제로 그랬다 — 상자 둘이
        왼발 하나를 반으로 가르고 오른발은 건드리지도 않았다."""
        for pokemon in sprites.POKEMON.values():
            if pokemon.move != "walk":
                continue
            frames = pokemon.frames()
            base = frames[0]
            height = len(base)
            grounded = [(left, right) for left, right in self._feet(base)
                        if right - left + 1 >= 3]
            stepped = 0
            for left, right in grounded:
                span = right - left + 1
                best = 0
                for index in range(1, len(frames)):
                    moved = sum(
                        1 for x in range(left, right + 1)
                        if any(base[y][x] != frames[index][y][x] for y in range(height)))
                    best = max(best, moved)
                if best == 0:
                    continue          # 바닥에 닿은 꼬리도 있다(리자몽). 안 움직여도 된다
                stepped += 1
                self.assertGreaterEqual(
                    best, span * 7 // 10,
                    "%s: x %d..%d 의 발이 %d칸 중 %d칸만 움직입니다 "
                    "(상자가 발을 세로로 가릅니다)"
                    % (pokemon.key, left, right, span, best))
            # 번갈아 디디려면 바닥에 닿은 덩어리가 둘 이상 움직여야 한다.
            # 라이츄는 상자 둘이 왼발 하나만 나눠 가져서 오른발이 내내
            # 붙어 있었다.
            #
            # 발이 하나로만 잡히는 경우(이상해꽃처럼 앞발이 뒷발보다 높이
            # 있는 그림)에는 판단할 수 없으므로 넘어간다. 잘못 잡느니
            # 안 잡는 편이 낫다.
            if len(grounded) >= 2:
                self.assertGreaterEqual(
                    stepped, 2,
                    "%s: 바닥에 닿은 덩어리 %d개 중 %d개만 움직입니다. 발 하나로는 "
                    "걷는 것으로 보이지 않습니다"
                    % (pokemon.key, len(grounded), stepped))

    def test_walking_only_moves_the_lower_body(self):
        for pokemon in sprites.POKEMON.values():
            if pokemon.move != "walk":
                continue
            frames = pokemon.frames()
            base = frames[0]
            height = len(base)
            width = len(base[0])
            for index in range(1, len(frames)):
                rows = [y for y in range(height) for x in range(width)
                        if base[y][x] != frames[index][y][x]]
                if not rows:
                    continue
                share = (height - 1 - min(rows)) * 100 // height
                self.assertLessEqual(
                    share, 40,
                    "%s 걷기%d: 아래에서 %d%% 지점까지 바뀝니다. 발이 아니라 "
                    "몸이 움직이고 있습니다" % (pokemon.key, index, share))

    def test_walking_does_not_tear_the_body(self):
        for pokemon in sprites.POKEMON.values():
            frames = pokemon.frames()
            base = self._pieces(frames[0])
            for index in range(1, len(frames)):
                self.assertLessEqual(
                    self._pieces(frames[index]), base,
                    "%s 프레임%d: 몸에서 조각이 떨어졌습니다"
                    % (pokemon.key, index))

    def test_background_did_not_leak_into_the_edge(self):
        for pokemon in sprites.POKEMON.values():
            white = [char for char, value in pokemon.palette.items()
                     if int(value[1:3], 16) > 235 and int(value[3:5], 16) > 235
                     and int(value[5:7], 16) > 235]
            if not white:
                continue
            rows = pokemon.frame_rows[0]
            width = max(len(row) for row in rows)
            grid = [row.ljust(width, ".") for row in rows]
            touching = 0
            for y in range(len(grid)):
                for x in range(width):
                    if grid[y][x] not in white:
                        continue
                    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        ny, nx = y + dy, x + dx
                        if (not (0 <= ny < len(grid) and 0 <= nx < width)
                                or grid[ny][nx] == "."):
                            touching += 1
                            break
            self.assertEqual(
                touching, 0,
                "%s: 흰 칸 %d개가 그림 테두리에 닿아 있습니다 (배경이 새어 "
                "들어왔을 수 있습니다)" % (pokemon.key, touching))


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


class AnimatedGifTest(unittest.TestCase):
    """움직이는 GIF 를 원본으로 받을 때.

    GIF 는 투명한 자리에 팔레트의 색이 그대로 남는다. 그 색이 검정일 때가
    많은데, 이 도구는 '거의 흰색' 만 배경으로 보므로 그냥 읽으면 배경이
    그림의 일부가 된다. 내용 상자가 캔버스 전체로 잡혀 도트 격자부터
    어긋난다.
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

    def _gif(self, path, transparent_color):
        """투명 배경을 가진 두 장짜리 GIF. 두 장의 그림 위치가 다르다."""
        from PIL import Image

        frames = []
        for shift in (0, 6):
            frame = Image.new("P", (60, 60), 0)
            frame.putpalette(list(transparent_color) + [30, 160, 90]
                             + [0] * (256 * 3 - 6))
            for y in range(20, 45):
                for x in range(15 + shift, 40 + shift):
                    frame.putpixel((x, y), 1)
            frames.append(frame)
        frames[0].save(path, save_all=True, append_images=frames[1:],
                       transparency=0, disposal=2, duration=200, loop=0)

    @staticmethod
    def _is_background(color):
        return color[0] > 235 and color[1] > 235 and color[2] > 235

    def test_transparent_background_becomes_white(self):
        import tempfile

        # 투명 자리가 검정이든 자홍이든 배경으로 읽혀야 한다.
        for transparent in ((0, 0, 0), (255, 0, 255)):
            with tempfile.NamedTemporaryFile(suffix=".gif") as handle:
                self._gif(handle.name, transparent)
                image = self.tool.load_frame(handle.name, 0)
                self.assertTrue(self._is_background(image.getpixel((0, 0))),
                                "투명한 자리가 배경으로 안 읽힌다: %r" % (transparent,))

    def test_content_box_finds_the_drawing_not_the_canvas(self):
        import tempfile

        with tempfile.NamedTemporaryFile(suffix=".gif") as handle:
            self._gif(handle.name, (0, 0, 0))
            image = self.tool.load_frame(handle.name, 0)
            box = self.tool.content_box(image, self._is_background)
            self.assertEqual(box, (15, 20, 39, 44))
            self.assertNotEqual(box, (0, 0, 59, 59), "캔버스 전체를 그림으로 봤다")

    def test_each_frame_can_be_read(self):
        import tempfile

        with tempfile.NamedTemporaryFile(suffix=".gif") as handle:
            self._gif(handle.name, (0, 0, 0))
            first = self.tool.content_box(self.tool.load_frame(handle.name, 0),
                                          self._is_background)
            second = self.tool.content_box(self.tool.load_frame(handle.name, 1),
                                           self._is_background)
            # 두 장의 그림 위치가 6 픽셀 다르다.
            self.assertEqual(second[0] - first[0], 6)

    def test_asking_for_a_frame_that_is_not_there_says_so(self):
        import tempfile

        with tempfile.NamedTemporaryFile(suffix=".gif") as handle:
            self._gif(handle.name, (0, 0, 0))
            with self.assertRaises(SystemExit):
                self.tool.load_frame(handle.name, 9)


class Net48CheckTest(unittest.TestCase):
    """만든 exe 가 .NET Framework 4.8 API 만 쓰는지 보는 검사기.

    이 검사기는 빌드를 막는 관문이므로, 멀쩡한 API 를 없다고 잘못 신고하면
    아무도 빌드할 수 없게 된다. 실제로 메서드 선언이 어디서 끝나는지 잘못 봐서
    (`cil managed noinlining` 처럼 뒤에 말이 더 붙는 경우) 뒤따르는 메서드들을
    통째로 놓친 적이 있다.
    """

    def setUp(self):
        if not os.path.isdir("/usr/lib/mono/4.8-api"):
            self.skipTest("4.8 참조 어셈블리가 없습니다")
        spec = importlib.util.spec_from_file_location(
            "check_net48", os.path.join(os.path.dirname(__file__),
                                        "tools", "check_net48.py")
        )
        self.tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.tool)
        self.defined = self.tool.methods_defined(
            self.tool.disassemble("/usr/lib/mono/4.8-api/mscorlib.dll")
        )

    def test_it_finds_plain_methods(self):
        self.assertIn(("System.String", "Split", ("char[]",)), self.defined)

    def test_it_finds_methods_after_a_decorated_one(self):
        """선언 끝을 못 알아보면 뒤따르는 메서드들이 통째로 사라진다."""
        self.assertIn(
            ("System.Runtime.InteropServices.Marshal", "Copy",
             ("uint8[]", "int32", "nativeint", "int32")),
            self.defined,
        )
        self.assertIn(("System.Decimal", ".ctor", ("int32",)), self.defined)

    def test_it_reads_a_useful_number_of_methods(self):
        # 파서가 조용히 망가지면 개수가 뚝 떨어진다.
        self.assertGreater(len(self.defined), 10000)

    def test_it_still_catches_a_mono_only_overload(self):
        """Mono 에만 있는 오버로드는 반드시 걸려야 한다."""
        everywhere = {(t, n, sig) for t, n, sig in self.defined}
        mono_only = ("System.String", "Split", ("char", "System.StringSplitOptions"))
        self.assertNotIn(mono_only, everywhere)




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

    def test_noto_sans_kr_is_packaged_for_both_versions(self):
        root = os.path.dirname(os.path.abspath(__file__))
        font_path = os.path.join(root, "assets", "fonts", "NotoSansKR-VF.ttf")
        license_path = os.path.join(root, "assets", "fonts", "OFL.txt")
        self.assertGreater(os.path.getsize(font_path), 1_000_000)
        with open(license_path, encoding="utf-8") as handle:
            self.assertIn("SIL OPEN FONT LICENSE Version 1.1", handle.read())

    def test_every_csharp_build_embeds_the_font_and_license(self):
        root = os.path.dirname(os.path.abspath(__file__))
        for path in (
                os.path.join(root, "tools", "build_exe.sh"),
                os.path.join(root, "csharp", "run.bat"),
                os.path.join(root, ".github", "workflows", "build-windows-exe.yml")):
            with open(path, encoding="utf-8") as handle:
                build = handle.read()
            self.assertIn("NotoSansKR-VF.ttf", build, path)
            self.assertIn("PokemonTaskbar.NotoSansKR.ttf", build, path)
            self.assertIn("PokemonTaskbar.NotoSansKR.OFL.txt", build, path)


if __name__ == "__main__":
    unittest.main()
