using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SSHManager;
using SSHManager.Commands;
using SSHManager.Models;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddSshManagerServices();
        var serviceProvider = services.BuildServiceProvider();

        // Configure command line options
        var nameOption = new Option<string>("--name", description: "Base filename (no extension)", getDefaultValue: () => "id_rsa_ado");
        var outDirOption = new Option<string>("--out", description: "Output directory", getDefaultValue: () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh"));
        var commentOption = new Option<string>("--comment", description: "OpenSSH public key comment", getDefaultValue: () => "AzureDevOps");
        var passOption = new Option<string>("--passphrase", description: "If provided, also export encrypted PKCS#8 with this passphrase", getDefaultValue: () => string.Empty);
        var clipboardOption = new Option<bool>("--clipboard", description: "Copy the public key to clipboard", getDefaultValue: () => true);
        var configOption = new Option<bool>("--config", description: "Generate SSH config file entry for Azure DevOps", getDefaultValue: () => true);
        var keySizeOption = new Option<int>("--keysize", description: "RSA key size in bits", getDefaultValue: () => 4096);

        var root = new RootCommand("Generate an RSA keypair for Azure DevOps (OpenSSH public key + PEM private key)");
        root.AddOption(nameOption);
        root.AddOption(outDirOption);
        root.AddOption(commentOption);
        root.AddOption(passOption);
        root.AddOption(clipboardOption);
        root.AddOption(configOption);
        root.AddOption(keySizeOption);

        root.SetHandler(async (string name, string outDir, string comment, string passphrase, bool copyToClipboard, bool generateConfig, int keySize) =>
        {
            try
            {
                var options = new KeyGenerationOptions
                {
                    Name = name,
                    OutputDirectory = outDir,
                    Comment = comment,
                    Passphrase = string.IsNullOrEmpty(passphrase) ? null : passphrase,
                    CopyToClipboard = copyToClipboard,
                    GenerateConfig = generateConfig,
                    KeySize = keySize
                };

                var command = serviceProvider.GetRequiredService<GenerateKeysCommand>();
                await command.ExecuteAsync(options);
            }
            catch (Exception ex)
            {
                var consoleService = serviceProvider.GetRequiredService<SSHManager.Services.Interfaces.IConsoleService>();
                consoleService.ShowError($"An error occurred: {ex.Message}");
                Environment.Exit(1);
            }
        }, nameOption, outDirOption, commentOption, passOption, clipboardOption, configOption, keySizeOption);

        return await root.InvokeAsync(args);
    }
}
