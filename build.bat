@echo off
chcp 65001 >nul
title WersDL Build Script

echo ============================================
echo   WersDL - Auto Build Script
echo ============================================
echo.

:: Check for .NET SDK
where dotnet >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK not found! Please install .NET 8 SDK.
    echo         https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

:: Check .NET version
for /f "tokens=2 delims= " %%a in ('dotnet --version') do set DOTNET_VER=%%a
if "%DOTNET_VER%"=="" set DOTNET_VER=unknown
echo [INFO] .NET SDK version: %DOTNET_VER%

:: Configuration
set CONFIG=Release
if not "%1"=="" set CONFIG=%1

echo [INFO] Configuration: %CONFIG%
echo.

:: Restore packages
echo [STEP 1/4] Restoring NuGet packages...
call dotnet restore YT-DLP-GUI.slnx
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Package restore failed!
    pause
    exit /b 1
)
echo [OK] Packages restored successfully.
echo.

:: Build
echo [STEP 2/4] Building project (%CONFIG%)...
call dotnet build YT-DLP-GUI.slnx --configuration %CONFIG% --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)
echo [OK] Build completed successfully.
echo.

:: Publish portable
echo [STEP 3/4] Publishing portable version...
call dotnet publish YT-DLP-GUI.csproj --configuration %CONFIG% --runtime win-x64 --self-contained false --output ./build/portable
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Portable publish failed!
    pause
    exit /b 1
)
echo [OK] Portable version published to ./build/portable/
echo.

:: Publish self-contained
echo [STEP 4/4] Publishing self-contained version...
call dotnet publish YT-DLP-GUI.csproj --configuration %CONFIG% --runtime win-x64 --self-contained true --output ./build/self-contained
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Self-contained publish failed!
    pause
    exit /b 1
)
echo [OK] Self-contained version published to ./build/self-contained/
echo.

:: Create ZIP archives
echo [INFO] Creating ZIP archives...
powershell -Command "if (Test-Path './build/portable') { Compress-Archive -Path './build/portable/*' -DestinationPath './build/WersDL-portable-%CONFIG%.zip' -Force }"
powershell -Command "if (Test-Path './build/self-contained') { Compress-Archive -Path './build/self-contained/*' -DestinationPath './build/WersDL-self-contained-%CONFIG%.zip' -Force }"
echo [OK] Archives created in ./build/
echo.

echo ============================================
echo   BUILD COMPLETE!
echo ============================================
echo.
echo Output:
echo   Portable:  ./build/portable/
echo   Standalone: ./build/self-contained/
echo   ZIP:       ./build/WersDL-portable-%CONFIG%.zip
echo   ZIP:       ./build/WersDL-self-contained-%CONFIG%.zip
echo.
echo Usage: build.bat [Debug^|Release]
echo.
pause