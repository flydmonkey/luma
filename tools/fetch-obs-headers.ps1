#Requires -Version 5.1
param(
    [string]$Version = "31.1.2",
    [string]$OutDir = (Join-Path $PSScriptRoot "..\third_party\obs-src")
)

$ErrorActionPreference = "Stop"
$obsH = Join-Path $OutDir "libobs\obs.h"
if (Test-Path $obsH) {
    Write-Host "OBS headers already present at $obsH"
    exit 0
}

$work = Join-Path $env:TEMP "luma-obs-src-$Version"
$zip = Join-Path $work "obs-src.zip"
$extract = Join-Path $work "extract"
New-Item -ItemType Directory -Force -Path $work | Out-Null
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }

$url = "https://github.com/obsproject/obs-studio/archive/refs/tags/$Version.zip"
Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
Expand-Archive -Path $zip -DestinationPath $extract -Force

$libobs = Get-ChildItem $extract -Recurse -Directory -Filter "libobs" |
    Where-Object { Test-Path (Join-Path $_.FullName "obs.h") } |
    Select-Object -First 1
if (-not $libobs) { throw "libobs/obs.h not found in OBS $Version source" }

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
$dest = Join-Path $OutDir "libobs"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item (Join-Path $libobs.FullName "*") $dest -Recurse -Force
if (-not (Test-Path (Join-Path $dest "obs.h"))) { throw "obs.h missing after copy" }
Write-Host "OBS $Version headers ready at $dest"
