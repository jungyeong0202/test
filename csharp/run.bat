@echo off
rem Pokemon taskbar pet - build and run WITHOUT Python.
rem Uses the C# compiler that ships with Windows (.NET Framework 4).
rem   run.bat                  -> one Pikachu
rem   run.bat --count 3        -> three random pokemon
rem   run.bat --scale 4 --offset 45
setlocal
cd /d "%~dp0"

set "CSC="
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC goto :no_csc

echo Building PokemonTaskbar.exe ...
"%CSC%" /nologo /target:winexe /optimize+ /out:"PokemonTaskbar.new.exe" ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    PokemonTaskbar.cs Sprites.cs
if errorlevel 1 goto :build_failed

move /y "PokemonTaskbar.new.exe" "PokemonTaskbar.exe" >nul 2>&1
if errorlevel 1 (
    del "PokemonTaskbar.new.exe" >nul 2>&1
    echo Could not replace PokemonTaskbar.exe. Starting the existing build.
)

start "" "PokemonTaskbar.exe" %*
exit /b 0

:build_failed
del "PokemonTaskbar.new.exe" >nul 2>&1
if exist "PokemonTaskbar.exe" (
    echo Build failed - starting the previous build instead.
    start "" "PokemonTaskbar.exe" %*
    exit /b 0
)
echo.
echo [!] Build failed. Please report the compiler message above.
echo.
pause
exit /b 1

:no_csc
echo.
echo [!] The C# compiler that ships with Windows was not found.
echo     Expected here:
echo       %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
echo.
echo     Windows 10 and 11 include it by default. On older systems,
echo     install the .NET Framework 4 runtime from microsoft.com.
echo.
pause
exit /b 1
