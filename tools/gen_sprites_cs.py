#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""sprites.py 의 도트 데이터를 C# 소스(csharp/Sprites.cs)로 변환한다.

도트 그림의 원본은 언제나 sprites.py 하나뿐이고, C# 판은 여기서 생성한다.

    python tools/gen_sprites_cs.py
"""

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

from sprites import POKEMON  # noqa: E402

HEADER = """// 이 파일은 자동 생성됩니다. 직접 고치지 말고 sprites.py 를 고친 뒤
//     python tools/gen_sprites_cs.py
// 를 실행하세요.
using System.Collections.Generic;

namespace PokemonTaskbar
{
    public class PokemonSprite
    {
        public string Key;
        public string NameKo;
        public Dictionary<char, string> Palette;
        public string[] Rows;
        public Dictionary<int, string> StepRows;
    }

    public static class Sprites
    {
        public static readonly List<PokemonSprite> All = new List<PokemonSprite>
        {
"""

FOOTER = """        };

        public static PokemonSprite Find(string key)
        {
            foreach (PokemonSprite sprite in All)
            {
                if (sprite.Key == key)
                {
                    return sprite;
                }
            }
            return null;
        }
    }
}
"""


def cs_string(text):
    return '"%s"' % text.replace("\\", "\\\\").replace('"', '\\"')


def build():
    out = [HEADER]
    for pokemon in POKEMON.values():
        out.append("            new PokemonSprite\n            {\n")
        out.append("                Key = %s,\n" % cs_string(pokemon.key))
        out.append("                NameKo = %s,\n" % cs_string(pokemon.name_ko))
        out.append("                Palette = new Dictionary<char, string>\n                {\n")
        for char, color in pokemon.palette.items():
            out.append("                    { '%s', %s },\n" % (char, cs_string(color)))
        out.append("                },\n")
        out.append("                Rows = new string[]\n                {\n")
        for row in pokemon.rows:
            out.append("                    %s,\n" % cs_string(row))
        out.append("                },\n")
        out.append("                StepRows = new Dictionary<int, string>\n                {\n")
        for index, row in sorted(pokemon.step_rows.items()):
            out.append("                    { %d, %s },\n" % (index, cs_string(row)))
        out.append("                },\n")
        out.append("            },\n")
    out.append(FOOTER)
    return "".join(out)


def main():
    target = os.path.join(
        os.path.dirname(os.path.abspath(__file__)), "..", "csharp", "Sprites.cs"
    )
    target = os.path.normpath(target)
    # C# 컴파일러가 한글을 제대로 읽도록 UTF-8 BOM 을 붙인다.
    with open(target, "w", encoding="utf-8-sig", newline="\r\n") as handle:
        handle.write(build())
    print("생성 완료: %s" % target)


if __name__ == "__main__":
    main()
