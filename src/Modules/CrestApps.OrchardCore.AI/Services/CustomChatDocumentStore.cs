using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Services;

public sealed class CustomChatDocumentStore
{
    private readonly string _root;

    public CustomChatDocumentStore(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "App_Data", "CustomChat");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(string sessionId, IFormFile file, CancellationToken ct)
    {
        var directoryPath = Path.Combine(_root, sessionId);

        Directory.CreateDirectory(directoryPath);

        var fileId = Path.GetRandomFileName();

        var path = Path.Combine(directoryPath, fileId);

        await using var fs = File.Create(path);

        await file.CopyToAsync(fs, ct);

        return path;
    }
}
