using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class CustomChatTempDocumentStore
{
    private readonly string _root;

    public CustomChatTempDocumentStore(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "App_Data", "CustomChat");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string sessionId, IFormFile file, CancellationToken ct)
    {
        var dir = Path.Combine(_root, sessionId);

        Directory.CreateDirectory(dir);

        var fileId = Path.GetRandomFileName();

        var path = Path.Combine(dir, fileId);

        await using var fs = File.Create(path);

        await file.CopyToAsync(fs, ct);

        return path;
    }
}
