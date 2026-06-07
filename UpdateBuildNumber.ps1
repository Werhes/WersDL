$buildFilePath = "$PSScriptRoot\BuildNumber.txt"
$verFilePath = "$PSScriptRoot\ver.json"

# Read and increment build number
$currentBuildNumber = [int](Get-Content $buildFilePath)
$currentBuildNumber++
$currentBuildNumber | Set-Content $buildFilePath

Write-Host "##vso[task.setvariable variable=BuildNumber]$currentBuildNumber"
Write-Host "[INFO] Build number incremented to: $currentBuildNumber"

# Sync ver.json
if (Test-Path $verFilePath) {
    $verJson = Get-Content $verFilePath -Raw | ConvertFrom-Json
    $verJson.build = "$currentBuildNumber"
    $verJson | ConvertTo-Json | Set-Content $verFilePath
    Write-Host "[INFO] ver.json updated to build: $currentBuildNumber"
} else {
    Write-Host "[WARN] ver.json not found, skipping sync"
}
