$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dotnet = Join-Path $root '.tools\dotnet10\dotnet.exe'
$certificateDirectory = Join-Path $root '.tools\secrets'
$aspNetCertificatePfx = Join-Path $certificateDirectory 'aspnet-development-certificate.pfx'
$publicWeb = Join-Path $root 'src\Frontend\PublicWeb\ArtMarketplace.PublicWeb.csproj'

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "The repository .NET SDK was not found at '$dotnet'."
}

[System.IO.Directory]::CreateDirectory($certificateDirectory) | Out-Null
& $dotnet dev-certs https --export-path $aspNetCertificatePfx --format Pfx --password 'Local-Debug-Https-Certificate-2026!' --quiet
if ($LASTEXITCODE -ne 0) {
    throw 'The ASP.NET Core development certificate could not be exported for ASP.NET Core.'
}

Set-Location -LiteralPath $root
& $dotnet run --project $publicWeb --launch-profile https
exit $LASTEXITCODE
