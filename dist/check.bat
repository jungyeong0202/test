@echo off
chcp 65001 >nul 2>&1
rem Pokemon taskbar - startup checker.
rem Run this by double-clicking it. The window stays open at the end.
setlocal enabledelayedexpansion
cd /d "%~dp0"
echo ==========================================================
echo  Pokemon taskbar - startup check
echo ==========================================================
echo.

echo [1] Files in this folder
if exist "PokemonTaskbar.exe" (
    for %%F in ("PokemonTaskbar.exe") do echo     PokemonTaskbar.exe       %%~zF bytes
) else (
    echo     PokemonTaskbar.exe       MISSING
)
if exist "PokemonTaskbar-debug.exe" (
    for %%F in ("PokemonTaskbar-debug.exe") do echo     PokemonTaskbar-debug.exe %%~zF bytes
) else (
    echo     PokemonTaskbar-debug.exe MISSING
)
echo     ^(expected: about 190000 and 190000 bytes^)
echo.

echo [2] Checksum
certutil -hashfile "PokemonTaskbar.exe" SHA256 2>nul | find /v "CertUtil" | find /v "SHA256"
echo.

echo [3] Blocked by "downloaded from the internet"?
set "BLOCKED=no"
more < "PokemonTaskbar.exe:Zone.Identifier" >nul 2>&1 && set "BLOCKED=yes"
if "!BLOCKED!"=="yes" (
    echo     YES - Windows marked it as downloaded. Unblocking now...
    powershell -NoProfile -Command "Unblock-File -Path '.\PokemonTaskbar.exe','.\PokemonTaskbar-debug.exe' -ErrorAction SilentlyContinue"
    echo     done.
) else (
    echo     no
)
echo.

echo [4] .NET Framework 4 installed?
set "NETREL="
for /f "tokens=3" %%A in ('reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Release 2^>nul') do set "NETREL=%%A"
if defined NETREL (
    echo     yes  ^(Release = !NETREL!^)
) else (
    echo     NOT FOUND - install .NET Framework 4.8 from microsoft.com
)
echo.

echo [5] Running the console build. Any error appears below.
echo ----------------------------------------------------------
"PokemonTaskbar-debug.exe" --check
set "RC=!errorlevel!"
echo ----------------------------------------------------------
echo     exit code = !RC!
if "!RC!"=="9009" echo     9009 = Windows could not start the file at all.
echo.

echo [6] Starting the real program now.
start "" "PokemonTaskbar.exe"
echo     If a Pokemon does not appear, look for the Pokemon icon
echo     in the notification area ^(bottom-right, click the ^^ arrow^)
echo     and double-click it.
echo.
echo ==========================================================
echo  Please send a screenshot of this whole window.
echo ==========================================================
pause
