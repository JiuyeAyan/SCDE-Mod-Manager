$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$sourceRoot = Join-Path $projectRoot 'third_party\BepInEx_win_x64_5.4.23.5'
$buildRoot = Join-Path $PSScriptRoot 'build'
$payloadRoot = Join-Path $buildRoot 'payload'
$releaseRoot = Join-Path $projectRoot 'release'
$packagePath = Join-Path $releaseRoot 'bepinex-runtime-5.4.23.5.scdemod'

foreach ($required in @(
  (Join-Path $sourceRoot 'winhttp.dll'),
  (Join-Path $sourceRoot 'doorstop_config.ini'),
  (Join-Path $sourceRoot '.doorstop_version'),
  (Join-Path $sourceRoot 'BepInEx\core\BepInEx.dll')
)) {
  if (-not (Test-Path -LiteralPath $required)) {
    throw "Missing runtime file: $required"
  }
}

if (Test-Path -LiteralPath $buildRoot) {
  $resolvedBuild = Resolve-Path -LiteralPath $buildRoot
  if (-not $resolvedBuild.Path.StartsWith($PSScriptRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove unexpected build path: $resolvedBuild"
  }
  Remove-Item -LiteralPath $resolvedBuild.Path -Recurse -Force
}

New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $sourceRoot 'winhttp.dll') -Destination $payloadRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot 'doorstop_config.ini') -Destination $payloadRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot '.doorstop_version') -Destination $payloadRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot 'BepInEx') -Destination $payloadRoot -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination $buildRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.txt') -Destination (Join-Path $payloadRoot 'BepInEx\SCDE_RUNTIME_NOTICES.txt')

& node (Join-Path $PSScriptRoot 'package.js') $buildRoot $packagePath
if ($LASTEXITCODE -ne 0) {
  throw "Runtime packaging failed with exit code $LASTEXITCODE"
}

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Package: $packagePath"
Write-Output "SHA256: $hash"
