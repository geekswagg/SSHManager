using Microsoft.Extensions.DependencyInjection;
using SSHManager.Commands;
using SSHManager.Services;
using SSHManager.Services.Interfaces;

namespace SSHManager;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSshManagerServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISshKeyGenerator, SshKeyGenerator>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ISshConfigService, SshConfigService>();
        services.AddSingleton<IConsoleService, SpectreConsoleService>();
        
        // Commands
        services.AddTransient<GenerateKeysCommand>();
        
        return services;
    }
}