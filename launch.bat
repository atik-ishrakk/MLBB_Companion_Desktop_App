@echo off
setlocal enabledelayedexpansion

:: -----------------------------------------------------------------------------
:: MLBB COMPANION - LAUNCH SCRIPT
:: -----------------------------------------------------------------------------
:: Location-independent: uses %~dp0
:: Launches the pre-built executable immediately with 0s delay.
:: If not built yet, automatically invokes build.bat first.
:: -----------------------------------------------------------------------------

title MLBB Companion Launcher
cd /d "%~dp0"

set "RELEASE_EXE=%~dp0src\MLBBCompanion.GUI\bin\Release\net10.0-windows\MLBBCompanion.GUI.exe"
set "DEBUG_EXE=%~dp0src\MLBBCompanion.GUI\bin\Debug\net10.0-windows\MLBBCompanion.GUI.exe"

:: If user explicitly wants to rebuild first via "launch.bat --build"
if /i "%~1"=="--build" goto DO_BUILD
if /i "%~1"=="-b"      goto DO_BUILD
if /i "%~1"=="build"   goto DO_BUILD

:: Check if Release executable exists -> Launch instantly
if exist "!RELEASE_EXE!" (
    echo [INFO] Launching MLBB Companion Release build...
    start "" "!RELEASE_EXE!"
    exit /b 0
)

:: Check if Debug executable exists -> Launch instantly
if exist "!DEBUG_EXE!" (
    echo [INFO] Launching MLBB Companion Debug build...
    start "" "!DEBUG_EXE!"
    exit /b 0
)

:: Executable was not found, trigger build.bat
:DO_BUILD
echo [INFO] No built executable found or rebuild requested.
echo Invoking build.bat...
echo.

if exist "%~dp0build.bat" (
    call "%~dp0build.bat" --no-pause
    if !ERRORLEVEL! neq 0 (
        echo [ERROR] Build failed. Could not launch application.
        pause
        exit /b 1
    )
) else (
    echo [ERROR] build.bat was not found in %~dp0
    pause
    exit /b 1
)

:: Re-check after build
if exist "!RELEASE_EXE!" (
    echo [INFO] Launching MLBB Companion Release build...
    start "" "!RELEASE_EXE!"
    exit /b 0
)

if exist "!DEBUG_EXE!" (
    echo [INFO] Launching MLBB Companion Debug build...
    start "" "!DEBUG_EXE!"
    exit /b 0
)

echo [ERROR] Application executable could not be found after build.
pause
exit /b 1
