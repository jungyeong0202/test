#!/bin/sh
# 리눅스/맥에서 윈도우용 exe 를 빌드한다 (Mono 필요: apt install mono-devel).
# 만들어진 어셈블리는 윈도우의 .NET Framework 4 에서 그대로 실행된다.
# 윈도우에서 빌드할 때는 csharp/run.bat 을 쓰면 된다.
set -e
cd "$(dirname "$0")/.."
python3 tools/gen_sprites_cs.py
python3 tools/make_icon.py
mkdir -p dist

# -sdk:4.8 이 없으면 절대 안 된다.
#
# mcs 는 기본으로 Mono 자신의 클래스 라이브러리를 기준으로 컴파일한다. Mono 에는
# .NET Core 시절 추가된 API 가 들어 있어서(예: string.Split(char, StringSplitOptions)),
# 그대로 빌드하면 윈도우의 .NET Framework 에 없는 메서드를 호출하는 exe 가 나온다.
# 그런 exe 는 윈도우에서 MissingMethodException 으로 조용히 죽는다. Mono 로는
# 멀쩡히 돌기 때문에 여기서는 절대 드러나지 않는다.
#
# -sdk:4.8 은 /usr/lib/mono/4.8-api 의 진짜 .NET Framework 4.8 참조 어셈블리를
# 쓰게 한다. 없는 API 를 쓰면 컴파일 단계에서 걸린다.
SDK=4.8

mcs -sdk:$SDK -target:winexe -optimize+ -win32icon:csharp/pokemon.ico \
    -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
    -resource:assets/fonts/NotoSansKR-VF.ttf,PokemonTaskbar.NotoSansKR.ttf \
    -resource:assets/fonts/OFL.txt,PokemonTaskbar.NotoSansKR.OFL.txt \
    -out:dist/PokemonTaskbar.exe csharp/PokemonTaskbar.cs csharp/Sprites.cs
# 콘솔 판. 창이 안 뜰 때 원인을 글자로 보여 준다(같은 소스, 콘솔 서브시스템).
mcs -sdk:$SDK -target:exe -optimize+ -win32icon:csharp/pokemon.ico \
    -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
    -resource:assets/fonts/NotoSansKR-VF.ttf,PokemonTaskbar.NotoSansKR.ttf \
    -resource:assets/fonts/OFL.txt,PokemonTaskbar.NotoSansKR.OFL.txt \
    -out:dist/PokemonTaskbar-debug.exe csharp/PokemonTaskbar.cs csharp/Sprites.cs

# 그래도 혹시 모르니 만들어진 IL 을 직접 검사한다.
python3 tools/check_net48.py dist/PokemonTaskbar.exe dist/PokemonTaskbar-debug.exe
echo "빌드 완료: dist/PokemonTaskbar.exe"
file dist/PokemonTaskbar.exe
echo "빌드 완료: dist/PokemonTaskbar-debug.exe"
file dist/PokemonTaskbar-debug.exe
