using System.Text.Json;
using CrestApps.AI.Models;

namespace CrestApps.Mvc.Web.Services;

/// <summary>
/// Manages persistence of <see cref="AIDataSourceSettings"/> in a JSON file stored under App_Data.
/// </summary>
public sealed class JsonFileAIDataSourceSettingsService
{
    public const string SectionKey = "AIDataSources";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;

    public JsonFileAIDataSourceSettingsService(string appDataPath)
    {
        _filePath = Path.Combine(appDataPath, "ai-data-source-settings.json");
    }

    public string FilePath => _filePath;

    public async Task<AIDataSourceSettings> GetAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new AIDataSourceSettings();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        var wrapper = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);

        if (TryGetSection(wrapper, out var section))
        {
            return section.Deserialize<AIDataSourceSettings>(_jsonOptions) ?? new AIDataSourceSettings();
        }

        return new AIDataSourceSettings();
    }

    public async Task SaveAsync(AIDataSourceSettings settings)
    {
        var sectionKey = JsonNamingPolicy.CamelCase.ConvertName(SectionKey);
        var wrapper = new Dictionary<string, AIDataSourceSettings>
        {
            [sectionKey] = settings,
        };

        var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    private static bool TryGetSection(JsonElement wrapper, out JsonElement section)
    {
        if (wrapper.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in wrapper.EnumerateObject())
            {
                if (string.Equals(property.Name, SectionKey, StringComparison.OrdinalIgnoreCase))
                {
                    section = property.Value;
                    return true;
                }
            }
        }

        section = default;
        return false;
    }
}
