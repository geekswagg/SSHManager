using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SSHManager.Models;
using SSHManager.Services.Interfaces;

namespace SSHManager.Services;

public class SshKeyGenerator : ISshKeyGenerator
{
    public SshKeyPair GenerateKeyPair(string keyName, string comment, string? passphrase = null, int keySize = 4096)
    {
        using var rsa = RSA.Create(keySize);

        // Generate private key (PKCS#1)
        var pkcs1 = rsa.ExportRSAPrivateKey();
        var privateKeyPem = PemEncode("RSA PRIVATE KEY", pkcs1);

        // Generate encrypted private key if passphrase provided
        string? encryptedPrivateKeyPem = null;
        if (!string.IsNullOrEmpty(passphrase))
        {
            var pkcs8Encrypted = rsa.ExportEncryptedPkcs8PrivateKey(
                password: passphrase.AsSpan(),
                pbeParameters: new PbeParameters(
                    PbeEncryptionAlgorithm.Aes256Cbc, 
                    HashAlgorithmName.SHA256, 
                    iterationCount: 100_000));
            encryptedPrivateKeyPem = PemEncode("ENCRYPTED PRIVATE KEY", pkcs8Encrypted);
        }

        // Generate public key (OpenSSH format)
        var publicKeyOpenSsh = BuildOpenSshRsaPublicKey(rsa, comment);

        return new SshKeyPair
        {
            PrivateKeyPem = privateKeyPem,
            PublicKeyOpenSsh = publicKeyOpenSsh,
            EncryptedPrivateKeyPem = encryptedPrivateKeyPem,
            KeyName = keyName,
            Comment = comment
        };
    }

    private static string BuildOpenSshRsaPublicKey(RSA rsa, string comment)
    {
        var parameters = rsa.ExportParameters(false);
        var sshType = Encoding.ASCII.GetBytes("ssh-rsa");
        var e = NormalizeMpint(parameters.Exponent!);
        var n = NormalizeMpint(parameters.Modulus!);

        byte[] payload;
        using (var ms = new MemoryStream())
        {
            WriteSshString(ms, sshType);
            WriteSshMpint(ms, e);
            WriteSshMpint(ms, n);
            payload = ms.ToArray();
        }

        var base64 = Convert.ToBase64String(payload);
        return $"ssh-rsa {base64} {comment}".TrimEnd();
    }

    private static void WriteSshString(Stream stream, byte[] bytes)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)bytes.Length);
        stream.Write(length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteSshMpint(Stream stream, byte[] mpint)
    {
        // mpint: two's-complement big-endian, no unnecessary leading 0x00.
        // If the high bit is set, prepend 0x00 to keep it positive.
        bool prependZero = mpint.Length > 0 && (mpint[0] & 0x80) != 0;
        var length = (uint)mpint.Length + (prependZero ? 1u : 0u);

        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, length);
        stream.Write(lengthBytes);

        if (prependZero)
            stream.WriteByte(0x00);

        stream.Write(mpint, 0, mpint.Length);
    }

    private static byte[] NormalizeMpint(byte[] value)
    {
        // Strip leading zeros
        int i = 0;
        while (i < value.Length - 1 && value[i] == 0x00) i++;
        if (i == 0) return value;
        
        var slice = new byte[value.Length - i];
        Buffer.BlockCopy(value, i, slice, 0, slice.Length);
        return slice;
    }

    private static string PemEncode(string label, byte[] der)
    {
        var base64 = Convert.ToBase64String(der, Base64FormattingOptions.InsertLineBreaks);
        var sb = new StringBuilder();
        sb.AppendLine($"-----BEGIN {label}-----");
        sb.AppendLine(base64);
        sb.AppendLine($"-----END {label}-----");
        return sb.ToString();
    }
}