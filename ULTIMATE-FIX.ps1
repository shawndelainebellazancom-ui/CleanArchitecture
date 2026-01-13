# ============================================================================
# PMCR-O NuGet Recovery Script
# Fixes locked files, path length issues, and corrupted package cache
# ============================================================================

param(
    [string]$SolutionDir = "T:\agents\ProjectName",
    [switch]$Force,
    [switch]$UseGlobalCache
)

Write-Host "🔧 PMCR-O NuGet Recovery Tool" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

# ============================================================================
# STEP 1: KILL LOCKED PROCESSES
# ============================================================================

Write-Host "`n[1/6] Terminating processes that may lock files..." -ForegroundColor Yellow

$processesToKill = @(
    "dotnet",
    "MSBuild",
    "devenv",
    "ServiceHub.*",
    "PerfWatson2",
    "vbcscompiler"
)

foreach ($proc in $processesToKill) {
    Get-Process -Name $proc -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Killed $proc processes" -ForegroundColor Gray
}

Start-Sleep -Seconds 2

# ============================================================================
# STEP 2: REMOVE LOCAL PACKAGE DIRECTORY (WITH RETRIES)
# ============================================================================

Write-Host "`n[2/6] Removing local packages directory..." -ForegroundColor Yellow

$packagesDir = Join-Path $SolutionDir "packages"

if (Test-Path $packagesDir) {
    # Try normal deletion first
    try {
        Remove-Item $packagesDir -Recurse -Force -ErrorAction Stop
        Write-Host "  ✓ Removed $packagesDir" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️  Normal deletion failed, using robocopy method..." -ForegroundColor DarkYellow
        
        # Create empty temp directory
        $emptyDir = Join-Path $env:TEMP "empty_$(Get-Random)"
        New-Item -ItemType Directory -Path $emptyDir -Force | Out-Null
        
        # Use robocopy to mirror empty directory (effectively deleting)
        robocopy $emptyDir $packagesDir /MIR /R:0 /W:0 /NFL /NDL /NJH /NJS | Out-Null
        
        Remove-Item $emptyDir -Force
        Remove-Item $packagesDir -Force -ErrorAction SilentlyContinue
        
        Write-Host "  ✓ Forcefully removed $packagesDir" -ForegroundColor Green
    }
}
else {
    Write-Host "  ℹ️  No local packages directory found" -ForegroundColor Gray
}

# ============================================================================
# STEP 3: CLEAN GLOBAL NUGET CACHE
# ============================================================================

Write-Host "`n[3/6] Cleaning global NuGet cache..." -ForegroundColor Yellow

if ($UseGlobalCache) {
    dotnet nuget locals all --clear
    Write-Host "  ✓ Cleared all NuGet caches" -ForegroundColor Green
}
else {
    dotnet nuget locals http-cache --clear
    dotnet nuget locals temp --clear
    Write-Host "  ✓ Cleared HTTP and temp caches" -ForegroundColor Green
}

# ============================================================================
# STEP 4: CLEAN BUILD OUTPUTS
# ============================================================================

Write-Host "`n[4/6] Cleaning build outputs..." -ForegroundColor Yellow

Push-Location $SolutionDir

dotnet clean --nologo --verbosity quiet

# Remove bin/obj directories
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | 
    ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  ✓ Removed $($_.FullName)" -ForegroundColor Gray
    }

Pop-Location

# ============================================================================
# STEP 5: UPDATE NUGET.CONFIG FOR SHORTER PATHS
# ============================================================================

Write-Host "`n[5/6] Updating NuGet configuration..." -ForegroundColor Yellow

$nugetConfigPath = Join-Path $SolutionDir "nuget.config"

# Read existing config
[xml]$nugetConfig = Get-Content $nugetConfigPath

# Remove local packages folder config (use global cache instead)
$configNode = $nugetConfig.configuration.config
if ($configNode) {
    $globalPackagesNode = $configNode.SelectSingleNode("add[@key='globalPackagesFolder']")
    if ($globalPackagesNode) {
        $configNode.RemoveChild($globalPackagesNode) | Out-Null
        Write-Host "  ✓ Removed local globalPackagesFolder setting" -ForegroundColor Green
    }
}

# Save updated config
$nugetConfig.Save($nugetConfigPath)

Write-Host "  ✓ Updated nuget.config to use global cache" -ForegroundColor Green

# ============================================================================
# STEP 6: RESTORE PACKAGES
# ============================================================================

Write-Host "`n[6/6] Restoring NuGet packages..." -ForegroundColor Yellow

Push-Location $SolutionDir

# Restore with detailed logging to catch errors
$restoreOutput = dotnet restore --verbosity normal 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Package restore successful" -ForegroundColor Green
}
else {
    Write-Host "  ❌ Package restore failed" -ForegroundColor Red
    Write-Host $restoreOutput -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

# ============================================================================
# VERIFICATION
# ============================================================================

Write-Host "`n✅ Recovery Complete!" -ForegroundColor Green
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  1. Close all Visual Studio instances" -ForegroundColor White
Write-Host "  2. Run: dotnet build" -ForegroundColor White
Write-Host "  3. If issues persist, run this script with -UseGlobalCache" -ForegroundColor White

Write-Host "`nPackage Cache Location:" -ForegroundColor Cyan
Write-Host "  Global: $env:USERPROFILE\.nuget\packages" -ForegroundColor Gray