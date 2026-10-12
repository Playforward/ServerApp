using ModdableWebServer.Helper;
using Serilog;
using ServerShared.Controllers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ServerApp;

internal static class CertLoader
{
    public static void Load()
    {
        if (!Directory.Exists("Cert"))
            Directory.CreateDirectory("Cert");

        foreach (var cert in Directory.GetFiles("Cert", "*.*", SearchOption.AllDirectories))
        {
            if (cert.EndsWith(".key"))
                continue;
            var certDetail = Program.ServerAppSettings.CertDetails.FirstOrDefault(x => cert.Contains(x.Name));
            if (certDetail == null)
                continue;
            string password = string.Empty;
            if (!string.IsNullOrEmpty(certDetail.Password))
                password = certDetail.Password;
            if (cert.EndsWith(".pfx"))
            {
                Log.Information("Adding cert: {cert}", cert);
                X509Certificate2 cert2 = (CertHelper.GetCert(cert, password) as X509Certificate2)!;
                LogPin(cert, cert2);
                if (certDetail.IsMainCert)
                {
                    ServerController.MainCertificate = cert2;
                    continue;
                }
                ServerController.Certificates.Add(cert2);
                continue;
            }
            var name = Path.GetFileNameWithoutExtension(cert);
            var keyName = Path.Combine("Cert", $"{name}.key");
            var cert1 = Path.Combine(Directory.GetCurrentDirectory(), cert);
            keyName = Path.Combine(Directory.GetCurrentDirectory(), keyName);
            Log.Information("Adding cert: {cert}, {key}", cert1, keyName);
            var certificate = X509Certificate2.CreateFromPemFile(cert1, keyName);
            if (string.IsNullOrEmpty(password))
                password = "test";
            var pfx = certificate.Export(X509ContentType.Pfx, password);
            certificate = X509CertificateLoader.LoadPkcs12(pfx, password);
            LogPin(cert, certificate);
            if (certDetail.IsMainCert)
            {
                ServerController.MainCertificate = certificate;
                continue;
            }
            ServerController.Certificates.Add(certificate);
        }
    }

    private static void LogPin(string file, X509Certificate2 certificate)
    {
        string pin = Convert.ToBase64String(SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo()));
        Log.Information("Cert {File}: shims trust it with tls_pin_sha256={Pin}", file, pin);
    }
}
