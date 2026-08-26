#!/bin/sh
# 리눅스/맥에서 윈도우용 exe 를 빌드한다 (Mono 필요: apt install mono-devel).
# 만들어진 어셈블리는 윈도우의 .NET Framework 4 에서 그대로 실행된다.
# 윈도우에서 빌드할 때는 csharp/run.bat 을 쓰면 된다.
set -e
cd "$(dirname "$0")/.."
python3 tools/gen_sprites_cs.py
python3 tools/make_icon.py
mkdir -p dist
mcs -target:winexe -optimize+ -win32icon:csharp/pokemon.ico \
    -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
    -out:dist/PokemonTaskbar.exe csharp/PokemonTaskbar.cs csharp/Sprites.cs
# 콘솔 판. 창이 안 뜰 때 원인을 글자로 보여 준다(같은 소스, 콘솔 서브시스템).
mcs -target:exe -optimize+ -win32icon:csharp/pokemon.ico \
    -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
    -out:dist/PokemonTaskbar-debug.exe csharp/PokemonTaskbar.cs csharp/Sprites.cs
echo "빌드 완료: dist/PokemonTaskbar.exe"
file dist/PokemonTaskbar.exe
echo "빌드 완료: dist/PokemonTaskbar-debug.exe"
file dist/PokemonTaskbar-debug.exe
