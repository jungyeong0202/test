# -*- coding: utf-8 -*-
"""도트(픽셀) 스프라이트 데이터.

외부 이미지 파일이나 네트워크 없이, 문자 그리드로 포켓몬 도트를 정의한다.
각 문자는 팔레트의 색(#RRGGBB)에 대응하고 '.' 은 투명 픽셀이다.

이 모듈은 tkinter에 의존하지 않으므로 GUI 없이도 검증/테스트가 가능하다.
"""

from __future__ import annotations


class Pokemon:
    """한 마리의 도트 그림과 걷기 애니메이션 정보."""

    def __init__(self, key, name_ko, palette, rows, step_rows=None):
        self.key = key
        self.name_ko = name_ko
        self.palette = palette
        self.rows = rows
        # 걷기 2번째 프레임에서 교체할 줄: {행 번호: 새 문자열}
        self.step_rows = step_rows or {}

    # --- 그리드 만들기 -------------------------------------------------
    def _grid(self, rows):
        width = max(len(r) for r in rows)
        grid = []
        for row in rows:
            row = row.ljust(width, ".")
            grid.append([None if ch == "." else self.palette[ch] for ch in row])
        return grid

    def frames(self):
        """걷기 프레임(색상 그리드) 목록을 돌려준다."""
        first = self._grid(self.rows)
        second_rows = list(self.rows)
        for index, replacement in self.step_rows.items():
            second_rows[index] = replacement
        second = self._grid(second_rows)
        return [first, second]

    # --- 검증 -----------------------------------------------------------
    def validate(self):
        for index, row in enumerate(self.rows):
            for ch in row:
                if ch != "." and ch not in self.palette:
                    raise ValueError(
                        "%s: %d행에 팔레트에 없는 문자 %r 이 있습니다" % (self.key, index, ch)
                    )
        for index, row in self.step_rows.items():
            if not 0 <= index < len(self.rows):
                raise ValueError("%s: step_rows 행 번호 %d 가 범위를 벗어납니다" % (self.key, index))
            for ch in row:
                if ch != "." and ch not in self.palette:
                    raise ValueError(
                        "%s: step_rows %d행에 팔레트에 없는 문자 %r 이 있습니다"
                        % (self.key, index, ch)
                    )


PIKACHU = Pokemon(
    key="pikachu",
    name_ko="피카츄",
    palette={
        "K": "#2b2b2b",  # 외곽선
        "Y": "#f8d030",  # 노란 몸
        "B": "#8a5a00",  # 귀 끝 / 무늬
        "R": "#e8646a",  # 볼
        "W": "#ffffff",  # 눈 반사광
    },
    rows=[
        "......KK...KK",
        ".....KKBK.KBKK",
        ".....KBBK.KBBK",
        "....KKYBKKKBYKK",
        "...KKYYYKKKYYYKK......KK",
        "..KKYYYYYYYYYYYK.....KYK",
        "..KYYYYYYYYYYYYK....KYYK",
        "..KYKWKYYYYKWKYK....KYKK",
        "..KYKKKYYYYKKKYK...KYYK",
        "..KYYYYYKKYYYYYK...KYK",
        "..KRRYYYYYYYYRRK..KYYK",
        "..KRRYYYYYYYYRRKKKYYK",
        "...KYYYYYYYYYYKKKYYK",
        "...KYYYYYYYYYYK.KKK",
        "...KYYYYYYYYYYK",
        "...KYYYYYYYYYYK",
        "...KKYYKKKKYYKK",
        "....KKKK..KKKK",
    ],
    step_rows={
        16: "..KKYYKKKKYYKK",
        17: "..KKKK....KKKK",
    },
)

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
    return True


if __name__ == "__main__":
    validate_all()
    for pokemon in POKEMON.values():
        frame = pokemon.frames()[0]
        print("%s (%s) %dx%d" % (pokemon.name_ko, pokemon.key, len(frame[0]), len(frame)))
