param(
    [switch] $SkipMigrations
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$tools = Join-Path $root '.tools'
$logDirectory = Join-Path $tools 'logs'
$secretDirectory = Join-Path $tools 'secrets'
$postgresRoot = Join-Path $tools 'postgresql-17.11\pgsql'
$postgresBin = Join-Path $postgresRoot 'bin'
$postgresData = Join-Path $tools 'postgres-data'
$seaweed = Join-Path $tools 'seaweedfs-4.41\weed.exe'
$seaweedData = Join-Path $tools 'seaweed-data'
$repositoryDotnet = Join-Path $tools 'dotnet10\dotnet.exe'

if (-not (Test-Path -LiteralPath (Join-Path $postgresBin 'pg_ctl.exe'))) {
    throw "PostgreSQL portable binaries were not found under '$postgresRoot'."
}
if (-not (Test-Path -LiteralPath (Join-Path $postgresData 'PG_VERSION'))) {
    throw "The PostgreSQL data directory is not initialized at '$postgresData'."
}
if (-not (Test-Path -LiteralPath $seaweed)) {
    throw "SeaweedFS was not found at '$seaweed'."
}
if (-not (Test-Path -LiteralPath $repositoryDotnet)) {
    throw "The repository .NET SDK was not found at '$repositoryDotnet'."
}

[System.IO.Directory]::CreateDirectory($logDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($secretDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($seaweedData) | Out-Null
$env:PATH = "$postgresBin;$postgresRoot\lib;$env:PATH"

& $repositoryDotnet dev-certs https `
    --export-path (Join-Path $secretDirectory 'aspnet-development-certificate.pfx') `
    --password 'Local-Debug-Https-Certificate-2026!' `
    --format Pfx `
    --quiet
if ($LASTEXITCODE -ne 0) { throw 'The ASP.NET Core development certificate could not be exported as PFX.' }
& $repositoryDotnet dev-certs https `
    --export-path (Join-Path $secretDirectory 'aspnet-development-certificate.pem') `
    --format Pem `
    --no-password `
    --quiet
if ($LASTEXITCODE -ne 0) { throw 'The ASP.NET Core development certificate could not be exported as PEM.' }

& (Join-Path $postgresBin 'pg_isready.exe') -h 127.0.0.1 -p 5432 *> $null
if ($LASTEXITCODE -ne 0) {
    & (Join-Path $postgresBin 'pg_ctl.exe') `
        -D $postgresData `
        -l (Join-Path $logDirectory 'postgres.log') `
        -o '"-p" "5432"' `
        start
    if ($LASTEXITCODE -ne 0) {
        throw 'PostgreSQL failed to start.'
    }
}

$seaweedListener = Get-NetTCPConnection -State Listen -LocalPort 8333 -ErrorAction SilentlyContinue
if ($null -eq $seaweedListener) {
    Start-Process -FilePath $seaweed `
        -ArgumentList @(
            'server',
            '-s3',
            "-dir=$seaweedData",
            "-s3.config=$(Join-Path $root 'deploy\seaweedfs\s3.json')"
        ) `
        -WorkingDirectory $root `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logDirectory 'seaweed.out.log') `
        -RedirectStandardError (Join-Path $logDirectory 'seaweed.err.log') | Out-Null
}

$deadline = [DateTimeOffset]::UtcNow.AddSeconds(30)
do {
    Start-Sleep -Milliseconds 500
    $tcpClient = [System.Net.Sockets.TcpClient]::new()
    try {
        $tcpClient.Connect('127.0.0.1', 8333)
        $storageReady = $tcpClient.Connected
    }
    catch {
        $storageReady = $false
    }
    finally {
        $tcpClient.Dispose()
    }
} until ($storageReady -or [DateTimeOffset]::UtcNow -ge $deadline)

if (-not $storageReady) {
    throw 'SeaweedFS failed to become ready on port 8333.'
}

foreach ($bucket in 'art-marketplace-quarantine', 'art-marketplace-private', 'art-marketplace-public') {
    & curl.exe -sS -o NUL -X POST -H 'Content-Type:' "http://127.0.0.1:8888/buckets/$bucket/"
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to ensure the '$bucket' bucket exists."
    }
}

if (-not $SkipMigrations) {
    $env:ConnectionStrings__Database = 'Host=localhost;Port=5432;Database=art_marketplace;Username=art_marketplace;Password=change-me-for-local-only'
    try {
        & $repositoryDotnet tool restore
        if ($LASTEXITCODE -ne 0) { throw 'The local .NET tools could not be restored.' }
        & $repositoryDotnet ef database update `
            --project (Join-Path $root 'src\Backend\ArtMarketplace.Infrastructure\ArtMarketplace.Infrastructure.csproj') `
            --startup-project (Join-Path $root 'src\Backend\ArtMarketplace.Api\ArtMarketplace.Api.csproj') `
            --configuration Debug
        if ($LASTEXITCODE -ne 0) { throw 'The database migrations failed.' }
    }
    finally {
        Remove-Item Env:\ConnectionStrings__Database -ErrorAction SilentlyContinue
    }
}

Write-Output 'Local dependencies are ready:'
Write-Output '  PostgreSQL  http://127.0.0.1:5432'
Write-Output '  SeaweedFS   http://127.0.0.1:8333'
