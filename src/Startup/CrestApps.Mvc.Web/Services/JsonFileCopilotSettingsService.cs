using System.Text.Json;
using CrestApps.Mvc.Web.Models;

namespace CrestApps.Mvc.Web.Services;

public sealed class JsonFileCopilotSettingsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFileCopilotSettingsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "copilot-settings.json");
    }

    public async Task<CopilotSettings> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new CopilotSettings();
        }

        var json = await File.ReadAllTextAsync(_filePath);

        return JsonSerializer.Deserialize<CopilotSettings>(json, _jsonOptions) ?? new CopilotSettings();
    }

    public async Task SaveAsync(CopilotSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);

        await File.WriteAllTextAsync(_filePath, json);
    }
}
