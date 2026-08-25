# 하단바 포켓몬 (Pokémon Taskbar Pet)

화면 맨 아래(작업 표시줄) 위를 포켓몬이 어슬렁어슬렁 돌아다니는 아주 간단한 데스크톱 펫입니다.

- 파이썬 **표준 라이브러리만** 사용합니다 (tkinter). 설치할 패키지가 없습니다.
- 도트 그림을 코드로 직접 그리기 때문에 **이미지 파일이나 인터넷 연결이 필요 없습니다.**
- 테두리 없는 항상-맨-앞 창이라 다른 창을 가리지 않고 화면 아래를 걸어 다닙니다.

```
피카츄        파이리        이상해씨       꼬부기
pikachu    charmander    bulbasaur    squirtle
```

## 실행 방법

파이썬 3.8 이상이 필요합니다.

```bash
python pokemon_taskbar.py
```

윈도우에서는 `run.bat`을 더블클릭하면 콘솔 창 없이 실행됩니다.
(파이썬이 PATH에 없어도 `py` 런처와 기본 설치 경로를 자동으로 찾습니다.)

실행이 안 되거나 창이 안 뜨면 `run_debug.bat`을 실행하세요. 콘솔 창에 오류가 그대로 남습니다.

리눅스에서 `ModuleNotFoundError: No module named 'tkinter'` 가 나오면 tkinter를 설치하세요.

```bash
sudo apt install python3-tk      # 데비안 / 우분투 계열
```

## 파이썬 없이 실행하기 (윈도우)

파이썬을 설치하기 싫다면 `csharp` 폴더의 C# 판을 쓰세요.
윈도우에 기본으로 들어 있는 .NET Framework 컴파일러(`csc.exe`)로 빌드하므로 **설치할 것이 하나도 없습니다.**

1. `csharp\run.bat` 더블클릭 (첫 실행 때 1~2초 빌드 후 바로 실행됩니다)
2. 그 다음부터는 만들어진 `csharp\PokemonTaskbar.exe` 를 직접 실행해도 됩니다

```bat
csharp\run.bat --count 3 --scale 4
csharp\PokemonTaskbar.exe -p squirtle --offset 45
```

옵션은 파이썬 판과 동일합니다. 도트 그림도 같은 데이터(`sprites.py`)에서 생성하므로 결과가 똑같습니다.
`sprites.py` 를 고쳤다면 아래 명령으로 C# 쪽 데이터를 다시 만들어 주세요.

```bash
python tools/gen_sprites_cs.py
```

> C# 판은 창을 활성화하지 않도록 만들어져 있어 `Esc` 종료가 없습니다. 우클릭 메뉴의 **전부 종료**를 쓰세요.

## 조작법

| 동작 | 결과 |
| --- | --- |
| 포켓몬을 왼쪽 클릭 | 폴짝 뛴다 |
| 포켓몬을 오른쪽 클릭 | 메뉴 (포켓몬 추가 / 보내주기 / 전부 종료) |
| 포켓몬을 클릭한 뒤 `Esc` 키 | 종료 |
| 터미널에서 `Ctrl+C` | 종료 |

## 옵션

```bash
python pokemon_taskbar.py --list                 # 사용 가능한 포켓몬 목록
python pokemon_taskbar.py -p squirtle            # 꼬부기 한 마리
python pokemon_taskbar.py -p pikachu -p charmander   # 두 마리 같이
python pokemon_taskbar.py --count 5              # 무작위로 5마리
python pokemon_taskbar.py --scale 4 --speed 90   # 더 크고 빠르게
python pokemon_taskbar.py --offset 40            # 작업 표시줄 위쪽에 올려놓기
```

| 옵션 | 설명 | 기본값 |
| --- | --- | --- |
| `-p`, `--pokemon` | 등장시킬 포켓몬 (여러 번 사용 가능) | `pikachu` |
| `-c`, `--count` | 마리 수 | `1` |
| `-s`, `--scale` | 도트 확대 배율 | `3` |
| `--speed` | 이동 속도 (초당 픽셀) | `55` |
| `--offset` | 화면 맨 아래에서 띄울 높이(px) | `0` |
| `--bg` | 투명 창을 못 쓰는 환경에서 쓸 배경색 | `#1e1e1e` |

## 운영체제별 참고

| OS | 배경 투명 | 비고 |
| --- | --- | --- |
| 윈도우 | ✅ (`-transparentcolor`) | 배경이 완전히 투명하고, 포켓몬 바깥쪽 클릭은 아래 창으로 그대로 전달됩니다. |
| macOS | ✅ (`-transparent`) | 처음 실행할 때 화면 접근 권한을 물어볼 수 있습니다. |
| 리눅스 | ⚠️ 창 관리자에 따라 다름 | 투명이 안 되면 `--bg` 로 작업 표시줄과 비슷한 색을 지정하세요. 예: `--bg "#2d2d2d"` |

## 구성

| 파일 | 내용 |
| --- | --- |
| `pokemon_taskbar.py` | 창 생성, 이동/애니메이션, 마우스 조작 등 프로그램 본체 |
| `sprites.py` | 도트 그림 데이터 (문자 그리드 + 색 팔레트). tkinter에 의존하지 않음 |
| `test_pokemon_taskbar.py` | 스프라이트 데이터와 이동 로직 테스트 |
| `run.bat` / `run_debug.bat` | 윈도우용 실행 스크립트 (일반 실행 / 오류 확인용) |
| `csharp/` | 파이썬 없이 도는 C# 판 (`run.bat` 이 빌드까지 해 줍니다) |
| `tools/gen_sprites_cs.py` | `sprites.py` 도트 데이터를 `csharp/Sprites.cs` 로 변환 |

도트 그림은 이렇게 문자로 정의되어 있어서, 글자만 바꾸면 새로운 포켓몬을 쉽게 추가할 수 있습니다.

```python
rows=[
    "......KK...KK",     # K = 외곽선, Y = 노란 몸,
    ".....KKBK.KBKK",    # R = 볼,     W = 눈 반사광 ...
    ...
]
```

`sprites.py` 의 `POKEMON` 목록에 새 `Pokemon(...)` 을 추가하면 바로 `--pokemon` 으로 부를 수 있습니다.

## 테스트

```bash
python -m unittest test_pokemon_taskbar -v
```

화면(디스플레이)이 없는 환경에서는 GUI가 필요한 테스트는 자동으로 건너뜁니다.

## 문제 해결 (윈도우)

**`'pythonw'을(를) 찾을 수 없습니다` 또는 `'python'은(는) 내부 또는 외부 명령이 아닙니다`**

파이썬이 없거나, 설치할 때 PATH에 등록하지 않은 경우입니다. cmd를 열고 확인해 보세요.

```bat
py --version
python --version
```

둘 다 실패하면 [python.org](https://www.python.org/downloads/) 에서 파이썬 3을 설치하되,
설치 첫 화면의 **"Add python.exe to PATH"** 를 반드시 체크하세요.
이미 설치돼 있다면 `run.bat` 이 `py` 런처와 아래 경로들을 자동으로 찾아 줍니다.

- `%LOCALAPPDATA%\Programs\Python\Python3xx\`
- `C:\Program Files\Python3xx\`
- `C:\Python3xx\`

**창이 잠깐 떴다가 바로 사라짐 / 아무 반응이 없음**

`run_debug.bat` 으로 실행하면 콘솔에 오류 메시지가 남습니다.
`ModuleNotFoundError: No module named 'tkinter'` 가 보이면 파이썬 설치 프로그램을 다시 실행해
**Modify → "tcl/tk and IDLE"** 를 켜 주세요.

**아예 파이썬을 안 쓰고 싶다면** 위의 [파이썬 없이 실행하기](#파이썬-없이-실행하기-윈도우) 를 보세요.

**Microsoft Store 버전 파이썬**

Store 버전은 `python` 을 입력하면 스토어 페이지만 열리는 경우가 있습니다.
이때는 python.org 설치본을 쓰는 편이 확실합니다.

**포켓몬이 작업 표시줄에 가려질 때**

`--offset` 으로 조금 띄우면 됩니다. 예: `run.bat --offset 45`

## 참고

포켓몬은 닌텐도 / 크리쳐스 / 게임프리크의 상표입니다. 이 저장소의 도트 그림은 공식 리소스를 사용하지 않고
직접 그린 오마주이며, 개인적인 용도로만 사용하세요.
