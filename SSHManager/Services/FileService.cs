using SSHManager.Services.Interfaces;

namespace SSHManager.Services;

public class FileService : IFileService
{
    public async Task WriteTextFileAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content);
    }

    public async Task<string> ReadTextFileAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }
}