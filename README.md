# 하단바 포켓몬 (Pokémon Taskbar Pet)

화면 맨 아래(작업 표시줄) 위를 포켓몬이 어슬렁어슬렁 돌아다니는 아주 간단한 데스크톱 펫입니다.

- 파이썬 **표준 라이브러리만** 사용합니다 (tkinter). 설치할 패키지가 없습니다.
- 도트 그림을 코드로 직접 그리기 때문에 **이미지 파일이나 인터넷 연결이 필요 없습니다.**
- 작업 표시줄을 가리지 않고 그 **위에 올라서서** 걸어 다닙니다 (작업 영역을 자동으로 감지).
- 가는 방향을 보고 걷습니다. 오른쪽으로 가면 오른쪽을, 왼쪽으로 가면 왼쪽을 봅니다.
- 다른 창을 클릭해도 항상 맨 앞에 남아 계속 보입니다.

```
피카츄        파이리        이상해씨       꼬부기
pikachu    charmander    bulbasaur    squirtle
```

피카츄는 37x39 도트에 걷기 4프레임, 나머지는 손으로 그린 작은 도트입니다.
`tools/import_sprite.py` 로 원하는 그림을 넣으면 누구든 걷게 만들 수 있습니다.

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

## exe 하나로 실행하기 (윈도우, 설치 불필요)

`dist\PokemonTaskbar.exe` 를 받아서 **더블클릭하면 끝입니다.** 파이썬도, 별도 런타임 설치도 필요 없습니다.
(윈도우 10 / 11 에 기본 포함된 .NET Framework 4 로 동작합니다.)

```bat
PokemonTaskbar.exe                    :: 피카츄 한 마리
PokemonTaskbar.exe --count 3 --scale 4
PokemonTaskbar.exe -p squirtle --offset 45
```

> 처음 실행할 때 "Windows의 PC 보호" 파란 창이 뜰 수 있습니다. 서명되지 않은 exe 라서 그렇습니다.
> **추가 정보 → 실행** 을 누르면 됩니다. 찜찜하면 아래처럼 직접 빌드해서 쓰세요.

exe 는 GitHub Actions 에서도 자동으로 빌드됩니다.
저장소의 **Actions** 탭 → 최신 `build-windows-exe` 실행 → **Artifacts** 에서 내려받을 수 있습니다.

## 직접 빌드해서 실행하기 (윈도우)

`csharp` 폴더의 C# 소스를 직접 빌드할 수도 있습니다.
윈도우에 기본으로 들어 있는 .NET Framework 컴파일러(`csc.exe`)를 쓰므로 **역시 설치할 것이 없습니다.**

1. `csharp\run.bat` 더블클릭 (1~2초 빌드 후 바로 실행됩니다)
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
| `--offset` | 바닥에서 더 띄울 높이(px) | `0` |
| `--on-taskbar` | 작업 표시줄 위에 올라서지 않고 표시줄 위를 걸어 다님 | 꺼짐 |
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
| `dist/PokemonTaskbar.exe` | 바로 실행할 수 있는 빌드 결과물 |
| `tools/gen_sprites_cs.py` | `sprites.py` 도트 데이터를 `csharp/Sprites.cs` 로 변환 |
| `tools/import_sprite.py` | 내 도트 그림(png/jpg)을 스프라이트 + 걷기 프레임으로 변환 |
| `tools/make_icon.py` | 도트로 exe 아이콘(`csharp/pokemon.ico`) 생성 |
| `tools/build_exe.sh` | 리눅스/맥에서 Mono 로 exe 빌드 |

### 내 그림으로 포켓몬 추가하기

가지고 있는 도트 그림(png/jpg)을 그대로 걷게 만들 수 있습니다.

```bash
pip install Pillow                       # 변환할 때만 필요합니다
python tools/import_sprite.py 그림.png --key pikachu --name 피카츄 \
    --colors 12 --preview 확인용.png
python tools/gen_sprites_cs.py           # C# 판에도 반영
```

변환기가 알아서 해 주는 일:

1. 몇 배로 확대된 그림인지 계산해 원래 도트 크기로 되돌립니다 (칸 안의 색이 고른 배율을 찾습니다)
2. 색을 `--colors` 개수로 정리해 팔레트를 만듭니다 (jpg 압축 잡음도 이 단계에서 걸러집니다)
3. 바깥에서 이어진 배경만 투명 처리합니다 (눈동자 속 흰색처럼 안쪽에 갇힌 밝은 색은 남깁니다)
4. 맨 아래에서 두 발을 찾아 번갈아 드는 **걷기 4프레임**을 만듭니다
5. `sprites.py` 의 해당 포켓몬 정의를 통째로 갈아 끼웁니다

발이 잘 안 잡히면 `--foot-band`(아래쪽 몇 줄을 발로 볼지), `--foot-rise`(몇 칸 들어 올릴지) 로 조절하세요.
격자 자동 인식이 틀리면 `--grid` 로 가로 도트 수를 직접 지정하면 됩니다.

#### 부위를 직접 움직여 동작 만들기

서 있는 그림 한 장뿐이라 자동 생성이 어색하면, 부위를 사각형으로 지정해 프레임마다 직접 움직일 수 있습니다.
기본으로 들어 있는 피카츄가 이 방식으로 만들어졌습니다 (37x39 도트, 14색, 4프레임).

```bash
python tools/import_sprite.py 피카츄.png --key pikachu --name 피카츄 --colors 14 \
    --facing left \
    --part lfoot:0,35,6,37 --part rfoot:14,36,21,38 \
    --motion "lfoot:0,0;2,-2;0,0;1,-1" \
    --motion "rfoot:0,0;1,-1;0,0;2,-2" \
    --preview 확인용.png
```

- `--facing left` 는 **원본 그림이 왼쪽을 보고 있다**는 뜻입니다. 프로그램이 이동 방향에 맞춰 알아서 뒤집습니다
- `--part 이름:x0,y0,x1,y1` 으로 움직일 덩어리를 지정합니다. 어느 사각형에도 안 들어간 픽셀은 `body` 로 묶입니다
- `--motion 이름:dx,dy;dx,dy;...` 는 프레임별 이동량입니다. 위 예시는 두 발을 번갈아 드는 4프레임입니다
- **발은 위로만 들어 올리세요.** 아래로 내리면 몸에서 떨어져 보입니다
- 좌표는 `--preview` 로 나온 그림을 보고 잡으면 됩니다

### 손으로 그리기

도트 그림은 이렇게 문자로 정의되어 있어서, 글자만 바꿔도 새로운 포켓몬을 만들 수 있습니다.

```python
rows=[
    "......KK...KK",     # K = 외곽선, Y = 노란 몸,
    ".....KKBK.KBKK",    # R = 볼,     W = 눈 반사광 ...
    ...
]
```

`sprites.py` 의 `POKEMON` 목록에 새 `Pokemon(...)` 을 추가하면 바로 `--pokemon` 으로 부를 수 있습니다.
도트가 촘촘한 그림은 `scale_factor` 를 작게 주면 (`1 / 3` 처럼) `--scale` 과 무관하게 알맞은 크기로 그려집니다.

## 테스트

```bash
python -m unittest test_pokemon_taskbar -v
```

화면(디스플레이)이 없는 환경에서는 GUI가 필요한 테스트는 자동으로 건너뜁니다.

## 문제 해결 (윈도우)

**예전 그림이 계속 나올 때**

exe 를 새로 받아도 이전 것이 그대로 도는 경우가 있습니다. 윈도우는 **실행 중인 exe 를 덮어쓸 수 없어서**,
포켓몬이 떠 있는 상태로 복사하면 조용히 실패하거나 `PokemonTaskbar (1).exe` 로 저장됩니다.

1. 포켓몬을 **오른쪽 클릭 → 전부 종료** (또는 작업 관리자에서 `PokemonTaskbar.exe` 종료)
2. 기존 exe 를 지우고 새 파일을 넣기
3. `PokemonTaskbar.exe --list` 로 지금 실행 중인 빌드에 어떤 도트가 들어 있는지 확인

`--list` 는 이렇게 나옵니다. 크기와 프레임 수가 아래와 다르면 예전 빌드입니다.

```
pikachu       피카츄    37x39  4프레임  왼쪽 보는 그림
charmander    파이리    38x42  4프레임  왼쪽 보는 그림
bulbasaur     이상해씨  19x16  2프레임  오른쪽 보는 그림
squirtle      꼬부기    38x39  4프레임  왼쪽 보는 그림
```

`csharp\run.bat` 으로 직접 빌드해 쓰신다면 소스가 최신인지 확인하세요 (`git pull`).
run.bat 은 `csharp` 폴더의 `.cs` 파일로 다시 빌드하므로, 소스가 예전 것이면 exe 도 예전 그림이 됩니다.


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

**아예 파이썬을 안 쓰고 싶다면** 위의 [exe 하나로 실행하기](#exe-하나로-실행하기-윈도우-설치-불필요) 를 보세요.

**Microsoft Store 버전 파이썬**

Store 버전은 `python` 을 입력하면 스토어 페이지만 열리는 경우가 있습니다.
이때는 python.org 설치본을 쓰는 편이 확실합니다.

**포켓몬 위치 조절**

기본값은 작업 표시줄을 가리지 않고 그 **위에 올라선** 상태입니다.
표시줄 위(아이콘과 같은 줄)를 걸어 다니게 하려면 `--on-taskbar` 를 주세요.
더 높이 띄우려면 `--offset 40` 처럼 픽셀을 더하면 됩니다.

## 참고

포켓몬은 닌텐도 / 크리쳐스 / 게임프리크의 상표입니다. 이 저장소의 도트 그림은 공식 리소스를 사용하지 않고
직접 그린 오마주이며, 개인적인 용도로만 사용하세요.
