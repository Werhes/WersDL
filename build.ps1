# WersDL Build Script (PowerShell)
# Usage: .\build.ps1 [-Configuration <Debug|Release>] [-Clean]

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
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

# Clean if requested
if ($Clean -and (Test-Path $BuildOutput)) {
    Write-Host "[INFO] Cleaning build output..." -ForegroundColor Yellow
    Remove-Item -Path $BuildOutput -Recurse -Force
    Write-Host "[OK] Build output cleaned." -ForegroundColor Green
}

# Restore packages
Write-Host "[STEP 1/4] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore (Join-Path $ProjectRoot 'YT-DLP-GUI.slnx')
if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
Write-Host "[OK] Packages restored successfully." -ForegroundColor Green

# Build
Write-Host "[STEP 2/4] Building project ($Configuration)..." -ForegroundColor Yellow
dotnet build (Join-Path $ProjectRoot 'YT-DLP-GUI.csproj') --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "[OK] Build completed successfully." -ForegroundColor Green

# Publish portable
Write-Host "[STEP 3/4] Publishing portable version..." -ForegroundColor Yellow
dotnet publish (Join-Path $ProjectRoot 'YT-DLP-GUI.csproj') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained $false `
    --output (Join-Path $BuildOutput 'portable')
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed" }
Write-Host "[OK] Portable version published to: $BuildOutput\portable\" -ForegroundColor Green

# Publish self-contained
Write-Host "[STEP 4/4] Publishing self-contained version..." -ForegroundColor Yellow
dotnet publish (Join-Path $ProjectRoot 'YT-DLP-GUI.csproj') `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained $true `
    --output (Join-Path $BuildOutput 'self-contained')
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed" }
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
Write-Host "Usage: .\build.ps1 [-Configuration Debug|Release] [-Clean]" -ForegroundColor Gray