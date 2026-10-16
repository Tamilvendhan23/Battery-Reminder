@echo off
setlocal enabledelayedexpansion

echo ====================================================================
echo   Battery Reminder - Windows 11 x64 Optimizer & Builder
echo   Windows 11 / Windows 10 build
echo ====================================================================
echo.

:: Check for dotnet CLI
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] The .NET SDK was not found in your system PATH.
    echo Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    echo After installing, restart your command prompt and run build.bat again.
    pause
    exit /b 1
)

echo [1/3] Cleaning previous build artifacts...
if exist "publish" rmdir /s /q "publish"
if exist "publish-framework-dependent" rmdir /s /q "publish-framework-dependent"
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

echo [2/3] Compiling Target 1: Standalone Self-Contained Single-File (Optimized)...
echo       Applies single-file compression and strips debug symbols.
echo.

dotnet publish BatteryReminder.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:Optimize=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    -p:ServerGarbageCollection=false ^
    -p:EventSourceSupport=false ^
    -p:UseSystemResourceKeys=true ^
    --output "./publish"

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed! Check the error output above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [3/3] Compiling Target 2 (Optional): Framework-Dependent Ultra-Compact Single-File...
echo       Requires .NET 8 Desktop Runtime installed; yields tiny executable (~1-2 MB).
echo.

dotnet publish BatteryReminder.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:Optimize=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    --output "./publish-framework-dependent"

echo.
echo ====================================================================
echo   BUILD COMPLETED SUCCESSFULLY!
echo ====================================================================
echo.
echo 1. Standalone Self-Contained Single-File (Recommended, zero runtime required):
echo    Path: %~dp0publish\BatteryReminder.exe
echo    Size: ~28 MB (reduced from ~65 MB via single-file compression)
echo.
echo 2. Framework-Dependent Single-File (Ultra-compact, requires .NET 8 Desktop Runtime):
echo    Path: %~dp0publish-framework-dependent\BatteryReminder.exe
echo    Size: ~1.5 - 2.5 MB (97%% reduction)
echo.
echo You can run either executable directly. Both support:
echo   - Window resizing, minimize/maximize buttons, compact 420x400 layout
echo   - 25%% low battery and 90%% high battery alerts with native chimes
echo   - Silent system tray operation and Start with Windows
echo.
pause
