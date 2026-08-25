@echo off
rem Pokemon taskbar pet launcher (Windows)
rem Finds Python even when it is not on PATH, then starts the pet.
rem   run.bat            -> start without a console window
rem   run.bat --debug    -> run in this console and keep errors on screen
setlocal
cd /d "%~dp0"

set "PY="
set "PYW="
set "PYARGS="

rem 1) the Python launcher (installed with python.org builds)
where py.exe >nul 2>&1
if not errorlevel 1 (
    set "PY=py.exe"
    set "PYW=pyw.exe"
    set "PYARGS=-3"
)

rem 2) python.exe on PATH
if not defined PY (
    where python.exe >nul 2>&1
    if not errorlevel 1 (
        set "PY=python.exe"
        set "PYW=pythonw.exe"
    )
)

rem 3) common install folders, newest first
if not defined PY (
    for %%R in ("%LOCALAPPDATA%\Programs\Python" "%ProgramFiles%" "%ProgramFiles(x86)%" "C:") do (
        for %%V in (315 314 313 312 311 310 39 38) do (
            if not defined PY if exist "%%~R\Python%%V\python.exe" (
                set "PY=%%~R\Python%%V\python.exe"
                set "PYW=%%~R\Python%%V\pythonw.exe"
            )
        )
    )
)

if not defined PY goto :no_python

rem fall back to the console interpreter if the windowed one is missing
if defined PYW (
    where "%PYW%" >nul 2>&1
    if errorlevel 1 if not exist "%PYW%" set "PYW=%PY%"
)

rem tkinter is required and is missing in some slim installs
"%PY%" %PYARGS% -c "import tkinter" >nul 2>&1
if errorlevel 1 goto :no_tkinter

if /i "%~1"=="--debug" goto :debug_run

start "" "%PYW%" %PYARGS% "%~dp0pokemon_taskbar.py" %*
exit /b 0

:debug_run
echo Using: %PY% %PYARGS%
echo.
"%PY%" %PYARGS% "%~dp0pokemon_taskbar.py" %2 %3 %4 %5 %6 %7 %8 %9
echo.
echo [exit code %errorlevel%]
pause
exit /b 0

:no_python
echo.
echo [!] Python was not found.
echo.
echo     Install Python 3 from https://www.python.org/downloads/
echo     and TICK "Add python.exe to PATH" in the installer.
echo.
echo     Already installed? Open cmd and try:  py --version
echo.
pause
exit /b 1

:no_tkinter
echo.
echo [!] Python was found (%PY%) but tkinter is missing.
echo.
echo     Re-run the Python installer, choose "Modify",
echo     and enable "tcl/tk and IDLE".
echo.
pause
exit /b 1
