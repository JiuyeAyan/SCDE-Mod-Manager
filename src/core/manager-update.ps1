$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$record = $null
try {
    $record = Get-Content -LiteralPath (Join-Path $taskRoot 'update.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $target = [IO.Path]::GetFullPath($record.target)
    $parent = [IO.Path]::GetDirectoryName($taskRoot)
    if ([IO.Path]::GetDirectoryName($target) -ine $parent -or
        [IO.Path]::GetExtension($target) -ine '.exe' -or
        [IO.Path]::GetFileName($taskRoot) -notlike '.scdemm-update-*') { throw 'Invalid update target.' }
    $candidate = Join-Path $taskRoot 'new.exe'
    $backup = Join-Path $taskRoot 'previous.exe'
    if ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ine $record.newHash) { throw 'Update file changed.' }
    if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Target is a link.' }
    [IO.File]::WriteAllText((Join-Path $taskRoot 'ready'), 'ready')
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    while (Get-Process -Id $record.pid -ErrorAction SilentlyContinue) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Manager did not exit; no files replaced.' }
        Start-Sleep -Milliseconds 200
    }
    if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'commit'))) { throw 'Update was not committed; no files replaced.' }
    # The outer portable launcher also needs time to release the executable.
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ($true) {
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ine $record.oldHash) { throw 'Installed manager changed; refusing replacement.' }
        try { [IO.File]::Replace($candidate, $target, $backup); break }
        catch [IO.IOException] {
            if ([DateTime]::UtcNow -gt $deadline) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
    # Do not pass the old launcher's extraction location to the new portable app.
    Remove-Item Env:PORTABLE_EXECUTABLE_DIR,Env:PORTABLE_EXECUTABLE_FILE,Env:PORTABLE_EXECUTABLE_APP_FILENAME -ErrorAction SilentlyContinue
    Start-Process -FilePath $target -WorkingDirectory $parent -WindowStyle Hidden
    [IO.File]::WriteAllText((Join-Path $taskRoot 'result.txt'), 'Updated and relaunched. previous.exe is the previous manager backup.')
} catch {
    [IO.File]::WriteAllText((Join-Path $taskRoot 'error.txt'), [string]$_)
    # An unsuccessful replacement leaves the original target intact; the backup
    # remains available if launching the successfully replaced EXE itself failed.
}
