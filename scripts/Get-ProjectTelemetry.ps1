<#
.SYNOPSIS
    Generates a full source code dump and directory tree for the PMCR-O Federation.
    UPDATED v2.1: Includes gRPC Protos, MSBuild Props/Targets, and Hybrid infrastructure.
#>

param (
    [string]$RootPath = "$PSScriptRoot\..", # Assumes script is in /scripts. If in root, use "."
    
    # EXTENSIONS UPDATED: Added *.proto, *.props, *.targets for Hybrid Architecture logic.
    [string[]]$Extensions = @(
        "*.cs", "*.csproj", "*.sln", "*.proto", "*.props", "*.targets",
        "*.py", "*.pyproj", "requirements.txt", "Dockerfile",
        "*.xml", "*.json", "*.yaml", "*.md", "*.config"
    ),
    
    [string[]]$IgnoreFolders = @(
        "bin", "obj", ".git", ".vs", ".artifacts", "TestResults", 
        "packages", "__pycache__", ".venv", "venv", "egg-info"
    )
)

# 1. Setup Artifacts
$ArtifactsDir = Join-Path $RootPath ".artifacts"
if (-not (Test-Path $ArtifactsDir)) { New-Item -ItemType Directory -Path $ArtifactsDir | Out-Null }

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$OutputFile = Join-Path $ArtifactsDir "SourceDump_$Timestamp.txt"

Write-Host "🤖 PMCR-O Telemetry Extractor v2.1 initialized..." -ForegroundColor Cyan
Write-Host "📡 Capturing Mind (Code), Language (Protos), and Law (Props/Targets)..." -ForegroundColor Gray
Write-Host "📂 Scanning Root: $RootPath" -ForegroundColor Gray

$OutputBuffer = new-object System.Text.StringBuilder

# 2. Function: Generate Tree
function Get-Tree ($Path, $Indent = "") {
    $Items = Get-ChildItem -Path $Path -Directory | Where-Object { $IgnoreFolders -notcontains $_.Name }
    $Files = Get-ChildItem -Path $Path -File | Where-Object { $IgnoreFolders -notcontains $_.Name }
    
    foreach ($Item in $Items) {
        $OutputBuffer.AppendLine("$Indent└── 📁 $($Item.Name)/") | Out-Null
        Get-Tree $Item.FullName "$Indent    "
    }
    foreach ($File in $Files) {
        $Match = $false
        foreach ($Pattern in $Extensions) {
            if ($File.Name -like $Pattern) { $Match = $true; break }
        }

        if ($Match) {
            $OutputBuffer.AppendLine("$Indent    📄 $($File.Name)") | Out-Null
        }
    }
}

# 3. Execution: Tree
$OutputBuffer.AppendLine("==============================================================================") | Out-Null
$OutputBuffer.AppendLine("PROJECT TOPOLOGY (Tree)") | Out-Null
$OutputBuffer.AppendLine("==============================================================================") | Out-Null
Get-Tree $RootPath
$OutputBuffer.AppendLine("") | Out-Null

# 4. Execution: Content Dump
$OutputBuffer.AppendLine("==============================================================================") | Out-Null
$OutputBuffer.AppendLine("SOURCE CODE INGESTION") | Out-Null
$OutputBuffer.AppendLine("==============================================================================") | Out-Null

# Use -Include with the array to catch all patterns
$AllFiles = Get-ChildItem -Path $RootPath -Recurse -Include $Extensions | 
            Where-Object { 
                $Path = $_.FullName
                $ShouldIgnore = $false
                foreach ($Ignore in $IgnoreFolders) {
                    # Accurate folder boundary check
                    if ($Path -match "[\\/]$Ignore[\\/]" -or $Path -match "[\\/]$Ignore$") { 
                        $ShouldIgnore = $true; break 
                    }
                }
                return -not $ShouldIgnore
            }

foreach ($File in $AllFiles) {
    $RelativePath = $File.FullName.Replace($RootPath, "")
    Write-Host "   Reading: $RelativePath" -ForegroundColor DarkGray
    
    $OutputBuffer.AppendLine("------------------------------------------------------------------------------") | Out-Null
    $OutputBuffer.AppendLine("FILE: $RelativePath") | Out-Null
    $OutputBuffer.AppendLine("------------------------------------------------------------------------------") | Out-Null
    
    try {
        $Content = Get-Content $File.FullName -Raw
        if ([string]::IsNullOrWhiteSpace($Content)) {
            $OutputBuffer.AppendLine("[EMPTY FILE]") | Out-Null
        } else {
            $OutputBuffer.AppendLine($Content) | Out-Null
        }
    }
    catch {
        $OutputBuffer.AppendLine("[ERROR READING FILE: $($_.Exception.Message)]") | Out-Null
    }
    $OutputBuffer.AppendLine("") | Out-Null
}

# 5. Write to Disk
Set-Content -Path $OutputFile -Value $OutputBuffer.ToString() -Encoding UTF8

Write-Host "✅ Telemetry Extraction Complete." -ForegroundColor Green
Write-Host "   Mind, Language, and Law captured to: $OutputFile" -ForegroundColor Yellow