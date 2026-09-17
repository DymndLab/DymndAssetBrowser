param(
    [string]$Version,
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [switch]$SkipInstaller,
    [ValidateSet('Deferred', 'Reviewed')][string]$Documentation = 'Reviewed',
    [switch]$PublicRelease
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE." }
}
function Assert-OutputPath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    $boundary = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + '\'
    if (-not $resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw "Output outside artifacts: $resolved" }
    $cursor = $resolved
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked output path: $cursor" }
        }
        $cursor = Split-Path -Parent $cursor
    }
}
Push-Location $repoRoot
try {
    [xml]$project = Get-Content -LiteralPath 'src\DymndAssetBrowser.App\DymndAssetBrowser.App.csproj'
    $projectVersion = [string]($project.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version
    if (-not $Version) { $Version = $projectVersion }
    if ($Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$' -or $Version -ne $projectVersion) { throw "Version must match project version $projectVersion." }
    $reviewedDocuments = @(
        'README.md', 'RELEASE-NOTES.md', 'DEVELOPMENT.md', 'THIRD-PARTY-NOTICES.md', 'docs\RELEASE-CHECKLIST.md',
        'docs\AI-DISCLOSURE.md', 'docs\Dymnd-Asset-Browser-Feature-Guide.pdf'
    )
    if ($PublicRelease -and $Documentation -ne 'Reviewed') { throw 'Public releases require reviewed documentation.' }
    if ($Documentation -eq 'Reviewed') {
        # The owner completed the guide in Google Docs; the exported PDF is now
        # authoritative. Never overwrite it using the older draft generator.
        $approval = Get-Content -LiteralPath 'docs\release-documents.json' -Raw | ConvertFrom-Json
        if ($approval.releaseVersion -ne $Version) { throw 'Documentation approval does not match the release version.' }
        $guideHash = (Get-FileHash -LiteralPath 'docs\Dymnd-Asset-Browser-Feature-Guide.pdf' -Algorithm SHA256).Hash
        if ($guideHash -ne $approval.guideSha256) { throw 'Guide differs from the approved PDF.' }
        foreach ($document in $reviewedDocuments) {
            if (-not (Test-Path -LiteralPath $document -PathType Leaf)) { throw "Missing reviewed document: $document" }
        }
    }
    if (-not $SkipInstaller) {
        $iscc = @(
            (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $iscc) { throw 'Inno Setup 6 is required. Use -SkipInstaller explicitly for a portable-only build.' }
    }
    # A new directory per run prevents stale installers or checksums being released.
    $releaseRoot = Join-Path $repoRoot ('artifacts\releases\' + $Version + '-' + [Guid]::NewGuid().ToString('N'))
    Assert-OutputPath $releaseRoot
    $publishRoot = Join-Path $releaseRoot 'publish'
    New-Item -ItemType Directory -Path $publishRoot | Out-Null
    Invoke-DotNet restore 'DymndAssetBrowser.slnx'
    Invoke-DotNet build 'DymndAssetBrowser.slnx' '-c' 'Release' '--no-restore' '-p:UseSharedCompilation=false' '-p:AnalysisModeSecurity=All'
    Invoke-DotNet run '--project' 'tests\DymndAssetBrowser.SmokeTests' '-c' 'Release' '--no-build'
    Invoke-DotNet run '--project' 'tests\DymndAssetBrowser.Tests' '-c' 'Release' '--no-build'
    Invoke-DotNet run '--project' 'tests\DymndAssetBrowser.Tests' '-c' 'Release' '--no-build' '--' '--theme'
    Invoke-DotNet restore 'src\DymndAssetBrowser.App\DymndAssetBrowser.App.csproj' '-r' $Runtime
    Invoke-DotNet publish 'src\DymndAssetBrowser.App\DymndAssetBrowser.App.csproj' '-c' 'Release' '-r' $Runtime '--self-contained' 'true' '--no-restore' '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=None' '-p:DebugSymbols=false' '-p:UseSharedCompilation=false' '-o' $publishRoot
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'DYM&D Asset Browser.exe'))) { throw 'Expected application executable missing.' }
    # Skia's Windows package includes a large native debugger symbol file. It is not a runtime dependency.
    $nativeSymbols = Join-Path $publishRoot 'libSkiaSharp.pdb'
    Assert-OutputPath $nativeSymbols
    if (Test-Path -LiteralPath $nativeSymbols) { Remove-Item -LiteralPath $nativeSymbols }
    Copy-Item -LiteralPath 'LICENSE' -Destination (Join-Path $publishRoot 'LICENSE.txt')
    $licenseRoot = Join-Path $publishRoot 'licenses'
    New-Item -ItemType Directory -Path $licenseRoot | Out-Null
    Copy-Item -LiteralPath 'THIRD-PARTY-NOTICES.md' -Destination (Join-Path $licenseRoot 'Existing-Dependency-Notices.md')
    foreach ($package in @('skiasharp', 'skiasharp.nativeassets.win32')) {
        $packageRoot = Join-Path $repoRoot ".packages\$package\4.152.0"
        Copy-Item -LiteralPath (Join-Path $packageRoot 'LICENSE.txt') -Destination (Join-Path $licenseRoot "$package-LICENSE.txt")
        $notices = Join-Path $packageRoot 'THIRD-PARTY-NOTICES.txt'
        if (Test-Path -LiteralPath $notices) { Copy-Item -LiteralPath $notices -Destination (Join-Path $licenseRoot "$package-THIRD-PARTY-NOTICES.txt") }
    }
    if ($Documentation -eq 'Reviewed') {
        foreach ($document in $reviewedDocuments) {
            $destination = Join-Path $publishRoot $document
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $document -Destination $destination
        }
    }
    @{ product='DYM&D Asset Browser'; version=$Version; runtime=$Runtime; documentation=$Documentation; publicReleaseApproved=[bool]$PublicRelease } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $publishRoot 'build-info.json') -Encoding UTF8
    $zipPath = Join-Path $releaseRoot "Dymnd-Asset-Browser-$Version-$Runtime.zip"
    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $releaseFiles = @($zipPath)
    if (-not $SkipInstaller) {
        & $iscc "/DMyAppVersion=$Version" "/DMyPublishDir=$publishRoot" "/DMyOutputDir=$releaseRoot" 'installer\DymndAssetBrowser.iss'
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed: $LASTEXITCODE" }
        $installerPath = Join-Path $releaseRoot "Dymnd-Asset-Browser-Setup-$Version.exe"
        if (-not (Test-Path -LiteralPath $installerPath)) { throw 'Installer was not created in this run.' }
        $releaseFiles += $installerPath
    }
    if ($Documentation -eq 'Reviewed') {
        $guidePath = Join-Path $releaseRoot 'Dymnd-Asset-Browser-Feature-Guide.pdf'
        Copy-Item -LiteralPath 'docs\Dymnd-Asset-Browser-Feature-Guide.pdf' -Destination $guidePath
        $releaseFiles += $guidePath
    }
    $records = @($releaseFiles | ForEach-Object { @{ name=[IO.Path]::GetFileName($_); sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } })
    $records | ForEach-Object { "$($_.sha256)  $($_.name)" } | Set-Content -LiteralPath (Join-Path $releaseRoot "SHA256SUMS-$Version.txt") -Encoding ASCII
    @{ version=$Version; documentation=$Documentation; files=$records } | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Encoding UTF8
    if ($env:GITHUB_OUTPUT) { "release_dir=$releaseRoot" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8 }
    Write-Host "RELEASE_DIRECTORY=$releaseRoot"
}
finally { Pop-Location }
