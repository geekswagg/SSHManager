using TextCopy;
using SSHManager.Services.Interfaces;

namespace SSHManager.Services;

public class ClipboardService : IClipboardService
{
    public async Task<bool> SetTextAsync(string text)
    {
        try
        {
            await TextCopy.ClipboardService.SetTextAsync(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}