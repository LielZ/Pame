param([switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetPath)) { $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $projectRoot
try {
    & $dotnetPath test Pame.slnx --nologo --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & $dotnetPath publish src/Pame.App/Pame.App.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=false -o dist/app
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item -LiteralPath THIRD_PARTY_NOTICES.md,KNOWN_LIMITATIONS.md,README.md,CHANGELOG.md,DEPENDENCIES.md -Destination dist/app
    New-Item -ItemType Directory -Path dist/app/docs -Force | Out-Null
    Copy-Item -LiteralPath docs/PACKAGE_INVENTORY.json,docs/UI_ASSET_SOURCES.json,docs/INPUT_ASSET_SOURCES.json,docs/BRAND_ASSET_SOURCES.json,docs/VALIDATION-0.2.md,docs/VALIDATION-0.2.1.md,docs/VALIDATION-0.3.md,docs/VALIDATION-0.3.2.md,docs/PLAYSTATION_SOUND_SOURCE.json -Destination dist/app/docs
    Copy-Item -LiteralPath docs/VALIDATION-0.3.3.md,docs/BACKGROUND-GAMING-RESEARCH.md -Destination dist/app/docs
    Copy-Item -LiteralPath docs/VALIDATION-0.4.0.md,docs/WEBVIEW2-SOURCE.json -Destination dist/app/docs
    Copy-Item -LiteralPath docs/VALIDATION-0.4.1.md -Destination dist/app/docs
    Copy-Item -LiteralPath docs/VALIDATION-0.4.2.md,docs/UPDATER.md,docs/USER_GUIDE.md -Destination dist/app/docs
    Copy-Item -LiteralPath docs/images -Destination dist/app/docs -Recurse -Force
    Copy-Item -LiteralPath licenses -Destination dist/app -Recurse -Force
    if ($Package) {
        $browserBootstrapper = Join-Path $projectRoot 'packaging/runtimes/MicrosoftEdgeWebview2Setup.exe'
        $browserSignature = Get-AuthenticodeSignature -LiteralPath $browserBootstrapper
        if ($browserSignature.Status -ne 'Valid' -or $browserSignature.SignerCertificate.Subject -notmatch 'CN=Microsoft Corporation,') { throw 'WebView2 bootstrapper must have a valid Microsoft signature' }
        & $dotnetPath publish src/Pame.ServiceAccess/Pame.ServiceAccess.csproj -c Release -r win-x64 --self-contained true -o dist/service
        if ($LASTEXITCODE -ne 0) { throw 'Service add-on publish failed' }
        $compiler = Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
        if (!(Test-Path -LiteralPath $compiler)) { $compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
        if (!(Test-Path -LiteralPath $compiler)) { throw 'Install Inno Setup 6 to build the installer' }
        & $compiler /Qp packaging/ServiceAccess.iss
        if ($LASTEXITCODE -ne 0) { throw 'Service add-on installer build failed' }
        Copy-Item -LiteralPath dist/Pame-ServiceAccess-Setup.exe -Destination dist/app/Pame-ServiceAccess-Setup.exe -Force
        & $compiler /Qp packaging/Pame.iss
        if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
        $appProject = [xml](Get-Content -LiteralPath src/Pame.App/Pame.App.csproj -Raw)
        $releaseVersion = [string]$appProject.Project.PropertyGroup.Version
        $installerName = "Pame-Setup-$releaseVersion-x64.exe"
        $installerHash = (Get-FileHash -LiteralPath (Join-Path dist $installerName) -Algorithm SHA256).Hash.ToLowerInvariant()
        [IO.File]::WriteAllText((Join-Path $projectRoot 'dist/SHA256SUMS.txt'), "$installerHash  $installerName`n", [Text.UTF8Encoding]::new($false))
    }
} finally { Pop-Location }
