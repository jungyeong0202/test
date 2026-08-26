@echo off
rem Pokemon taskbar - startup checker.
rem Double-click this file. The window stays open at the end.
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
cd /d "%~dp0"
set "LOG=%APPDATA%\PokemonTaskbar\startup.log"

echo ==========================================================
echo  Pokemon taskbar - startup check
echo ==========================================================
echo.

echo [1] Files in this folder
if exist "PokemonTaskbar.exe" (
    for %%F in ("PokemonTaskbar.exe") do echo     PokemonTaskbar.exe  %%~zF bytes
) else (
    echo     PokemonTaskbar.exe  MISSING  ^<-- put it next to this .bat
    goto :theend
)
echo.

echo [2] Checksum
certutil -hashfile "PokemonTaskbar.exe" SHA256 2>nul | find /v "CertUtil" | find /v "SHA256"
echo.

echo [3] Blocked by "downloaded from the internet"?
set "BLOCKED=no"
more < "PokemonTaskbar.exe:Zone.Identifier" >nul 2>&1 && set "BLOCKED=yes"
if "!BLOCKED!"=="yes" (
    echo     YES - unblocking now...
    powershell -NoProfile -Command "Get-ChildItem -Path '.\*.exe' ^| Unblock-File" >nul 2>&1
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
    echo     NOT FOUND - install .NET Framework 4.8
)
echo.

echo [5] Self check ^(writes a log file, no console output^)
if exist "!LOG!" del "!LOG!" >nul 2>&1
rem the log is appended to, so both step 5 and step 6 end up in it
"PokemonTaskbar.exe" --check
echo     exit code = !errorlevel!
echo     log file  = !LOG!
echo.

echo [6] Starting the real program
start "" "PokemonTaskbar.exe"
timeout /t 5 /nobreak >nul 2>&1 || ping -n 6 127.0.0.1 >nul 2>&1
tasklist /fi "imagename eq PokemonTaskbar.exe" 2>nul | find /i "PokemonTaskbar.exe" >nul
if errorlevel 1 (
    echo     the process is NOT running - it started and died.
) else (
    echo     the process IS running.
    echo     Look for the Pokemon icon in the notification area
    echo     ^(bottom-right, click the ^^ arrow^) and double-click it.
)
echo.

echo ==========================================================
echo  [7] Startup log - this says exactly how far it got
echo ==========================================================
if exist "!LOG!" (
    type "!LOG!"
) else (
    echo     no log file was written at all.
    echo     that means it could not even start running.
)
echo.

:theend
echo ==========================================================
echo  Please send a screenshot of this whole window.
echo  If it is too long, the log file is here:
echo  %APPDATA%\PokemonTaskbar\startup.log
echo ==========================================================
pause
