@echo off
setlocal enabledelayedexpansion

:: -----------------------------------------------------------------------------
:: MLBB COMPANION - BUILD SCRIPT
:: -----------------------------------------------------------------------------
:: Location-independent: uses %~dp0
:: Can be run from anywhere, or called by other scripts.
:: -----------------------------------------------------------------------------

title MLBB Companion Builder
cd /d "%~dp0"

echo =====================================================================
echo                 MLBB COMPANION - BUILD ENGINE
echo =====================================================================
echo Project Root: %~dp0
echo.

:: Check for dotnet CLI
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET SDK dotnet CLI was not found in PATH.
    echo Please install .NET 10 SDK from https://dotnet.microsoft.com/download
    echo.
    if "%~1" neq "--no-pause" pause
    exit /b 1
)

:: Determine configuration
set "CONFIG=Release"
if /i "%~1"=="debug" set "CONFIG=Debug"
if /i "%~1"=="-d"    set "CONFIG=Debug"

echo [*] Building project in %CONFIG% configuration...
dotnet build "%~dp0MLBBCompanion.slnx" -c %CONFIG% --nologo

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed with configuration %CONFIG%.
    if "%CONFIG%"=="Release" (
        echo [*] Attempting fallback build in Debug mode...
        dotnet build "%~dp0MLBBCompanion.slnx" -c Debug --nologo
        if !ERRORLEVEL! neq 0 (
            echo.
            echo [FATAL] Both Release and Debug builds failed.
            if "%~1" neq "--no-pause" pause
            exit /b 1
        )
        set "CONFIG=Debug"
    ) else (
        if "%~1" neq "--no-pause" pause
        exit /b 1
    )
)

echo.
echo =====================================================================
echo [SUCCESS] Build completed successfully in %CONFIG% mode!
echo Executable: %~dp0src\MLBBCompanion.GUI\bin\%CONFIG%\net10.0-windows\MLBBCompanion.GUI.exe
echo =====================================================================
echo.

if "%~1" neq "--no-pause" if "%~2" neq "--no-pause" (
    echo You can now run launch.bat to start the application.
    echo Press any key to exit...
    pause >nul
)

exit /b 0
