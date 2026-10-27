param(
    [string]$OutputPath = "artifacts/migrations.sql"
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$absoluteOutput = Join-Path $repositoryRoot $OutputPath
$outputDirectory = Split-Path -Parent $absoluteOutput
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

Push-Location $repositoryRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }
    dotnet build src/Backend/ArtMarketplace.Api/ArtMarketplace.Api.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'API build failed' }
    dotnet ef migrations script --idempotent --project src/Backend/ArtMarketplace.Infrastructure/ArtMarketplace.Infrastructure.csproj --startup-project src/Backend/ArtMarketplace.Api/ArtMarketplace.Api.csproj --configuration Release --no-build --output $absoluteOutput
    if ($LASTEXITCODE -ne 0) { throw 'Migration script generation failed' }
}
finally { Pop-Location }

Write-Host "Generated migration bundle: $absoluteOutput"
