# AGENTS.md

작업 표시줄 위를 돌아다니는 포켓몬 데스크톱 펫. **파이썬 판과 C# 판이 같은 동작을 각자
구현**하고 있고, 사용자에게 배포하는 것은 C# 으로 만든 윈도우용 `dist/PokemonTaskbar.exe`
하나다(설치도, 런타임 설치도 필요 없다).

사람이 읽을 문서는 `README.md` 에 있다. 이 파일은 **코드를 고칠 때 알아야 할 것**만 적는다.

## 여러 도구로 나눠서 작업할 때

이 저장소는 Claude Code 와 Codex 를 오가며 작업한다. **인계 지점은 저장소 자체다.**
서로를 프로세스로 잇지 않고, 한쪽이 커밋하면 다른 쪽이 이어받는다. git 히스토리가
그대로 인계 기록이 된다.

- 지침은 **이 파일(`AGENTS.md`) 한 곳에만** 둔다. `CLAUDE.md` 는 이 파일을 가져다 쓸
  뿐이므로, 새 규칙은 여기에 적어야 두 도구가 함께 본다.
- 한 번씩 상대 도구를 불러 쓸 수도 있다(`codex exec "..."`, `claude -p "..."`).
  `codex exec` 는 기본이 승인 없이 실행되므로 `--sandbox workspace-write` 를 같이 준다.

### 켤 때 pull, 떠날 때 push

```bash
git pull                 # 시작할 때: 상대가 올린 것을 먼저 받는다
# ... 작업 ...
git add -A && git commit && git push    # 끝낼 때: 여기까지 해야 넘어간다
```

**떠날 때가 더 중요하다.** 커밋만 하고 푸시하지 않으면 그 기기에만 남아, 다음 도구는
옛 코드 위에 작업하게 되고 나중에 크게 엉킨다. 커밋조차 안 한 변경은 아무 데도 안 남는다.
원격 컨테이너에서 작업하는 도구도 있는데, 그 폴더는 세션이 끝나면 사라진다.

넘기기 전에 `python3 tools/gen_sprites_cs.py` 를 돌려 자동 생성물을 맞춰 둔다.
안 맞으면 다음 사람이 영문 모를 테스트 실패부터 만난다.

### exe 는 자동으로 합쳐지지 않는다

`dist/PokemonTaskbar.exe` 와 `dist/PokemonTaskbar-debug.exe` 는 git 에 들어 있고 빌드할
때마다 바뀐다. **바이너리라서 git 이 합치지 못한다.** 양쪽에서 빌드했다면 pull 할 때
거의 매번 충돌한다.

```
CONFLICT (content): Merge conflict in dist/PokemonTaskbar.exe
```

**어느 쪽 exe 를 고를지 고민할 필요 없다.** exe 는 소스에서 다시 만드는 것이므로,
소스 충돌부터 풀고 새로 빌드해 덮으면 그것으로 끝난다.

```bash
# 1. 소스 충돌을 먼저 해결한다 (이게 진짜 일이다)
# 2. 합쳐진 소스로 exe 를 새로 만든다 — 충돌 난 파일을 덮어쓴다
sh tools/build_exe.sh
# 3. 새로 만든 파일로 충돌이 풀린다
git add dist/ && git commit
```

`git checkout --ours/--theirs` 로 한쪽을 고를 필요는 없다. 어차피 다시 만들 파일이고,
소스 충돌이 남아 있으면 빌드부터 실패한다.

## 무엇이 어디에 있나

| 파일 | 하는 일 |
| --- | --- |
| `sprites.py` | 도트 그림 데이터(원본). tkinter 를 쓰지 않는다 |
| `pokemon_taskbar.py` | 파이썬 판 본체 (tkinter) |
| `settings.py` | 설정 파일 읽기/쓰기 |
| `csharp/PokemonTaskbar.cs` | C# 판 본체 (WinForms). 파이썬 판을 그대로 옮긴 것 |
| `csharp/Sprites.cs` | **자동 생성물.** 손으로 고치지 말 것 |
| `tools/import_sprite.py` | 그림 → 도트 데이터 변환기 |
| `tools/gen_sprites_cs.py` | `sprites.py` → `csharp/Sprites.cs` |
| `tools/check_net48.py` | 만든 exe 가 .NET Framework 4.8 API 만 쓰는지 검사 |
| `tools/build_exe.sh` | 리눅스/맥에서 윈도우용 exe 빌드 |
| `test_pokemon_taskbar.py` | 파이썬 테스트 (127개) |

## 규칙

1. **파이썬과 C# 을 항상 같이 고친다.** 한쪽만 고치면 두 판의 동작이 갈라진다.
   상수 이름·값, 함수 이름, 주석까지 서로 대응시켜 두었다.
2. **`sprites.py` 를 고쳤으면 `python3 tools/gen_sprites_cs.py` 를 돌린다.**
   안 돌리면 테스트가 잡아낸다(`test_sprites_cs_is_up_to_date`).
3. **`csharp/Sprites.cs` 는 손으로 고치지 않는다.** 다음 생성 때 사라진다.
4. `.bat` 파일은 **ASCII + CRLF** 로 쓴다. 한글을 넣으면 윈도우 콘솔에서 깨진다
   (`.gitattributes` 가 CRLF 를 강제한다).
5. `csharp/PokemonTaskbar.cs` 는 **UTF-8 BOM + CRLF** 다. 편집할 때 유지할 것.

## 명령

```bash
# 테스트 (화면이 없으면 GUI 테스트는 자동으로 건너뛴다)
python3 -m unittest test_pokemon_taskbar -q

# 화면 없는 리눅스에서 GUI 테스트까지 돌리려면
Xvfb :99 -screen 0 1280x720x24 &
DISPLAY=:99 python3 -m unittest test_pokemon_taskbar -q

# 파이썬 판 실행
python3 pokemon_taskbar.py

# 윈도우용 exe 빌드 (Mono 필요)
sh tools/build_exe.sh
```

테스트에는 **tkinter 가 있는 파이썬 3** 가 필요하다. 배포판에 따라 `python3` 에
tkinter 가 없을 수 있으니(`apt install python3-tk`), 없으면 있는 쪽을 골라 쓴다.

빌드에는 `mono-devel`(mcs)과 `mono-utils`(ikdasm)가 필요하다. `tools/check_net48.py` 는
`/usr/lib/mono/4.8-api` 를 읽는다. 없으면 검사를 건너뛴다고 알리고 넘어간다.

테스트는 환경 변수 `POKEMON_TASKBAR_SETTINGS` 로 설정 파일을 임시 폴더로 돌려 두므로
**진짜 사용자 설정을 건드리지 않는다.** 직접 스크립트를 짜서 앱을 띄울 때도 이 변수를
반드시 설정할 것.

## 반드시 지켜야 하는 것들 (전부 실제로 사고가 났던 것)

### 1. mcs 는 `-sdk:4.8` 없이 쓰지 않는다

`mcs` 는 기본으로 **Mono 자신의** 클래스 라이브러리를 기준으로 컴파일한다. Mono 에는
.NET Core 시절 추가된 API 가 들어 있어서, 그냥 빌드하면 윈도우의 .NET Framework 에
**없는 메서드를 호출하는 exe** 가 만들어진다. 리눅스에서 Mono 로 돌리면 멀쩡히 돌기
때문에 개발 중에는 절대 드러나지 않고, 윈도우에서만 `MissingMethodException` 으로
조용히 죽는다.

실제로 평범한 `value.Split(',')` 이 Mono 에만 있는
`Split(char, StringSplitOptions)` 으로 묶여, 옵션 해석 첫 단계에서 아무 창도 못 띄우고
죽은 적이 있다. 원인을 찾는 데 오래 걸렸다.

막는 장치가 두 겹 있다. 둘 다 `tools/build_exe.sh` 안에 있으니 빌드는 그 스크립트로 한다.

| 장치 | 언제 걸리나 |
| --- | --- |
| `-sdk:4.8` | 컴파일할 때. 진짜 .NET Framework 4.8 참조 어셈블리를 쓴다 |
| `tools/check_net48.py` | 빌드 뒤. IL 의 호출을 매개변수 타입까지 4.8 정의와 맞춰 본다 |

윈도우에서 `csc.exe` 로 빌드할 때(`csharp/run.bat`, GitHub Actions)는 이 문제가 없다.

### 2. 창이 안 뜰 때를 위한 장치를 지운다면 대안을 먼저 만든다

GUI 프로그램은 실패해도 흔적이 안 남는다. 그래서:

- 시작 과정을 `%APPDATA%\PokemonTaskbar\startup.log` 에 단계마다 남긴다.
- 처리되지 않은 예외를 잡아 오류 창으로 띄운다. `Main` 은 **로그 열기와 예외 처리기
  등록을 그 무엇보다 먼저** 한다(예전에 옵션 해석이 처리기보다 앞서 있어 거기서 터지면
  아무것도 안 남았다).
- `--check` 로 화면·설정·창 위치·스프라이트 목록을 로그에 남기고 끝낼 수 있다.
- `dist/PokemonTaskbar-debug.exe` 는 같은 소스의 콘솔 판이다.
- `dist/check.bat` 이 파일 무결성·차단 여부·런타임 설치 여부를 한 번에 확인해 준다.
- 알림 영역 아이콘에서 포켓몬을 화면 가운데로 불러올 수 있다.

**콘솔에 한글을 쓰지 말 것.** `chcp 65001` 과 .NET Framework 콘솔이 충돌해
`Exception.ToString()` 조차 실패하며 죽는다. 그래서 `--check` 는 화면이 아니라 파일에 쓴다.

### 3. 도트 격자 크기를 잘못 재면 그림이 뭉개진다

`tools/import_sprite.py` 의 첫 단계는 **도트 한 칸이 원본에서 몇 픽셀인지** 알아내는
것이다. 여기서 틀리면 그 뒤가 전부 어긋난다. 칸을 실제보다 작게 잡으면 몇 칸마다 한 칸씩
늘어나 외곽선이 두꺼워지고 그림이 뭉개진다(7.82 픽셀짜리를 6.95 로 잡아 58x49 로
부푼 적이 있다. 제 크기는 52x44 였다).

지금은 도트 경계가 일정 간격으로 되풀이된다는 점을 이용해 잰다. **칸 크기의 두 배·세 배도
점수가 높으므로**(한 칸 걸러 격자선을 놓는 셈), 최고점에 가까운 것들 중 **가장 작은** 값을
골라야 진짜 한 칸이다. 이걸 빠뜨리면 어떤 그림은 6x7 칸으로 깨진다.

결과가 미심쩍으면 `--preview` 로 나온 그림을 원본과 나란히 놓고 **외곽선 굵기**를 보라.
원본보다 두꺼우면 칸을 작게 잡은 것이다. `--grid`, `--rows` 로 직접 정할 수도 있다.

### 4. 부위는 몸쪽으로(위로만) 움직인다

`--part`/`--motion` 으로 발이나 꼬리를 움직일 때 **아래로 내리면 잘라낸 자리가 비어**
몸을 가로지르는 어두운 줄이 생긴다. 위로 올리면 겹치기만 하므로 구멍이 안 생긴다.

### 5. 비율

도트를 화면에 그릴 때 배율이 정수가 아니다(기본 1.5배). **가로세로에 같은 반올림 규칙**
(`int(x + 0.5)` / `Math.Floor(x + 0.5)`)을 써야 비율이 유지된다. 언어 기본 반올림은
.5 를 짝수로 보내므로 축마다 결과가 달라진다.

### 6. 팔레트에 `#ff00ff` 를 넣지 않는다

투명 처리에 쓰는 색이다. 스프라이트가 그 색을 쓰면 그 부분이 뚫린다.

## 이동 방식

포켓몬마다 `move` 가 다르다. 새 방식을 더하려면 `sprites.py` 의 검증, 파이썬 `tick`/`draw`,
C# `OnTick`/`OnPaint`, 그리고 `tools/gen_sprites_cs.py` 를 모두 손봐야 한다.

| 방식 | 누가 | 어떻게 |
| --- | --- | --- |
| `walk` | 피카츄, 파이리, 이상해씨, 꼬부기, 어니부기 | 발을 번갈아 디디며 걷는다 (4프레임) |
| `hop` | 메타몽 | 웅크렸다 늘어나며 폴짝 뛴다 (3프레임, 공중에서만 전진) |
| `float` | 뮤 | 중력을 받지 않고 떠다닌다 (4프레임) |

## 진화

`sprites.py` 의 `EVOLUTIONS` 표에 적는다. 도트는 자동 생성 구역 안에 들어가므로,
진화 관계는 **그 바깥**에 두어 그림을 다시 들여와도 지워지지 않게 했다.

- 8번 쓰다듬고 600px를 산책하면 진화할 준비가 된다. **시간이 흘렀다고 저절로 진화하지는 않는다**.
  조건을 채운 뒤에도 메뉴에서 직접 선택해야 진화하므로, 아끼던 모습이 예고 없이 바뀌지 않는다.
- 진화체는 `포켓몬 추가` 메뉴, 무작위, `--count` 어디에도 나오면 안 된다.
  이름을 직접 댈 때만(`-p wartortle`) 쓸 수 있다.
- 진화하는 포켓몬의 창은 **두 모습이 모두 들어갈 크기**로 잡고 그림을 아래쪽에 맞춰
  그린다. 그래야 번쩍이는 동안 잘리지 않으면서 발이 바닥에 붙어 있다.

## 새 포켓몬 넣기

```bash
python3 tools/import_sprite.py 그림.png --key 키 --name 한글이름 \
    --colors 16 --facing left --preview /tmp/확인.png
```

`--preview` 로 확인하고, `--part`/`--motion` 으로 걷는 모습을 만든 뒤,
`python3 tools/gen_sprites_cs.py` 를 돌리고 테스트를 통과시킨다.
실제로 쓴 명령들은 `README.md` 에 포켓몬별로 적어 두었다.

## 검증

고치고 나서 아래가 전부 통과해야 한다.

- `python3 -m unittest test_pokemon_taskbar -q` (127개)
- `sh tools/build_exe.sh` — 경고 없이 빌드되고 API 검사를 통과
- 파이썬과 C# 의 도트 데이터가 완전히 일치 (양쪽에서 덤프해 `diff`)
- 실제로 앱을 띄워 눈으로 확인 — 테스트가 잡지 못하는 문제가 많다
  (흰 테두리, 뭉갠 도트, 창이 안 보임 같은 것들은 전부 눈으로 발견했다)
