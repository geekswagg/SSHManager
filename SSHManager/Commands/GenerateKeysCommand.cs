using SSHManager.Models;
using SSHManager.Services.Interfaces;

namespace SSHManager.Commands;

public class GenerateKeysCommand
{
    private readonly ISshKeyGenerator _keyGenerator;
    private readonly IFileService _fileService;
    private readonly IClipboardService _clipboardService;
    private readonly ISshConfigService _configService;
    private readonly IConsoleService _consoleService;

    public GenerateKeysCommand(
        ISshKeyGenerator keyGenerator,
        IFileService fileService,
        IClipboardService clipboardService,
        ISshConfigService configService,
        IConsoleService consoleService)
    {
        _keyGenerator = keyGenerator;
        _fileService = fileService;
        _clipboardService = clipboardService;
        _configService = configService;
        _consoleService = consoleService;
    }

    public async Task<KeyGenerationResult> ExecuteAsync(KeyGenerationOptions options)
    {
        await _consoleService.ShowHeaderAsync();

        // Ensure output directory exists
        _fileService.CreateDirectory(options.OutputDirectory);

        // Build file paths
        var privateKeyPath = Path.Combine(options.OutputDirectory, options.Name);
        var publicKeyPath = Path.Combine(options.OutputDirectory, options.Name + ".pub");
        var encryptedKeyPath = !string.IsNullOrEmpty(options.Passphrase) 
            ? Path.Combine(options.OutputDirectory, options.Name + ".pkcs8.enc.pem") 
            : null;
        var configPath = Path.Combine(options.OutputDirectory, "config");

        KeyGenerationResult result = null!;

        await _consoleService.ShowProgressAsync("Generating RSA key pair...", async ctx =>
        {
            // Generate the key pair
            ctx.UpdateStatus($"Creating {options.KeySize}-bit RSA key...");
            var keyPair = _keyGenerator.GenerateKeyPair(options.Name, options.Comment, options.Passphrase, options.KeySize);

            // Save private key
            ctx.UpdateStatus("Exporting private key (PKCS#1)...");
            await _fileService.WriteTextFileAsync(privateKeyPath, keyPair.PrivateKeyPem);

            // Save encrypted private key if passphrase provided
            if (!string.IsNullOrEmpty(options.Passphrase) && encryptedKeyPath != null)
            {
                ctx.UpdateStatus("Exporting encrypted private key (PKCS#8)...");
                await _fileService.WriteTextFileAsync(encryptedKeyPath, keyPair.EncryptedPrivateKeyPem!);
            }

            // Save public key
            ctx.UpdateStatus("Generating OpenSSH public key...");
            await _fileService.WriteTextFileAsync(publicKeyPath, keyPair.PublicKeyOpenSsh);

            // Update SSH config if requested
            bool configUpdated = false;
            if (options.GenerateConfig)
            {
                ctx.UpdateStatus("Updating SSH config file...");
                configUpdated = await _configService.UpdateConfigFileAsync(configPath, options.Name, options.OutputDirectory);
            }

            // Copy to clipboard if requested
            bool copiedToClipboard = false;
            if (options.CopyToClipboard)
            {
                ctx.UpdateStatus("Copying public key to clipboard...");
                copiedToClipboard = await _clipboardService.SetTextAsync(keyPair.PublicKeyOpenSsh.TrimEnd());
                
                if (!copiedToClipboard)
                {
                    _consoleService.ShowWarning("Warning: Could not copy to clipboard");
                }
            }

            result = new KeyGenerationResult
            {
                KeyPair = keyPair,
                PrivateKeyPath = privateKeyPath,
                PublicKeyPath = publicKeyPath,
                EncryptedKeyPath = encryptedKeyPath,
                ConfigPath = configUpdated ? configPath : null,
                ConfigUpdated = configUpdated,
                CopiedToClipboard = copiedToClipboard
            };
        });

        // Show results
        _consoleService.ShowResults(result, options);

        // Handle manual clipboard copy if needed
        if (!options.CopyToClipboard && await _consoleService.ConfirmAsync("[bold yellow]Would you like to copy the public key to clipboard now?[/]"))
        {
            var copied = await _clipboardService.SetTextAsync(result.KeyPair.PublicKeyOpenSsh.TrimEnd());
            if (copied)
            {
                _consoleService.ShowSuccess("?? Public key copied to clipboard!");
            }
            else
            {
                _consoleService.ShowError("Error copying to clipboard");
            }
        }

        return result;
    }
}