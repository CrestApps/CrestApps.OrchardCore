using System.Text.Json;
using CrestApps.AI.Mcp.Models;

namespace CrestApps.Mvc.Web.Services;

public sealed class JsonFileMcpServerSettingsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFileMcpServerSettingsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "mcp-server-settings.json");
    }

    public async Task<McpServerOptions> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new McpServerOptions();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<McpServerOptions>(json, _jsonOptions) ?? new McpServerOptions();
    }

    public async Task SaveAsync(McpServerOptions settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
