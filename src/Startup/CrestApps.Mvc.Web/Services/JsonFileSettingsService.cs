using System.Text.Json;
using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Services;

public sealed class JsonFileSettingsService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFileSettingsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "ai-settings.json");
    }

    public async Task<GeneralAISettings> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new GeneralAISettings();
        }

        var json = await File.ReadAllTextAsync(_filePath);

        return JsonSerializer.Deserialize<GeneralAISettings>(json, _jsonOptions) ?? new GeneralAISettings();
    }

    public async Task SaveAsync(GeneralAISettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);

        await File.WriteAllTextAsync(_filePath, json);
    }
}
