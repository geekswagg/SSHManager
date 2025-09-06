using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.CommandLine;
using Spectre.Console;
using TextCopy;

class Program
{
    static int Main(string[] args)
    {
        var nameOption = new Option<string>("--name", description: "Base filename (no extension)", getDefaultValue: () => "id_rsa_ado");
        var outDirOption = new Option<string>("--out", description: "Output directory", getDefaultValue: () => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh"));
        var commentOption = new Option<string>("--comment", description: "OpenSSH public key comment", getDefaultValue: () => "AzureDevOps");
        var passOption = new Option<string>("--passphrase", description: "If provided, also export encrypted PKCS#8 with this passphrase", getDefaultValue: () => string.Empty);
        var clipboardOption = new Option<bool>("--clipboard", description: "Copy the public key to clipboard", getDefaultValue: () => true);
        var configOption = new Option<bool>("--config", description: "Generate SSH config file entry for Azure DevOps", getDefaultValue: () => true);

        var root = new RootCommand("Generate an RSA keypair for Azure DevOps (OpenSSH public key + PEM private key)");
        root.AddOption(nameOption);
        root.AddOption(outDirOption);
        root.AddOption(commentOption);
        root.AddOption(passOption);
        root.AddOption(clipboardOption);
        root.AddOption(configOption);

        root.SetHandler(async (string name, string outDir, string comment, string passphrase, bool copyToClipboard, bool generateConfig) =>
        {
            // Create a nice header
            AnsiConsole.Write(
                new FigletText("SSH Key Generator")
                    .LeftJustified()
                    .Color(Color.Blue));

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]Generating RSA keypair for Azure DevOps...[/]");
            AnsiConsole.WriteLine();

            Directory.CreateDirectory(outDir);
            var privPath = Path.Combine(outDir, name);
            var pubPath = Path.Combine(outDir, name + ".pub");
            var configPath = Path.Combine(outDir, "config");

            string sshPub = "";
            bool configFileUpdated = false;

            // Show progress
            await AnsiConsole.Status()
                .StartAsync("Generating RSA key pair...", async ctx =>
                {
                    ctx.Status("Creating 4096-bit RSA key...");
                    ctx.Spinner(Spinner.Known.Star);
                    ctx.SpinnerStyle(Style.Parse("green"));

                    using var rsa = RSA.Create(4096);

                    ctx.Status("Exporting private key (PKCS#1)...");
                    // ----- PRIVATE KEY (PKCS#1 PEM: "BEGIN RSA PRIVATE KEY") -----
                    var pkcs1 = rsa.ExportRSAPrivateKey(); // PKCS#1
                    var pkcs1Pem = PemEncode("RSA PRIVATE KEY", pkcs1);
                    File.WriteAllText(privPath, pkcs1Pem);

                    // ----- OPTIONAL ENCRYPTED PKCS#8 -----
                    string? pkcs8Path = null;
                    if (!string.IsNullOrEmpty(passphrase))
                    {
                        ctx.Status("Exporting encrypted private key (PKCS#8)...");
                        var pkcs8Encrypted = rsa.ExportEncryptedPkcs8PrivateKey(
                            password: passphrase.AsSpan(),
                            pbeParameters: new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, iterationCount: 100_000)
                        );
                        pkcs8Path = Path.Combine(outDir, name + ".pkcs8.enc.pem");
                        var pkcs8Pem = PemEncode("ENCRYPTED PRIVATE KEY", pkcs8Encrypted);
                        File.WriteAllText(pkcs8Path, pkcs8Pem);
                    }

                    ctx.Status("Generating OpenSSH public key...");
                    // ----- PUBLIC KEY (OpenSSH "ssh-rsa AAAA... comment") -----
                    sshPub = BuildOpenSshRsaPublicKey(rsa, comment);
                    File.WriteAllText(pubPath, sshPub);

                    // Generate SSH config file entry
                    if (generateConfig)
                    {
                        ctx.Status("Updating SSH config file...");
                        configFileUpdated = await UpdateSshConfigFile(configPath, name, outDir);
                    }

                    // Copy to clipboard if requested
                    if (copyToClipboard)
                    {
                        ctx.Status("Copying public key to clipboard...");
                        try
                        {
                            await ClipboardService.SetTextAsync(sshPub.TrimEnd());
                        }
                        catch (Exception ex)
                        {
                            // Clipboard might not be available in some environments
                            AnsiConsole.MarkupLine($"[yellow]Warning: Could not copy to clipboard: {ex.Message}[/]");
                        }
                    }

                    ctx.Status("Complete!");
                });

            // Create a nice results table
            var table = new Table();
            table.AddColumn("[bold blue]File Type[/]");
            table.AddColumn("[bold blue]Path[/]");
            table.AddColumn("[bold blue]Description[/]");

            table.AddRow("[green]Private Key[/]", $"[dim]{privPath}[/]", "PKCS#1 format for SSH clients");
            
            if (!string.IsNullOrEmpty(passphrase))
            {
                table.AddRow("[yellow]Encrypted Private[/]", $"[dim]{Path.Combine(outDir, name + ".pkcs8.enc.pem")}[/]", "Password-protected PKCS#8 format");
            }
            
            table.AddRow("[cyan]Public Key[/]", $"[dim]{pubPath}[/]", "OpenSSH format for Azure DevOps");

            if (generateConfig && configFileUpdated)
            {
                table.AddRow("[magenta]SSH Config[/]", $"[dim]{configPath}[/]", "SSH configuration for Azure DevOps");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]✓ SSH Key pair generated successfully![/]");
            
            if (generateConfig && configFileUpdated)
            {
                AnsiConsole.MarkupLine("[bold green]✓ SSH config file updated with Azure DevOps entry![/]");
            }
            
            AnsiConsole.WriteLine();
            AnsiConsole.Write(table);

            // Display clipboard status
            if (copyToClipboard)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold green]📋 Public key copied to clipboard![/]");
            }

            // Display the public key content
            var publicKeyContent = sshPub.TrimEnd();
            
            AnsiConsole.WriteLine();
            var publicKeyPanel = new Panel(new Markup($"[dim]{publicKeyContent}[/]"))
                .Header(copyToClipboard ? 
                    "[bold cyan]Public Key Content (already copied to clipboard)[/]" : 
                    "[bold cyan]Public Key Content (copy this to Azure DevOps)[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Blue);
            
            AnsiConsole.Write(publicKeyPanel);

            // Show manual copy option if clipboard wasn't used
            if (!copyToClipboard)
            {
                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("[bold yellow]Would you like to copy the public key to clipboard now?[/]"))
                {
                    try
                    {
                        await ClipboardService.SetTextAsync(publicKeyContent);
                        AnsiConsole.MarkupLine("[bold green]📋 Public key copied to clipboard![/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error copying to clipboard: {ex.Message}[/]");
                    }
                }
            }

            // Show next steps with nice formatting
            AnsiConsole.WriteLine();
            var nextStepsText = "[bold yellow]1.[/] Go to Azure DevOps → User Settings → SSH Public Keys\n" +
                               "[bold yellow]2.[/] Click 'Add' and paste the public key " + 
                               (copyToClipboard ? "(already in clipboard)" : "content above") + "\n";

            if (generateConfig && configFileUpdated)
            {
                nextStepsText += "[bold yellow]3.[/] SSH is now configured! You can clone repos with: [dim]git clone git@dev.azure.com:org/repo.git[/]\n" +
                               "[bold yellow]4.[/] Test the connection: [dim]ssh -T git@ssh.dev.azure.com[/]";
            }
            else
            {
                nextStepsText += "[bold yellow]3.[/] Configure your SSH client to use the private key\n" +
                               "[bold yellow]4.[/] Test the connection: [dim]ssh -T git@ssh.dev.azure.com[/]";
            }

            var stepsPanel = new Panel(new Markup(nextStepsText))
                .Header("[bold green]Next Steps[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green);
            
            AnsiConsole.Write(stepsPanel);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue]Happy coding! 🚀[/]");

        }, nameOption, outDirOption, commentOption, passOption, clipboardOption, configOption);

        return root.Invoke(args);
    }

    // Update SSH config file with Azure DevOps entry
    static async Task<bool> UpdateSshConfigFile(string configPath, string keyName, string sshDir)
    {
        try
        {
            var identityFilePath = Path.Combine(sshDir, keyName).Replace('\\', '/');
            var configEntry = $@"
# Azure DevOps SSH Configuration (Generated by SSH Key Generator)
Host dev.azure.com
  HostName ssh.dev.azure.com
  User git
  IdentityFile {identityFilePath}
  IdentitiesOnly yes

";

            var configExists = File.Exists(configPath);
            var existingContent = configExists ? await File.ReadAllTextAsync(configPath) : "";

            // Check if Azure DevOps config already exists
            if (existingContent.Contains("Host dev.azure.com"))
            {
                // Replace existing Azure DevOps configuration
                var lines = existingContent.Split('\n');
                var newLines = new List<string>();
                bool inAzureDevOpsSection = false;
                bool foundSection = false;

                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("Host dev.azure.com"))
                    {
                        inAzureDevOpsSection = true;
                        foundSection = true;
                        // Add our new configuration
                        newLines.Add(configEntry.TrimEnd());
                        continue;
                    }
                    
                    if (inAzureDevOpsSection && line.Trim().StartsWith("Host ") && !line.Trim().StartsWith("Host dev.azure.com"))
                    {
                        inAzureDevOpsSection = false;
                    }

                    if (!inAzureDevOpsSection)
                    {
                        newLines.Add(line);
                    }
                }

                await File.WriteAllTextAsync(configPath, string.Join('\n', newLines));
            }
            else
            {
                // Append new configuration
                var content = existingContent;
                if (!string.IsNullOrEmpty(content) && !content.EndsWith('\n'))
                {
                    content += '\n';
                }
                content += configEntry;
                await File.WriteAllTextAsync(configPath, content);
            }

            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not update SSH config file: {ex.Message}[/]");
            return false;
        }
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
