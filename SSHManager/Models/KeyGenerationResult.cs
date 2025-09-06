namespace SSHManager.Models;

public record KeyGenerationResult
{
    public required SshKeyPair KeyPair { get; init; }
    public required string PrivateKeyPath { get; init; }
    public required string PublicKeyPath { get; init; }
    public string? EncryptedKeyPath { get; init; }
    public string? ConfigPath { get; init; }
    public bool ConfigUpdated { get; init; }
    public bool CopiedToClipboard { get; init; }
}