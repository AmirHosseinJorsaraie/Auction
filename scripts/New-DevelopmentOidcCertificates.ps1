param(
    [Parameter(Mandatory = $false)]
    [string] $OutputDirectory = (Join-Path $PSScriptRoot "..\secrets"),

    [Parameter(Mandatory = $true)]
    [ValidateLength(16, 256)]
    [string] $SigningPassword,

    [Parameter(Mandatory = $true)]
    [ValidateLength(16, 256)]
    [string] $EncryptionPassword
)

$ErrorActionPreference = "Stop"
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

function New-OidcCertificate {
    param(
        [Parameter(Mandatory = $true)] [string] $Subject,
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $Password,
        [Parameter(Mandatory = $true)] [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags] $Usage
    )

    $rsa = [System.Security.Cryptography.RSA]::Create(3072)
    try {
        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $Subject,
            $rsa,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new($Usage, $true))
        $certificate = $request.CreateSelfSigned(
            [DateTimeOffset]::UtcNow.AddMinutes(-5),
            [DateTimeOffset]::UtcNow.AddYears(2))
        try {
            [System.IO.File]::WriteAllBytes(
                $Path,
                $certificate.Export(
                    [System.Security.Cryptography.X509Certificates.X509ContentType]::Pkcs12,
                    $Password))
        }
        finally {
            $certificate.Dispose()
        }
    }
    finally {
        $rsa.Dispose()
    }
}

New-OidcCertificate `
    -Subject "CN=Art Marketplace Development OIDC Signing" `
    -Path (Join-Path $resolvedOutput "oidc-signing.pfx") `
    -Password $SigningPassword `
    -Usage ([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature)

New-OidcCertificate `
    -Subject "CN=Art Marketplace Development OIDC Encryption" `
    -Path (Join-Path $resolvedOutput "oidc-encryption.pfx") `
    -Password $EncryptionPassword `
    -Usage ([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment)

Write-Output "Created development-only OIDC certificates in $resolvedOutput"
