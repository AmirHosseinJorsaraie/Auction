using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ArtMarketplace.Identity.Security;

internal static class CertificateLoader
{
    public static X509Certificate2 LoadRequired(IConfigurationSection section, string purpose)
    {
        var path = section["Path"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{purpose} certificate path is required.");
        }

        var fullPath = Path.GetFullPath(path, AppContext.BaseDirectory);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"{purpose} certificate was not found at '{fullPath}'.");
        }

        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                fullPath,
                password,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new InvalidOperationException($"{purpose} certificate must contain a private key.");
            }

            return certificate;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException($"{purpose} certificate could not be loaded.", exception);
        }
    }
}
