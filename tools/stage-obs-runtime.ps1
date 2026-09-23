#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [string]$Dest
)

$ErrorActionPreference = "Stop"
$Dest = $Dest.Trim().Trim('"', "'").TrimEnd('\', '/')
$root = Split-Path $PSScriptRoot -Parent
$obsRoot = Join-Path $root "third_party\obs"
$obsBin = Join-Path $obsRoot "bin\64bit"
$hostExe = Get-Item (Join-Path $root "native\Luma.ObsHost\build\bin\Luma.ObsHost.exe") -ErrorAction SilentlyContinue

if (-not $hostExe) {
    & (Join-Path $PSScriptRoot "build-obs-host.ps1")
    $hostExe = Get-ChildItem (Join-Path $root "native\Luma.ObsHost\build") -Recurse -Filter "Luma.ObsHost.exe" |
        Select-Object -First 1
}
if (-not $hostExe) {
    throw "Luma.ObsHost.exe not found."
}
if (-not (Test-Path (Join-Path $obsBin "obs.dll"))) {
    throw "OBS runtime not found. Run tools/fetch-obs-runtime.ps1 first."
}

New-Item -ItemType Directory -Force -Path $Dest | Out-Null
$destHost = Join-Path $Dest "Luma.ObsHost.exe"
if ([System.IO.Path]::GetFullPath($hostExe.FullName) -ne [System.IO.Path]::GetFullPath($destHost)) {
    Copy-Item $hostExe.FullName $destHost -Force
}

$skipDll = @(
    "Qt6Core.dll", "Qt6Gui.dll", "Qt6Network.dll", "Qt6Svg.dll", "Qt6Widgets.dll", "Qt6Xml.dll",
    "lua51.dll", "obs-frontend-api.dll", "obs-scripting.dll"
)
Get-ChildItem $obsBin -Filter *.dll | Where-Object { $skipDll -notcontains $_.Name } | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $Dest $_.Name) -Force
}
foreach ($exe in @("obs-ffmpeg-mux.exe", "obs-amf-test.exe", "obs-qsv-test.exe", "obs-nvenc-test.exe")) {
    $src = Join-Path $obsBin $exe
    if (Test-Path $src) {
        Copy-Item $src $Dest -Force
    }
}

$pluginKeep = @(
    "win-capture.dll", "win-wasapi.dll", "win-dshow.dll",
    "obs-x264.dll", "obs-ffmpeg.dll", "obs-nvenc.dll", "obs-qsv11.dll",
    "obs-outputs.dll", "image-source.dll", "text-freetype2.dll", "obs-text.dll",
    "obs-filters.dll", "coreaudio-encoder.dll"
)
$pluginDest = Join-Path $Dest "obs-plugins\64bit"
New-Item -ItemType Directory -Force -Path $pluginDest | Out-Null
$pluginSrc = Join-Path $obsRoot "obs-plugins\64bit"
foreach ($name in $pluginKeep) {
    $src = Join-Path $pluginSrc $name
    if (Test-Path $src) {
        Copy-Item $src $pluginDest -Force
    }
}

$dataSrc = Join-Path $obsRoot "data"
$dataDest = Join-Path $Dest "data"
New-Item -ItemType Directory -Force -Path (Join-Path $dataDest "libobs") | Out-Null
Copy-Item (Join-Path $dataSrc "libobs\*") (Join-Path $dataDest "libobs") -Recurse -Force
if (Test-Path (Join-Path $dataSrc "obs-studio")) {
    New-Item -ItemType Directory -Force -Path (Join-Path $dataDest "obs-studio") | Out-Null
    Copy-Item (Join-Path $dataSrc "obs-studio\*") (Join-Path $dataDest "obs-studio") -Recurse -Force
}

$pluginDataKeep = @(
    "win-capture", "win-wasapi", "win-dshow", "obs-x264", "obs-ffmpeg",
    "obs-nvenc", "obs-qsv11", "obs-outputs", "image-source", "text-freetype2",
    "obs-text", "obs-filters", "coreaudio-encoder"
)
foreach ($name in $pluginDataKeep) {
    $src = Join-Path $dataSrc "obs-plugins\$name"
    if (Test-Path $src) {
        $destDir = Join-Path $dataDest "obs-plugins\$name"
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
        Copy-Item (Join-Path $src "*") $destDir -Recurse -Force
    }
}

Write-Host ("Staged OBS runtime -> {0}" -f $Dest)
