param([Parameter(Mandatory=$true)][string]$JobFile)
$ErrorActionPreference = 'Stop'
$updateMutex = $null
$updateOwnsMutex = $false
$updateRestart = $false
$updateRoot = $null
$updateExecutable = $null
$updateInstallerLock = $null
function Write-UpdateResult([string]$status, [string]$message) {
    if ($updateRoot) {
        $resultPath = Join-Path $updateRoot 'result.json'
        $resultTemp = $resultPath + '.tmp'
        @{ Status=$status; Message=$message; Version=$updateJob.Version; Time=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath $resultTemp -Encoding UTF8
        Move-Item -LiteralPath $resultTemp -Destination $resultPath -Force
    }
}
try {
    $updateMutex = [Threading.Mutex]::new($true, 'Local\Pame.Update', [ref]$updateOwnsMutex)
    if (!$updateOwnsMutex) { exit 1 }
    $updateJob = Get-Content -LiteralPath $JobFile -Raw | ConvertFrom-Json
    $updateRoot = [IO.Path]::GetFullPath($updateJob.Root).TrimEnd('\')
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($JobFile)) -ne $updateRoot) { throw 'Invalid update job location.' }
    if ($updateJob.Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$' -or $updateJob.Hash -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid update identity.' }
    $updateDirectory = [IO.Path]::GetFullPath($updateJob.Directory).TrimEnd('\')
    $updateExecutable = Join-Path $updateDirectory 'Pame.exe'
    if (!(Test-Path -LiteralPath $updateExecutable) -or !(Test-Path -LiteralPath (Join-Path $updateDirectory 'unins000.exe'))) { throw 'Pame installation was not found.' }
    $updatePrevious = [Diagnostics.FileVersionInfo]::GetVersionInfo($updateExecutable)
    $updatePreviousVersion = [Version]::new($updatePrevious.FileMajorPart, $updatePrevious.FileMinorPart, $updatePrevious.FileBuildPart)
    if ([Version]$updateJob.Version -le $updatePreviousVersion) { throw 'The installed Pame version is already current or newer.' }
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($updateJob.ReadyFile)) -ne $updateRoot) { throw 'Invalid update handshake.' }
    $updateInstaller = Join-Path (Join-Path $updateRoot $updateJob.Version) ('Pame-Setup-' + $updateJob.Version + '-x64.exe')
    $updateInstallerLock = [IO.File]::Open($updateInstaller, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    if ($updateInstallerLock.Length -ne $updateJob.Size -or $updateJob.Size -gt 536870912) { throw 'Update size verification failed.' }
    $updateHasher = [Security.Cryptography.SHA256]::Create()
    try { $updateDigest = [BitConverter]::ToString($updateHasher.ComputeHash($updateInstallerLock)).Replace('-', '') }
    finally { $updateHasher.Dispose() }
    if ($updateDigest -ne $updateJob.Hash) { throw 'Update checksum verification failed.' }
    $updateFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($updateInstaller)
    if (($updateFileVersion.FileMajorPart.ToString()+'.'+$updateFileVersion.FileMinorPart+'.'+$updateFileVersion.FileBuildPart) -ne $updateJob.Version) { throw 'Installer version does not match the release.' }
    # Consume before handoff so *any* later failure cannot create a startup loop.
    $updatePending = Join-Path $updateRoot 'pending.json'
    if (Test-Path -LiteralPath $updatePending) { Remove-Item -LiteralPath $updatePending }
    [IO.File]::WriteAllText($updateJob.ReadyFile, 'ready')
    $updateDeadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        if (Test-Path -LiteralPath ($JobFile + '.cancel')) { throw 'The update handoff was cancelled. Pame was not changed.' }
        $updateOwner = Get-Process -Id $updateJob.Owner -ErrorAction SilentlyContinue
        if (!$updateOwner -or $updateOwner.StartTime.ToUniversalTime().Ticks -ne $updateJob.OwnerStarted) { break }
        if ([DateTime]::UtcNow -gt $updateDeadline) { throw 'Pame did not finish shutting down. The update was not installed.' }
        Start-Sleep -Milliseconds 200
    } while ($true)
    if (Test-Path -LiteralPath ($JobFile + '.cancel')) { throw 'The update handoff was cancelled. Pame was not changed.' }
    $updateRestart = $true
    # A helper for the outgoing Pame instance may still be restoring Windows.
    # Wait for it; never terminate a game, browser or recovery process.
    $updateDeadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        $updateRemaining = @(Get-CimInstance Win32_Process -Filter "Name = 'Pame.exe'" | Where-Object { $_.ExecutablePath -eq $updateExecutable })
        if (!$updateRemaining.Count) { break }
        if ([DateTime]::UtcNow -gt $updateDeadline) { throw 'Pame recovery is still running. Try the update again later.' }
        Start-Sleep -Milliseconds 300
    } while ($true)
    $updateLog = Join-Path $updateRoot 'installer.log'
    $updateArguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /RESTARTEXITCODE=3010 /PAMEUPDATE=1 /DIR="' + $updateDirectory + '" /LOG="' + $updateLog + '"'
    $updateSetup = Start-Process -FilePath $updateInstaller -ArgumentList $updateArguments -WindowStyle Hidden -PassThru -Wait
    $updateInstalledVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($updateExecutable)
    $updateActual = $updateInstalledVersion.FileMajorPart.ToString()+'.'+$updateInstalledVersion.FileMinorPart+'.'+$updateInstalledVersion.FileBuildPart
    if ($updateSetup.ExitCode -eq 0 -and $updateActual -eq $updateJob.Version) {
        Write-UpdateResult 'Installed' ('Pame updated to ' + $updateJob.Version + '.')
    } elseif ($updateSetup.ExitCode -eq 3010) {
        Write-UpdateResult 'RestartRequired' 'Windows needs a restart to finish the update. Your PC was not restarted automatically.'
    } else { throw ('The installer did not complete successfully (exit ' + $updateSetup.ExitCode + '). Open Updates to retry.') }
} catch {
    Write-UpdateResult 'Failed' $_.Exception.Message
} finally {
    if ($updateInstallerLock) { $updateInstallerLock.Dispose() }
    if ($updateOwnsMutex) { $updateMutex.ReleaseMutex() }
    if ($updateMutex) { $updateMutex.Dispose() }
    if ($updateRestart -and $updateExecutable -and (Test-Path -LiteralPath $updateExecutable)) {
        Start-Process -FilePath $updateExecutable -ArgumentList '--updated' -WindowStyle Hidden
    }
    if (Test-Path -LiteralPath $JobFile) { Remove-Item -LiteralPath $JobFile }
    if (Test-Path -LiteralPath ($JobFile + '.cancel')) { Remove-Item -LiteralPath ($JobFile + '.cancel') }
}
