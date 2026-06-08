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
for /f "tokens=*" %%a in ('dotnet --version 2^>nul') do set DOTNET_VER=%%a
if "%DOTNET_VER%"=="" set DOTNET_VER=unknown
echo [INFO] .NET SDK version: %DOTNET_VER%

:: Configuration
set CONFIG=Release
if not "%1"=="" set CONFIG=%1

echo [INFO] Configuration: %CONFIG%
echo.

:: Download dependencies if missing
echo [STEP 0/5] Checking dependencies...
if not exist "yt-dlp.exe" (
    echo [INFO] Downloading yt-dlp.exe...
    powershell -Command "Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile 'yt-dlp.exe'"
    if exist "yt-dlp.exe" ( echo [OK] yt-dlp.exe downloaded. ) else ( echo [WARN] Failed to download yt-dlp.exe )
) else (
    echo [OK] yt-dlp.exe found.
)

if not exist "ffmpeg.exe" (
    echo [INFO] Downloading ffmpeg.exe...
    powershell -Command "$tmp = [System.IO.Path]::GetTempPath(); $zip = $tmp + 'ffmpeg.zip'; Invoke-WebRequest -Uri 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip' -OutFile $zip; Expand-Archive -Path $zip -DestinationPath $tmp -Force; $exe = Get-ChildItem -Path $tmp -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1; if ($exe) { Copy-Item -Path $exe.FullName -Destination 'ffmpeg.exe' -Force; Write-Host '[OK] ffmpeg.exe downloaded.' } else { Write-Host '[WARN] Could not extract ffmpeg.exe' }; Remove-Item -Path $zip -Force -ErrorAction SilentlyContinue"
    if exist "ffmpeg.exe" ( echo [OK] ffmpeg.exe downloaded. ) else ( echo [WARN] ffmpeg.exe not found. Download manually from https://ffmpeg.org/ )
) else (
    echo [OK] ffmpeg.exe found.
)
echo.

:: Restore packages
echo [STEP 1/5] Restoring NuGet packages...
call dotnet restore YT-DLP-GUI.slnx
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Package restore failed!
    pause
    exit /b 1
)
echo [OK] Packages restored successfully.
echo.

:: Build
echo [STEP 2/5] Building project (%CONFIG%)...
call dotnet build YT-DLP-GUI.slnx --configuration %CONFIG% --no-restore
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)
echo [OK] Build completed successfully.
echo.

:: Copy dependencies to build output
echo [STEP 3/5] Copying dependencies to build output...
if exist "yt-dlp.exe" (
    if exist ".\bin\%CONFIG%\net8.0-windows" copy /Y "yt-dlp.exe" ".\bin\%CONFIG%\net8.0-windows\" >nul
)
if exist "ffmpeg.exe" (
    if exist ".\bin\%CONFIG%\net8.0-windows" copy /Y "ffmpeg.exe" ".\bin\%CONFIG%\net8.0-windows\" >nul
)
echo [OK] Dependencies copied.
echo.

:: Publish portable
echo [STEP 4/5] Publishing portable version...
call dotnet publish YT-DLP-GUI.csproj --configuration %CONFIG% --runtime win-x64 --self-contained false --output ./build/portable
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Portable publish failed!
    pause
    exit /b 1
)
:: Copy deps to portable
if exist "yt-dlp.exe" copy /Y "yt-dlp.exe" ".\build\portable\" >nul
if exist "ffmpeg.exe" copy /Y "ffmpeg.exe" ".\build\portable\" >nul
echo [OK] Portable version published to ./build/portable/
echo.

:: Publish self-contained
echo [STEP 5/5] Publishing self-contained version...
call dotnet publish YT-DLP-GUI.csproj --configuration %CONFIG% --runtime win-x64 --self-contained true --output ./build/self-contained
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Self-contained publish failed!
    pause
    exit /b 1
)
:: Copy deps to self-contained
if exist "yt-dlp.exe" copy /Y "yt-dlp.exe" ".\build\self-contained\" >nul
if exist "ffmpeg.exe" copy /Y "ffmpeg.exe" ".\build\self-contained\" >nul
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