@echo off
rem Run the C# tests on Windows with the built-in compiler.
setlocal
cd /d "%~dp0.."

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto :no_csc

"%CSC%" /nologo /target:exe /warnaserror /main:PokemonTaskbar.Tests.Program ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /out:"%TEMP%\PokemonTaskbar-tests.exe" ^
    csharp\PokemonTaskbar.cs csharp\Sprites.cs csharp\Tests.cs
if errorlevel 1 exit /b 1
"%TEMP%\PokemonTaskbar-tests.exe"
exit /b %errorlevel%

:no_csc
echo Could not find csc.exe. Install the .NET Framework.
exit /b 1
