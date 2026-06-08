# WersDL Build Script (PowerShell)
# Usage: .\build.ps1 [-Configuration <Debug|Release>] [-Clean] [-DownloadDeps]

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean,
    [switch]$DownloadDeps
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = $PSScriptRoot
$BuildOutput = Join-Path $ProjectRoot 'build'

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  WersDL - Auto Build Script (PowerShell)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "[INFO] .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] .NET SDK not found! Please install .NET 8 SDK." -ForegroundColor Red
    Write-Host "        https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

# Download dependencies (yt-dlp.exe and ffmpeg.exe)
if ($DownloadDeps) {
    Write-Host "[STEP 0/5] Checking dependencies..." -ForegroundColor Yellow

    $ytdlpPath = Join-Path $ProjectRoot 'yt-dlp.exe'
    if (-not (Test-Path $ytdlpPath)) {
        Write-Host "[INFO] Downloading yt-dlp.exe..." -ForegroundColor Yellow
        Invoke-WebRequest -Uri 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile $ytdlpPath
        Write-Host "[OK] yt-dlp.exe downloaded." -ForegroundColor Green
    } else {
        Write-Host "[OK] yt-dlp.exe found." -ForegroundColor Green
    }

    $ffmpegPath = Join-Path $ProjectRoot 'ffmpeg.exe'
    if (-not (Test-Path $ffmpegPath)) {
        Write-Host "[INFO] Downloading ffmpeg.exe..." -ForegroundColor Yellow
        $ffmpegZip = Join-Path $env:TEMP 'ffmpeg.zip'
        Invoke-WebRequest -Uri 'https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip' -OutFile $ffmpegZip
        Expand-Archive -Path $ffmpegZip -DestinationPath $env:TEMP -Force
        $ffmpegExe = Get-ChildItem -Path $env:TEMP -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
        if ($ffmpegExe) {
            Copy-Item -Path $ffmpegExe.FullName -Destination $ffmpegPath -Force
            Write-Host "[OK] ffmpeg.exe downloaded." -ForegroundColor Green
        } else {
            Write-Host "[WARN] Could not extract ffmpeg.exe. Download manually." -ForegroundColor Yellow
        }
        Remove-Item -Path $ffmpegZip -Force -ErrorAction SilentlyContinue
    } else {
        Write-Host "[OK] ffmpeg.exe found." -ForegroundColor Green
    }
}

# Clean if requested
if ($Clean -and (Test-Path $BuildOutput)) {
    Write-Host "[INFO] Cleaning build output..." -ForegroundColor Yellow
    Remove-Item -Path $BuildOutput -Recurse -Force
    Write-Host "[OK] Build output cleaned." -ForegroundColor Green
}

# Restore packages
Write-Host "[STEP 1/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore (Join-Path $ProjectRoot 'YT-DLP-GUI.slnx')
if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
Write-Host "[OK] Packages restored successfully." -ForegroundColor Green

# Build
Write-Host "[STEP 2/5] Building project ($Configuration)..." -ForegroundColor Yellow
dotnet build (Join-Path $ProjectRoot 'YT-DLP-GUI.csproj') --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "[OK] Build completed successfully." -ForegroundColor Green

# Copy dependencies to build output
Write-Host "[STEP 3/5] Copying dependencies to build output..." -ForegroundColor Yellow
$ytdlpPath = Join-Path $ProjectRoot 'yt-dlp.exe'
$ffmpegPath = Join-Path $ProjectRoot 'ffmpeg.exe'
$outputDirs = @()
if (Test-Path (Join-Path $BuildOutput 'portable')) { $outputDirs += (Join-Path $BuildOutput 'portable') }
if (Test-Path (Join-Path $BuildOutput 'self-contained')) { $outputDirs += (Join-Path $BuildOutput 'self-contained') }
# Also copy to the direct build output
$buildOut = Join-Path $ProjectRoot 'bin' $Configuration 'net8.0-windows'
if (Test-Path $buildOut) { $outputDirs += $buildOut }

foreach ($dir in $outputDirs) {
    if (Test-Path $ytdlpPath) { Copy-Item -Path $ytdlpPath -Destination $dir -Force -ErrorAction SilentlyContinue }
    if (Test-Path $ffmpegPath) { Copy-Item -Path $ffmpegPath -Destination $dir -Force -ErrorAction SilentlyContinue }
}
Write-Host "[OK] Dependencies copied." -ForegroundColor Green

# Publish portable
Write-Host "[STEP 4/5] Publishing portable version..." -ForegroundColor Yellow
dotnet publish (Join-Path $ProjectRoot 'YT-DLP-GUI.csproj') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained $false `
    --output (Join-Path $BuildOutput 'portable')
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed" }
# Copy deps again after publish
if (Test-Path $ytdlpPath) { Copy-Item -Path $ytdlpPath -Destination (Join-Path $BuildOutput 'portable') -Force -ErrorAction SilentlyContinue }
if (Test-Path $ffmpegPath) { Copy-Item -Path $ffmpegPath -Destination (Join-Path $BuildOutput 'portable') -Force -ErrorAction SilentlyContinue }
Write-Host "[OK] Portable version published to: $BuildOutput\portable\" -ForegroundColor Green

# Publish self-contained
Write-Host "[STEP 5/5] Publishing self-contained version..." -ForegroundColor Yellow
dotnet publish (Join-Path $ProjectRoot 'YT-DLP-GUI.csproj') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained $true `
    --output (Join-Path $BuildOutput 'self-contained')
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed" }
# Copy deps again after publish
if (Test-Path $ytdlpPath) { Copy-Item -Path $ytdlpPath -Destination (Join-Path $BuildOutput 'self-contained') -Force -ErrorAction SilentlyContinue }
if (Test-Path $ffmpegPath) { Copy-Item -Path $ffmpegPath -Destination (Join-Path $BuildOutput 'self-contained') -Force -ErrorAction SilentlyContinue }
Write-Host "[OK] Self-contained version published to: $BuildOutput\self-contained\" -ForegroundColor Green

# Create ZIP archives
Write-Host "[INFO] Creating ZIP archives..." -ForegroundColor Yellow
if (Test-Path (Join-Path $BuildOutput 'portable')) {
    Compress-Archive -Path "$BuildOutput\portable\*" -DestinationPath "$BuildOutput\WersDL-portable-$Configuration.zip" -Force
    Write-Host "[OK] ZIP: $BuildOutput\WersDL-portable-$Configuration.zip" -ForegroundColor Green
}
if (Test-Path (Join-Path $BuildOutput 'self-contained')) {
    Compress-Archive -Path "$BuildOutput\self-contained\*" -DestinationPath "$BuildOutput\WersDL-self-contained-$Configuration.zip" -Force
    Write-Host "[OK] ZIP: $BuildOutput\WersDL-self-contained-$Configuration.zip" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  BUILD COMPLETE!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output:" -ForegroundColor White
Write-Host "  Portable:       $BuildOutput\portable\" -ForegroundColor White
Write-Host "  Self-contained: $BuildOutput\self-contained\" -ForegroundColor White
Write-Host "  ZIP portable:   $BuildOutput\WersDL-portable-$Configuration.zip" -ForegroundColor White
Write-Host "  ZIP standalone: $BuildOutput\WersDL-self-contained-$Configuration.zip" -ForegroundColor White
Write-Host ""
Write-Host "Usage: .\build.ps1 [-Configuration Debug|Release] [-Clean] [-DownloadDeps]" -ForegroundColor Gray