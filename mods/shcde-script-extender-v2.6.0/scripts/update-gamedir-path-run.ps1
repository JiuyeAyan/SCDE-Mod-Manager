# DESCRIPTION:
# A simple one-click script for developers to configure the project's
# library path to their local game installation.

# --- Configuration ---
$appId = 312520 # Rain World AppID

# The path to the props file is relative to this script's location.
$propsFilePath = Join-Path -Path $PSScriptRoot -ChildPath "../Directory.Build.props"

# The path to the script that does the work.
$updateScriptPath = Join-Path -Path $PSScriptRoot -ChildPath "update-gamedir-path.ps1"

# --- Execution ---
Write-Host "Configuring local development environment..."
& $updateScriptPath -PropsFilePath $propsFilePath -AppId $appId

Write-Host "Setup complete."