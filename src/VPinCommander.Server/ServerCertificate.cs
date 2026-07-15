using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace VPinCommander.Server;

/// <summary>
/// The cabinet's self-signed HTTPS certificate: generated once, persisted as a
/// PFX in the app data folder, and identified to clients by its SHA-256
/// fingerprint (shown in Settings for pairing).
/// </summary>
public static class ServerCertificate
{
    public static X509Certificate2 GetOrCreate(string pfxPath)
    {
        if (File.Exists(pfxPath))
            return new X509Certificate2(pfxPath, (string?)null, X509KeyStorageFlags.Exportable);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=VPinCommander", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName(Environment.MachineName);
        request.CertificateExtensions.Add(sanBuilder.Build());

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

        Directory.CreateDirectory(Path.GetDirectoryName(pfxPath)!);
        File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pkcs12));
        return new X509Certificate2(pfxPath, (string?)null, X509KeyStorageFlags.Exportable);
    }

    public static string FingerprintOf(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData));
}
