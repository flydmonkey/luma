#Requires -Version 5.1
param(
    [string]$Version = "31.1.2",
    [string]$OutDir = (Join-Path $PSScriptRoot "..\third_party\obs")
)

$ErrorActionPreference = "Stop"
$release = "https://github.com/obsproject/obs-studio/releases/download/$Version"
$zipName = "OBS-Studio-$Version-Windows.zip"
$altZip = "OBS-Studio-$Version-Windows-x64.zip"
$work = Join-Path $env:TEMP "luma-obs-$Version"
New-Item -ItemType Directory -Force -Path $work, $OutDir | Out-Null

function Get-ObsZip {
    foreach ($name in @($zipName, $altZip)) {
        $url = "$release/$name"
        $dest = Join-Path $work $name
        Write-Host "Trying $url"
        try {
            Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
            return $dest
        } catch {
            Write-Host "Skip $name : $($_.Exception.Message)"
        }
    }

    throw "Could not download an official OBS Windows zip for $Version"
}

$zip = Get-ObsZip
$extract = Join-Path $work "extract"
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $extract -Force

$root = Get-ChildItem $extract -Directory | Select-Object -First 1
if (-not $root) { $root = Get-Item $extract }

$bin = @("bin\64bit", "bin\64bit\obs.dll", "obs-plugins\64bit", "data") |
    ForEach-Object { Join-Path $root.FullName $_ }

$obsDll = Get-ChildItem $extract -Recurse -Filter "obs.dll" | Select-Object -First 1
if (-not $obsDll) { throw "obs.dll not found in archive" }

$runtimeRoot = Split-Path (Split-Path $obsDll.FullName -Parent) -Parent
if ((Split-Path $obsDll.DirectoryName -Leaf) -eq "64bit") {
    $runtimeRoot = Split-Path $obsDll.DirectoryName -Parent
    if ((Split-Path $runtimeRoot -Leaf) -eq "bin") {
        $runtimeRoot = Split-Path $runtimeRoot -Parent
    }
}

Write-Host "Runtime root: $runtimeRoot"
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Copy-Item (Join-Path $runtimeRoot "*") $OutDir -Recurse -Force

Get-ChildItem $OutDir -Recurse -Include "obs64.exe","obs32.exe","obs-browser*.dll" -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

$dataKeep = @(
    "libobs", "obs-plugins", "locale"
)
# Keep plugin data needed for capture/encode/mux.
$pluginKeep = @(
    "win-capture", "win-dshow", "win-wasapi", "obs-x264", "obs-ffmpeg",
    "obs-nvenc", "obs-qsv11", "obs-outputs", "image-source", "text-freetype2"
)

$dll = Join-Path $OutDir "bin\64bit\obs.dll"
if (-not (Test-Path $dll)) {
    $dll = Get-ChildItem $OutDir -Recurse -Filter "obs.dll" | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $dll) { throw "obs.dll missing after copy" }
if (Get-ChildItem $OutDir -Recurse -Filter "obs64.exe") {
    throw "obs64.exe should have been removed"
}

Write-Host "OBS $Version runtime ready at $OutDir"
Write-Host "obs.dll = $dll"
