using System.Text.Json;
using CrestApps.Core.AI.Copilot;

namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

internal sealed class JsonFileCopilotCredentialStore : ICopilotCredentialStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _credentialsPath;

    public JsonFileCopilotCredentialStore(IWebHostEnvironment env)
    {
        _credentialsPath = Path.Combine(env.ContentRootPath, "App_Data", "CopilotCredentials");
        Directory.CreateDirectory(_credentialsPath);
    }

    public async Task<CopilotProtectedCredential> GetProtectedCredentialAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(userId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);

        return JsonSerializer.Deserialize<CopilotProtectedCredential>(json, _jsonOptions);
    }

    public async Task SaveProtectedCredentialAsync(string userId, CopilotProtectedCredential credential, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(userId);
        var json = JsonSerializer.Serialize(credential, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public Task ClearCredentialAsync(string userId, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(userId);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private string GetFilePath(string userId) => Path.Combine(_credentialsPath, $"{userId}.json");
}
