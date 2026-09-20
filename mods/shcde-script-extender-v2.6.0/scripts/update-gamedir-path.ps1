# DESCRIPTION:
# Finds the installation directory of a Steam game and updates the <UnityLibsPath>
# tag in a Directory.Build.props file with the discovered path.

param (
    # The Steam Application ID of the game you want to find.
    [Parameter(Mandatory=$true)]
    [int]$AppId,

    # The path to the .props file that contains the <UnityLibsPath> tag.
    [Parameter(Mandatory=$true)]
    [string]$PropsFilePath
)

# --- 1. Validate that the props file exists ---
if (-not (Test-Path $PropsFilePath)) {
    Write-Error "Props file not found at: '$PropsFilePath'"
    return
}

# --- 2. Find the game installation directory using the Steam helper ---
# Assumes "steam-helper.psm1" is in the same directory as this script.
$helperPath = Join-Path -Path $PSScriptRoot -ChildPath "steam-helper.psm1"
if (-not (Test-Path $helperPath)) {
    Write-Error "Could not find 'steam-helper.psm1'. Make sure it's in the same directory as this script."
    return
}
Import-Module -Name $helperPath

# Find the game install directory
$steamInstallPath = Get-SteamInstallPath
$libraryPaths = Get-SteamLibraryPaths -SteamInstallPath $steamInstallPath
$gameInstallDir = Find-GameInstallPath -LibraryPaths $libraryPaths -AppId $AppId

if (-not $gameInstallDir) {
    Write-Error "Could not find game installation directory for AppId '$AppId'. Is the game installed?"
    return
}

Write-Host "Found game installation directory: $gameInstallDir"

# --- 3. Load the .props file and update the path ---
try {
    # Load the XML document
    $xmlDoc = New-Object System.Xml.XmlDocument
    $xmlDoc.Load($PropsFilePath)

    # Find the <UnityLibsPath> node.
    $propertyNode = $xmlDoc.SelectSingleNode("//PropertyGroup/UnityLibsPath")

    if ($null -eq $propertyNode) {
        Write-Error "<UnityLibsPath> node not found in '$PropsFilePath'. Nothing to update."
        return
    }

    # Update the path inside the tag
    $propertyNode.InnerText = $gameInstallDir

    # Save the modified XML document
    $xmlDoc.Save($PropsFilePath)

    Write-Host "Successfully updated '$PropsFilePath' with the new game path."
}
catch {
    Write-Error "An error occurred while updating the XML file: $_"
}

    

IGNORE_WHEN_COPYING_START
Use code with cautio