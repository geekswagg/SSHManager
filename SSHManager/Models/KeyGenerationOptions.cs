namespace SSHManager.Models;

public record KeyGenerationOptions
{
    public required string Name { get; init; }
    public required string OutputDirectory { get; init; }
    public required string Comment { get; init; }
    public string? Passphrase { get; init; }
    public bool CopyToClipboard { get; init; } = true;
    public bool GenerateConfig { get; init; } = true;
    public int KeySize { get; init; } = 4096;
}