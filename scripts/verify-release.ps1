param(
    [switch]$SkipContainers
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    dotnet restore ArtMarketplace.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }
    dotnet build ArtMarketplace.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    dotnet test ArtMarketplace.sln -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed' }
    dotnet ef migrations has-pending-model-changes --project src/Backend/ArtMarketplace.Infrastructure/ArtMarketplace.Infrastructure.csproj --startup-project src/Backend/ArtMarketplace.Api/ArtMarketplace.Api.csproj --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'EF model drift detected' }

    if (-not $SkipContainers) {
        docker compose -f deploy/docker-compose.yml config --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Compose validation failed' }
        docker compose -f deploy/docker-compose.yml build api identity backoffice public-web
        if ($LASTEXITCODE -ne 0) { throw 'container build failed' }
    }
}
finally { Pop-Location }
