# A cross-platform PowerShell script to find the installation path of a Steam game.
function Get-SteamInstallPath {
    <#
    .SYNOPSIS
        Finds the primary Steam installation directory on Windows or Linux.
        Returns $null if not found.
    #>
    
    $installPath = $null
    
    if ($IsWindows) {
        # Windows: Use the registry to find the Steam installation path.
        $registryPath = "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam"
        try {
            # Use -ErrorAction SilentlyContinue to prevent errors from stopping the script
            $installPath = Get-ItemProperty -Path $registryPath -Name "InstallPath" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty "InstallPath"
        }
        catch {
            # Catch is now only for unexpected errors, but we still just return null.
            return $null
        }
    }
    elseif ($IsLinux) {
        # Linux: Check common installation paths.
        $possiblePaths = @(
            "$env:HOME/.steam/steam",
            "$env:HOME/.local/share/Steam",
            "$env:HOME/.var/app/com.valvesoftware.Steam/data/Steam" # Flatpak path
        )
        
        foreach ($path in $possiblePaths) {
            if (Test-Path -Path $path) {
                $installPath = $path
                break
            }
        }
    }
    
    # Do not exit. Simply return the path found, or $null if not found.
    return $installPath
}

function Get-SteamLibraryPaths {
    <#
    .SYNOPSIS
        Parses libraryfolders.vdf to find all Steam library paths.
        Returns $null if the library file is not found.
    #>
    param (
        # Allow null or empty input without failing
        [string]$SteamInstallPath
    )

    # If the input path is null or empty, we can't proceed.
    if (-not $SteamInstallPath) {
        return $null
    }
    
    $libraryFoldersFile = Join-Path -Path $SteamInstallPath -ChildPath "steamapps/libraryfolders.vdf"
    if (-not (Test-Path -Path $libraryFoldersFile)) {
        # Do not exit. Just return null.
        return $null
    }
    
    $libraryFoldersContent = Get-Content -Path $libraryFoldersFile -Raw
    $libraryPaths = @($SteamInstallPath)
    
    $regmatches = [regex]::Matches($libraryFoldersContent, '"path"\s*"\s*(.+?)\s*"')
    
    foreach ($match in $regmatches) {
        $libraryPath = $match.Groups[1].Value
        
        if ($IsWindows) {
            $libraryPath = $libraryPath -replace '\\\\', '\'
        }

        if ($libraryPaths -notcontains $libraryPath) {
            $libraryPaths += $libraryPath
        }
    }
    
    return $libraryPaths
}

function Find-GameInstallPath {
    <#
    .SYNOPSIS
        Finds the installation directory of a game by its AppID.
        Returns $null if not found.
    #>
    param (
        [string[]]$LibraryPaths,
        [int]$AppId
    )
    
    # If the input array is null or empty, we can't proceed.
    if (-not $LibraryPaths) {
        return $null
    }

    foreach ($path in $LibraryPaths) {
        $manifestPath = Join-Path -Path $path -ChildPath "steamapps/appmanifest_$AppId.acf"
        if (Test-Path -Path $manifestPath) {
            $installdir = Get-Content -Path $manifestPath | Select-String -Pattern '"installdir"\s*"\s*(.+?)\s*"' | ForEach-Object { $_.Matches[0].Groups[1].Value }
            
            # Make sure we found an install dir before proceeding
            if ($installdir) {
                $gameInstallDir = Join-Path -Path $path -ChildPath "steamapps/common/$installdir"
                return $gameInstallDir
            }
        }
    }
    
    # Do not exit. Return null if the game wasn't found after checking all paths.
    return $null
}