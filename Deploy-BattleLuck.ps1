[CmdletBinding()]
param(
    [string]$ServerPath = "C:\Users\ahmad\OneDrive\Desktop\DedicatedServerLauncher\VRisingServer",
    [string]$SourcePath = (Get-Location).Path,
    [switch]$Restart = $false
)

$ErrorActionPreference = "Stop"

$pluginSource = Join-Path $SourcePath "bin\Debug\net6.0\BattleLuck.dll"
$pluginDest = Join-Path $ServerPath "BepInEx\plugins\BattleLuck.dll"

$configSource = Join-Path $SourcePath "config"
$configDest = Join-Path $ServerPath "BepInEx\plugins\config"

$dataSource = Join-Path $SourcePath "Data"
$dataDest = Join-Path $ServerPath "BepInEx\plugins\Data"

Write-Host "=== BattleLuck Deployment ===" -ForegroundColor Cyan
Write-Host "Source: $SourcePath"
Write-Host "Server: $ServerPath"
Write-Host ""

$processName = "VRisingServer"
$existing = Get-Process -Name $processName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Server is running. Stopping server to deploy..." -ForegroundColor Yellow
    Stop-Process -Name $processName -Force
    Start-Sleep -Seconds 3
    
    $verify = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($verify) {
        Write-Host "Warning: Server process still running, forcing..." -ForegroundColor Red
        Stop-Process -Name $processName -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
    Write-Host "Server stopped." -ForegroundColor Green
}

if (-not (Test-Path -LiteralPath $pluginSource -PathType Leaf)) {
    throw "Build output not found. Please run 'dotnet build' first. Expected: $pluginSource"
}

Write-Host "Deploying plugin DLL..." -ForegroundColor Yellow
Copy-Item -LiteralPath $pluginSource -Destination $pluginDest -Force
Write-Host "Plugin deployed to: $pluginDest" -ForegroundColor Green

Write-Host "`nDeploying dependencies..." -ForegroundColor Yellow
$deps = @(
    "bin\Debug\net6.0\BouncyCastle.Cryptography.dll",
    "bin\Debug\net6.0\HookDOTS.API.dll"
)
foreach ($dep in $deps) {
    $src = Join-Path $SourcePath $dep
    $dst = Join-Path $ServerPath "BepInEx\plugins\$((Split-Path -Leaf $dep))"
    if (Test-Path -LiteralPath $src -PathType Leaf) {
        Copy-Item -LiteralPath $src -Destination $dst -Force
        Write-Host "  Deployed: $((Split-Path -Leaf $dep))"
    } else {
        Write-Host "  Skipped (not found): $((Split-Path -Leaf $dep))" -ForegroundColor DarkGray
    }
}

Write-Host "`nUpdating configuration..." -ForegroundColor Yellow
if (Test-Path -LiteralPath $configSource -PathType Container) {
    if (-not (Test-Path -LiteralPath $configDest -PathType Container)) {
        New-Item -ItemType Directory -Path $configDest -Force | Out-Null
    }
    
    $cfgFiles = Get-ChildItem -LiteralPath $configSource -File -Recurse | Where-Object { $_.Extension -match '\.(cfg|json|md)$' }
    foreach ($file in $cfgFiles) {
        $relative = $file.FullName.Substring($configSource.Length + 1)
        $destFile = Join-Path $configDest $relative
        $destDir = Split-Path -Parent $destFile
        if (-not (Test-Path -LiteralPath $destDir -PathType Container)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item -LiteralPath $file.FullName -Destination $destFile -Force
        Write-Host "  Updated config: $relative"
    }
    Write-Host "Configuration updated." -ForegroundColor Green
} else {
    Write-Host "  No config folder found at: $configSource" -ForegroundColor DarkYellow
}

Write-Host "`nUpdating data files..." -ForegroundColor Yellow
if (Test-Path -LiteralPath $dataSource -PathType Container) {
    if (-not (Test-Path -LiteralPath $dataDest -PathType Container)) {
        New-Item -ItemType Directory -Path $dataDest -Force | Out-Null
    }
    
    $dataFiles = Get-ChildItem -LiteralPath $dataSource -File -Recurse
    foreach ($file in $dataFiles) {
        $relative = $file.FullName.Substring($dataSource.Length + 1)
        $destFile = Join-Path $dataDest $relative
        $destDir = Split-Path -Parent $destFile
        if (-not (Test-Path -LiteralPath $destDir -PathType Container)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item -LiteralPath $file.FullName -Destination $destFile -Force
        Write-Host "  Updated data: $relative"
    }
    Write-Host "Data files updated." -ForegroundColor Green
} else {
    Write-Host "  No Data folder found at: $dataSource" -ForegroundColor DarkYellow
}

Write-Host "`nCopying manifest and metadata..." -ForegroundColor Yellow
$metadata = @("manifest.json", "README.md", "CHANGELOG.md", "LICENSE")
foreach ($meta in $metadata) {
    $src = Join-Path $SourcePath $meta
    if (Test-Path -LiteralPath $src -PathType Leaf) {
        $dst = Join-Path $ServerPath "BepInEx\plugins\$meta"
        Copy-Item -LiteralPath $src -Destination $dst -Force
        Write-Host "  Copied: $meta"
    }
}

if ($Restart) {
    Write-Host "`nRestarting server..." -ForegroundColor Yellow
    
    $serverExe = Join-Path $ServerPath "VRisingServer.exe"
    if (-not (Test-Path -LiteralPath $serverExe -PathType Leaf)) {
        throw "Server executable not found: $serverExe"
    }
    
    Write-Host "Starting server..." -ForegroundColor Yellow
    Start-Process -FilePath $serverExe -WorkingDirectory $ServerPath
    
    Start-Sleep -Seconds 5
    $verify = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($verify) {
        Write-Host "Server started successfully (PID: $($verify.Id))" -ForegroundColor Green
    } else {
        Write-Host "Warning: Server process not detected after 5 seconds. Check logs." -ForegroundColor Red
    }
} else {
    Write-Host "`nTo start the server, run:" -ForegroundColor Cyan
    Write-Host "  & `"$ServerPath\VRisingServer.exe`"" -ForegroundColor White
    Write-Host "`nOr use this script with -Restart flag to deploy and restart:" -ForegroundColor Cyan
    Write-Host "  .\Deploy-BattleLuck.ps1 -Restart" -ForegroundColor White
}

Write-Host "`n=== Deployment Complete ===" -ForegroundColor Cyan