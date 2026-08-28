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
        public double ScaleFactor;
        public bool FacesRight;   // 원본 그림이 오른쪽을 보고 있는지
        public bool Hops;         // 걷지 않고 폴짝 뛰어 다니는지
        public bool Floats;       // 바닥을 딛지 않고 공중에 떠다니는지
        public string EvolvesTo;  // 진화하면 무엇이 되는지(키). 진화 안 하면 null
        public bool Bounce;       // 걸을 때 프로그램이 살짝 띄워 줄지
        public int IdleMs;        // 대기 애니메이션이 원본에서 한 바퀴 돌던 시간(ms)
        public Dictionary<char, string> Palette;
        public string[][] Frames;   // 걷기 프레임마다 도트 줄 묶음
        public Dictionary<string, string[]> Poses;  // 상황별 자세 (squash/stretch/blink)
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

        /// <summary>진화해야 만날 수 있는 포켓몬인지. 메뉴에는 넣지 않는다.</summary>
        public static bool IsEvolvedOnly(string key)
        {
            foreach (PokemonSprite sprite in All)
            {
                if (sprite.EvolvesTo == key)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>처음부터 고를 수 있는 포켓몬들(진화체 제외).</summary>
        public static List<PokemonSprite> BaseSpecies()
        {
            List<PokemonSprite> list = new List<PokemonSprite>();
            foreach (PokemonSprite sprite in All)
            {
                if (!IsEvolvedOnly(sprite.Key))
                {
                    list.Add(sprite);
                }
            }
            return list;
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
        out.append("                ScaleFactor = %r,\n" % float(pokemon.scale_factor))
        out.append("                FacesRight = %s,\n"
                   % ("true" if pokemon.facing == "right" else "false"))
        out.append("                Hops = %s,\n" % ("true" if pokemon.move == "hop" else "false"))
        out.append("                Floats = %s,\n" % ("true" if pokemon.move == "float" else "false"))
        out.append("                EvolvesTo = %s,\n" % (
            ('"%s"' % pokemon.evolves_to) if pokemon.evolves_to else "null"))
        out.append("                Bounce = %s,\n" % ("true" if pokemon.bounce else "false"))
        out.append("                IdleMs = %d,\n" % int(getattr(pokemon, "idle_ms", 0)))
        out.append("                Palette = new Dictionary<char, string>\n                {\n")
        for char, color in pokemon.palette.items():
            out.append("                    { '%s', %s },\n" % (char, cs_string(color)))
        out.append("                },\n")
        out.append("                Frames = new string[][]\n                {\n")
        for frame in pokemon.frame_rows:
            out.append("                    new string[]\n                    {\n")
            for row in frame:
                out.append("                        %s,\n" % cs_string(row))
            out.append("                    },\n")
        out.append("                },\n")
        out.append("                Poses = new Dictionary<string, string[]>\n                {\n")
        for pose_name in sorted(pokemon.pose_rows):
            out.append("                    { %s, new string[]\n                        {\n"
                       % cs_string(pose_name))
            for row in pokemon.pose_rows[pose_name]:
                out.append("                            %s,\n" % cs_string(row))
            out.append("                        }\n                    },\n")
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
