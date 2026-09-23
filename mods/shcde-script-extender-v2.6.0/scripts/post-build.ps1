# This script runs after the main C# project has been successfully built.
# Its purpose is to gather all necessary build artifacts (managed DLLs, native DLLs, PDBs)
# and arrange them into the final, clean 'mod_output' directory structure,
# which is then used for packaging and deployment.
param (
    [string]$ArgsFile
)
$lines = Get-Content $ArgsFile

$TargetDir = $lines[0].Trim()
$ModOutputDir = $lines[1].Trim()
$TargetName = $lines[2].Trim()
$RootNamespace = $lines[3].Trim()
$SolutionDir = $lines[4].Trim()
$AppId = [int]$lines[5].Trim()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- Helper Function for Optimization ---
function Test-ShouldCopy {
    param([string]$Source, [string]$Dest)
    if (-not (Test-Path $Dest)) { return $true }
    # Copy if Source is newer than Destination
    if ((Get-Item $Source).LastWriteTime -gt (Get-Item $Dest).LastWriteTime) { return $true }
    return $false
}

# --- Setup Paths ---
# The final destination for the mod's plugin files.
Write-Host "DEBUG: Received parameters:"
Write-Host "TargetDir: $TargetDir"
Write-Host "ModOutputDir: $ModOutputDir"
Write-Host "TargetName: $TargetName"
Write-Host "RootNamespace: $RootNamespace"
Write-Host "SolutionDir: $SolutionDir"
Write-Host "AppId: $AppId"
$ModPluginsDir = Join-Path -Path $ModOutputDir -ChildPath ($RootNamespace.ToLower())

# Create the destination directory if it doesn't exist.
if (-not (Test-Path -Path $ModPluginsDir)) {
    Write-Host "INFO: Creating mod plugins directory: $ModPluginsDir"
    New-Item -Path $ModPluginsDir -ItemType Directory | Out-Null
}

# --- Copy Managed C# Artifacts ---
Write-Host "INFO: Copying managed DLLs and PDBs..."
$managedFilesToCopy = @(
    "$TargetName.dll", "$TargetName.pdb", "$TargetName.xml",
	"Iced.dll", 
	"MessagePack.Annotations.dll",
	"MessagePack.dll",
	"Microsoft.Bcl.AsyncInterfaces.dll",
	"Microsoft.Bcl.TimeProvider.dll",
	"Microsoft.Extensions.DependencyInjection.Abstractions.dll",
	"Microsoft.Extensions.DependencyInjection.dll",
	"Microsoft.Extensions.Logging.Abstractions.dll",
	"Microsoft.Extensions.Logging.dll",
	"Microsoft.Extensions.Options.dll",
	"Microsoft.Extensions.Primitives.dll",
	"Microsoft.NET.StringTools.dll",
	"PolyHook2.NET.dll", "PolyHook2.NET.pdb",
	"Serilog.dll", 
	"Serilog.Extensions.Logging.dll", 
	"Serilog.Sinks.Console.dll", 
	"Serilog.Sinks.File.dll",
	"System.Collections.Immutable.dll",
	"System.Buffers.dll", 
	"System.Diagnostics.DiagnosticSource.dll",
	"System.Memory.dll",
	"System.Numerics.Vectors.dll",
    "System.IO.Hashing.dll",
	"System.IO.Pipelines.dll",
	"System.Runtime.CompilerServices.Unsafe.dll",
	"System.Diagnostics.DiagnosticSource.dll",
	"System.ComponentModel.Annotations.dll",
	"System.Text.Encodings.Web.dll",
	"System.Text.Json.dll",
	"System.Threading.Channels.dll",
	"System.Threading.Tasks.Extensions.dll",
	"System.ValueTuple.dll",
	"NLua.dll",
	"KeraLua.dll",
	"lua54.dll",
	"liblua54.so",
	"liblua54.dylib",
	"R3.dll",
	"ICSharpCode.SharpZipLib.dll",
	"netstandard.dll",
	"System.Xml.Linq.dll",
	"NVorbis.dll",
    "Pfim.dll",
	"Zhuqiaomon.dll", "Zhuqiaomon.pdb",
    "RedBird.Abstractions.dll", "RedBird.Abstractions.pdb",
    "RedBird.Backends.NativeX64.dll", "RedBird.Backends.NativeX64.pdb",
    "RedBird.Core.dll", "RedBird.Core.pdb",
    "RedBird.X64.dll", "RedBird.X64.pdb",
    "RedBird.dll", "RedBird.pdb"
)
foreach ($file in $managedFilesToCopy) {
    $sourceFile = Join-Path -Path $TargetDir -ChildPath $file
    if (Test-Path $sourceFile) {
        $destFile = Join-Path -Path $ModPluginsDir -ChildPath $file
        
        # Only copy and log if the file is newer or missing
        if (Test-ShouldCopy -Source $sourceFile -Dest $destFile) {
            Copy-Item -Path $sourceFile -Destination $ModPluginsDir -Force
            Write-Host "Copied '$file'"
        }
    } else {
        Write-Warning "Could not find source file: $sourceFile"
    }
}

# --- Local Deployment (Windows-Only) ---
Write-Host "INFO: Attempting local deployment to game directory..."
try {
    # Import the helper functions (assuming steam-helper.psm1 is cross-platform)
    Import-Module -Name (Join-Path -Path $PSScriptRoot -ChildPath "steam-helper.psm1") -ErrorAction Stop
    
    # These functions will find the game on either Windows or Linux
    $steamInstallPath = Get-SteamInstallPath
    $libraryPaths = Get-SteamLibraryPaths -SteamInstallPath $steamInstallPath
    $gameInstallDir = Find-GameInstallPath -LibraryPaths $libraryPaths -AppId $AppId
    
    if ($gameInstallDir) {
        # The relative path to the mods folder is the same on both Windows and Linux.
        # Join-Path handles the OS-specific path separators automatically.
        $modsDir = Join-Path -Path $gameInstallDir -ChildPath "BepInEx" | Join-Path -ChildPath "plugins"

        if (Test-Path $modsDir) {
            $finalModDir = Join-Path -Path $modsDir -ChildPath ($RootNamespace.ToLower())
            
            Write-Host "INFO: Deploying mod to '$finalModDir'..."
            
            # Optimized recursive copy: Iterate and check timestamps individually
            $sourceDir = Join-Path $ModOutputDir ($RootNamespace.ToLower())
            
            Get-ChildItem -Path $sourceDir -Recurse | ForEach-Object {
                $relativePath = $_.FullName.Substring($sourceDir.Length).TrimStart('\', '/')
                $targetPath = Join-Path -Path $finalModDir -ChildPath $relativePath

                if ($_.PSIsContainer) {
                    if (-not (Test-Path $targetPath)) {
                        New-Item -Path $targetPath -ItemType Directory | Out-Null
                    }
                } else {
                    if (Test-ShouldCopy -Source $_.FullName -Dest $targetPath) {
                        Copy-Item -Path $_.FullName -Destination $targetPath -Force
                    }
                }
            }
            
            Write-Host "SUCCESS: Mod deployed locally."
        } else {
            Write-Warning "Could not find the mods directory at the expected path: '$modsDir'. Skipping local deployment."
        }

    } else {
        Write-Warning "Could not find game installation for AppId $AppId. Skipping local deployment."
    }
} catch {
    Write-Warning "An error occurred during local deployment. It may be skipped. Error: $_"
}

Write-Host "INFO: Post-build script finished."