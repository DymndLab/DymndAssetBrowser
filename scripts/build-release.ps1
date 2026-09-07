param(
    [string]$Version = '2.5.1',
    [string]$Runtime = 'win-x64',
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$publishRoot = Join-Path $artifactsRoot "publish\$Runtime"
$documentationRoot = Join-Path $publishRoot 'docs'
$zipPath = Join-Path $artifactsRoot "Dymnd-Asset-Companion-$Version-$Runtime.zip"
$installerPath = Join-Path $artifactsRoot "installer\Dymnd-Asset-Companion-Setup-$Version.exe"
$checksumPath = Join-Path $artifactsRoot "SHA256SUMS-$Version.txt"

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE." }
}

Push-Location $repoRoot
try {
    [xml]$project = Get-Content -LiteralPath 'src\FAFamilyBrowser.App\FAFamilyBrowser.App.csproj'
    $projectVersion = [string]$project.Project.PropertyGroup.Version
    if ($projectVersion -ne $Version) {
        throw "Release version $Version does not match project version $projectVersion."
    }

    Invoke-DotNet restore 'FAFamilyBrowser.slnx'
    Invoke-DotNet build 'FAFamilyBrowser.slnx' '-c' 'Release' '--no-restore'
    Invoke-DotNet run '--project' 'tests\FAFamilyBrowser.SmokeTests\FAFamilyBrowser.SmokeTests.csproj' '-c' 'Release' '--no-build'
    Invoke-DotNet restore 'src\FAFamilyBrowser.App\FAFamilyBrowser.App.csproj' '-r' $Runtime

    if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
    Invoke-DotNet publish 'src\FAFamilyBrowser.App\FAFamilyBrowser.App.csproj' '-c' 'Release' '-r' $Runtime '--self-contained' 'true' '--no-restore' '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:DebugType=None' '-p:DebugSymbols=false' '-o' $publishRoot

    $releaseDocumentation = @(
        @{ Source = 'RELEASE-NOTES.md'; Destination = 'RELEASE-NOTES.md' },
        @{ Source = 'LICENSE'; Destination = 'LICENSE.txt' },
        @{ Source = 'THIRD-PARTY-NOTICES.md'; Destination = 'THIRD-PARTY-NOTICES.md' },
        @{ Source = 'docs\AI-DISCLOSURE.md'; Destination = 'docs\AI-DISCLOSURE.md' },
        @{ Source = 'docs\Dymnd-Asset-Companion-Feature-Guide.pdf'; Destination = 'docs\Dymnd-Asset-Companion-Feature-Guide.pdf' },
        @{ Source = 'docs\QUICK-START-IT.md'; Destination = 'docs\QUICK-START-IT.md' }
    )
    New-Item -ItemType Directory -Path $documentationRoot -Force | Out-Null
    foreach ($document in $releaseDocumentation) {
        if (-not (Test-Path -LiteralPath $document.Source)) {
            throw "Required release documentation is missing: $($document.Source)"
        }
        $destination = Join-Path $publishRoot $document.Destination
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $document.Source -Destination $destination
    }

    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "Created portable ZIP: $zipPath"

    if (-not $SkipInstaller) {
        $innoCandidates = @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
        $iscc = $innoCandidates | Select-Object -First 1
        if ($iscc) {
            & $iscc "/DMyAppVersion=$Version" 'installer\DymndAssetBrowser.iss'
            if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }
        }
        else {
            Write-Warning 'Inno Setup 6 was not found. The portable ZIP is complete; install Inno Setup and rerun to produce the setup EXE.'
        }
    }

    $releaseFiles = @($zipPath)
    if (Test-Path -LiteralPath $installerPath) { $releaseFiles += $installerPath }
    $checksums = foreach ($releaseFile in $releaseFiles) {
        $hash = Get-FileHash -LiteralPath $releaseFile -Algorithm SHA256
        "$($hash.Hash)  $([System.IO.Path]::GetFileName($releaseFile))"
    }
    [System.IO.File]::WriteAllLines($checksumPath, $checksums)
    Write-Host "Created checksums: $checksumPath"
}
finally {
    Pop-Location
}
