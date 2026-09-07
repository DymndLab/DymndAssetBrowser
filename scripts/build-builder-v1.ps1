param([string]$Runtime = 'win-x64')

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = Join-Path $repoRoot "artifacts\builder-v1\$Runtime"
Push-Location $repoRoot
try {
    dotnet run --project 'experiments\DymndBuilder.V1.SmokeTests\DymndBuilder.V1.SmokeTests.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Builder smoke tests failed.' }
    dotnet restore 'experiments\DymndBuilder.V1\DymndBuilder.V1.csproj' -r $Runtime
    if ($LASTEXITCODE -ne 0) { throw 'Builder restore failed.' }
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    dotnet publish 'experiments\DymndBuilder.V1\DymndBuilder.V1.csproj' -c Release -r $Runtime --self-contained true --no-restore `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Builder publish failed.' }
    Write-Host "Builder V1 prototype: $output"
}
finally { Pop-Location }
