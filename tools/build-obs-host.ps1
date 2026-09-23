#Requires -Version 5.1
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$obsDll = Join-Path $root "third_party\obs\bin\64bit\obs.dll"
$hostDir = Join-Path $root "native\Luma.ObsHost"
$libDir = Join-Path $hostDir "lib"
$buildDir = Join-Path $hostDir "build"
$defFile = Join-Path $libDir "obs.def"
$libFile = Join-Path $libDir "obs.lib"

if (-not (Test-Path $obsDll)) {
    throw "obs.dll not found. Run tools/fetch-obs-runtime.ps1 first."
}

function Find-VcVars {
    $candidates = @(
        'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat',
        'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat'
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    $found = Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio' -Recurse -Filter vcvars64.bat -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'Auxiliary\\Build' } |
        Select-Object -First 1
    if ($found) { return $found.FullName }
    throw "vcvars64.bat not found."
}

New-Item -ItemType Directory -Force -Path $libDir, $buildDir | Out-Null

$vcvars = Find-VcVars
Write-Host "Using $vcvars"

$exportsFile = Join-Path $libDir "obs.exports.txt"
$genLines = @(
    '@echo off',
    ('call "{0}"' -f $vcvars),
    ('dumpbin /exports "{0}" > "{1}"' -f $obsDll, $exportsFile)
)
$genScript = Join-Path $libDir "gen-obs-lib.cmd"
Set-Content -Path $genScript -Value $genLines -Encoding ASCII
cmd /c $genScript
if ($LASTEXITCODE -ne 0) { throw "dumpbin /exports failed." }

$exports = Get-Content $exportsFile
$names = foreach ($line in $exports) {
    if ($line -match '^\s+\d+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+(\S+)') {
        $Matches[1]
    }
}
if (-not $names) {
    throw "Could not parse exports from obs.dll."
}

$def = New-Object System.Collections.Generic.List[string]
[void]$def.Add("LIBRARY obs")
[void]$def.Add("EXPORTS")
foreach ($name in $names) {
    [void]$def.Add(("    {0}" -f $name))
}
Set-Content -Path $defFile -Value $def -Encoding ASCII
Write-Host ("Generated {0} exports" -f $names.Count)

$libLines = @(
    '@echo off',
    ('call "{0}"' -f $vcvars),
    ('lib /nologo /def:"{0}" /machine:x64 /out:"{1}"' -f $defFile, $libFile)
)
$libScript = Join-Path $libDir "link-obs-lib.cmd"
Set-Content -Path $libScript -Value $libLines -Encoding ASCII
cmd /c $libScript
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $libFile)) {
    throw "Failed to generate obs.lib."
}

Remove-Item (Join-Path $buildDir "CMakeCache.txt") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $buildDir "CMakeFiles") -Recurse -Force -ErrorAction SilentlyContinue

$cmakeScript = Join-Path $buildDir "configure-build.cmd"
@(
    '@echo off',
    ('call "{0}"' -f $vcvars),
    ('cd /d "{0}"' -f $buildDir),
    ('cmake -G "Ninja" -DCMAKE_BUILD_TYPE={0} "{1}"' -f $Configuration, $hostDir),
    'if errorlevel 1 exit /b 1',
    ('cmake --build . --config {0}' -f $Configuration)
) | Set-Content -Path $cmakeScript -Encoding ASCII
cmd /c $cmakeScript
if ($LASTEXITCODE -ne 0) { throw "Failed to build Luma.ObsHost." }

$built = Get-ChildItem $buildDir -Recurse -Filter "Luma.ObsHost.exe" | Select-Object -First 1
if (-not $built) { throw "Build finished but Luma.ObsHost.exe was not found." }
Write-Host ("Built {0}" -f $built.FullName)
