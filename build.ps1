<#
.SYNOPSIS
    Builds optimized Battery Reminder executables (Self-Contained and Framework-Dependent).
#>

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host "  Battery Reminder - Windows 11 x64 Optimizer & Builder" -ForegroundColor Cyan
Write-Host "  Windows 11 / Windows 10 build" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] 'dotnet' CLI was not found." -ForegroundColor Red
    Write-Host "Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

Write-Host "[1/3] Cleaning previous build folders..." -ForegroundColor Gray
if (Test-Path "publish") { Remove-Item -Recurse -Force "publish" }
if (Test-Path "publish-framework-dependent") { Remove-Item -Recurse -Force "publish-framework-dependent" }
if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }

Write-Host "[2/3] Compiling Target 1: Self-Contained Single-File (Optimized with compression)..." -ForegroundColor Gray

dotnet publish BatteryReminder.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Optimize=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:ServerGarbageCollection=false `
    -p:EventSourceSupport=false `
    -p:UseSystemResourceKeys=true `
    --output "./publish"

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Target 1 compilation failed." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[3/3] Compiling Target 2: Framework-Dependent Single-File (Ultra-compact)..." -ForegroundColor Gray

dotnet publish BatteryReminder.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:Optimize=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    --output "./publish-framework-dependent"

Write-Host ""
Write-Host "====================================================================" -ForegroundColor Green
Write-Host "  BUILD SUCCESSFUL - MEASURED OUTPUT SIZES:" -ForegroundColor Green
Write-Host "====================================================================" -ForegroundColor Green

$exe1 = Join-Path (Get-Location) "publish\BatteryReminder.exe"
if (Test-Path $exe1) {
    $size1 = [math]::Round((Get-Item $exe1).Length / 1MB, 2)
    Write-Host "  1. Self-Contained (No runtime needed): $exe1" -ForegroundColor Yellow
    Write-Host "     Size: $size1 MB (Optimized down from ~65 MB)" -ForegroundColor White
}

$exe2 = Join-Path (Get-Location) "publish-framework-dependent\BatteryReminder.exe"
if (Test-Path $exe2) {
    $size2 = [math]::Round((Get-Item $exe2).Length / 1MB, 2)
    Write-Host "  2. Framework-Dependent (Needs .NET 8 Runtime): $exe2" -ForegroundColor Cyan
    Write-Host "     Size: $size2 MB (Ultra-compact)" -ForegroundColor White
}
