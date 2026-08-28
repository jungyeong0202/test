#!/bin/sh
# 배포하는 C# 판을 검사한다 (리눅스/맥, Mono 필요).
# 윈도우에서는 csharp\run_tests.bat 을 쓴다.
#
# 도트 데이터와 도구는 파이썬으로 만들므로, 그쪽 테스트도 함께 돌린다.
set -e
cd "$(dirname "$0")/.."

echo "== 도구 테스트 (파이썬) =="
python3 -m unittest test_tools -q

echo
echo "== 프로그램 테스트 (C#) =="
# 화면이 없으면 Xvfb 로 만들어 준다. WinForms 는 화면 없이는 못 뜬다.
if [ -z "$DISPLAY" ] && command -v Xvfb >/dev/null 2>&1; then
    Xvfb :99 -screen 0 1280x720x24 >/dev/null 2>&1 &
    XVFB=$!
    DISPLAY=:99
    export DISPLAY
    trap 'kill $XVFB 2>/dev/null' EXIT
fi

OUT="${TMPDIR:-/tmp}/PokemonTaskbar-tests.exe"
mcs -sdk:4.8 -target:exe -langversion:5 -warnaserror \
    -main:PokemonTaskbar.Tests.Program \
    -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll \
    -out:"$OUT" csharp/PokemonTaskbar.cs csharp/Sprites.cs csharp/Tests.cs
mono "$OUT"
