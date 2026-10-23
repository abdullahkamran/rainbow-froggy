# Build the WebGL player and serve it on the LAN.
#   ./scripts/play-webgl.ps1            build + serve
#   ./scripts/play-webgl.ps1 -NoBuild   serve the last build
param([int]$Port = 8080, [switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out  = Join-Path $root 'Builds/WebGL'

if (-not $NoBuild) {
    unity build --target WebGL --execute-method Build.PerformWebGLBuild -buildOutput $out
    if ($LASTEXITCODE -ne 0) { throw "WebGL build failed" }
}
if (-not (Test-Path (Join-Path $out 'index.html'))) { throw "No build at $out" }

node (Join-Path $PSScriptRoot 'serve.js') $out $Port
