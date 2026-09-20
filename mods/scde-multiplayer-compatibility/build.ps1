param(
  [string]$GameDir = 'D:\0_zhuangji\Softwares\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$bepInExRoot = Join-Path $projectRoot 'third_party\BepInEx_win_x64_5.4.23.5'
$managedRoot = Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Managed'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$buildRoot = Join-Path $PSScriptRoot 'build'
$payloadRoot = Join-Path $buildRoot 'payload'
$pluginRoot = Join-Path $payloadRoot 'BepInEx\plugins\SCDEMultiplayerCompatibility'
$releaseRoot = Join-Path $projectRoot 'release'
$packagePath = Join-Path $releaseRoot 'scde-multiplayer-compatibility-0.3.1.scdemod'

foreach ($required in @(
  $compiler,
  (Join-Path $bepInExRoot 'BepInEx\core\BepInEx.dll'),
  (Join-Path $bepInExRoot 'BepInEx\core\0Harmony.dll'),
  (Join-Path $managedRoot 'Assembly-CSharp.dll'),
  (Join-Path $managedRoot 'Noesis.NoesisGUI.dll'),
  (Join-Path $managedRoot 'com.rlabrecque.steamworks.net.dll'),
  (Join-Path $managedRoot 'UnityEngine.TextRenderingModule.dll')
)) {
  if (-not (Test-Path -LiteralPath $required)) {
    throw "Missing build dependency: $required"
  }
}

if (Test-Path -LiteralPath $buildRoot) {
  $resolvedBuild = Resolve-Path -LiteralPath $buildRoot
  if (-not $resolvedBuild.Path.StartsWith($PSScriptRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove unexpected build path: $resolvedBuild"
  }
  Remove-Item -LiteralPath $resolvedBuild.Path -Recurse -Force
}

New-Item -ItemType Directory -Path $pluginRoot -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Destination $buildRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination $pluginRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.txt') -Destination $pluginRoot

$references = @(
  (Join-Path $bepInExRoot 'BepInEx\core\BepInEx.dll'),
  (Join-Path $bepInExRoot 'BepInEx\core\0Harmony.dll'),
  (Join-Path $managedRoot 'Assembly-CSharp.dll'),
  (Join-Path $managedRoot 'Noesis.NoesisGUI.dll'),
  (Join-Path $managedRoot 'com.rlabrecque.steamworks.net.dll'),
  (Join-Path $managedRoot 'UnityEngine.dll'),
  (Join-Path $managedRoot 'UnityEngine.CoreModule.dll'),
  (Join-Path $managedRoot 'UnityEngine.IMGUIModule.dll'),
  (Join-Path $managedRoot 'UnityEngine.TextRenderingModule.dll')
)
$compilerArgs = @(
  '/nologo',
  '/target:library',
  '/optimize+',
  "/out:$pluginRoot\SCDEMultiplayerCompatibility.dll"
)
$compilerArgs += $references | ForEach-Object { "/reference:$_" }
$compilerArgs += (Join-Path $PSScriptRoot 'src\SCDEMultiplayerCompatibilityPlugin.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'src\ScriptExtenderBridge.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'src\SeWorkshopMetadata.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'src\StartupReadyReporter.cs')

& $compiler $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Plugin compilation failed with exit code $LASTEXITCODE" }

$testRoot = Join-Path $buildRoot 'test'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$harmonyPath = Join-Path $bepInExRoot 'BepInEx\core\0Harmony.dll'
$cecilPath = Join-Path $projectRoot 'tools\Mono.Cecil.dll'
Copy-Item -LiteralPath $cecilPath -Destination $testRoot
& $compiler /nologo /target:exe /optimize+ "/reference:$cecilPath" "/out:$testRoot\SparseKeyMapLoadTests.exe" (Join-Path $PSScriptRoot 'test\SparseKeyMapLoadTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Sparse key-map regression test compilation failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'test\SparseKeyMapLoadTests.runtimeconfig.json') -Destination $testRoot
& dotnet (Join-Path $testRoot 'SparseKeyMapLoadTests.exe') $managedRoot (Split-Path $harmonyPath) (Join-Path $pluginRoot 'SCDEMultiplayerCompatibility.dll')
if ($LASTEXITCODE -ne 0) { throw 'Sparse key-map regression test failed' }

& $compiler /nologo /target:exe /optimize+ "/out:$testRoot\RuntimeProfileTests.exe" (Join-Path $PSScriptRoot 'test\RuntimeProfileTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Runtime profile test compilation failed' }
# The game's bundled Harmony targets the desktop CLR, not the .NET 8 test host.
& (Join-Path $testRoot 'RuntimeProfileTests.exe') $managedRoot (Split-Path $harmonyPath) (Join-Path $pluginRoot 'SCDEMultiplayerCompatibility.dll')
if ($LASTEXITCODE -ne 0) { throw 'Runtime profile tests failed' }

& node (Join-Path $PSScriptRoot 'package.js') $buildRoot $packagePath
if ($LASTEXITCODE -ne 0) { throw "Mod packaging failed with exit code $LASTEXITCODE" }

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "Package: $packagePath"
Write-Output "SHA256: $hash"
