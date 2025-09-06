using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.CommandLine; // <-- Add this using directive

class Program
{
    static int Main(string[] args)
    {
        var nameOption = new Option<string>("--name", description: "Base filename (no extension)", getDefaultValue: () => "id_rsa_ado");
        var outDirOption = new Option<string>("--out", description: "Output directory", getDefaultValue: () => Directory.GetCurrentDirectory());
        var commentOption = new Option<string>("--comment", description: "OpenSSH public key comment", getDefaultValue: () => "AzureDevOps");
        var passOption = new Option<string>("--passphrase", description: "If provided, also export encrypted PKCS#8 with this passphrase", getDefaultValue: () => string.Empty);

        var root = new RootCommand("Generate an RSA keypair for Azure DevOps (OpenSSH public key + PEM private key)");
        root.AddOption(nameOption);
        root.AddOption(outDirOption);
        root.AddOption(commentOption);
        root.AddOption(passOption);

        root.SetHandler((string name, string outDir, string comment, string passphrase) =>
        {
            Directory.CreateDirectory(outDir);
            var privPath = Path.Combine(outDir, name);
            var pubPath = Path.Combine(outDir, name + ".pub");

            using var rsa = RSA.Create(4096);

            // ----- PRIVATE KEY (PKCS#1 PEM: "BEGIN RSA PRIVATE KEY") -----
            var pkcs1 = rsa.ExportRSAPrivateKey(); // PKCS#1
            var pkcs1Pem = PemEncode("RSA PRIVATE KEY", pkcs1);
            File.WriteAllText(privPath, pkcs1Pem);
            // Lock down permissions if on *nix; on Windows rely on NTFS defaults.

            // ----- OPTIONAL ENCRYPTED PKCS#8 -----
            if (!string.IsNullOrEmpty(passphrase))
            {
                var pkcs8Encrypted = rsa.ExportEncryptedPkcs8PrivateKey(
                    password: passphrase.AsSpan(),
                    pbeParameters: new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterationCount: 100_000)
                );
                var pkcs8Path = Path.Combine(outDir, name + ".pkcs8.enc.pem");
                var pkcs8Pem = PemEncode("ENCRYPTED PRIVATE KEY", pkcs8Encrypted);
                File.WriteAllText(pkcs8Path, pkcs8Pem);
            }

            // ----- PUBLIC KEY (OpenSSH "ssh-rsa AAAA... comment") -----
            var sshPub = BuildOpenSshRsaPublicKey(rsa, comment);
            File.WriteAllText(pubPath, sshPub);

            Console.WriteLine("Generated:");
            Console.WriteLine($"  Private key (PKCS#1): {privPath}");
            if (!string.IsNullOrEmpty(passphrase))
                Console.WriteLine($"  Encrypted private key (PKCS#8): {Path.Combine(outDir, name + ".pkcs8.enc.pem")}");
            Console.WriteLine($"  Public key (OpenSSH): {pubPath}");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine("  1) Add the public key (.pub) to Azure DevOps > User Settings > SSH Public Keys.");
            Console.WriteLine("  2) Ensure your SSH client uses the private key when talking to dev.azure.com.");
            Console.WriteLine("  3) Test: ssh -T git@ssh.dev.azure.com");
        }, nameOption, outDirOption, commentOption, passOption);

        return root.Invoke(args);
    }

    // Build OpenSSH public key string: "ssh-rsa <base64(payload)> <comment>"
    static string BuildOpenSshRsaPublicKey(RSA rsa, string comment)
    {
        var p = rsa.ExportParameters(false);

        var sshType = Encoding.ASCII.GetBytes("ssh-rsa");
        var e = NormalizeMpint(p.Exponent);
        var n = NormalizeMpint(p.Modulus);

        byte[] payload;
        using (var ms = new MemoryStream())
        {
            WriteSshString(ms, sshType);
            WriteSshMpint(ms, e);
            WriteSshMpint(ms, n);
            payload = ms.ToArray();
        }

        var b64 = Convert.ToBase64String(payload);
        return $"ssh-rsa {b64} {comment}".TrimEnd();
    }

    // SSH binary encoding helpers
    static void WriteSshString(Stream s, byte[] bytes)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, (uint)bytes.Length);
        s.Write(len);
        s.Write(bytes, 0, bytes.Length);
    }

    static void WriteSshMpint(Stream s, byte[] mpint)
    {
        // mpint: two's-complement big-endian, no unnecessary leading 0x00.
        // If the high bit is set, prepend 0x00 to keep it positive.
        bool prependZero = (mpint.Length > 0 && (mpint[0] & 0x80) != 0);
        var length = (uint)mpint.Length + (prependZero ? 1u : 0u);

        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(len, length);
        s.Write(len);

        if (prependZero)
            s.WriteByte(0x00);

        s.Write(mpint, 0, mpint.Length);
    }

    static byte[] NormalizeMpint(byte[] v)
    {
        // Strip leading zeros
        int i = 0;
        while (i < v.Length - 1 && v[i] == 0x00) i++;
        if (i == 0) return v;
        var slice = new byte[v.Length - i];
        Buffer.BlockCopy(v, i, slice, 0, slice.Length);
        return slice;
    }

    static string PemEncode(string label, byte[] der)
    {
        var b64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        sb.AppendLine(b64);
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }
}
