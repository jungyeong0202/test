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
