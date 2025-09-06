using Spectre.Console;
using SSHManager.Models;
using SSHManager.Services.Interfaces;

namespace SSHManager.Services;

public class SpectreConsoleService : IConsoleService
{
    public Task ShowHeaderAsync()
    {
        AnsiConsole.Write(
            new FigletText("SSH Key Generator")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Generating RSA keypair for Azure DevOps...[/]");
        AnsiConsole.WriteLine();

        // Use Task.Run to make this method truly asynchronous
        return Task.Run(() => { });
    }

    public async Task ShowProgressAsync(string message, Func<IProgressContext, Task> operation)
    {
        await AnsiConsole.Status()
            .StartAsync(message, async ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("green"));
                
                var progressContext = new SpectreProgressContext(ctx);
                await operation(progressContext);
                
                ctx.Status("Complete!");
            });
    }

    public void ShowResults(KeyGenerationResult result, KeyGenerationOptions options)
    {
        ShowResultsTable(result, options);
        ShowSuccessMessages(result, options);
        ShowPublicKeyPanel(result, options);
        ShowNextSteps(result, options);
    }

    public Task<bool> ConfirmAsync(string message)
    {
        // No need for async/await since AnsiConsole.Confirm is synchronous
        return Task.FromResult(AnsiConsole.Confirm(message));
    }

    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]{message}[/]");
    }

    public void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{message}[/]");
    }

    public void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]{message}[/]");
    }

    private void ShowResultsTable(KeyGenerationResult result, KeyGenerationOptions options)
    {
        var table = new Table();
        table.AddColumn("[bold blue]File Type[/]");
        table.AddColumn("[bold blue]Path[/]");
        table.AddColumn("[bold blue]Description[/]");

        table.AddRow("[green]Private Key[/]", $"[dim]{result.PrivateKeyPath}[/]", "PKCS#1 format for SSH clients");
        
        if (!string.IsNullOrEmpty(options.Passphrase))
        {
            table.AddRow("[yellow]Encrypted Private[/]", $"[dim]{result.EncryptedKeyPath}[/]", "Password-protected PKCS#8 format");
        }
        
        table.AddRow("[cyan]Public Key[/]", $"[dim]{result.PublicKeyPath}[/]", "OpenSSH format for Azure DevOps");

        if (options.GenerateConfig && result.ConfigUpdated)
        {
            table.AddRow("[magenta]SSH Config[/]", $"[dim]{result.ConfigPath}[/]", "SSH configuration for Azure DevOps");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
    }

    private void ShowSuccessMessages(KeyGenerationResult result, KeyGenerationOptions options)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]? SSH Key pair generated successfully![/]");
        
        if (options.GenerateConfig && result.ConfigUpdated)
        {
            AnsiConsole.MarkupLine("[bold green]? SSH config file updated with Azure DevOps entry![/]");
        }

        if (result.CopiedToClipboard)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]?? Public key copied to clipboard![/]");
        }
    }

    private void ShowPublicKeyPanel(KeyGenerationResult result, KeyGenerationOptions options)
    {
        AnsiConsole.WriteLine();
        var header = result.CopiedToClipboard 
            ? "[bold cyan]Public Key Content (already copied to clipboard)[/]" 
            : "[bold cyan]Public Key Content (copy this to Azure DevOps)[/]";

        var publicKeyPanel = new Panel(new Markup($"[dim]{result.KeyPair.PublicKeyOpenSsh}[/]"))
            .Header(header)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Blue);
        
        AnsiConsole.Write(publicKeyPanel);
    }

    private void ShowNextSteps(KeyGenerationResult result, KeyGenerationOptions options)
    {
        AnsiConsole.WriteLine();
        var nextStepsText = "[bold yellow]1.[/] Go to Azure DevOps ? User Settings ? SSH Public Keys\n" +
                           "[bold yellow]2.[/] Click 'Add' and paste the public key " + 
                           (result.CopiedToClipboard ? "(already in clipboard)" : "content above") + "\n";

        if (options.GenerateConfig && result.ConfigUpdated)
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
        AnsiConsole.MarkupLine("[bold blue]Happy coding! ??[/]");
    }
}

internal class SpectreProgressContext : IProgressContext
{
    private readonly StatusContext _context;

    public SpectreProgressContext(StatusContext context)
    {
        _context = context;
    }

    public void UpdateStatus(string status)
    {
        _context.Status(status);
    }
}