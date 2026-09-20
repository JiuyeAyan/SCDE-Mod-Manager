# This script updates the version number in various project files. It is
# designed to be called by semantic-release. It expects the new version number
# as its first and only argument.

param (
    [Parameter(Mandatory=$true)]
    [string]$NewVersion
)

# Strict mode for better error handling.
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# --- Define File Paths ---
$PluginCsPath = Join-Path -Path $PSScriptRoot -ChildPath "../src/SHCDESE.BepInEx/Bootstrap/Plugin.cs"
$CsprojPath = Join-Path -Path $PSScriptRoot -ChildPath "../src/SHCDESE.BepInEx/SHCDESE.csproj"
$InfoJsonPath = Join-Path -Path $PSScriptRoot -ChildPath "../deps/info.json"

Write-Host "Bumping versions to $NewVersion..."

# --- Logic: Sanitize SemVer for BepInEx/Windows ---
# BepInEx and AssemblyVersion require strict Major.Minor.Build.Revision format (System.Version).
# They crash on hyphens.
$SemVer = $NewVersion
$SafeVersion = "0.0.0.0"

# Regex Explanation:
# Captures Major.Minor.Patch (Group 1, 2, 3)
# Optionally looks for a hyphen followed by anything, ending in a dot and a number (Group 4)
# Example: 1.2.3-preview.5 -> Matches. Group 4 is "5".
if ($SemVer -match "^(\d+)\.(\d+)\.(\d+)(?:.*\.(\d+))?$") {
    $Major = $Matches[1]
    $Minor = $Matches[2]
    $Patch = $Matches[3]
    
    # If we found a pre-release number (like the '5' in preview.5), use it. 
    # Otherwise default to 0.
    if ($Matches[4]) {
        $Revision = $Matches[4]
    } else {
        $Revision = "0"
    }

    $SafeVersion = "$Major.$Minor.$Patch.$Revision"
} else {
    Write-Warning "Could not parse SemVer '$SemVer' into strict numeric format. Fallback to 1.0.0.0"
    $SafeVersion = "1.0.0.0"
}

Write-Host "Full SemVer (for NuGet): '$SemVer'"
Write-Host "Safe Version (for BepInEx/Assembly): '$SafeVersion'"

# --- Update the C# constant in Plugin.cs ---

# We MUST use $SafeVersion here so [BepInPlugin] doesn't crash
Write-Host "Updating '$PluginCsPath'..."
$pluginCsContent = Get-Content -Path $PluginCsPath -Raw
$newPluginCsContent = $pluginCsContent -replace '(public const string PLUGIN_VERSION = )".*";', "`$1`"$SafeVersion`";"
$lfPluginCsContent = $newPluginCsContent.Replace("`r`n", "`n")

[System.IO.File]::WriteAllText($PluginCsPath, $lfPluginCsContent, [System.Text.UTF8Encoding]::new($false))

# --- Update the version in the .csproj file ---
Write-Host "Updating '$CsprojPath' using XML parser..."
try {
    [xml]$csprojXml = Get-Content -Path $CsprojPath

    $versionNode = $csprojXml.SelectSingleNode("/Project/PropertyGroup/Version")
    $assemblyVersionNode = $csprojXml.SelectSingleNode("/Project/PropertyGroup/AssemblyVersion")
    $fileVersionNode = $csprojXml.SelectSingleNode("/Project/PropertyGroup/FileVersion")

    # Update the <Version> tag with the FULL SemVer string.
    # NuGet/MSBuild handles SemVer fine, and this ensures the file name is 'SHCDESE-1.2.3-preview.1.nupkg' (or dll metadata)
    if ($null -ne $versionNode) {
        $versionNode.'#text' = $SemVer
        Write-Host "Set <Version> to $SemVer"
    }
    
    # Update Assembly and File versions with the SAFE numeric-only version.
    # Windows File Properties and Assembly loaders require this strict format.
    if ($null -ne $assemblyVersionNode) {
        $assemblyVersionNode.'#text' = $SafeVersion
        Write-Host "Set <AssemblyVersion> to $SafeVersion"
    }
    if ($null -ne $fileVersionNode) {
        $fileVersionNode.'#text' = $SafeVersion
        Write-Host "Set <FileVersion> to $SafeVersion"
    }

    $csprojXml.Save($CsprojPath)
    Write-Host "Successfully updated XML properties in '$CsprojPath'."
}
catch {
    Write-Error "Failed to update XML in '$CsprojPath'. Error: $_"
    exit 1
}

# --- Update the info.json file ---
Write-Host "Updating '$InfoJsonPath'..."
if (Test-Path $InfoJsonPath) {
    try {
        $jsonContent = Get-Content -Path $InfoJsonPath -Raw
        
        $newJsonContent = $jsonContent -replace '("Version"\s*:\s*)".*"', "`$1`"$SemVer`""
        
        [System.IO.File]::WriteAllText($InfoJsonPath, $newJsonContent, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Successfully updated JSON version in '$InfoJsonPath'."
    }
    catch {
        Write-Error "Failed to update '$InfoJsonPath'. Error: $_"
        exit 1
    }
} else {
    Write-Warning "File '$InfoJsonPath' does not exist. Skipping."
}

Write-Host "Version bumping complete for all files."