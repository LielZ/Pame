param([Parameter(Mandatory=$true)][string]$NotesFile, [switch]$Preview)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $gh = Join-Path $projectRoot '.tools/gh/bin/gh.exe'
    if (!(Test-Path -LiteralPath $gh)) { $gh = (Get-Command gh -ErrorAction Stop).Source }
    $project = [xml](Get-Content -LiteralPath src/Pame.App/Pame.App.csproj -Raw)
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part numeric release version.' }
    $installer = Join-Path $projectRoot "dist/Pame-Setup-$version-x64.exe"
    $checksums = Join-Path $projectRoot 'dist/SHA256SUMS.txt'
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    if ((Get-Content -LiteralPath $checksums -Raw).Trim() -ne "$hash  Pame-Setup-$version-x64.exe") { throw 'Build the installer and matching checksums first.' }
    if ((git status --porcelain)) { throw 'Commit and push the release source before publishing.' }
    $revision = (git rev-parse HEAD).Trim()
    $releaseTitle = if ($Preview) { "Pame $version - Public alpha" } else { "Pame $version - Release" }
    $releaseArgs = @('release','create',"v$version",$installer,$checksums,'--repo','LielZ/Pame','--draft','--target',$revision,'--title',$releaseTitle,'--notes-file',(Resolve-Path -LiteralPath $NotesFile).Path)
    if ($Preview) { $releaseArgs += '--prerelease' }
    & $gh @releaseArgs
    if ($LASTEXITCODE -ne 0) { throw 'Release creation failed. Any incomplete draft remains unpublished.' }
    $releaseList = (& $gh api 'repos/LielZ/Pame/releases?per_page=100') | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the draft release for verification.' }
    $draftMatches = @($releaseList | Where-Object { $_.tag_name -eq "v$version" -and $_.draft })
    if ($draftMatches.Count -ne 1) { throw 'Could not uniquely identify the new draft release.' }
    $release = $draftMatches[0]
    foreach ($assetName in @("Pame-Setup-$version-x64.exe",'SHA256SUMS.txt')) {
        $asset = @($release.assets | Where-Object { $_.name -eq $assetName -and $_.state -eq 'uploaded' })
        $localHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot "dist/$assetName") -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($asset.Count -ne 1 -or $asset[0].digest -ne "sha256:$localHash") { throw "Asset verification failed: $assetName. The release remains a draft." }
    }
    & $gh release edit "v$version" --repo LielZ/Pame --draft=false
    if ($LASTEXITCODE -ne 0) { throw 'Could not publish the completed release.' }
} finally { Pop-Location }
