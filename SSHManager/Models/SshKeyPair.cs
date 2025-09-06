namespace SSHManager.Models;

public record SshKeyPair
{
    public required string PrivateKeyPem { get; init; }
    public required string PublicKeyOpenSsh { get; init; }
    public string? EncryptedPrivateKeyPem { get; init; }
    public required string KeyName { get; init; }
    public required string Comment { get; init; }
}