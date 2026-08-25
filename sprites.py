# -*- coding: utf-8 -*-
"""도트(픽셀) 스프라이트 데이터.

외부 이미지 파일이나 네트워크 없이, 문자 그리드로 포켓몬 도트를 정의한다.
각 문자는 팔레트의 색(#RRGGBB)에 대응하고 '.' 은 투명 픽셀이다.

이 모듈은 tkinter에 의존하지 않으므로 GUI 없이도 검증/테스트가 가능하다.
"""

from __future__ import annotations


class Pokemon:
    """한 마리의 도트 그림과 걷기 애니메이션 정보.

    두 가지 방식으로 정의할 수 있다.

    * rows + step_rows : 손으로 그린 스프라이트. 기본 자세와, 두 번째 프레임에서
      바꿔 끼울 줄만 적는다.
    * frame_rows       : 프레임마다 전체 줄을 적는다. tools/import_sprite.py 가
      이미지에서 만들어 내는 형식이다.
    """

    def __init__(self, key, name_ko, palette, rows=None, step_rows=None,
                 frame_rows=None, scale_factor=1.0):
        self.key = key
        self.name_ko = name_ko
        self.palette = palette
        # --scale 에 곱해질 배율. 도트가 촘촘한 그림일수록 작게 잡는다.
        self.scale_factor = scale_factor

        if frame_rows:
            self.frame_rows = [list(frame) for frame in frame_rows]
            self.rows = self.frame_rows[0]
            self.step_rows = {}
        else:
            if not rows:
                raise ValueError("%s: rows 또는 frame_rows 가 필요합니다" % key)
            self.rows = list(rows)
            self.step_rows = step_rows or {}
            stepped = list(self.rows)
            for index, replacement in self.step_rows.items():
                stepped[index] = replacement
            self.frame_rows = [self.rows, stepped]

    # --- 그리드 만들기 -------------------------------------------------
    def width(self):
        return max(len(row) for frame in self.frame_rows for row in frame)

    def _grid(self, rows, width):
        grid = []
        for row in rows:
            row = row.ljust(width, ".")
            grid.append([None if ch == "." else self.palette[ch] for ch in row])
        return grid

    def frames(self):
        """걷기 프레임(색상 그리드) 목록을 돌려준다."""
        width = self.width()
        return [self._grid(frame, width) for frame in self.frame_rows]

    # --- 검증 -----------------------------------------------------------
    def validate(self):
        heights = {len(frame) for frame in self.frame_rows}
        if len(heights) != 1:
            raise ValueError("%s: 프레임마다 줄 수가 다릅니다" % self.key)
        for number, frame in enumerate(self.frame_rows):
            for index, row in enumerate(frame):
                for ch in row:
                    if ch != "." and ch not in self.palette:
                        raise ValueError(
                            "%s: %d번 프레임 %d행에 팔레트에 없는 문자 %r 이 있습니다"
                            % (self.key, number, index, ch)
                        )
        if self.scale_factor <= 0:
            raise ValueError("%s: scale_factor 는 0보다 커야 합니다" % self.key)


# --- 자동 생성 시작: pikachu ---
PIKACHU = Pokemon(
    key="pikachu",
    name_ko="피카츄",
    scale_factor=1 / 3,
    palette={
        "K": "#f5e452",
        "Y": "#f2bc24",
        "O": "#050102",
        "W": "#975408",
        "R": "#db9407",
        "B": "#bc7a3c",
        "G": "#60320b",
        "T": "#2c2927",
        "D": "#5b5753",
        "E": "#bcb6b2",
        "C": "#c02417",
        "S": "#fbf6a6",
    },
    frame_rows=[
        [
            ".................DD.......................DDD",
            ".................TTEE...................EETTT",
            ".................TDDTD.................DDDDTT",
            ".................TDDDTB...............DBDDTTE",
            ".................TTDKBW..............BYKDTTT",
            ".................ETBKKRB............BRKKKTTE",
            "..................TYKKKW............WKKKKTT",
            "..................GYKKKRB..........BYKKKYTE",
            ".....BB...........EWKKKYW.BBBBBB...WKKKYGE",
            "....BYYB...........GYKKWWYKKKKYYYBBYKKKWE",
            "...BYKKYB..........EWYBKKKSSSSSKYYRKKKWE",
            "...WKKKKYYS.........EWYKSSSSSKKKKKKKKWE",
            "..WKKKKKKKR.........WROYKKKKKKYOOYKKWG",
            ".BYKKKKKKKKY........WTETKKKKKKDEOGKKRG",
            ".WKKKKKKKKKKW......WKTOTKKKKKKOODTKKYW",
            "BYKKKKKKKKKKYB.....GKBTBKWYKKKBTTBKYYRD",
            "WKKKKKKKKKKKKW.....TEKKKKKKKKKKKKKBCYYO",
            "TKKKKKKKKKKKYRB....TBKKYBDDBBKKKKBBCCYO",
            ".OOKKKKKKKKKYYYW...TBKKKYTCCYKKKKBBCCYO",
            "...OOKKKKKKYYYYYW...TKKKKCBCKKKKKBCCCYO",
            ".....OOKKKYYYYYYO...TKKKKKBYKKKKKYCCRYG",
            ".......OOYYYYYYO.....TKKKKKKKKKKYYYYYYRG",
            ".........OOYYYGO.....GKKKKKKKKYYYYYYYYYO",
            "...........WYYG......WKKYYYYYYYYYYYYYYYO",
            "..........GYYO......WKKKKYYYYYYYYYYRKYYO",
            "..........GYYRO.....GKKKKKKRYYYYYYRKKKYO",
            ".........GYYYYRO....GKKKKKKKRYYYYYWKKKYO",
            ".........OGRYRRRG..GKKYKKKKKWYYYYWKKKYRO",
            "...........GGRRRG..GKKRYKKKKYOYYWYKKYYWO",
            ".............ORG..WKKKKWYKKKKSGYWWYYYGYW",
            ".............OWO.GKKKKKKWWYKKOYYYOYOOYYRW",
            "............OWWWGWKKKKKKKKWGOYYYYOOYYYYYO",
            "............OWWWGKKKKKKKKKKKYYYYYYYYYYYYO",
            ".............OOWOKKKKKKKKKYYYYYYYYYYYYYYO",
            "...............OOKKKKKKKKKYYYYYYYYYYYYYOO",
            "................GYKKKKKKKYYYYYYYYYYYYYYO",
            ".................GYKKKKYYYYRRRRRYYYYYYO",
            ".................ORYYYYYYYRRRRRRRRYYRO",
            "..................ORYYYYYGOOOGWWWRRWOG",
            "...................OWRRWWG.....ORRRYYYG",
            "..................GYKKYRG.......OOGRYWYG",
            ".................GKKKYOO...........GOOO",
            ".................ORWROO",
            "..................OOO",
        ],
        [
            ".................DD.......................DDD",
            ".................TTEE...................EETTT",
            ".................TDDTD.................DDDDTT",
            ".................TDDDTB...............DBDDTTE",
            ".................TTDKBW..............BYKDTTT",
            ".................ETBKKRB............BRKKKTTE",
            "..................TYKKKW............WKKKKTT",
            "..................GYKKKRB..........BYKKKYTE",
            ".....BB...........EWKKKYW.BBBBBB...WKKKYGE",
            "....BYYB...........GYKKWWYKKKKYYYBBYKKKWE",
            "...BYKKYB..........EWYBKKKSSSSSKYYRKKKWE",
            "...WKKKKYYS.........EWYKSSSSSKKKKKKKKWE",
            "..WKKKKKKKR.........WROYKKKKKKYOOYKKWG",
            ".BYKKKKKKKKY........WTETKKKKKKDEOGKKRG",
            ".WKKKKKKKKKKW......WKTOTKKKKKKOODTKKYW",
            "BYKKKKKKKKKKYB.....GKBTBKWYKKKBTTBKYYRD",
            "WKKKKKKKKKKKKW.....TEKKKKKKKKKKKKKBCYYO",
            "TKKKKKKKKKKKYRB....TBKKYBDDBBKKKKBBCCYO",
            ".OOKKKKKKKKKYYYW...TBKKKYTCCYKKKKBBCCYO",
            "...OOKKKKKKYYYYYW...TKKKKCBCKKKKKBCCCYO",
            ".....OOKKKYYYYYYO...TKKKKKBYKKKKKYCCRYG",
            ".......OOYYYYYYO.....TKKKKKKKKKKYYYYYYRG",
            ".........OOYYYGO.....GKKKKKKKKYYYYYYYYYO",
            "...........WYYG......WKKYYYYYYYYYYYYYYYO",
            "..........GYYO......WKKKKYYYYYYYYYYRKYYO",
            "..........GYYRO.....GKKKKKKRYYYYYYRKKKYO",
            ".........GYYYYRO....GKKKKKKKRYYYYYWKKKYO",
            ".........OGRYRRRG..GKKYKKKKKWYYYYWKKKYRO",
            "...........GGRRRG..GKKRYKKKKYOYYWYKKYYWO",
            ".............ORG..WKKKKWYKKKKSGYWWYYYGYW",
            ".............OWO.GKKKKKKWWYKKOYYYOYOOYYRW",
            "............OWWWGWKKKKKKKKWGOYYYYOOYYYYYO",
            "............OWWWGKKKKKKKKKKKYYYYYYYYYYYYO",
            ".............OOWOKKKKKKKKKYYYYYYYYYYYYYYO",
            "...............OOKKKKKKKKKYYYYYYYYYYYYYOO",
            "................GYKKKKKKKYYYYYYYYYYYYYYO",
            ".................GYKKKKYYYYRRRRRYYYYYYO",
            ".................OROWRRWWGRRRRRRRRYYRO",
            "..................GYKKYRGGOOOGWWWRRWOG",
            ".................GKKKYOO.......ORRRYYYG",
            ".................ORWROO.........OOGRYWYG",
            "..................OOO..............GOOO",
            ".",
            ".",
        ],
        [
            ".................DD.......................DDD",
            ".................TTEE...................EETTT",
            ".................TDDTD.................DDDDTT",
            ".................TDDDTB...............DBDDTTE",
            ".................TTDKBW..............BYKDTTT",
            ".................ETBKKRB............BRKKKTTE",
            "..................TYKKKW............WKKKKTT",
            "..................GYKKKRB..........BYKKKYTE",
            ".....BB...........EWKKKYW.BBBBBB...WKKKYGE",
            "....BYYB...........GYKKWWYKKKKYYYBBYKKKWE",
            "...BYKKYB..........EWYBKKKSSSSSKYYRKKKWE",
            "...WKKKKYYS.........EWYKSSSSSKKKKKKKKWE",
            "..WKKKKKKKR.........WROYKKKKKKYOOYKKWG",
            ".BYKKKKKKKKY........WTETKKKKKKDEOGKKRG",
            ".WKKKKKKKKKKW......WKTOTKKKKKKOODTKKYW",
            "BYKKKKKKKKKKYB.....GKBTBKWYKKKBTTBKYYRD",
            "WKKKKKKKKKKKKW.....TEKKKKKKKKKKKKKBCYYO",
            "TKKKKKKKKKKKYRB....TBKKYBDDBBKKKKBBCCYO",
            ".OOKKKKKKKKKYYYW...TBKKKYTCCYKKKKBBCCYO",
            "...OOKKKKKKYYYYYW...TKKKKCBCKKKKKBCCCYO",
            ".....OOKKKYYYYYYO...TKKKKKBYKKKKKYCCRYG",
            ".......OOYYYYYYO.....TKKKKKKKKKKYYYYYYRG",
            ".........OOYYYGO.....GKKKKKKKKYYYYYYYYYO",
            "...........WYYG......WKKYYYYYYYYYYYYYYYO",
            "..........GYYO......WKKKKYYYYYYYYYYRKYYO",
            "..........GYYRO.....GKKKKKKRYYYYYYRKKKYO",
            ".........GYYYYRO....GKKKKKKKRYYYYYWKKKYO",
            ".........OGRYRRRG..GKKYKKKKKWYYYYWKKKYRO",
            "...........GGRRRG..GKKRYKKKKYOYYWYKKYYWO",
            ".............ORG..WKKKKWYKKKKSGYWWYYYGYW",
            ".............OWO.GKKKKKKWWYKKOYYYOYOOYYRW",
            "............OWWWGWKKKKKKKKWGOYYYYOOYYYYYO",
            "............OWWWGKKKKKKKKKKKYYYYYYYYYYYYO",
            ".............OOWOKKKKKKKKKYYYYYYYYYYYYYYO",
            "...............OOKKKKKKKKKYYYYYYYYYYYYYOO",
            "................GYKKKKKKKYYYYYYYYYYYYYYO",
            ".................GYKKKKYYYYRRRRRYYYYYYO",
            ".................ORYYYYYYYRRRRRRRRYYRO",
            "..................ORYYYYYGOOOGWWWRRWOG",
            "...................OWRRWWG.....ORRRYYYG",
            "..................GYKKYRG.......OOGRYWYG",
            ".................GKKKYOO...........GOOO",
            ".................ORWROO",
            "..................OOO",
        ],
        [
            ".................DD.......................DDD",
            ".................TTEE...................EETTT",
            ".................TDDTD.................DDDDTT",
            ".................TDDDTB...............DBDDTTE",
            ".................TTDKBW..............BYKDTTT",
            ".................ETBKKRB............BRKKKTTE",
            "..................TYKKKW............WKKKKTT",
            "..................GYKKKRB..........BYKKKYTE",
            ".....BB...........EWKKKYW.BBBBBB...WKKKYGE",
            "....BYYB...........GYKKWWYKKKKYYYBBYKKKWE",
            "...BYKKYB..........EWYBKKKSSSSSKYYRKKKWE",
            "...WKKKKYYS.........EWYKSSSSSKKKKKKKKWE",
            "..WKKKKKKKR.........WROYKKKKKKYOOYKKWG",
            ".BYKKKKKKKKY........WTETKKKKKKDEOGKKRG",
            ".WKKKKKKKKKKW......WKTOTKKKKKKOODTKKYW",
            "BYKKKKKKKKKKYB.....GKBTBKWYKKKBTTBKYYRD",
            "WKKKKKKKKKKKKW.....TEKKKKKKKKKKKKKBCYYO",
            "TKKKKKKKKKKKYRB....TBKKYBDDBBKKKKBBCCYO",
            ".OOKKKKKKKKKYYYW...TBKKKYTCCYKKKKBBCCYO",
            "...OOKKKKKKYYYYYW...TKKKKCBCKKKKKBCCCYO",
            ".....OOKKKYYYYYYO...TKKKKKBYKKKKKYCCRYG",
            ".......OOYYYYYYO.....TKKKKKKKKKKYYYYYYRG",
            ".........OOYYYGO.....GKKKKKKKKYYYYYYYYYO",
            "...........WYYG......WKKYYYYYYYYYYYYYYYO",
            "..........GYYO......WKKKKYYYYYYYYYYRKYYO",
            "..........GYYRO.....GKKKKKKRYYYYYYRKKKYO",
            ".........GYYYYRO....GKKKKKKKRYYYYYWKKKYO",
            ".........OGRYRRRG..GKKYKKKKKWYYYYWKKKYRO",
            "...........GGRRRG..GKKRYKKKKYOYYWYKKYYWO",
            ".............ORG..WKKKKWYKKKKSGYWWYYYGYW",
            ".............OWO.GKKKKKKWWYKKOYYYOYOOYYRW",
            "............OWWWGWKKKKKKKKWGOYYYYOOYYYYYO",
            "............OWWWGKKKKKKKKKKKYYYYYYYYYYYYO",
            ".............OOWOKKKKKKKKKYYYYYYYYYYYYYYO",
            "...............OOKKKKKKKKKYYYYYYYYYYYYYOO",
            "................GYKKKKKKKYYYYYYYYYYYYYYO",
            ".................GYKKKKYYYYRRRRRYYYYYYO",
            ".................ORYYYYYYYRRRRRORRRYYYG",
            "..................ORYYYYYGOOOGWWOOGRYWYG",
            "...................OWRRWWG.........GOOO",
            "..................GYKKYRG",
            ".................GKKKYOO",
            ".................ORWROO",
            "..................OOO",
        ],
    ],
)
# --- 자동 생성 끝: pikachu ---

CHARMANDER = Pokemon(
    key="charmander",
    name_ko="파이리",
    palette={
        "K": "#2b2b2b",
        "O": "#f0803c",  # 주황 몸
        "S": "#c85a1e",  # 그늘
        "C": "#f8dcae",  # 배
        "W": "#ffffff",
        "R": "#e8482c",  # 불꽃 바깥
        "Y": "#ffd24a",  # 불꽃 안쪽
    },
    rows=[
        ".......KKKKKK",
        "......KOOOOOOK",
        ".....KOOOOOOOOK",
        ".....KOKWKOOKWKOK",
        ".....KOKKKOOKKKOK",
        ".....KOOOOOOOOOOK...KRK",
        "......KOOSSSSOOK..KRYRK",
        "......KKOOOOOOKK..KRYYRK",
        ".....KOOOOOOOOOOK.KRRYRK",
        "....KOOCCCCCCCCOK..KRRK",
        "....KOCCCCCCCCCOKKKSOK",
        "....KOCCCCCCCCCOKKSOK",
        "....KOCCCCCCCCCOK",
        ".....KOCCCCCCCOK",
        ".....KOOKKKKOOK",
        "....KKKK..KKKK",
    ],
    step_rows={
        14: "....KOOKKKKOOK",
        15: "...KKKK....KKKK",
    },
)


BULBASAUR = Pokemon(
    key="bulbasaur",
    name_ko="이상해씨",
    palette={
        "K": "#2b2b2b",
        "T": "#7ec8a4",  # 청록 몸
        "D": "#4e9e7c",  # 무늬 / 그늘
        "G": "#6dbe45",  # 씨앗(구근)
        "E": "#4a9130",  # 구근 그늘
        "W": "#ffffff",
    },
    rows=[
        ".......KKGGGGKK",
        "......KGGEGGEGGK",
        ".....KGGGGEGGGGGK",
        ".....KGGEGGGGEGGK",
        "....KKKGGGGGGGKKK",
        "...KTTTKKKKKKKTTTK",
        "..KTTDTTTTTTTTTDTTK",
        "..KTTTTTTTTTTTTTTTK",
        "..KTKWKTTTTTTKWKTTK",
        "..KTKKKTTTTTTKKKTTK",
        "..KTTTTTTKKTTTTTTTK",
        "..KTTDTTTTTTTTTDTTK",
        "...KTTTTTTTTTTTTTK",
        "...KTTTTTTTTTTTTTK",
        "...KKTTKKKKKKTTKK",
        "....KKKK....KKKK",
    ],
    step_rows={
        14: "..KKTTKKKKKKTTKK",
        15: "..KKKK......KKKK",
    },
)


SQUIRTLE = Pokemon(
    key="squirtle",
    name_ko="꼬부기",
    palette={
        "K": "#2b2b2b",
        "B": "#78c8f0",  # 하늘색 몸
        "D": "#3878a8",  # 그늘
        "S": "#c87838",  # 등껍질
        "L": "#f0c078",  # 등껍질 무늬
        "C": "#f8e8c0",  # 배
        "W": "#ffffff",
    },
    rows=[
        "......KKKKKK",
        ".....KBBBBBBK",
        "....KBBBBBBBBK",
        "....KBKWKBBKWKBK",
        "....KBKKKBBKKKBK",
        "....KBBBBBBBBBBK",
        ".....KBBDDDDBBK",
        "....KKKKKKKKKKKK",
        "...KSSLLSSSSLLSSK",
        "..KSLLSSSSSSSSLLSK",
        "..KSSSSCCCCCCSSSSK",
        "..KSLLSCCCCCCSLLSK",
        "..KSSSSCCCCCCSSSSK",
        "...KSSSSSSSSSSSSK",
        "....KKBBKKKKBBKK",
        ".....KKKK..KKKK",
    ],
    step_rows={
        14: "...KKBBKKKKBBKK",
        15: "...KKKK....KKKK",
    },
)


POKEMON = {p.key: p for p in (PIKACHU, CHARMANDER, BULBASAUR, SQUIRTLE)}


def validate_all():
    """모든 스프라이트 데이터가 정상인지 확인한다."""
    for pokemon in POKEMON.values():
        pokemon.validate()
        frames = pokemon.frames()
        widths = {len(row) for frame in frames for row in frame}
        heights = {len(frame) for frame in frames}
        if len(widths) != 1 or len(heights) != 1:
            raise ValueError("%s: 프레임 크기가 서로 다릅니다" % pokemon.key)
        if len(frames) < 2:
            raise ValueError("%s: 걷기 프레임이 2장 이상이어야 합니다" % pokemon.key)
    return True


if __name__ == "__main__":
    validate_all()
    for pokemon in POKEMON.values():
        frame = pokemon.frames()[0]
        print("%s (%s) %dx%d, %d프레임, x%.2f" % (
            pokemon.name_ko, pokemon.key, len(frame[0]), len(frame),
            len(pokemon.frames()), pokemon.scale_factor,
        ))
