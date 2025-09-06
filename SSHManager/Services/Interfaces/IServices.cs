using System.Security.Cryptography;
using SSHManager.Models;

namespace SSHManager.Services.Interfaces;

public interface ISshKeyGenerator
{
    SshKeyPair GenerateKeyPair(string keyName, string comment, string? passphrase = null, int keySize = 4096);
}

public interface IFileService
{
    Task WriteTextFileAsync(string path, string content);
    Task<string> ReadTextFileAsync(string path);
    bool FileExists(string path);
    void CreateDirectory(string path);
}

public interface IClipboardService
{
    Task<bool> SetTextAsync(string text);
}

public interface ISshConfigService
{
    Task<bool> UpdateConfigFileAsync(string configPath, string keyName, string sshDirectory);
}

public interface IConsoleService
{
    Task ShowHeaderAsync();
    Task ShowProgressAsync(string message, Func<IProgressContext, Task> operation);
    void ShowResults(KeyGenerationResult result, KeyGenerationOptions options);
    Task<bool> ConfirmAsync(string message);
    void ShowError(string message);
    void ShowWarning(string message);
    void ShowSuccess(string message);
}

public interface IProgressContext
{
    void UpdateStatus(string status);
}